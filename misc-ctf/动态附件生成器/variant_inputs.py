# -*- coding: utf-8 -*-
"""方案 C 构造器：变体化「输入」，flag 由输入推出。

和 make_mapped.py 的区别：
    make_mapped.py  给 flag 加后缀 → 对 starwalk 这类「推算型」题无效（附件一字节不变）
    本脚本          每份变体的 GPS / 拍摄时间 / 地标 都不同，flag 从那组数据算出来

产出同样兼容平台的 mapped.rx：
    out/<目标>-variant/mapped/…        上传到 $BUCKET/mapped/
    out/<目标>-variant/checker/flag.json

用法：
    python variant_inputs.py               # 列出支持的目标
    python variant_inputs.py all 4         # 四类各 4 份
    python variant_inputs.py trio 8        # 只做 Level1 属性三件套
    python variant_inputs.py gps 8         # Level2 五张照片
    python variant_inputs.py hongyadong 8  # Level3 游客照
    python variant_inputs.py route 8       # Level5 路线复现
    python variant_inputs.py all 4 --zip   # 强制打包（默认附件>1 才打包）

自检不是"探字符串"，而是**按题解把 flag 重新算一遍**：
    读 EXIF → 反查地标 → 换算 UTC → 拼 flag → 与 flag.json 比对
"""
import datetime
import hashlib
import io
import json
import os
import random
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

OUT = os.path.join(TOOLS, "out")
PHOTOS = CH.PHOTOS

# ---------------------------------------------------------------------------
# 地标库：照片 ↔ 真实坐标 ↔ 拼音
# 坐标取原图 EXIF / OSM，用来反查（选手在地图上定位，这里用最近点近似）
# ---------------------------------------------------------------------------
LANDMARKS = [
    dict(photo="p_gugong_turtle2.jpg", city="beijing",    province="beijing",
         place="gugong",        lat=39.9163,   lon=116.3972),
    dict(photo="p_greatwall.jpg",      city="beijing",    province="beijing",
         place="mutianyu",      lat=40.4341,   lon=116.5656),
    dict(photo="p_mutianyu.jpg",       city="beijing",    province="beijing",
         place="mutianyu",      lat=40.4380,   lon=116.5619),
    dict(photo="p_pearl_gps.jpg",      city="shanghai",   province="shanghai",
         place="oriental_pearl", lat=31.2404,  lon=121.4969),
    dict(photo="p_xihu_pano.jpg",      city="hangzhou",   province="zhejiang",
         place="xihu",          lat=30.2425,   lon=120.1490),
    dict(photo="p_garden_wangshi.jpg", city="suzhou",     province="jiangsu",
         place="wangshiyuan",   lat=31.3012,   lon=120.6305),
    dict(photo="p_garden_shizilin.jpg", city="suzhou",    province="jiangsu",
         place="shizilin",      lat=31.3236,   lon=120.6243),
    dict(photo="p_zjj_gps.jpg",        city="zhangjiajie", province="hunan",
         place="zhangjiajie",   lat=29.3323,   lon=110.4261),
    dict(photo="p_guilin.jpg",         city="guilin",     province="guangxi",
         place="guilin",        lat=25.2736,   lon=110.2900),
    dict(photo="p_karst.jpg",          city="guilin",     province="guangxi",
         place="guilin",        lat=24.7780,   lon=110.4920),
    dict(photo="p_liriver.jpg",        city="guilin",     province="guangxi",
         place="lijiang",       lat=24.8200,   lon=110.4600),
    dict(photo="p_yangshuo.jpg",       city="yangshuo",   province="guangxi",
         place="yangshuo",      lat=24.7785,   lon=110.4960),
    dict(photo="(geo) hongyadong.jpg", city="chongqing",  province="chongqing",
         place="hongyadong",    lat=29.5667,   lon=106.5758),
]

BRANDS = [("Xiaomi", "xiaomi"), ("vivo", "vivo"), ("OPPO", "oppo"),
          ("HUAWEI", "huawei"), ("OnePlus", "oneplus")]
THRESH = 0.05          # 反查地标的容差（度），约 5 km


# ---------------------------------------------------------------------------
def dms(deg):
    """十进制度 → EXIF 的 度/分/秒（整数秒）"""
    d = int(deg)
    m_f = (deg - d) * 60
    m = int(m_f)
    s = int(round((m_f - m) * 60))
    if s == 60:
        s, m = 0, m + 1
    if m == 60:
        m, d = 0, d + 1
    return ((d, 1), (m, 1), (s, 1))


def photo_path(name):
    if name.startswith("(geo) "):
        return os.path.join(BUILD, "_assets", "geo", name[6:])
    return os.path.join(PHOTOS, name)


def nearest(lat, lon):
    best, bestd = None, 9e9
    for lm in LANDMARKS:
        d = ((lm["lat"] - lat) ** 2 + (lm["lon"] - lon) ** 2) ** 0.5
        if d < bestd:
            best, bestd = lm, d
    return best if bestd < THRESH else None


def open_photo(name, width=1200):
    im = CH.Image.open(photo_path(name)).convert("RGB")
    if im.width > width:
        im = im.resize((width, int(im.height * width / im.width)), CH.Image.LANCZOS)
    return im


def write_shot(dst, photo, make, model, lat, lon, local_dt, desc=b"trip"):
    """写一张带 GPS + 拍摄时间的照片"""
    im = open_photo(photo, 1200)
    tiff = CH.build_exif_tiff(make, model, desc,
                              gps=((dms(lat), "N"), (dms(lon), "E")),
                              software=b"iOS 17.4.1", datetime_orig=local_dt)
    CH.write_bytes(dst, CH.jpeg_with_exif(im, tiff, quality=90))


def read_shot(p):
    """读出一张照片的 (lat, lon, 本地时间字符串, 型号)"""
    ex = CH.Image.open(p).getexif()
    g = ex.get_ifd(0x8825)
    if not g:
        return None
    one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
    deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
    lat, lon = deg(g[2]), deg(g[4])
    if g.get(1) == "S":
        lat = -lat
    if g.get(3) == "W":
        lon = -lon
    return lat, lon, ex.get(0x0132), ex.get(0x0110) or ""


def utc_hm(local_dt):
    t = datetime.datetime.strptime(local_dt, "%Y:%m:%d %H:%M:%S")
    return (t - datetime.timedelta(hours=8)).strftime("%H%M"), t


# ---------------------------------------------------------------------------
# 四类变体构造
# ---------------------------------------------------------------------------
def make_trio(base, rnd):
    """Level1 属性三件套：随机 城市 / 品牌 / 拍摄时间 → flag{城市_品牌_UTC}"""
    lm = rnd.choice(LANDMARKS)
    brand, brand_lc = rnd.choice(BRANDS)
    local = "2026:%02d:%02d %02d:%02d:00" % (rnd.randint(3, 11), rnd.randint(1, 28),
                                             rnd.randint(6, 22), rnd.randint(0, 59))
    CH.ensure_dir(os.path.join(base, "files"))
    write_shot(os.path.join(base, "files", "photo.jpg"), lm["photo"], brand.encode(),
               ("%s 14 Ultra" % brand).encode(), lm["lat"], lm["lon"], local,
               b"palace courtyard")
    CH.write_text(os.path.join(base, "files", "readme.txt"),
                  "一张游客照，元数据完整。\n\n"
                  "任务：从文件里凑出三样东西 —— 拍摄地点（城市）、手机品牌、拍摄时刻。\n"
                  "提交格式：flag{城市拼音_品牌小写_UTC时刻(HHMM)}\n\n"
                  "注意：文件里记的是北京时间（UTC+8）。\n", bom=True)
    hm, _ = utc_hm(local)
    return "flag{%s_%s_%s}" % (lm["city"], brand_lc, hm)


def make_gps(base, rnd):
    """Level2 五张照片：唯一带 GPS 的那张指向随场景区 → flag{省份_景区}"""
    target = rnd.choice([lm for lm in LANDMARKS if lm["place"] != "lijiang"])
    others = rnd.sample([lm for lm in LANDMARKS
                         if lm["photo"] != target["photo"]], 4)
    spots = [target] + others
    rnd.shuffle(spots)
    outdir = CH.ensure_dir(os.path.join(base, "files", "photos"))
    for i, lm in enumerate(spots, 1):
        local = "2026:07:%02d %02d:%02d:%02d" % (rnd.randint(8, 20), rnd.randint(6, 21),
                                                 rnd.randint(0, 59), rnd.randint(0, 59))
        use_gps = (lm is target)
        dst = os.path.join(outdir, "IMG_%04d.jpg" % (2030 + i))
        if use_gps:
            write_shot(dst, lm["photo"], b"Canon", b"Canon EOS 6D", lm["lat"], lm["lon"],
                       local, b"a quick shot")
        else:                                     # 同一张图，但不写 GPS
            im = open_photo(lm["photo"], 1200)
            tiff = CH.build_exif_tiff(b"Apple", b"iPhone 13", b"trip",
                                      software=b"iOS 17.4.1", datetime_orig=local)
            CH.write_bytes(dst, CH.jpeg_with_exif(im, tiff, quality=90))
    CH.write_text(os.path.join(base, "files", "readme.txt"),
                  "有人在朋友圈发了五张旅行照。\n"
                  "其中只有一张，把拍摄地点一起带了出来。\n\n"
                  "任务：找出那张照片，把经纬度换算出来，定位到那片景区。\n"
                  "提交格式：flag{省份拼音_景区名拼音}\n\n"
                  "提示：exiftool 支持一次跑一整个目录。\n", bom=True)
    return "flag{%s_%s}" % (target["province"], target["place"])


def make_hongyadong(base, rnd):
    """Level3 游客照：随机地标 + 随机拍摄时间 → flag{城市_建筑_本地日期_UTC时刻}"""
    lm = rnd.choice(LANDMARKS)
    local = "20%02d:%02d:%02d %02d:%02d:%02d" % (rnd.randint(23, 26), rnd.randint(1, 12),
                                                 rnd.randint(1, 28), rnd.randint(18, 23),
                                                 rnd.randint(0, 59), rnd.randint(0, 59))
    CH.ensure_dir(os.path.join(base, "files"))
    write_shot(os.path.join(base, "files", "photo.jpg"), lm["photo"], b"", b"",
               lm["lat"], lm["lon"], local, b"")
    CH.write_text(os.path.join(base, "files", "readme.txt"),
                  "一张游客照，直接发的原图，元数据一点没清。\n\n"
                  "任务：从文件里读出拍摄地点和拍摄时间，定位到那片建筑。\n"
                  "提交格式：flag{城市拼音_建筑名拼音_拍摄日期(YYYYMMDD)_UTC时刻(HHMM)}\n\n"
                  "注意：文件里记的是北京时间（UTC+8），flag 要的是 UTC 时刻；日期用本地日期。\n", bom=True)
    hm, t = utc_hm(local)
    return "flag{%s_%s_%s_%s}" % (lm["city"], lm["place"], t.strftime("%Y%m%d"), hm)


def make_route(base, rnd):
    """Level5 路线：随机 3 城 + 打乱时间 → flag{起_中_终}"""
    by_city = {}
    for lm in LANDMARKS:
        by_city.setdefault(lm["city"], []).append(lm)
    cities_pick = rnd.sample(sorted(by_city), 3)      # 三座不同的城市
    picks = [rnd.choice(by_city[c]) for c in cities_pick]
    times = []
    day = rnd.randint(1, 20)
    for _ in picks:
        day += rnd.randint(1, 3)
        times.append("2026:09:%02d %02d:%02d:%02d" % (min(day, 28), rnd.randint(7, 22),
                                                     rnd.randint(0, 59), rnd.randint(0, 59)))
    CH.ensure_dir(os.path.join(base, "files"))
    order = list(range(3))
    rnd.shuffle(order)                            # 文件名乱序，真实顺序只在 EXIF 里
    for pos, k in enumerate(order, 1):
        lm = picks[k]
        write_shot(os.path.join(base, "files", "IMG_%04d.jpg" % (1000 + pos)),
                   lm["photo"], b"Apple", b"iPhone 13", lm["lat"], lm["lon"],
                   times[k], b"trip")
    CH.write_text(os.path.join(base, "files", "readme.txt"),
                  "一次旅行，三张照片，文件名是乱的。\n\n"
                  "任务：按拍摄时间排好顺序，认出三座城市，写出这趟路线。\n"
                  "提交格式：flag{起点城市_中间城市_终点城市}（全小写拼音）\n", bom=True)
    seq = [picks[k]["city"] for k in sorted(range(3), key=lambda k: times[k])]
    return "flag{%s}" % "_".join(seq)


KINDS = {
    "trio":       ("Level1 属性三件套",       make_trio),
    "gps":        ("Level2 五张照片",         make_gps),
    "hongyadong": ("Level3 游客照",           make_hongyadong),
    "route":      ("Level5 路线复现",         make_route),
}


# ---------------------------------------------------------------------------
# 自检：按题解把 flag 重新算一遍
# ---------------------------------------------------------------------------
def one_img(d):
    """在 files/ 里找那张图（单文件变体上传后名字会带序号）"""
    fdir = os.path.join(d, "files")
    for n in sorted(os.listdir(fdir)):
        if n.lower().endswith((".jpg", ".jpeg", ".png")):
            return os.path.join(fdir, n)
    raise FileNotFoundError("files/ 里没有图片")


def solve_trio(d):
    lat, lon, dt, model = read_shot(one_img(d))
    lm = nearest(lat, lon)
    brand = model.split()[0].lower()
    hm, _ = utc_hm(dt)
    return lm, "flag{%s_%s_%s}" % (lm["city"], brand, hm)


def solve_gps(d):
    pdir = os.path.join(d, "files", "photos")
    for n in sorted(os.listdir(pdir)):
        r = read_shot(os.path.join(pdir, n))
        if r:
            lm = nearest(r[0], r[1])
            return lm, "flag{%s_%s}" % (lm["province"], lm["place"])
    return None, None


def solve_hongyadong(d):
    lat, lon, dt, _ = read_shot(one_img(d))
    lm = nearest(lat, lon)
    hm, t = utc_hm(dt)
    return lm, "flag{%s_%s_%s_%s}" % (lm["city"], lm["place"], t.strftime("%Y%m%d"), hm)


def solve_route(d):
    shots = []
    fdir = os.path.join(d, "files")
    for n in sorted(os.listdir(fdir)):
        if not n.lower().endswith(".jpg"):
            continue
        r = read_shot(os.path.join(fdir, n))
        if r:
            shots.append((r[2], nearest(r[0], r[1])))
    shots.sort()
    return [s[1] for s in shots], "flag{%s}" % "_".join(s[1]["city"] for s in shots)


SOLVERS = {"trio": solve_trio, "gps": solve_gps, "hongyadong": solve_hongyadong,
           "route": solve_route}


# ---------------------------------------------------------------------------
def item_name(i, n, ext):
    """mapped/ 里的条目名一律 chall-NN（补零，保证 sort 顺序 = 序号顺序）"""
    w = max(2, len(str(n)))
    return "chall-%0*d%s" % (w, i, ext)


def build(kind, n, force_zip=None):
    title, maker = KINDS[kind]
    solver = SOLVERS[kind]
    root = os.path.join(OUT, "%s-variant" % kind)
    if os.path.exists(root):
        shutil.rmtree(root)
    md = CH.ensure_dir(os.path.join(root, "mapped"))
    ck = CH.ensure_dir(os.path.join(root, "checker"))

    flags, manifest, mode = {}, {}, None
    print()
    print("  目标：%s（%s）  份数：%d" % (kind, title, n))
    for i in range(1, n + 1):
        rnd = random.Random(1000 + i)             # 固定种子 → 可复现
        tmp = tempfile.mkdtemp(prefix="var_")
        try:
            flag = maker(tmp, rnd)
            payload = [os.path.join(dp, f) for dp, _dn, fn in os.walk(os.path.join(tmp, "files"))
                       for f in fn]
            # readme 属于「题面」，不算附件：单独写到 题面.md，不发给选手
            readme_src = os.path.join(tmp, "files", "readme.txt")
            if os.path.exists(readme_src) and not os.path.exists(os.path.join(root, "题面.md")):
                CH.write_bytes(os.path.join(root, "题面.md"),
                               io.open(readme_src, encoding="utf-8-sig").read().encode("utf-8"))
            attachments = [p for p in payload if not p.endswith("readme.txt")]
            use_zip = (len(attachments) > 1) if force_zip is None else force_zip
            if mode is None:
                mode = "zip" if use_zip else "single"
            if use_zip:
                name = item_name(i, n, ".zip")
                with zipfile.ZipFile(os.path.join(md, name), "w", zipfile.ZIP_DEFLATED) as z:
                    for p in sorted(attachments):
                        z.write(p, os.path.relpath(p, tmp))
            else:
                src = attachments[0]
                name = item_name(i, n, os.path.splitext(src)[1].lower())
                shutil.copy2(src, os.path.join(md, name))
            flags[name] = flag
            manifest[name] = dict(flag=flag, bytes=os.path.getsize(os.path.join(md, name)),
                                  sha256=hashlib.sha256(open(os.path.join(md, name), "rb").read()).hexdigest())
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    json.dump(flags, open(os.path.join(ck, "flag.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=2)
    json.dump(dict(kind=kind, title=title, mode=mode, variants=manifest),
              open(os.path.join(root, "manifest.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=2)

    # ---- 自检：解一遍 ----
    print("  ── 自检（按题解重新算一遍）─────────────────")
    ok = 0
    for name in sorted(flags):
        p = os.path.join(md, name)
        with tempfile.TemporaryDirectory() as tmp:
            if name.endswith(".zip"):
                zipfile.ZipFile(p).extractall(tmp)
                d = tmp
            else:
                CH.ensure_dir(os.path.join(tmp, "files"))
                shutil.copy2(p, os.path.join(tmp, "files", os.path.basename(p)))
                d = tmp
            lm, got = solver(d)
        good = (got == flags[name])
        ok += good
        mark = "✓" if good else "✗"
        if isinstance(lm, list):
            where = " → ".join(x["city"] for x in lm if x)
        else:
            where = "%s/%s" % (lm["city"], lm["place"]) if lm else "?"
        print("    %s %-18s %-46s  %s" % (mark, name, flags[name], where))
    print("    → %d / %d 一致（flag.json 与附件里真实数据算出来的一样）" % (ok, len(flags)))
    total = sum(m["bytes"] for m in manifest.values()) / 1048576
    print("  产物：%s  （%.1f MB，形态 %s）" % (root, total, mode))
    return ok == len(flags)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    force = True if "--zip" in sys.argv else (False if "--nozip" in sys.argv else None)
    if not args:
        print()
        for k, (title, _m) in KINDS.items():
            print("    %-12s %s" % (k, title))
        print()
        print("  用法: python variant_inputs.py <target|all> [份数] [--zip|--nozip]")
        return 1
    target = args[0]
    n = int(args[1]) if len(args) > 1 else 8
    kinds = list(KINDS) if target == "all" else [target]
    good = True
    for k in kinds:
        if k not in KINDS:
            print("  未知目标:", k)
            return 1
        good &= build(k, n, force)
    print()
    print("  全部通过" if good else "  有未通过项，见上")
    return 0 if good else 1


if __name__ == "__main__":
    sys.exit(main())
