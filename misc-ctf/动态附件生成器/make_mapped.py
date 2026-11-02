# -*- coding: utf-8 -*-
"""动态附件（$BUCKET/mapped）生成器 · 生成后自动校验

依据本地 ret2shell 源码（crates/bucket/src/challenge.rs）：

    get_mapped_files()  → 列出 mapped/ 下所有「非 . 开头」的条目，排序
    get_mapped_file(id) → files.sort(); files[id % files.len()]

也就是说：
  * 每个选手/队伍只会拿到 **一个** 条目
  * 映射规则是 **id 对文件数取模**（不是哈希），文件名排序决定下标
  * mapped/ 里放的是普通文件；**只有当一题要发多个附件时才需要打成 zip**
  * 份数少于人数会循环复用；`mapped/README.md` 这类非 . 开头的文件也会占用一个名额

用法：
    python make_mapped.py                  # 交互：直接输入 <题目名> <份数> [--variant]
    python make_mapped.py demo-01 3        # 命令行
    python make_mapped.py trio 4 --variant               # 方案C：变体化输入
    python make_mapped.py starwalk-series/Level2 8 --zip     # 强制打包
"""
import hashlib
import io
import json
import os
import re
import shutil
import struct
import sys
import tempfile
import zipfile

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TOOLS = os.path.dirname(os.path.abspath(__file__))
MC = os.path.dirname(TOOLS)
BUILD = os.path.join(MC, "_build")
sys.path.insert(0, BUILD)

import make_challenges as CH                     # noqa: E402
import make_live_demo as LD                      # noqa: E402
import variant_inputs as VI                      # noqa: E402  C 版引擎

OUT = os.path.join(TOOLS, "out")
DEFAULT_N = 16


# ---------------------------------------------------------------------------
# 平台会重写上传的文件名（crates/bucket/src/challenge.rs: to_file_name）
#   deunicode 把中文转写成拼音 → 空格与 \ / : * ? " < > | 变 _ → 去首尾 _ → 转小写
#   例：作业-15.jpg → zuo_ye_-15.jpg
# 所以 mapped/ 的条目名必须自己就是纯 ASCII，否则 flag.json 的 key 对不上。
# ---------------------------------------------------------------------------
SAFE_CHARS = set("abcdefghijklmnopqrstuvwxyz0123456789-._")
ESCAPE_CHARS = set('\\/:*?"<>| ')

EXPECT_MANUAL = {
    # 题目本身就没法自动探出 flag，属于预期
    "demo-04": "zip 是真加密（密码在题面），探针不猜密码",
    "demo-06": "flag 画在缩略图像素里，要人眼看图",
    "starwalk-series/Level2": "flag 靠 GPS 坐标推算，不在文件里",
    "starwalk-series/Level3": "flag 靠 GPS + 时间推算，不在文件里",
    "starwalk-series/Level4": "flag 靠招牌定位，不在文件里",
    "starwalk-series/Level5": "flag 靠坐标+时间排序推算，不在文件里",
}


def item_name(i, n, ext):
    """mapped/ 里的条目名一律 chall-NN（补零，保证 sort 顺序 = 序号顺序）"""
    w = max(2, len(str(n)))
    return "chall-%0*d%s" % (w, i, ext)


def platform_safe(name):
    """平台 to_file_name() 会不会改这个名字"""
    if not name or name != name.lower():
        return False
    if any(c in ESCAPE_CHARS for c in name) or any(ord(c) > 127 for c in name):
        return False
    return all(c in SAFE_CHARS for c in name)


# ---------------------------------------------------------------------------
def catalog():
    items = {}
    meta = json.load(open(os.path.join(BUILD, "challenges.json"), encoding="utf-8"))
    for k, v in meta.items():
        full = (v.get("group") + "/" if v.get("group") else "") + (v.get("path") or k)
        rec = dict(kind="challenge", key=k, flag=v["flag"], title=v["title"], cfg=v)
        items[full] = rec
        items.setdefault(k, rec)
    for key, title, flag, _m in LD.ORDER:
        items[key] = dict(kind="demo", key=key, flag=flag, title=title, cfg=None)
    return items


def print_catalog(items):
    print()
    print("  可生成动态附件的目标：")
    n = 0
    for label in sorted(items):
        it = items[label]
        if it["kind"] == "challenge":
            full = (it["cfg"].get("group") + "/" if it["cfg"].get("group") else "") + \
                   (it["cfg"].get("path") or it["key"])
            if label != full:
                continue
        n += 1
        print("    %-30s %-24s [%s]" % (label, it["title"], it["kind"]))
    print("    （共 %d 个）" % n)
    print()
    print("  换成 --variant / -v 时，用「变体化输入」策略（flag 由附件里的数据算出）：")
    for k, (title, _m) in VI.KINDS.items():
        print("    %-14s %s" % (k, title))


# ---------------------------------------------------------------------------
def variant_flag(base, i):
    inner = base[len("flag{"):-1]
    return "flag{%s_%05x}" % (inner, (i * 2654435761) & 0xFFFFF)


def build_variant(item, flag, base_dir):
    if item["kind"] == "challenge":
        cfg = dict(item["cfg"])
        cfg["flag"] = flag
        cfg["dir"] = base_dir
        CH.ensure_dir(os.path.join(base_dir, "files"))
        CH.BUILDERS[item["key"]](cfg)
    else:
        CH.ensure_dir(os.path.join(base_dir, "files"))
        getattr(LD, item["key"].replace("-", "_"))(base_dir, flag)


def collect_files(root):
    out = []
    for dp, _dn, fn in os.walk(root):
        for f in sorted(fn):
            out.append(os.path.join(dp, f))
    return sorted(out)


def build_pack(item, label, n, force_zip=None):
    """force_zip: None=自动（附件>1 才打包） / True=总是打包 / False=总是单文件"""
    out = os.path.join(OUT, label.replace("/", "_"))
    if os.path.exists(out):
        shutil.rmtree(out)
    md = CH.ensure_dir(os.path.join(out, "mapped"))
    ck = CH.ensure_dir(os.path.join(out, "checker"))

    flag_map, manifest, mode = {}, {}, None
    for i in range(1, n + 1):
        flag = variant_flag(item["flag"], i)
        tmp = tempfile.mkdtemp(prefix="mapped_")
        try:
            build_variant(item, flag, tmp)
            payload = collect_files(os.path.join(tmp, "files"))
            use_zip = (len(payload) > 1) if force_zip is None else force_zip
            if mode is None:
                mode = "zip" if use_zip else "single"
            if use_zip:
                name = item_name(i, n, ".zip")
                zp = os.path.join(md, name)
                with zipfile.ZipFile(zp, "w", zipfile.ZIP_DEFLATED) as z:
                    for p in collect_files(tmp):
                        z.write(p, os.path.relpath(p, tmp))
            else:
                src = payload[0]
                ext = os.path.splitext(src)[1].lower()
                name = item_name(i, n, ext)                       # 纯 ASCII，平台不会改名
                shutil.copy2(src, os.path.join(md, name))
            flag_map[name] = flag
            manifest[name] = dict(flag=flag, bytes=os.path.getsize(os.path.join(md, name)),
                                  sha256=hashlib.sha256(open(os.path.join(md, name), "rb").read()).hexdigest(),
                                  mode=mode)
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    json.dump(flag_map, open(os.path.join(ck, "flag.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=2)
    json.dump(dict(target=label, title=item["title"], mode=mode, variants=manifest),
              open(os.path.join(out, "manifest.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=2)
    return out, flag_map, mode


# ---------------------------------------------------------------------------
def find_zip(data):
    """在任意二进制里定位内嵌 zip（PNG 的 IEND 之后那种也认）。

    不能只用 data.find(PK03_04)：图片像素里几乎必然出现这 4 字节的巧合。
    正确做法：从末尾找 EOCD（PK0506），用它的「中央目录大小 + 偏移」反推 zip 起点。
    """
    tries = []
    pos = data.rfind(b"PK\x05\x06")
    if pos >= 0 and pos + 22 <= len(data):
        cd_size, cd_off = struct.unpack_from("<II", data, pos + 12)
        start = pos - cd_size - cd_off
        if 0 <= start <= pos:
            tries.append(start)
    i = data.rfind(b"PK\x03\x04")
    while i >= 0 and len(tries) < 8:
        tries.append(i)
        i = data.rfind(b"PK\x03\x04", 0, i)
    for st in tries:
        try:
            z = zipfile.ZipFile(io.BytesIO(data[st:]))
            if z.namelist():
                return z
        except Exception:
            continue
    return None


def probe_file(p, flag):
    low = p.lower()
    if low.endswith((".txt", ".md", ".json")):
        try:
            if flag in open(p, encoding="utf-8", errors="replace").read():
                return "纯文本 " + os.path.basename(p)
        except OSError:
            pass
    try:
        z = find_zip(open(p, "rb").read())
        if z:
            for nm in z.namelist():
                if nm.lower().endswith((".txt", ".md", ".json")):
                    try:
                        if flag in z.read(nm).decode("utf-8", "replace"):
                            return "内嵌压缩包 %s → %s" % (os.path.basename(p), nm)
                    except RuntimeError:
                        raise
    except Exception:
        pass
    if low.endswith(".png"):
        try:
            from PIL import Image
            im = Image.open(p).convert("RGB")
            px = im.load()
            rgb, red = [], []
            for y in range(im.height):
                for x in range(im.width):
                    r, g, b = px[x, y]
                    rgb.extend((r & 1, g & 1, b & 1))
                    red.append(r & 1)
            for bits, label in ((rgb, "LSB-RGB"), (red, "LSB-R")):
                buf = bytearray()
                for i in range(0, len(bits) - 7, 8):
                    v = 0
                    for k in range(8):
                        v = (v << 1) | bits[i + k]
                    if v in (0, 0xFF):
                        break
                    buf.append(v)
                if flag.encode() in bytes(buf):
                    return "%s %s" % (label, os.path.basename(p))
        except Exception:
            pass
    return None


def verify_pack(out, label):
    md = os.path.join(out, "mapped")
    flag_map = json.load(open(os.path.join(out, "checker", "flag.json"), encoding="utf-8"))
    names = sorted(os.listdir(md))

    print("  ── 自动校验 ────────────────────────────────")
    print("    mapped/ %d 个 | flag.json %d 条" % (len(names), len(flag_map)))
    if set(names) != set(flag_map):
        print("    ✗ 文件名与 flag.json 不一致")
        return False, 0, len(names), 0

    ok = manual = 0
    for n in names:
        p = os.path.join(md, n)
        if n.lower().endswith(".zip"):
            with tempfile.TemporaryDirectory() as tmp:
                zipfile.ZipFile(p).extractall(tmp)
                how = next((h for h in (probe_file(q, flag_map[n]) for q in collect_files(tmp)) if h), None)
        else:
            how = probe_file(p, flag_map[n])
        if how:
            ok += 1
            print("    ✓ %-16s %-13s ← %s" % (n, flag_map[n][-12:], how))
        else:
            manual += 1
            print("    · %-16s %-13s 需人工确认（该类附件无法自动探出）" % (n, flag_map[n][-12:]))

    why = EXPECT_MANUAL.get(label)
    if manual and why:
        print("    ◎ %d 份属预期需人工：%s" % (manual, why))
    print("    → 自动解出 %d / %d，需人工 %d，异常 0" % (ok, len(names), manual))
    unexpected = 0 if why else manual
    if unexpected == len(names):
        print("    ⚠ 一份都没探到 flag —— 这题的 flag 可能根本不在附件里，")
        print("      动态化没意义（直接发静态附件 + 静态 checker 更省事）。")
    return True, ok, len(names), unexpected


# ---------------------------------------------------------------------------
def run_one(items, target, n, force_zip=None):
    item = items.get(target)
    if item is None:
        print("  找不到目标：%s（输入 ls 看清单）" % target)
        return False
    if n < 1 or n > 999:
        print("  份数要在 1–999 之间")
        return False

    print()
    print("  目标：%s  %s" % (target, item["title"]))
    print("  份数：%d" % n)
    out, flag_map, mode = build_pack(item, target, n, force_zip)
    print("  形态：%s" % ("打包成 zip（附件不止一个）" if mode == "zip"
                        else "单文件（每人直接拿一个文件，不打包）"))
    for name, flag in flag_map.items():
        print("    %-18s %s" % (name, flag))

    mapped_names = sorted(os.listdir(os.path.join(out, "mapped")))
    renamed = [n for n in mapped_names if not platform_safe(n)]
    if renamed:
        print("  ── 文件名检查 ──────────────────────────────")
        print("    ⚠ 这些名字会被平台转写，flag.json 的 key 会对不上：")
        for n in renamed[:5]:
            print("      %s" % n)

    hashes = {}
    for f in mapped_names:
        h = hashlib.sha256(open(os.path.join(out, "mapped", f), "rb").read()).hexdigest()
        hashes.setdefault(h, []).append(f)
    dup_warn = len(hashes) < len(flag_map)
    if dup_warn:
        print("  ── 变体检查 ────────────────────────────────")
        print("    ⚠ %d 份变体只有 %d 种不同内容 —— flag 没被写进附件，动态化没意义"
              % (len(flag_map), len(hashes)))

    ok, good, unexpected, total = verify_pack(out, target)
    print("  ──────────────────────────────────────────")
    total_mb = sum(m["bytes"] for m in
                   json.load(open(os.path.join(out, "manifest.json"), encoding="utf-8"))["variants"].values()) / 1048576
    print("  产物：%s   （%.1f MB）" % (out, total_mb))
    print("    mapped/            → 全部上传到 $BUCKET/mapped/")
    print("    checker/flag.json  → 上传到 $BUCKET/checker/")
    print("  映射：按文件名排序后 玩家 id %% %d 取下标（源码是取模，不是哈希）" % n)
    if not ok:
        print("  校验：有异常，看上面的 ✗")
    elif unexpected == total or dup_warn:
        print("  校验：结构通过，但有警告（见上）")
    else:
        print("  校验：全部通过")
    return ok


def parse_args(tokens):
    """返回 (目标, 份数, 强制zip, 是否C版)"""
    force, variant, out = None, False, []
    for t in tokens:
        if t == "--zip":
            force = True
        elif t == "--nozip":
            force = False
        elif t in ("--variant", "-v"):
            variant = True
        else:
            out.append(t)
    if not out:
        return None, None, force, variant
    n = DEFAULT_N
    if len(out) > 1:
        try:
            n = int(out[1])
        except ValueError:
            return None, None, force, variant
    return out[0].replace("\\", "/").strip("/"), n, force, variant


def run_variant(kind, n, force):
    """方案 C：变体化输入（GPS/时间/地标随机，flag 由数据算出）+ 按题解自检"""
    kinds = list(VI.KINDS) if kind == "all" else [kind]
    bad = [k for k in kinds if k not in VI.KINDS]
    if bad:
        print("  未知的 --variant 目标：%s（可选 all 或 %s）"
              % ("、".join(bad), "、".join(VI.KINDS)))
        return False
    oks = [VI.build(k, n, force) for k in kinds]
    print()
    print("  --variant 汇总：%d / %d 类通过" % (sum(oks), len(oks)))
    return all(oks)


def main():
    args = sys.argv[1:]
    items = catalog()
    if args:
        target, n, force, variant = parse_args(args)
        if target is None:
            print("份数要写数字，例如 demo-01 3 / trio 4 --variant")
            return 2
        if variant:
            return 0 if run_variant(target, n, force) else 1
        return 0 if run_one(items, target, n, force) else 1

    print_catalog(items)
    print()
    print("  直接输入 <题目名> <份数>（例如 demo-01 3），份数不填默认 %d" % DEFAULT_N)
    print("  加 --variant / -v 用「变体化输入」策略（目标改成 trio / gps / hongyadong / route）")
    print("  加 --zip 强制打包 · ls 重列 · q 退出")
    while True:
        try:
            sys.stdout.write("\n  > ")
            sys.stdout.flush()
            line = input().strip()
        except (EOFError, KeyboardInterrupt):
            print("\n  再见")
            return 0
        if not line:
            continue
        low = line.lower()
        if low in ("q", "quit", "exit"):
            print("\n  再见")
            return 0
        if low in ("ls", "list", "?"):
            print_catalog(items)
            continue
        target, n, force, variant = parse_args(line.split())
        if target is None:
            print("  份数要写数字，例如 demo-01 3 / trio 4 --variant")
            continue
        if variant:
            run_variant(target, n, force)
        else:
            run_one(items, target, n, force)


if __name__ == "__main__":
    sys.exit(main())
