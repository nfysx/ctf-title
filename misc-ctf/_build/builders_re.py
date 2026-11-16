# -*- coding: utf-8 -*-
r"""RE-ctf 动态附件 builder（13 题）。

每个 builder 接收 cfg：
    cfg["dir"]   输出目录（附件写进 <dir>/files/）
    cfg["flag"]  本次变体的 flag

对外只导出：BUILDERS = {题目目录名: builder}

设计要点
--------
* 源模板直接取 D:\ctf\RE-ctf\<题>/src/main.c，只替换常量数组/字符串，逻辑一行不动；
* 正向变换是各题 exp/solve.py 的反向逻辑取逆；
* 编译在 docker 里做（默认镜像 re-builder:22.04，可用 RE_BUILD_IMAGE 覆盖）；
* 每个 builder 自带两道自检：
    1) 静态：用该题的提取逻辑把 flag 从产物里反解出来，必须逐字相等；
    2) 运行：容器里跑产物，正确 flag -> Good!，错误输入 -> Wrong!
      （rev-flip-branch 的分支是反的，期望相反）
"""
import hashlib
import os
import random
import re
import shutil
import struct
import subprocess
import tempfile

# --------------------------------------------------------------------------
# 路径与镜像
# --------------------------------------------------------------------------
HERE = os.path.dirname(os.path.abspath(__file__))       # ...\misc-ctf\_build
CTF = os.path.dirname(os.path.dirname(HERE))            # ...\ctf
SRC_ROOT = os.environ.get("RE_SRC_ROOT", os.path.join(CTF, "RE-ctf"))

IMAGE = os.environ.get("RE_BUILD_IMAGE", "")
DEFAULT_IMAGE = "re-builder:22.04"

_DOCKER = shutil.which("docker")


def _image():
    """返回可用镜像名；没有就先建一个（只需一次，之后离线可用）。"""
    global IMAGE
    if IMAGE:
        return IMAGE
    for cand in (DEFAULT_IMAGE, "rebox:latest"):
        r = subprocess.run([_DOCKER, "image", "inspect", cand],
                           capture_output=True, text=True)
        if r.returncode == 0:
            IMAGE = cand
            return IMAGE
    tmp = tempfile.mkdtemp(prefix="rebuild_img_")
    with open(os.path.join(tmp, "Dockerfile"), "w", newline="\n") as f:
        f.write("FROM ubuntu:22.04\n"
                "RUN apt-get update -qq && apt-get install -y -qq gcc binutils upx-ucl python3 "
                "&& rm -rf /var/lib/apt/lists/*\n")
    subprocess.run([_DOCKER, "build", "-q", "-t", DEFAULT_IMAGE, tmp], check=True)
    shutil.rmtree(tmp, ignore_errors=True)
    IMAGE = DEFAULT_IMAGE
    return IMAGE


def _sh(workdir, script, timeout=900, check=True):
    """在容器里跑一段 bash，卷挂载 workdir -> /w。"""
    p = subprocess.run([_DOCKER, "run", "--rm", "-v", "%s:/w" % workdir, "-w", "/w",
                        _image(), "bash", "-lc", script],
                       capture_output=True, text=True, timeout=timeout,
                       encoding="utf-8", errors="replace")
    if check and p.returncode != 0:
        raise RuntimeError("容器执行失败：\n%s\n%s\n%s" % (script, p.stdout, p.stderr))
    return p.stdout


# --------------------------------------------------------------------------
# 源码改写小工具
# --------------------------------------------------------------------------
def _src(key):
    p = os.path.join(SRC_ROOT, key, "src", "main.c")
    with open(p, encoding="utf-8") as f:
        return f.read()


def _set_define(src, name, value):
    pat = re.compile(r"(?m)^#define\s+%s\s+\S+.*$" % re.escape(name))
    if not pat.search(src):
        raise KeyError("源码里没有 #define %s" % name)
    return pat.sub("#define %s %d" % (name, value), src, count=1)


def _fmt_bytes(vals, per=12):
    rows = []
    for i in range(0, len(vals), per):
        rows.append("    " + ", ".join("0x%02x" % v for v in vals[i:i + per]) + ",")
    return "\n".join(rows)


def _set_carr(src, name, vals):
    """替换 unsigned char NAME[N] = {...};（保留 static/const 前缀）。"""
    pat = re.compile(r"(?ms)^((?:static\s+)?(?:const\s+)?)unsigned\s+char\s+%s\s*\[[^\]]*\]\s*=\s*\{.*?\};"
                     % re.escape(name))
    m = pat.search(src)
    if not m:
        raise KeyError("源码里没有数组 %s" % name)
    new = "%sunsigned char %s[%d] = {\n%s\n};" % (m.group(1), name, len(vals), _fmt_bytes(vals))
    return src[:m.start()] + new + src[m.end():]


def _set_cstr(src, name, text):
    pat = re.compile(r'(?ms)^((?:static\s+)?(?:const\s+)?)char\s+%s\s*\[[^\]]*\]\s*=\s*"[^"]*";'
                     % re.escape(name))
    m = pat.search(src)
    if not m:
        raise KeyError("源码里没有字符串 %s" % name)
    new = '%schar %s[] = "%s";' % (m.group(1), name, text)
    return src[:m.start()] + new + src[m.end():]


def _get_carr(src, name):
    pat = re.compile(r"(?ms)unsigned\s+char\s+%s\s*\[[^\]]*\]\s*=\s*\{(.*?)\};" % re.escape(name))
    m = pat.search(src)
    if not m:
        raise KeyError("源码里没有数组 %s" % name)
    body = m.group(1)
    vals = []
    for tok in re.findall(r"0x[0-9a-fA-F]{2}|'(?:\\.|[^'\\])'", body):
        if tok.startswith("0x"):
            vals.append(int(tok, 16))
        else:
            ch = tok[1:-1]
            vals.append(ord(ch.encode().decode("unicode_escape")) if ch.startswith("\\") else ord(ch))
    return vals


def _get_cstr(src, name):
    pat = re.compile(r'(?ms)char\s+%s\s*\[[^\]]*\]\s*=\s*"([^"]*)"' % re.escape(name))
    m = pat.search(src)
    if not m:
        raise KeyError("源码里没有字符串 %s" % name)
    return m.group(1)


def _get_define(src, name):
    m = re.search(r"(?m)^#define\s+%s\s+(\S+)" % re.escape(name), src)
    if not m:
        raise KeyError("源码里没有 #define %s" % name)
    return int(m.group(1), 0)


# --------------------------------------------------------------------------
# 编译 + 运行自检
# --------------------------------------------------------------------------
def _workdir():
    return tempfile.mkdtemp(prefix="re_var_")


def _build(cfg, key, src, flags="-O2 -s -no-pie", name="challenge",
           filename="challenge", post=None):
    """写 main.c -> 容器里编译 -> 复制到 <dir>/files/<filename>，返回二进制字节。"""
    out_files = os.path.join(cfg["dir"], "files")
    os.makedirs(out_files, exist_ok=True)
    wd = _workdir()
    try:
        with open(os.path.join(wd, "main.c"), "w", newline="\n") as f:
            f.write(src)
        _sh(wd, "gcc %s -o %s main.c" % (flags, name))
        binpath = os.path.join(wd, name)
        if post:
            post(binpath, cfg, wd)
        data = open(binpath, "rb").read()
        with open(os.path.join(out_files, filename), "wb") as f:
            f.write(data)
        return data
    finally:
        shutil.rmtree(wd, ignore_errors=True)


LAST_RUN = {}


def _run_expect(cfg, filename, flag, expect="Good!"):
    """在容器里把产物跑一遍：正确 flag 与随机错误输入。"""
    out_files = os.path.join(cfg["dir"], "files")
    wd = _workdir()
    try:
        shutil.copy2(os.path.join(out_files, filename), os.path.join(wd, filename))
        with open(os.path.join(wd, "good.txt"), "w", newline="\n") as f:
            f.write(flag)
        with open(os.path.join(wd, "bad.txt"), "w", newline="\n") as f:
            f.write("flag{definitely_not_it}")
        raw = _sh(wd, "chmod +x ./%s; echo @@G@@; ./%s < good.txt; "
                       "echo @@B@@; ./%s < bad.txt; exit 0"
                   % (filename, filename, filename))
        part_g = raw.split("@@G@@")[1].split("@@B@@")[0]
        part_b = raw.split("@@B@@")[1]
        good = [l for l in part_g.splitlines() if l.strip()][-1:] or [""]
        bad = [l for l in part_b.splitlines() if l.strip()][-1:] or [""]
        good, bad = good[0].strip(), bad[0].strip()
        if expect == "Good!":
            ok = good.endswith("Good!") and bad.endswith("Wrong!")
        else:                                   # 分支反了的题
            ok = good.endswith("Wrong!") and bad.endswith("Good!")
        LAST_RUN[cfg.get("key", filename)] = (good, bad)
        if not ok:
            raise AssertionError("%s 运行自检失败：good=%r bad=%r" % (cfg.get("key", "?"), good, bad))
        return good, bad
    finally:
        shutil.rmtree(wd, ignore_errors=True)


def _sha(b):
    return hashlib.sha256(b).hexdigest()


# --------------------------------------------------------------------------
# ELF 小工具
# --------------------------------------------------------------------------
def _elf_sections(d):
    e_shoff = struct.unpack_from("<Q", d, 0x28)[0]
    es = struct.unpack_from("<H", d, 0x3A)[0]
    n = struct.unpack_from("<H", d, 0x3C)[0]
    si = struct.unpack_from("<H", d, 0x3E)[0]
    stroff = struct.unpack_from("<Q", d, e_shoff + si * es + 0x18)[0]
    out = {}
    for i in range(n):
        o = e_shoff + i * es
        noff = struct.unpack_from("<I", d, o)[0]
        end = d.index(b"\x00", stroff + noff)
        name = d[stroff + noff:end].decode()
        flags, addr, off, size = struct.unpack_from("<QQQQ", d, o + 8)
        out[name] = dict(addr=addr, off=off, size=size, flags=flags)
    return out


def _elf_symbols(d):
    """返回 {name: address}（.symtab 里的 FUNC/OBJECT 都收）。"""
    secs = _elf_sections(d)
    if ".symtab" not in secs or ".strtab" not in secs:
        return {}
    st = secs[".symtab"]
    stt = secs[".strtab"]
    n = st["size"] // 24
    out = {}
    for i in range(n):
        o = st["off"] + i * 24
        name_off, info, _other, shndx, value, _size = struct.unpack_from("<IBBHQQ", d, o)
        if not name_off:
            continue
        end = d.index(b"\x00", stt["off"] + name_off)
        name = d[stt["off"] + name_off:end].decode()
        out[name] = value
    return out


def _va_to_off(d, va):
    for s in _elf_sections(d).values():
        if s["off"] and s["addr"] <= va < s["addr"] + s["size"]:
            return s["off"] + (va - s["addr"])
    return None


# --------------------------------------------------------------------------
# 1. rev-strings
# --------------------------------------------------------------------------
def build_rev_strings(cfg):
    flag = cfg["flag"]
    src = _set_cstr(_src("rev-strings"), "TARGET", flag)
    data = _build(cfg, "rev-strings", src, filename="challenge")
    if flag.encode() not in data:
        raise AssertionError("rev-strings: 明文未写入二进制")
    _run_expect(cfg, "challenge", flag)
    return data


# --------------------------------------------------------------------------
# 2. rev-xor-loop
# --------------------------------------------------------------------------
def build_rev_xor_loop(cfg):
    flag = cfg["flag"].encode()
    src0 = _src("rev-xor-loop")
    KEY = bytes(_get_carr(src0, "KEY"))
    enc = bytes(b ^ KEY[i % len(KEY)] ^ (i & 0xFF) for i, b in enumerate(flag))
    src = _set_define(src0, "LEN", len(flag))
    src = _set_carr(src, "ENC", list(enc))
    data = _build(cfg, "rev-xor-loop", src, filename="challenge")
    # 静态自检：按 exp 的扫法还原
    got = None
    for off in range(len(data) - len(flag)):
        cand = bytes(data[off + i] ^ KEY[i % len(KEY)] ^ i for i in range(len(flag)))
        if cand.startswith(b"flag{") and cand.endswith(b"}"):
            got = cand
            break
    if got != flag:
        raise AssertionError("rev-xor-loop: 还原失败 %r" % (got,))
    _run_expect(cfg, "challenge", flag.decode())
    return data


# --------------------------------------------------------------------------
# 3. rev-flip-branch
# --------------------------------------------------------------------------
def build_rev_flip_branch(cfg):
    flag = cfg["flag"].encode()
    src0 = _src("rev-flip-branch")
    MASK = _get_define(src0, "MASK")
    enc = bytes(b ^ MASK for b in flag)
    src = _set_define(src0, "LEN", len(flag))
    src = _set_carr(src, "ENC", list(enc))
    data = _build(cfg, "rev-flip-branch", src, filename="challenge")
    got = None
    for off in range(len(data) - len(flag)):
        dec = bytes(b ^ MASK for b in data[off:off + len(flag)])
        if dec.startswith(b"flag{") and dec.endswith(b"}"):
            got = dec
            break
    if got != flag:
        raise AssertionError("rev-flip-branch: 还原失败 %r" % (got,))
    _run_expect(cfg, "challenge", flag.decode(), expect="inverted")
    return data

# --------------------------------------------------------------------------
# 4. rev-serial-eq
# --------------------------------------------------------------------------
def _matrix_4x4(src):
    m = re.search(r"(?ms)unsigned\s+char\s+M\[4\]\[4\]\s*=\s*\{(.*?)\};", src)
    rows = re.findall(r"\{([^}]*)\}", m.group(1))
    return [[int(x, 0) for x in re.findall(r"0x[0-9a-fA-F]+|\d+", r)] for r in rows]


def build_rev_serial_eq(cfg):
    flag = cfg["flag"].encode()
    src0 = _src("rev-serial-eq")
    M = _matrix_4x4(src0)
    pad = (-len(flag)) % 4
    x = list(flag) + [0] * pad
    blocks = len(x) // 4
    T = []
    for b in range(blocks):
        blk = x[4 * b:4 * b + 4]
        for r in range(4):
            T.append(sum(M[r][c] * blk[c] for c in range(4)) & 0xFF)
    src = _set_define(src0, "LEN", len(flag))
    src = _set_define(src, "BLOCKS", blocks)
    src = _set_carr(src, "T", T)
    data = _build(cfg, "rev-serial-eq", src, filename="challenge")

    # 静态自检：按 exp 的定位方法（M 未知偏移）反解
    for off in range(len(data) - 16):
        m = [list(data[off + 4 * r: off + 4 * r + 4]) for r in range(4)]
        if _det4(m) % 2 == 0:
            continue
        y0 = bytes(sum(m[r][c] * b for c, b in enumerate(b"flag")) & 0xFF for r in range(4))
        pos = data.find(y0)
        if pos < 0:
            continue
        Tb = data[pos:pos + 4 * blocks]
        out = []
        for b in range(blocks):
            out += _solve_block(m, list(Tb[4 * b:4 * b + 4]))
        cand = bytes(out).rstrip(b"\x00")
        if cand == flag:
            break
    else:
        raise AssertionError("rev-serial-eq: 还原失败")
    _run_expect(cfg, "challenge", flag.decode())
    return data


def _det3(a):
    return (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1])
            - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
            + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))


def _det4(m):
    return sum((-1) ** j * m[0][j] *
               _det3([[m[r][c] for c in range(4) if c != j] for r in range(1, 4)])
               for j in range(4))


def _solve_block(m, y):
    inv = pow(_det4(m) % 256, -1, 256)
    cof = [[(-1) ** (i + j) *
            _det3([[m[r][c] for c in range(4) if c != j] for r in range(4) if r != i])
            for j in range(4)] for i in range(4)]
    adj = [[cof[j][i] % 256 for j in range(4)] for i in range(4)]
    return [sum(adj[i][j] * y[j] for j in range(4)) * inv % 256 for i in range(4)]


# --------------------------------------------------------------------------
# 5. rev-upx
# --------------------------------------------------------------------------
def build_rev_upx(cfg):
    flag = cfg["flag"]
    src = _set_cstr(_src("rev-upx"), "KEYSTR", flag)

    def pack(binpath, c, wd):
        _sh(wd, "upx -9 %s" % os.path.basename(binpath))

    data = _build(cfg, "rev-upx", src, flags="-O2 -s -static",
                  filename="challenge", post=pack)
    if b"UPX!" not in data:
        raise AssertionError("rev-upx: 没打上壳")

    # 静态自检：脱壳后取明文
    wd = _workdir()
    try:
        shutil.copy2(os.path.join(cfg["dir"], "files", "challenge"),
                     os.path.join(wd, "challenge"))
        out = _sh(wd, "cp challenge packed && upx -d challenge >/dev/null 2>&1; "
                      "strings -n 6 challenge | grep -o 'flag{[a-z0-9_]*}' | head -1")
        got = out.strip()
    finally:
        shutil.rmtree(wd, ignore_errors=True)
    if got != flag:
        raise AssertionError("rev-upx: 脱壳后取到 %r" % (got,))
    _run_expect(cfg, "challenge", flag)
    return data


# --------------------------------------------------------------------------
# 6. rev-rc4
# --------------------------------------------------------------------------
def _rc4(key, data):
    s = list(range(256))
    j = 0
    for i in range(256):
        j = (j + s[i] + key[i % len(key)]) & 0xFF
        s[i], s[j] = s[j], s[i]
    out, i, j = bytearray(), 0, 0
    for b in data:
        i = (i + 1) & 0xFF
        j = (j + s[i]) & 0xFF
        s[i], s[j] = s[j], s[i]
        out.append(b ^ s[(s[i] + s[j]) & 0xFF])
    return bytes(out)


def _ks0(key):
    s = list(range(256))
    j = 0
    for i in range(256):
        j = (j + s[i] + key[i % len(key)]) & 0xFF
        s[i], s[j] = s[j], s[i]
    j = s[1]
    s[1], s[j] = s[j], s[1]
    return s[(s[1] + s[j]) & 0xFF]


def build_rev_rc4(cfg):
    flag = cfg["flag"].encode()
    src0 = _src("rev-rc4")
    KEY = bytes(_get_carr(src0, "KEY"))
    enc = _rc4(KEY, flag)
    src = _set_define(src0, "LEN", len(flag))
    src = _set_define(src, "KEYLEN", len(KEY))
    src = _set_carr(src, "ENC", list(enc))
    data = _build(cfg, "rev-rc4", src, filename="challenge")

    table = {}
    for k in range(len(data) - 8):
        table.setdefault(_ks0(data[k:k + 8]), []).append(k)
    got = None
    for c in range(len(data) - len(flag)):
        for k in table.get(data[c] ^ ord("f"), []):
            pt = _rc4(data[k:k + 8], data[c:c + len(flag)])
            if pt.startswith(b"flag{") and pt.endswith(b"}"):
                got = pt
                break
        if got:
            break
    if got != flag:
        raise AssertionError("rev-rc4: 还原失败 %r" % (got,))
    _run_expect(cfg, "challenge", flag.decode())
    return data


# --------------------------------------------------------------------------
# 7. rev-smc
# --------------------------------------------------------------------------
def _encrypt_smc(path, SMK):
    d = bytearray(open(path, "rb").read())
    secs = _elf_sections(d)
    for name in ("smcx", "smcd"):
        s = secs[name]
        for i in range(s["size"]):
            d[s["off"] + i] ^= SMK[i & 7]
    open(path, "wb").write(d)


def build_rev_smc(cfg):
    flag = cfg["flag"].encode()
    src0 = _src("rev-smc")
    SMK = bytes(_get_carr(src0, "SMK"))
    TK = bytes(_get_carr(src0, "TK"))
    enc = bytes(b ^ TK[i & 7] for i, b in enumerate(flag))
    src = _set_define(src0, "LEN", len(flag))
    src = _set_carr(src, "ENC", list(enc))

    def post(binpath, c, wd):
        _encrypt_smc(binpath, SMK)

    data = _build(cfg, "rev-smc", src, filename="challenge", post=post)

    # 静态自检：解节表 -> 暴力试 SMK -> TK -> ENC -> flag
    secs = _elf_sections(data)
    smcd = data[secs["smcd"]["off"]:secs["smcd"]["off"] + secs["smcd"]["size"]]
    got = None
    for k in range(len(data) - 8):
        key = data[k:k + 8]
        tk = bytes(b ^ key[i & 7] for i, b in enumerate(smcd))
        want = bytes(c ^ tk[i & 7] for i, c in enumerate(b"flag{"))
        pos = data.find(want)
        if pos < 0:
            continue
        dec = bytes(data[pos + i] ^ tk[i & 7] for i in range(64))
        end = dec.find(b"}")
        if end < 0:
            continue
        cand = dec[:end + 1]
        if cand == flag:
            got = cand
            break
    if got != flag:
        raise AssertionError("rev-smc: 还原失败 %r" % (got,))
    _run_expect(cfg, "challenge", flag.decode())
    return data


# --------------------------------------------------------------------------
# 8. rev-math-check
# --------------------------------------------------------------------------
def _rol8(v, s):
    return ((v << s) | (v >> (8 - s))) & 0xFF


def _ror8(v, s):
    return ((v >> s) | (v << (8 - s))) & 0xFF


def build_rev_math_check(cfg):
    flag = cfg["flag"].encode()
    src0 = _src("rev-math-check")
    MUL = _get_define(src0, "MUL")
    ADD = _get_define(src0, "ADD")
    ROL = _get_define(src0, "ROL")
    n = len(flag)
    seed = int(hashlib.sha256(flag).hexdigest()[:8], 16)
    perm = list(range(n))
    random.Random(seed).shuffle(perm)
    a = [b ^ 0xA7 for b in flag]
    b = [0] * n
    for i in range(n):
        b[perm[i]] = a[i]
    T = [_rol8((b[i] * MUL + i * 7 + ADD) & 0xFF, ROL) for i in range(n)]

    src = _set_define(src0, "LEN", n)
    src = _set_carr(src, "PERM", perm)
    src = _set_carr(src, "T", T)
    data = _build(cfg, "rev-math-check", src, filename="challenge")

    got = None
    for off in range(len(data) - n):
        if sorted(data[off:off + n]) != list(range(n)):
            continue
        p = list(data[off:off + n])
        for t_off in range(len(data) - n):
            if abs(t_off - off) < n:
                continue
            Tb = data[t_off:t_off + n]
            c = [_ror8(x, ROL) for x in Tb]
            bb = [((c[i] - i * 7 - ADD) * pow(MUL, -1, 256)) & 0xFF for i in range(n)]
            cand = bytes(bb[p[i]] ^ 0xA7 for i in range(n))
            if cand.startswith(b"flag{") and cand.endswith(b"}"):
                got = cand
                break
        if got:
            break
    if got != flag:
        raise AssertionError("rev-math-check: 还原失败 %r" % (got,))
    _run_expect(cfg, "challenge", flag.decode())
    return data


# --------------------------------------------------------------------------
# 9. rev-anti-debug
# --------------------------------------------------------------------------
def build_rev_anti_debug(cfg):
    flag = cfg["flag"].encode()
    src0 = _src("rev-anti-debug")
    K = bytes(_get_carr(src0, "K"))
    T = []
    for i, ch in enumerate(flag):
        v = ch ^ K[i % 8]
        v = ((v << 4) | (v >> 4)) & 0xFF
        v = (v + i * 0x13) & 0xFF
        T.append(v)
    src = _set_define(src0, "LEN", len(flag))
    src = _set_carr(src, "T", T)
    data = _build(cfg, "rev-anti-debug", src, filename="challenge")

    got = None
    for t_off in range(len(data) - len(flag)):
        Tb = data[t_off:t_off + len(flag)]
        for k_off in range(max(0, t_off - 128), min(len(data) - 8, t_off + 128)):
            if (_ror8(Tb[0], 4) ^ data[k_off]) != ord("f"):
                continue
            Kb = data[k_off:k_off + 8]
            out = bytes(((Tb[i] - i * 0x13) & 0xFF) for i in range(len(flag)))
            out = bytes(_ror8(v, 4) ^ Kb[i % 8] for i, v in enumerate(out))
            if out == flag:
                got = out
                break
        if got:
            break
    if got != flag:
        raise AssertionError("rev-anti-debug: 还原失败 %r" % (got,))
    _run_expect(cfg, "challenge", flag.decode())
    return data

# --------------------------------------------------------------------------
# 10. rev-tiny-vm
# --------------------------------------------------------------------------
_HALT_FAIL, _HALT_OK, _LD, _XOR, _ADD, _SUB, _MUL, _ROL, _ROR, _CMP_BYTE, _JMP, _JZ, _CMP_LEN, _NOT = range(14)


def _vm_inv(a):
    for x in range(1, 256):
        if (a * x) % 256 == 1:
            return x
    raise ValueError("不可逆的乘数 %d" % a)


def _vm_build(flag):
    n = len(flag)
    rng = random.Random(int(hashlib.sha256(flag).hexdigest()[:8], 16))
    odd = [3, 5, 7, 9, 11, 13, 0x1b, 0x35, 0x4d, 0x6b, 0x8f, 0xa1, 0xc9, 0xe5]
    prog = bytearray([_CMP_LEN, n])
    for i, ch in enumerate(flag):
        a = rng.choice(odd)
        b = rng.randrange(0, 256)
        k = rng.randrange(0, 256)
        t = rng.randrange(1, 8)
        target = _ror8(ch, t) ^ k
        s = ((target - b) * _vm_inv(a)) % 256
        prog += bytes([_LD, s, _MUL, a, _ADD, b, _XOR, k, _ROL, t, _CMP_BYTE])
        if i % 5 == 4:
            junk = bytes(rng.randrange(0, 256) for _ in range(3))
            prog += bytes([_JMP, len(junk)]) + junk
    prog += bytes([_HALT_OK])
    return bytes(prog)


def _vm_emulate(code):
    r, pc, out = 0, 0, bytearray()
    while pc < len(code):
        op = code[pc]
        pc += 1
        if op == _HALT_FAIL:
            return None
        if op == _HALT_OK:
            return bytes(out)
        if op == _LD:
            r = code[pc]; pc += 1
        elif op == _XOR:
            r ^= code[pc]; pc += 1
        elif op == _ADD:
            r = (r + code[pc]) & 0xFF; pc += 1
        elif op == _SUB:
            r = (r - code[pc]) & 0xFF; pc += 1
        elif op == _MUL:
            r = (r * code[pc]) & 0xFF; pc += 1
        elif op == _ROL:
            r = _rol8(r, code[pc] & 7); pc += 1
        elif op == _ROR:
            r = _ror8(r, code[pc] & 7); pc += 1
        elif op == _CMP_BYTE:
            out.append(r)
        elif op == _JMP:
            d8 = code[pc]; pc += 1; pc += d8 if d8 < 0x80 else d8 - 256
        elif op == _JZ:
            d8 = code[pc]; pc += 1
            if r == 0:
                pc += d8 if d8 < 0x80 else d8 - 256
        elif op == _CMP_LEN:
            pc += 1
        elif op == _NOT:
            r = (~r) & 0xFF
        else:
            return None
    return None


def build_rev_tiny_vm(cfg):
    flag = cfg["flag"].encode()
    prog = _vm_build(flag)
    src = _set_carr(_src("rev-tiny-vm"), "PROG", list(prog))
    data = _build(cfg, "rev-tiny-vm", src, filename="challenge")

    got = None
    for off in range(len(data) - 4):
        if data[off] == _CMP_LEN and data[off + 1] == len(flag) and data[off + 2] == _LD:
            got = _vm_emulate(data[off:off + len(prog)])
            if got:
                break
    if got != flag:
        raise AssertionError("rev-tiny-vm: 模拟结果 %r" % (got,))
    _run_expect(cfg, "challenge", flag.decode())
    return data


# --------------------------------------------------------------------------
# 11. rev-hello-toolchain
# --------------------------------------------------------------------------
def _split_at(inner, want, seed_text, after_underscore=False):
    """在 inner 里挑 want 个切点，切点优先落在下划线上（各段非空）。

    after_underscore=True  -> 切点在下划线**之后**（前一段以 '_' 结尾）
    after_underscore=False -> 切点在下划线**之前**（后一段以 '_' 开头）
    """
    n = len(inner)
    cand = []
    for pos in range(1, n):
        if inner[pos] == "_":
            cand.append(pos + 1 if after_underscore else pos)
    cuts, lo = [], 1
    for j in range(want):
        target = round(n * (j + 1) / (want + 1))
        best, bestd = None, 10 ** 6
        for pos in cand:
            if pos < lo or pos > n - (want - j):
                continue
            d = abs(pos - target)
            if d < bestd:
                best, bestd = pos, d
        if best is None:                       # 没有可用下划线就均分
            best = min(max(target, lo), n - (want - j))
        cuts.append(best)
        lo = best + 1
    return cuts


def build_rev_hello_toolchain(cfg):
    flag = cfg["flag"]
    inner = flag[5:-1]
    assert len(inner) >= 4, "flag 太短"
    cut = _split_at(inner, 1, flag, after_underscore=True)[0]
    key1, key2 = inner[:cut], inner[cut:]
    assert key1 and key2

    src = _src("rev-hello-toolchain")
    src = src.replace("hello_re.exe", "hello_re")
    src = _set_cstr(src, "KEY1", key1)
    body = "\n".join("    dst[%d] = '%s';" % (i, c) for i, c in enumerate(key2))
    body += "\n    dst[%d] = 0;" % len(key2)
    pat = re.compile(r"(?ms)(static void build_key2\(volatile char \*dst\)\s*\{).*?(\n\})")
    src = pat.sub(lambda m: m.group(1) + "\n" + body + m.group(2), src, count=1)

    data = _build(cfg, "rev-hello-toolchain", src, filename="hello_re")

    # 静态自检：strings 拿 KEY1，objdump 拿 KEY2
    wd = _workdir()
    try:
        shutil.copy2(os.path.join(cfg["dir"], "files", "hello_re"), os.path.join(wd, "hello_re"))
        out = _sh(wd, "strings -n 4 hello_re | grep -x '%s' | head -1" % key1)
        got1 = out.strip()
        out2 = _sh(wd, "objdump -d hello_re | grep movb")
        pairs = []
        for line in out2.splitlines():
            m = re.search(r"movb\s+\$0x([0-9a-f]{2}),(?:0x([0-9a-f]+))?\(%rsp\)", line)
            if m:
                pairs.append((int(m.group(2), 16) if m.group(2) else 0, int(m.group(1), 16)))
        pairs.sort()
        got2 = bytes(v for _, v in pairs if v).decode(errors="replace")
    finally:
        shutil.rmtree(wd, ignore_errors=True)
    if got1 != key1 or got2 != key2:
        raise AssertionError("rev-hello-toolchain: KEY1=%r KEY2=%r" % (got1, got2))
    _run_expect(cfg, "hello_re", flag)
    return data


# --------------------------------------------------------------------------
# 12. rev-hello-decompile
# --------------------------------------------------------------------------
def build_rev_hello_decompile(cfg):
    flag = cfg["flag"]
    inner = flag[5:-1]
    cuts = _split_at(inner, 3, flag, after_underscore=False)
    pieces = [inner[:cuts[0]], inner[cuts[0]:cuts[1]], inner[cuts[1]:cuts[2]], inner[cuts[2]:]]
    assert all(pieces), "切段失败：%r" % (pieces,)
    # 拼接顺序是 P3 + P4 + P1 + P2
    p3, p4, p1, p2 = pieces
    src = _src("rev-hello-decompile")
    src = _set_cstr(src, "P1", p1)
    src = _set_cstr(src, "P2", p2)
    src = _set_cstr(src, "P3", p3)
    src = _set_cstr(src, "P4", p4)

    data = _build(cfg, "rev-hello-decompile", src, flags="-O2 -no-pie", filename="hello_decomp")

    # 静态自检：符号 + 反汇编里的 lea 顺序 -> 读回四段 -> 拼接
    syms = _elf_symbols(data)
    wd = _workdir()
    try:
        shutil.copy2(os.path.join(cfg["dir"], "files", "hello_decomp"),
                     os.path.join(wd, "hello_decomp"))
        out = _sh(wd, "objdump -d hello_decomp | grep -oE 'lea\\s+0x[0-9a-f]+\\(%rip\\),%rsi\\s+#\\s+[0-9a-f]+ <P[1-4]>'")
    finally:
        shutil.rmtree(wd, ignore_errors=True)
    order = re.findall(r"<P([1-4])>", out)
    if not order:
        raise AssertionError("rev-hello-decompile: 没解析到拼接顺序")
    joined = ""
    for idx in order:
        va = syms.get("P" + idx)
        off = _va_to_off(data, va)
        end = data.index(b"\x00", off)
        joined += data[off:end].decode()
    if joined != inner:
        raise AssertionError("rev-hello-decompile: 拼接得到 %r" % (joined,))
    _run_expect(cfg, "hello_decomp", flag)
    return data


# --------------------------------------------------------------------------
# 13. custom-b64-ctf
# --------------------------------------------------------------------------
STD_B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"


def _b64_custom(inp, tbl):
    out, i = [], 0
    n = len(inp)
    while i + 2 < n:
        out.append(tbl[(inp[i] >> 2) & 0x3F])
        out.append(tbl[((inp[i] & 0x03) << 4) | ((inp[i + 1] >> 4) & 0x0F)])
        out.append(tbl[((inp[i + 1] & 0x0F) << 2) | ((inp[i + 2] >> 6) & 0x03)])
        out.append(tbl[inp[i + 2] & 0x3F])
        i += 3
    if n - i == 1:
        out.append(tbl[(inp[i] >> 2) & 0x3F])
        out.append(tbl[(inp[i] & 0x03) << 4])
        out += ["=", "="]
    elif n - i == 2:
        out.append(tbl[(inp[i] >> 2) & 0x3F])
        out.append(tbl[((inp[i] & 0x03) << 4) | ((inp[i + 1] >> 4) & 0x0F)])
        out.append(tbl[(inp[i + 1] & 0x0F) << 2])
        out.append("=")
    return "".join(out)


def _b64_decode_custom(ct, tbl):
    std = "".join("=" if c == "=" else STD_B64[tbl.index(c)] for c in ct)
    import base64 as _b64
    return _b64.b64decode(std)


def build_custom_b64(cfg):
    flag = cfg["flag"]
    src0 = _src("custom-b64-ctf")
    tbl = _get_cstr(src0, "TBL")
    assert len(tbl) == 64, "字母表长度不对"
    ct = _b64_custom(flag.encode(), tbl)
    src = _set_cstr(src0, "TARGET", ct)
    data = _build(cfg, "custom-b64-ctf", src, filename="challenge")

    m = re.search(rb"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{64}(?![A-Za-z0-9+/])", data)
    if not m:
        raise AssertionError("custom-b64: 二进制里没有字母表")
    tbl2 = m.group().decode()
    span = m.span()
    ct2 = None
    for mm in re.finditer(rb"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{16,}={0,2}(?![A-Za-z0-9+/])", data):
        s, e = mm.span()
        if e <= span[0] or s >= span[1]:
            ct2 = mm.group().decode()
            break
    got = _b64_decode_custom(ct2, tbl2).decode()
    if got != flag:
        raise AssertionError("custom-b64: 还原 %r" % (got,))
    _run_expect(cfg, "challenge", flag)
    return data


# --------------------------------------------------------------------------
BUILDERS = {
    "rev-strings": build_rev_strings,
    "rev-xor-loop": build_rev_xor_loop,
    "rev-flip-branch": build_rev_flip_branch,
    "rev-serial-eq": build_rev_serial_eq,
    "rev-upx": build_rev_upx,
    "rev-rc4": build_rev_rc4,
    "rev-smc": build_rev_smc,
    "rev-math-check": build_rev_math_check,
    "rev-anti-debug": build_rev_anti_debug,
    "rev-tiny-vm": build_rev_tiny_vm,
    "rev-hello-toolchain": build_rev_hello_toolchain,
    "rev-hello-decompile": build_rev_hello_decompile,
    "custom-b64-ctf": build_custom_b64,
}


# --------------------------------------------------------------------------
# 自测：python builders_re.py [key ...]
#   * 用各题原始 flag 生成一遍（顺带验证 builder 能跑）
#   * 用原 exp/solve.py 解新产品，必须解出原 flag（正向算法与 C 逐字节对拍）
# --------------------------------------------------------------------------
def _orig_flag(key):
    p = os.path.join(SRC_ROOT, key, "checker", "main.rx")
    txt = open(p, encoding="utf-8").read()
    return re.search(r'const FLAG = "([^"]+)"', txt).group(1)


def _selftest(keys):
    ok = True
    for key in keys:
        flag = _orig_flag(key)
        tmp = tempfile.mkdtemp(prefix="re_selftest_")
        try:
            cfg = {"key": key, "flag": flag, "dir": tmp}
            data = BUILDERS[key](cfg)
            name = os.listdir(os.path.join(tmp, "files"))[0]
            size = len(data)
            # 用现有 exp 反解（独立于 builder 的实现）
            line = "-"
            exp = os.path.join(SRC_ROOT, key, "exp", "solve.py")
            if os.path.isfile(exp) and key == "rev-flip-branch":
                line = "exp=skipped（该 exp 写死了原 PIE 的 patch 偏移）"
            elif os.path.isfile(exp):
                shutil.copytree(os.path.join(SRC_ROOT, key, "exp"), os.path.join(tmp, "exp"))
                raw = _sh(tmp, "cd exp && python3 solve.py 2>/dev/null", check=False)
                if not re.search(r"flag{", raw):
                    raw = _sh(tmp, "cd files && python3 ../exp/solve.py 2>/dev/null", check=False)
                got = re.search(r"flag{[a-z0-9_]+}", raw)
                got = got.group() if got else ""
                line = "exp=%s" % ("OK" if got == flag else "MISMATCH(%s)" % (got or "no-flag"))
                ok &= got == flag
            print("%-22s %-14s %7d B  %s  %s" % (key, name, size, _sha(data)[:16], line))
        except Exception as e:
            ok = False
            print("%-22s FAIL %s" % (key, e))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
    print("\nSELFTEST", "PASS" if ok else "FAIL")



# --------------------------------------------------------------------------
# 逐字节对拍：拿「原始 flag」跑一遍正向算法，产物必须与现有附件里的常量一致
# --------------------------------------------------------------------------
def _orig_bin(key):
    d = os.path.join(SRC_ROOT, key, "files")
    for f in sorted(os.listdir(d)):
        p = os.path.join(d, f)
        if os.path.isfile(p):
            return open(p, "rb").read()
    raise FileNotFoundError(d)


def _forward_consts(key, flag):
    """返回 [(说明, bytes)] —— 该题在新 flag 下应出现的常量。"""
    src = _src(key)
    if key == "rev-strings":
        return [("TARGET", flag.encode())]
    if key == "rev-xor-loop":
        KEY = bytes(_get_carr(src, "KEY"))
        return [("ENC", bytes(b ^ KEY[i % len(KEY)] ^ (i & 0xFF) for i, b in enumerate(flag.encode())))]
    if key == "rev-flip-branch":
        MASK = _get_define(src, "MASK")
        return [("ENC", bytes(b ^ MASK for b in flag.encode()))]
    if key == "rev-serial-eq":
        M = _matrix_4x4(src)
        f = flag.encode()
        x = list(f) + [0] * ((-len(f)) % 4)
        T = []
        for b in range(len(x) // 4):
            blk = x[4 * b:4 * b + 4]
            for r in range(4):
                T.append(sum(M[r][c] * blk[c] for c in range(4)) & 0xFF)
        return [("T", bytes(T))]
    if key == "rev-rc4":
        return [("ENC", _rc4(bytes(_get_carr(src, "KEY")), flag.encode()))]
    if key == "rev-smc":
        TK = bytes(_get_carr(src, "TK"))
        return [("ENC", bytes(b ^ TK[i & 7] for i, b in enumerate(flag.encode())))]
    if key == "rev-math-check":
        f = flag.encode()
        n = len(f)
        seed = int(hashlib.sha256(f).hexdigest()[:8], 16)
        perm = list(range(n))
        random.Random(seed).shuffle(perm)
        MUL, ADD, ROL = (_get_define(src, "MUL"), _get_define(src, "ADD"), _get_define(src, "ROL"))
        a = [b ^ 0xA7 for b in f]
        b = [0] * n
        for i in range(n):
            b[perm[i]] = a[i]
        T = [_rol8((b[i] * MUL + i * 7 + ADD) & 0xFF, ROL) for i in range(n)]
        return [("PERM", bytes(perm)), ("T", bytes(T))]
    if key == "rev-anti-debug":
        K = bytes(_get_carr(src, "K"))
        f = flag.encode()
        T = []
        for i, ch in enumerate(f):
            v = ch ^ K[i % 8]
            v = ((v << 4) | (v >> 4)) & 0xFF
            T.append((v + i * 0x13) & 0xFF)
        return [("T", bytes(T))]
    if key == "rev-tiny-vm":
        return [("PROG", _vm_build(flag.encode()))]
    if key == "rev-hello-toolchain":
        inner = flag[5:-1]
        cut = _split_at(inner, 1, flag, after_underscore=True)[0]
        return [("KEY1", inner[:cut].encode())]
    if key == "rev-hello-decompile":
        inner = flag[5:-1]
        cuts = _split_at(inner, 3, flag, after_underscore=False)
        pieces = [inner[:cuts[0]], inner[cuts[0]:cuts[1]], inner[cuts[1]:cuts[2]], inner[cuts[2]:]]
        return [("P%d" % i, p.encode()) for i, p in enumerate(pieces, 1)]
    if key == "custom-b64-ctf":
        return [("TARGET", _b64_custom(flag.encode(), _get_cstr(src, "TBL")).encode())]
    return []


def _match_special(key, flag, blob):
    """两道「参数随机」的题：用附件里的参数复算，验证正向变换。"""
    src = _src(key)
    n = len(flag)
    if key == "rev-math-check":
        perm = None
        for off in range(len(blob) - n):
            if sorted(blob[off:off + n]) == list(range(n)):
                perm = list(blob[off:off + n])
                break
        if perm is None:
            return False, "附件里找不到 PERM"
        MUL, ADD, ROL = (_get_define(src, "MUL"), _get_define(src, "ADD"), _get_define(src, "ROL"))
        a = [b ^ 0xA7 for b in flag.encode()]
        b = [0] * n
        for i in range(n):
            b[perm[i]] = a[i]
        T = bytes(_rol8((b[i] * MUL + i * 7 + ADD) & 0xFF, ROL) for i in range(n))
        return (T in blob), "PERM(附件)+ T 复算"
    if key == "rev-tiny-vm":
        for off in range(len(blob) - 4):
            if blob[off] == _CMP_LEN and blob[off + 1] == n and blob[off + 2] == _LD:
                got = _vm_emulate(blob[off:off + 400])
                return (got == flag.encode()), "模拟附件里的 PROG"
        return False, "附件里找不到 PROG"
    return None, ""


def _match_originals(keys):
    ok = True
    for key in keys:
        flag = _orig_flag(key)
        if key == "rev-math-check" or key == "rev-tiny-vm":
            good, note = _match_special(key, flag, _orig_bin(key))
            ok &= good
            print("%-22s %s %s" % (key, "MATCH" if good else "MISMATCH", note))
            continue
        if key == "rev-upx":
            print("%-22s SKIP（附件是壳后文件，由 selftest 的 upx -d 验证）" % key)
            continue
        blob = _orig_bin(key)
        bad = []
        for label, want in _forward_consts(key, flag):
            if want not in blob:
                bad.append(label)
        if bad:
            ok = False
            print("%-22s MISMATCH %s" % (key, ",".join(bad)))
        else:
            print("%-22s MATCH %s" % (key, ", ".join(l for l, _ in _forward_consts(key, flag))))
    print("\nBYTE-MATCH", "PASS" if ok else "FAIL")
    return ok

if __name__ == "__main__":
    import sys
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    if "--match" in sys.argv[1:]:
        _match_originals(args or sorted(BUILDERS))
    else:
        _selftest(args or sorted(BUILDERS))