# -*- coding: utf-8 -*-
"""生成每道题的 README.md / checker/main.rx / verify.py，以及总索引与 verify_all。

与 make_challenges.py 分工：
  make_challenges.py  -> 造附件（二进制）
  make_docs.py        -> 写文档、checker、自校验脚本
"""
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
with open(os.path.join(ROOT, "_build", "challenges.json"), encoding="utf-8") as f:
    META = json.load(f)

# ---------------------------------------------------------------------------
# checker/main.rx —— ret2shell 可直接用（静态 flag）
# ---------------------------------------------------------------------------
CHECKER = '''// checker/main.rx · %(title)s（%(name)s）
//
// ret2shell 评测脚本：静态 flag 版本。
//   check   —— 比对选手提交的 flag
//   environ —— 附件型题目没有容器，返回空对象即可
//
// 想换成「动态附件评测」：用平台自带的 mapped 预设替换本文件，
// 由它按用户映射附件并在附件里注入每用户唯一的 flag；本文件的 check 逻辑不再需要。

const FLAG = "%(flag)s";

pub fn check(bucket, user, team, submission) {
    if submission.content == FLAG {
        return Ok((true, "Correct!", None));
    }
    Ok((false, "Incorrect!", None));
}

pub async fn environ(bucket, user, team) {
    Ok(#{})
}
'''


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


# ---------------------------------------------------------------------------
# 每题的自校验脚本（stdlib only；需要图片的用 Pillow）
# ---------------------------------------------------------------------------
VERIFY = {}

VERIFY["enc-chain-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：按题解的三层解码跑一遍，必须解出 flag。"""
import base64, codecs, os, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
raw = open(os.path.join(HERE, "files", "readme.txt"), encoding="utf-8-sig").read()
blob = max(re.findall(r"[A-Za-z0-9+/=]{40,}", raw), key=len)
inner_hex = base64.b64decode(blob).decode()          # 第 1 层：base64
rot = bytes.fromhex(inner_hex).decode()              # 第 2 层：hex
flag = codecs.decode(rot, "rot13")                   # 第 3 层：rot13
print("  解出:", flag)
assert flag == EXPECT, "解码结果与预期不符"
print("  PASS enc-chain-ctf")
'''

VERIFY["file-header-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：确认文件头确实被抹掉，补回 89 50 4E 47 后能正常打开。"""
import os, sys, tempfile
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, "files", "logo.png")
data = open(p, "rb").read()
assert data[:4] == b"\x00\x00\x00\x00", "文件头没有被破坏，题目不对"
assert data[4:8] == b"\x0d\x0a\x1a\x0a", "PNG 签名后半段应仍在，否则不可解"
fixed = b"\x89PNG" + data[4:]
assert fixed[1:4] == b"PNG"
out = os.path.join(tempfile.gettempdir(), "misc_file_header_fixed.png")
open(out, "wb").write(fixed)
im = Image.open(out); im.load()
print("  修复后: %%s  %%dx%%d" %% (im.format, im.width, im.height))
assert im.format == "PNG" and im.width == 1280 and im.height > 500
print("  修复图已写出:", out)
print("  预期 flag:", "%(flag)s")
print("  PASS file-header-ctf（图片内容需人工看图确认）")
'''

VERIFY["append-zip-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：从 PNG 尾部切出 zip，读出 flag。"""
import io, os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
data = open(os.path.join(HERE, "files", "cover.png"), "rb").read()
iend = data.find(b"IEND")
assert iend != -1, "不是 PNG"
off = data.find(b"PK\x03\x04", iend)
assert off != -1, "IEND 之后没有 ZIP —— 题目不对"
print("  PNG 长度: %%d, ZIP 偏移: %%d, 附加字节: %%d" %% (iend + 8, off, len(data) - off))
z = zipfile.ZipFile(io.BytesIO(data[off:]))
flag = z.read("flag.txt").decode().strip()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS append-zip-ctf")
'''

VERIFY["fake-zip-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：先确认「假装加密」，清除标志位后读出 flag。"""
import os, struct, sys, tempfile, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
p = os.path.join(HERE, "files", "secret.zip")
raw = open(p, "rb").read()

def clear_bit(data):
    out = bytearray(data)
    for sig, off in ((b"PK\x03\x04", 6), (b"PK\x01\x02", 8)):
        i = out.find(sig)
        while i != -1:
            (fb,) = struct.unpack_from("<H", out, i + off)
            struct.pack_into("<H", out, i + off, fb & ~1)
            if sig == b"PK\x03\x04":
                nlen, xlen = struct.unpack_from("<HH", out, i + 26)
                (clen,) = struct.unpack_from("<I", out, i + 18)
                nxt = i + 30 + nlen + xlen + clen
            else:
                nlen, xlen, clen = struct.unpack_from("<HHH", out, i + 28)
                nxt = i + 46 + nlen + xlen + clen
            i = out.find(sig, nxt)
    return bytes(out)

try:
    zipfile.ZipFile(p).read("flag.txt")
    raise AssertionError("没有加密位，题目不对")
except RuntimeError as e:
    print("  原文件确实「要求密码」:", str(e)[:60])
fixed = clear_bit(raw)
out = os.path.join(tempfile.gettempdir(), "misc_fake_zip_fixed.zip")
open(out, "wb").write(fixed)
flag = zipfile.ZipFile(out).read("flag.txt").decode().strip()
print("  清除标志位后解出:", flag)
assert flag == EXPECT
print("  PASS fake-zip-ctf")
'''

VERIFY["lsb-stego-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：按 RGB 顺序读最低位，遇 0xFF 结束。"""
import os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
im = Image.open(os.path.join(HERE, "files", "photo.png")).convert("RGB")
px = im.load()
bits = []
for y in range(im.height):
    for x in range(im.width):
        r, g, b = px[x, y]
        bits.extend((r & 1, g & 1, b & 1))
        if len(bits) > 8 * 400:
            break
    else:
        continue
    break
out = bytearray()
for i in range(0, len(bits) - 7, 8):
    v = 0
    for k in range(8):
        v = (v << 1) | bits[i + k]
    if v == 0xFF:
        break
    out.append(v)
flag = out.decode("utf-8", "replace")
print("  解出:", flag)
assert flag == EXPECT
print("  PASS lsb-stego-ctf")
'''

VERIFY["exif-meta-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：从 EXIF ImageDescription 取 base64 并解码。"""
import base64, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
ex = Image.open(os.path.join(HERE, "files", "photo.jpg")).getexif()
desc = ex.get(0x010E)
assert desc, "ImageDescription 为空"
flag = base64.b64decode(desc).decode()
print("  ImageDescription:", desc[:40] + ("…" if len(desc) > 40 else ""))
print("  解出:", flag)
assert flag == EXPECT
print("  PASS exif-meta-ctf")
'''

VERIFY["traffic-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：解析 pcap，从 HTTP 请求行里取出 base64 并解码。"""
import base64, os, re, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
data = open(os.path.join(HERE, "files", "capture.pcap"), "rb").read()
magic = struct.unpack_from("<I", data, 0)[0]
assert magic in (0xA1B2C3D4, 0xD4C3B2A1), "不是 pcap"
off, n, payloads = 24, 0, []
while off + 16 <= len(data):
    _, _, cap, orig = struct.unpack_from("<IIII", data, off)
    off += 16
    payloads.append(data[off:off + cap])
    off += cap
    n += 1
text = b"".join(payloads).decode("latin-1")
m = re.search(r"GET /api/log\?q=([A-Za-z0-9+/=]+)", text)
assert m, "没找到关键请求"
flag = base64.b64decode(m.group(1)).decode()
print("  共解析 %%d 个包" %% n)
print("  请求行:", text[m.start():m.start() + 46].replace("\r\n", "\\r\\n"))
print("  解出:", flag)
assert flag == EXPECT
print("  PASS traffic-ctf")
'''

VERIFY["geo-hongyadong-ctf"] = r'''# -*- coding: utf-8 -*-

"""自校验：读出 EXIF 的 GPS 与拍摄时间，按题解换算 UTC 并拼出 flag。"""
import datetime, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
PREFIX = "chongqing_hongyadong"

ex = Image.open(os.path.join(HERE, "files", "photo.jpg")).getexif()
g = ex.get_ifd(0x8825)
assert g, "EXIF 里没有 GPS —— 题目要求保留元数据"
one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
lat, lon = deg(g[2]), deg(g[4])
dt_raw = ex.get(0x0132)
print("  GPS: %.6f, %.6f" % (lat, lon))
print("  拍摄时间(本地):", dt_raw)
assert abs(lat - 29.566714) < 0.005 and abs(lon - 106.575697) < 0.005, "坐标不对"
assert dt_raw, "没有拍摄时间"
local = datetime.datetime.strptime(dt_raw, "%Y:%%m:%%d %%H:%%M:%%S")
utc = local - datetime.timedelta(hours=8)
flag = "flag{%%s_%%s_%%s}" %% (PREFIX, local.strftime("%%Y%%m%%d"), utc.strftime("%%H%%M"))
print("  UTC 时刻:", utc.strftime("%%H%%M"))
print("  拼出:", flag)
assert flag == EXPECT
print("  PASS geo-hongyadong-ctf（EXIF 已保留，含时间换算）")
'''


VERIFY["geo-nanjinglu-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：照片存在、可解码、元数据已清空。"""
import os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
ANSWER = "%(flag)s"
p = os.path.join(HERE, "files", "photo.jpg")
im = Image.open(p); im.load()
print("  照片: %%s  %%dx%%d" %% (im.format, im.width, im.height))
ex = im.getexif()
leak = {k: v for k, v in ex.items() if k in (0x010E, 0x0132, 0x8825, 0x9003)}
assert not leak, "元数据没清干净: %%r" %% leak
print("  元数据: 已清空")
print("  预期答案:", ANSWER)
print("  PASS geo-nanjinglu-ctf（招牌文字由人工看图确认）")
'''


VERIFY["exif-gps-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：五张照片里找出带 GPS 的那张，换算坐标并断言落点。"""
import io, os, struct, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
TARGET = (29.332338, 110.426066)          # 东方明珠

def gps_of(path):
    ex = Image.open(path).getexif()
    g = ex.get_ifd(0x8825)
    if not g or 2 not in g or 4 not in g:
        return None
    one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
    deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
    lat, lon = deg(g[2]), deg(g[4])
    return (-lat if g.get(1) == "S" else lat), (-lon if g.get(3) == "W" else lon)

d = os.path.join(HERE, "files", "photos")
found = []
for name in sorted(os.listdir(d)):
    p = os.path.join(d, name)
    g = gps_of(p)
    print("  %-14s %%s" %% (name, ("%%s" %% (g,)) if g else "无 GPS"))
    if g:
        found.append((name, g))
assert len(found) == 1, "带 GPS 的照片应当只有一张，实际 %%d 张" %% len(found)
name, (lat, lon) = found[0]
print("  命中: %%s  %%.5f, %%.5f" %% (name, lat, lon))
assert abs(lat - TARGET[0]) < 0.01 and abs(lon - TARGET[1]) < 0.01, "坐标不在预期地标附近"
print("  预期答案:", EXPECT)
print("  PASS exif-gps-ctf")
'''

VERIFY["exif-thumb-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：从 IFD1 取出缩略图，确认是可解码的 JPEG（内容需人工看图）。"""
import io, os, struct, sys, tempfile
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
raw = open(os.path.join(HERE, "files", "photo.jpg"), "rb").read()
i = raw.find(b"Exif\x00\x00")
assert i > 0, "没有 EXIF 段"
t = raw[i + 6:]
first = struct.unpack_from("<I", t, 4)[0]
n = struct.unpack_from("<H", t, first)[0]
nxt = struct.unpack_from("<I", t, first + 2 + n * 12)[0]
assert nxt, "没有 IFD1（也就没有缩略图）—— 题目不对"
m = struct.unpack_from("<H", t, nxt)[0]
tags = {}
for k in range(m):
    tag = struct.unpack_from("<H", t, nxt + 2 + k * 12)[0]
    tags[tag] = struct.unpack_from("<I", t, nxt + 2 + k * 12 + 8)[0]
off, ln = tags.get(0x0201), tags.get(0x0202)
assert off and ln, "IFD1 里缺 ThumbnailOffset/Length"
th = t[off:off + ln]
assert th[:2] == b"\xff\xd8" and th[-2:] == b"\xff\xd9", "缩略图不是完整 JPEG"
im = Image.open(io.BytesIO(th)); im.load()
out = os.path.join(tempfile.gettempdir(), "misc_exif_thumb.jpg")
open(out, "wb").write(th)
print("  主图尺寸:", Image.open(os.path.join(HERE, "files", "photo.jpg")).size)
print("  缩略图: %%d 字节  %%s %%s" %% (ln, im.format, im.size))
print("  已导出到:", out)
print("  预期 flag:", "%(flag)s")
print("  PASS exif-thumb-ctf（缩略图内容需人工看图确认）")
'''

VERIFY["exif-erase-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：before 的 APP1 段里有 ZIP；after 抹掉后就没了。"""
import io, os, struct, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
before = open(os.path.join(HERE, "files", "before.jpg"), "rb").read()
after = open(os.path.join(HERE, "files", "after.jpg"), "rb").read()

def app1_span(raw):
    i = raw.find(b"\xff\xe1")
    assert i > 0, "没有 APP1"
    ln = struct.unpack_from(">H", raw, i + 2)[0]
    return i, i + 2 + ln

b0, b1 = app1_span(before)
blk = before[b0:b1]
print("  before: %%d 字节, APP1 段 [%%d, %%d) 共 %%d 字节" %% (len(before), b0, b1, b1 - b0))
print("  after : %%d 字节, 还含 Exif 标记: %%s" %% (len(after), b"Exif\x00\x00" in after))
assert b"Exif\x00\x00" in blk, "before 的 APP1 不是 EXIF"
assert b"PK\x03\x04" not in after, "after 应当已经把 EXIF 抹干净"
off = blk.find(b"PK\x03\x04")
assert off > 0, "before 的 EXIF 段里没有 ZIP —— 题目不对"
z = zipfile.ZipFile(io.BytesIO(blk[off:]))
flag = z.read("flag.txt").decode().strip()
print("  从 APP1 尾部取出 ZIP，读 flag:", flag)
assert flag == EXPECT
assert len(before) > len(after), "抹除后应当更小"
print("  PASS exif-erase-ctf")
'''


VERIFY["png-text-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：读 PNG 的 tEXt 块，取出 base64 并解码。"""
import base64, os, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
raw = open(os.path.join(HERE, "files", "photo.png"), "rb").read()
assert raw[:8] == b"\x89PNG\r\n\x1a\n", "不是 PNG"
off, texts = 8, {}
while off < len(raw):
    ln = struct.unpack_from(">I", raw, off)[0]
    typ = raw[off + 4:off + 8]
    data = raw[off + 8:off + 8 + ln]
    if typ == b"tEXt":
        k, _, v = data.partition(b"\x00")
        texts[k.decode("latin-1")] = v.decode("latin-1")
    off += 12 + ln
    if typ == b"IEND":
        break
print("  tEXt 块:", {k: (v[:24] + "..." if len(v) > 24 else v) for k, v in texts.items()})
assert "Comment" in texts, "没有 Comment 文本块"
flag = base64.b64decode(texts["Comment"]).decode()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS png-text-ctf")
'''

# ---------------------------------------------------------------------------
# 每题的题解（Markdown 片段）
# ---------------------------------------------------------------------------
DOC = {}

DOC["enc-chain-ctf"] = dict(
    lesson="p12 Encoding · p13 编码/加密/哈希",
    summary="三层编码套娃，一层一层剥。考的是「认出字符集」而不是「会写解密脚本」。",
    steps=[
        ("先看字符集",
         "打开 `files/readme.txt`，正文里有一大段字符：\n\n"
         "```\n"
         "看起来像这样：TjNkMWJqTXdNak0zTkRrME1UTTVPVEk1T1M0eE5ERTBPVGd1TVRZNU1USTVMakUyT1RFeU5UUTVMakV6TVRrek5qUTVMakV6TVRrek5qUTVMakV6TVRrek5qUTVMakV6TVRrek5qUTVM...\n"
         "```\n\n"
         "只有大小写字母和数字、长度是 4 的倍数、结尾可能有 `=` —— 这些特征指向 **base64**。"),
        ("第一层：base64",
         "```bash\n"
         "python -c \"import base64,sys;print(base64.b64decode(open('readme.txt',encoding='utf-8-sig').read().split('下面是题目给的全部内容：')[1].split('提示')[0].strip()).decode())\"\n"
         "```\n\n"
         "结果是一串 **全是 0-9a-f 的十六进制** —— 下一层是 hex。"),
        ("第二层：十六进制",
         "```bash\n"
         "python -c \"print(bytes.fromhex('<上一步的结果>').decode())\"\n"
         "```\n\n"
         "结果是一段能读的英文，但单词是乱的：`syn0q1at` 这种 —— 典型 **ROT13**。"),
        ("第三层：ROT13",
         "```bash\n"
         "python -c \"import codecs;print(codecs.decode('<上一步的结果>','rot13'))\"\n"
         "```\n\n"
         "得到 flag。也可以直接把字符串贴进 CyberChef，用 Magic 一键识别。"),
    ],
    why="每一层都是**可逆的编码**，没有密钥：base64 只是换字符集、hex 只是换进制、ROT13 只是字母位移 13 位。\n"
        "所以「看到乱码先别慌」—— 先看字符集判断是哪一类编码，再决定用哪个工具拆。",
    defend="真实系统里编码不是加密，**把敏感数据 base64 一下就当成「加密」是常见错误**（比如把密码、token 塞进 URL 参数）。\n"
           "防御：敏感数据必须用真正的加密（AES 等）+ 传输层保护（HTTPS），而不是编码。",
)

DOC["file-header-ctf"] = dict(
    lesson="p27 十六进制视角 · p32 文件头速查表",
    summary="一张 PNG 的前 4 个字节被抹掉了。认不出类型 → 补回头部 → 看图。",
    steps=[
        ("看开头", "```bash\nxxd files/logo.png | head -2\n```\n\n"
                 "看到：\n"
                 "```\n00000000: 0000 0000 0d0a 1a0a 0000 000d 4948 4452  ............IHDR\n```\n\n"
                 "开头是 4 个 `00`，但紧跟着 `0d 0a 1a 0a` —— 这是 PNG 签名 `89 50 4E 47 0D 0A 1A 0A` 的**后半段**。\n"
                 "后面还能看到 `IHDR` 块，进一步确认是 PNG。"),
        ("补回文件头",
         "PNG 签名前 4 字节是 `89 50 4E 47`（`\\x89PNG`）。用 Python 补回去：\n\n"
         "```python\n"
         "data = open('files/logo.png','rb').read()\n"
         "open('fixed.png','wb').write(b'\\x89PNG' + data[4:])\n"
         "```\n\n"
         "`verify.py` 里就是这段代码（它把修复结果写到系统临时目录，不污染题目目录）。"),
        ("看图拿 flag",
         "打开 `_fixed.png` —— 一张真实的长城照片，底部说明条上就是 flag。"),
    ],
    why="文件类型由**字节**决定，不由后缀决定；而文件头（魔数）是程序判断类型的依据。\n"
        "所以「抹掉文件头」不等于「换了个文件」—— 只要剩下的结构还在，补回签名就能还原。",
    defend="上传文件的服务端必须**按内容判断类型**（`finfo_file` / 读魔数），而不是信任后缀或 Content-Type。\n"
           "这也是第二课上传题的同一条原则：客户端说的一切都要验证。",
)

DOC["append-zip-ctf"] = dict(
    lesson="p14 Stego · p33 附加数据",
    summary="一张动漫截图，能正常打开；但 IEND 之后还接着一个压缩包。",
    steps=[
        ("确认它真的是 PNG", "```bash\nfile files/cover.png\nxxd files/cover.png | head -2\n```"),
        ("看尾部", "```bash\nxxd files/cover.png | tail -3\n```\n\n"
                 "PNG 正常以 `IEND` 结尾，但这里 `IEND` 之后还有数据，且能看到 `50 4b 03 04`（`PK`，ZIP 的开头）。"),
        ("切出来", "```bash\nbinwalk -e files/cover.png\n# 或者手工按偏移切：\npython -c \"d=open('files/cover.png','rb').read();i=d.find(b'PK\\x03\\x04');open('inner.zip','wb').write(d[i:])\"\n```"),
        ("解压拿 flag", "```bash\nunzip inner.zip\ncat flag.txt\n```"),
    ],
    why="很多文件格式都有**明确的结束标记**（PNG 的 `IEND`、JPEG 的 `FFD9`），解析器读完标记就不再看了。\n"
        "于是「在结束标记后面追加数据」既能藏东西，又不影响原文件正常打开 —— 这就是附加数据隐写。",
    defend="接外部文件时按格式规范校验：PNG 必须 IEND 结束、大小与像素数匹配；\n"
           "上传/下载链路要检查是否存在尾随数据（`binwalk` 一扫就能发现）。",
)

DOC["fake-zip-ctf"] = dict(
    lesson="p33 伪加密",
    summary="解压时提示要密码，其实根本没有密码 —— ZIP 头里的加密标志位被置上了。",
    steps=[
        ("先确认「要密码」", "```bash\nunzip files/secret.zip\n# → [secret.zip] flag.txt Password:\n```\n"),
        ("看字节", "```bash\nxxd files/secret.zip | head -3\n```\n\n"
                 "本地文件头里有一个 2 字节的「通用标志位」（偏移 6），bit0 = 1 表示「已加密」。\n"
                 "真正的加密包，这个位置上还会有一大段加密数据；而这里除了标志位，别的都是明文 —— 所以是**伪加密**。\n\n"
                 "**注意**：中央目录（`PK\\x01\\x02`，偏移 +8）里也有一份同样的标志位，要多数工具看的是那一份。"),
        ("清掉标志位", "`verify.py` 里给了完整代码：把本地头和中央目录两处 bit0 都清掉，另存一份（写到系统临时目录）。"),
        ("解压拿 flag", "```bash\nunzip _fixed.zip && cat flag.txt\n```"),
    ],
    why="ZIP 是「本地头 + 中央目录」双份元数据。很多工具只读其中一份判断是否加密，\n"
        "把标志位置 1 就足以让工具弹密码框，而实际数据压根没加密 —— 这就是伪加密。",
    defend="不要把「文件加密」当成安全保证：真正的机密必须用标准加密（AES-256 的 7z / age / GPG），\n"
           "而不是靠 ZIP 的加密位。收到可疑压缩包时，也不要相信「要密码」这个提示。",
)

DOC["lsb-stego-ctf"] = dict(
    lesson="p14 Stego · p27 字节视角",
    summary="一张真实的阳朔村庄照，flag 藏在每个像素 RGB 的最低位里。",
    steps=[
        ("先排除元数据和附件", "```bash\nexiftool files/photo.png\nbinwalk files/photo.png\n```\n\n"
                          "两者都干净 —— 那就在**像素**里。"),
        ("看最低位", "每个像素有 R/G/B 三个 0-255 的值。把每个值的最后一位取出来，串成一串 bit，\n"
                  "每 8 bit 拼一个字节 —— 这就是 LSB 隐写。\n\n"
                  "```python\n"
                  "from PIL import Image\n"
                  "im = Image.open('files/photo.png').convert('RGB')\n"
                  "px = im.load(); bits = []\n"
                  "for y in range(im.height):\n"
                  "    for x in range(im.width):\n"
                  "        r, g, b = px[x, y]\n"
                  "        bits += [r & 1, g & 1, b & 1]\n"
                  "```\n\n"
                  "拼字节时遇到 `0xFF` 就停（这是约定的结束符）。"),
        ("工具也可以", "```bash\nzsteg files/photo.png      # 会自动试各种位平面\nstegsolve                    # 图形界面，切 Bit Plane\n```"),
    ],
    why="改掉最低位，颜色只变化 1/255 —— 人眼完全看不出，但信息已经存进去了。\n"
        "一张 640×400 的图有 768000 个 RGB 字节，理论能藏 96 KB 文本。",
    defend="隐写检测（steganalysis）靠统计特征：LSB 平面应当接近随机噪声，若呈现规整结构就说明被写过。\n"
           "社交平台通常会**重压缩图片**，这会破坏 LSB —— 这也是平台方顺手做的一层防护。",
)

DOC["exif-meta-ctf"] = dict(
    lesson="p37 图片题顺序（EXIF 优先） · p10 Recon",
    summary="像素里什么都没有，答案在相机写下的元数据里，还是 base64。",
    steps=[
        ("先看元数据", "```bash\nexiftool files/photo.jpg\n```\n\n"
                   "或 Windows：右键 → 属性 → 详细信息。\n\n"
                   "`ImageDescription` 里是一长串字符，形如 `ZmxhZ3ttM3Q0ZDR0NF90NGxrcyE=`。"),
        ("认出 base64", "结尾的 `=`、只有字母数字和 `+/` —— 直接解码：\n\n"
                    "```bash\npython -c \"import base64;print(base64.b64decode('ZmxhZ3ttM3Q0ZDR0NF90NGxrcyE=').decode())\"\n```"),
        ("注意干扰项", "`UserComment` 里写着 `nothing here, keep looking` —— 别被它带跑。"),
    ],
    why="JPEG 的 EXIF 是**相机/软件自动写入**的信息：机型、时间、甚至 GPS 坐标。\n"
        "上传图片时不清理元数据，等于把拍摄地点和时间一起交出去 —— 这正是 p10 讲的那条：\n"
        "「你发出去的每张照片，都在替你说出位置」。",
    defend="发图前清元数据：`exiftool -all= photo.jpg`，或截图/重压缩。\n"
           "服务端也应该在接收图片时剥离 EXIF，避免用户位置被间接泄露。",
)

DOC["traffic-ctf"] = dict(
    lesson="p18 Traffic · p36 流量题五步",
    summary="一个 pcap，几十个包里只有一次请求带着东西 —— 先统计，再过滤，最后解码。",
    steps=[
        ("先看统计（别逐包翻）", "Wireshark 打开 `capture.pcap`：\n\n"
                            "```\n统计 → 协议分级\n```\n\n"
                            "会看到 DNS、HTTP（80）、以及一段疑似 TLS（443）。关键在 HTTP。"),
        ("过滤 HTTP", "```\nhttp.request\n```\n\n"
                   "只有两个请求：一个 `/index.html`（噪声），一个 `/api/log?q=...`。"),
        ("看请求行", "`/api/log?q=` 后面是一长串 base64。右键 → 跟踪 TCP 流也能看到完整报文。"),
        ("解码", "```bash\npython -c \"import base64;print(base64.b64decode('<那串>').decode())\"\n```"),
    ],
    why="流量题的核心不是「读完所有包」，而是**缩小范围**：\n"
        "协议统计 → 过滤目标流量 → 跟踪单条会话 → 导出/解码。\n"
        "真实网络里绝大部分包都是噪声，工具的意义就是帮你把这些噪声排掉。",
    defend="明文传输 = 全裸：HTTP 的 URL、Cookie、表单在网络上是可见的。\n"
           "这就是为什么登录、支付必须走 HTTPS —— 而且敏感参数不该放在 URL 里（会进日志、进 Referer）。",
)

DOC["geo-hongyadong-ctf"] = dict(
    lesson="p37 图片题顺序 · p10 Recon · 参考 MoeCTF「星走路的旅程 level1」",
    summary="一张没清元数据的游客照：坐标给出地点，时间要换算成 UTC，两段拼起来才是 flag。",
    steps=[
        ("第一步：看属性", "```bash\nexiftool files/photo.jpg\n```\n\n"
                    "三个关键字段：\n"
                    "```\nGPS Position : 29 deg 34' 0.00\" N, 106 deg 34' 33.00\" E\n"
                    "Modify Date  : 2024:10:09 21:25:22\n"
                    "Artist       : Lianguanlun\n```"),
        ("第二步：坐标 → 地点", "```\n29°34′00″N → 29.5667\n106°34′33″E → 106.5757\n```\n\n"
                        "贴进地图：**重庆渝中区 · 洪崖洞**（嘉陵江边的吊脚楼群）。"),
        ("第三步：时间 → UTC", "文件里是**北京时间**（UTC+8）：\n\n"
                        "```\n2024-10-09 21:25:22  (UTC+8)\n  − 8 小时\n"
                        "= 2024-10-09 13:25  (UTC)\n```\n\n"
                        "所以日期段 `20241009`，时刻段 `1325`。"),
        ("第四步：拼接", "```\nflag{城市拼音_建筑名拼音_日期_UTC时刻}\n"
                   "flag{chongqing_hongyadong_20241009_1325}\n```"),
    ],
    why="这道题照 MoeCTF「星走路的旅程 level1」的路子：**flag 不是一个词，而是几个字段拼出来的**。\n"
        "单看坐标只能得到地名，单看时间只能得到一个时刻 —— 两段都得对、格式也得对，才能提交。\n"
        "最容易翻车的是时间：EXIF 记的是**当地时间**，很多题要的是 UTC，差 8 小时就是错答案。",
    defend="EXIF 一旦保留，坐标 + 精确到秒的时间 + 作者全在文件里。\n"
           "公开分享前 `exiftool -all= 图.jpg`，或直接截图（截图不带原图元数据）。",
)


DOC["geo-nanjinglu-ctf"] = dict(
    lesson="p10 Recon · 多步推理",
    summary="三个线索叠起来才能定位：老百货的招牌、背后的高塔、以及这条街本身。",
    steps=[
        ("线索 1：老百货的招牌", "画面左侧那栋被灯照亮的弧形建筑，顶部红色霓虹写着「**永安百货**」。"),
        ("线索 2：背后的高塔", "它身后那栋带双尖顶的高楼，顶部招牌是「**SHIMAO 世茂**」。\n"
                        "两个都是上海南京东路一带的标志性建筑 —— 互相印证，不是巧合。"),
        ("线索 3：街本身", "宽阔的步行街、两侧老字号与钟表店（画面右侧有 `MIDO` 表店招牌）——\n"
                     "这条街是**南京东路步行街**。"),
        ("拼出答案", "```\n城市拼音_街道名拼音_建筑名拼音\nflag{shanghai_nanjingdonglu_yongan}\n```"),
    ],
    why="多步图寻题的解法是**交叉验证**：任何单一线索都可能误判（全国不止一家百货叫「永安」），\n"
        "但三条独立线索同时指向同一个地点，结论就稳了。\n"
        "实务上还会配合地图街景做最后确认：搜「永安百货 南京东路」，街景一看就对上。",
    defend="同 p10：公开的照片会被用来做地理定位。发布前问自己一句\n"
           "「这张照片里的招牌和地标，会暴露我在哪吗」。",
)


DOC["exif-gps-ctf"] = dict(
    lesson="p37 图片题顺序（EXIF 优先） · p10 Recon · 参考 MoeCTF「星走路的旅程」",
    summary="五张旅行照，只有一张把坐标和拍摄时间一起带了出来。读 EXIF、换算、定位、拼 flag。",
    steps=[
        ("批量查元数据", "```bash\nexiftool files/photos/\n```\n\n"
                     "五张里只有 `IMG_2033.jpg` 同时有 `GPS Position` 和 `Create Date`。"),
        ("只挖那一张", "```bash\nexiftool -GPS:all -CreateDate files/photos/IMG_2033.jpg\n```\n\n"
                    "`GPS Position : 29 deg 19' 56.00\" N, 110 deg 25' 34.00\" E`\n"
                    "`Create Date  : 2026:07:14 09:26:00`"),
        ("换算并定位", "```\n29°19′56″N → 29.33222\n110°25′34″E → 110.42611\n```\n\n"
                   "贴进地图 —— 落在**湖南张家界国家森林公园**（砂岩峰林）。"),
        ("拼出答案", "省份 + 景区：\n\n```\nflag{hunan_zhangjiajie}\n```"),
    ],
    why="相机拍照时把 GPS 写进 EXIF 的 GPS IFD（0x8825），单位是「度/分/秒」的分数形式，\n"
        "要自己算成小数才能贴进地图。批量题的关键是**先用一条命令横向比一遍**，\n"
        "谁是那个「多出两个字段」的文件，一眼就看出来。",
    defend="发图前清元数据：`exiftool -all= photo.jpg`；手机相机里关掉「保存位置信息」更彻底。",
)


DOC["exif-thumb-ctf"] = dict(
    lesson="p37 图片题顺序 · p14 隐写（容器类）",
    summary="一张真实的故宫铜龟照，主图被打码了，但相机顺手存的一张缩略图没打码。",
    steps=[
        ("先确认主图真的没东西", "```bash\nxxd files/photo.jpg | head -3\n# 能看到 Exif 标志\n```"),
        ("看 IFD1", "JPEG 的 EXIF 里有两条 IFD 链：IFD0 是主信息，**IFD1 是缩略图**。\n"
                 "IFD1 里两个关键标签：`0x0201 ThumbnailOffset`（缩略图从哪开始）、"
                 "`0x0202 ThumbnailLength`（多长）。\n\n"
                 "一条命令直接取：\n"
                 "```bash\nexiftool -b -ThumbnailImage files/photo.jpg > thumb.jpg\n```"),
        ("打开缩略图", "`thumb.jpg` 里就是 flag。"),
    ],
    why="相机/手机为了在相册里快速预览，会把一张小图塞进 EXIF（IFD1）。\n"
        "**对主图做的任何处理（打码、裁剪、加水印）都不会自动同步到缩略图** —— \n"
        "所以「打了码」的图常常还能从缩略图里看到原貌。这是真实存在过的隐私事故。",
    defend="发图前不仅要打码，还要**清空元数据**（`exiftool -all=`），否则缩略图会出卖你。\n"
           "社交平台一般会重压缩图片并剥离 EXIF，这正是它顺手做的一层保护。",
)


DOC["png-text-ctf"] = dict(
    lesson="p31 附加数据 · p27 字节视角（PNG 的块结构）",
    summary="照片本身没问题，flag 藏在 PNG 的 tEXt 文本块里 —— 肉眼看不见，字符串能看见。",
    steps=[
        ("先常规检查", "```bash\nfile files/photo.png\nexiftool files/photo.png\nbinwalk files/photo.png\n```\n\n"
                  "图片正常、没有附加数据、EXIF 里也没有东西。"),
        ("看字符串", "```bash\nstrings files/photo.png | head -20\n```\n\n"
                 "会看到一行形如 `Comment` + 一串 base64（`ZmxhZ3t0M3h0X2NodW5rXzFzXzRfdGgxbm...`）。\n"
                 "PNG 的 tEXt 块是**不压缩的明文字段**，`strings` 直接就能扫出来。"),
        ("按块结构读", "```python\nimport struct\nraw = open('files/photo.png','rb').read()\n"
                   "off = 8\nwhile off < len(raw):\n"
                   "    ln = struct.unpack_from('>I', raw, off)[0]\n"
                   "    typ = raw[off+4:off+8]\n"
                   "    if typ == b'tEXt':\n"
                   "        print(raw[off+8:off+8+ln])\n"
                   "    off += 12 + ln\n"
                   "    if typ == b'IEND': break\n```"),
        ("base64 解码", "```bash\npython -c \"import base64;print(base64.b64decode('<那串>').decode())\"\n```"),
    ],
    why="PNG 除了像素数据（IDAT），还有若干**元数据块**：`tEXt`（纯文本）、`iTXt`（UTF-8）、`zTXt`（压缩）。\n"
        "它们不参与显示，所以「图片看着完全正常」；但它们是**明文**存在文件里的，`strings` 一扫就露。\n"
        "注意还有 `Software` / `Author` 两个干扰项 —— 别拿到就以为是答案。",
    defend="发图前除了清 EXIF，也要清 PNG 的文本块：`exiftool -all= 图.png` 或 `pngcrush -rem text`。\n"
           "很多「截图打码」的翻车，正是漏了这些看不见的字段。",
)

DOC["exif-erase-ctf"] = dict(
    lesson="p37 EXIF · p33 附加数据（同一个思路换了容器）",
    summary="有人把 EXIF 清掉了，还说「清完就干净了」。对比两张图，你会发现他清掉了别的东西。",
    steps=[
        ("先对比两张图", "```bash\nls -l files/before.jpg files/after.jpg\ncmp -l files/before.jpg files/after.jpg | head\n```\n\n"
                     "`after.jpg` 比 `before.jpg` **小了一百多字节** —— 少的就是那段 EXIF。"),
        ("看 APP1 段结构", "JPEG 的元数据在 `FFE1` 段里：\n\n"
                       "```\nFF E1 | 长度(2字节) | 「Exif\\0\\0」 | TIFF 结构 ...\n```\n\n"
                       "```bash\nxxd files/before.jpg | head -6\n```"),
        ("把 APP1 段整个 dump 出来", "```python\n"
                              "raw = open('files/before.jpg','rb').read()\n"
                              "i = raw.find(b'\\xff\\xe1')\n"
                              "ln = int.from_bytes(raw[i+2:i+4], 'big')\n"
                              "blk = raw[i:i+2+ln]\n"
                              "open('app1.bin','wb').write(blk)\n"
                              "```"),
        ("从里面切出 ZIP", "APP1 段里 `PK\\x03\\x04` 之后就是压缩包：\n\n"
                       "```python\n"
                       "off = blk.find(b'PK\\x03\\x04')\n"
                       "open('inner.zip','wb').write(blk[off:])\n"
                       "```\n\n"
                       "解压 → `flag.txt`。"),
    ],
    why="APP1 段只规定了「长度 + 开头是 Exif 标记」，**中间的字节没有任何强制含义** ——\n"
        "所以只要长度字段写对，就能在里面塞任意数据：ZIP、文本、甚至另一张图。\n"
        "抹除 EXIF 工具的做法是「整段丢掉」，于是这些私货跟着一起消失（对比 `after.jpg`）。\n\n"
        "**结论：抹除之前先备份，或者先 dump 出来看一眼。**",
    defend="收到可疑图片：先 `exiftool -b -all:all` 或按 APP1 段 dump 备份，再做处理。\n"
           "自己做「抹除」时，也不要以为「清掉 EXIF 就等于清干净」——\n"
           "附加数据（p33）、缩略图（上一题）、LSB（p14）都不在 EXIF 里。",
)


VERIFY["exif-trio-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：型号 + 坐标 + 时间，三件套拼出 flag。"""
import datetime, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"

ex = Image.open(os.path.join(HERE, "files", "photo.jpg")).getexif()
g = ex.get_ifd(0x8825)
assert g, "没有 GPS"
one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
lat, lon = deg(g[2]), deg(g[4])
model = ex.get(0x0110) or ""
dt_raw = ex.get(0x0132)
print("  Model:", model)
print("  GPS: %.5f, %.5f" % (lat, lon))
print("  拍摄时间(本地):", dt_raw)
assert abs(lat - 39.9163) < 0.01 and abs(lon - 116.3972) < 0.01, "坐标不在故宫附近"
assert "xiaomi" in model.lower(), "型号不是小米"
local = datetime.datetime.strptime(dt_raw, "%%Y:%%m:%%d %%H:%%M:%%S")
utc = (local - datetime.timedelta(hours=8)).strftime("%%H%%M")
flag = "flag{%%s_%%s_%%s}" %% ("beijing", "xiaomi", utc)
print("  UTC 时刻:", utc)
print("  拼出:", flag)
assert flag == EXPECT
print("  PASS exif-trio-ctf")
'''

VERIFY["route-trace-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：三张照片按拍摄时间排序 → 路线。"""
import datetime, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"

def city_of(lon):
    if lon > 118:
        return "shanghai"
    if lon > 108:
        return "zhangjiajie"
    return "chongqing"

rows = []
for name in sorted(os.listdir(os.path.join(HERE, "files"))):
    if not name.lower().endswith(".jpg"):
        continue
    ex = Image.open(os.path.join(HERE, "files", name)).getexif()
    g = ex.get_ifd(0x8825)
    assert g, name + " 没有 GPS"
    one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
    deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
    lat, lon = deg(g[2]), deg(g[4])
    dt = datetime.datetime.strptime(ex.get(0x0132), "%%Y:%%m:%%d %%H:%%M:%%S")
    rows.append((dt, name, lat, lon, city_of(lon)))
    print("  %-14s %s  %.3f,%.3f → %s" %% (name, dt, lat, lon, city_of(lon)))

rows.sort()
order = [r[4] for r in rows]
print("  按时间排序:", " → ".join(order))
flag = "flag{%%s}" %% "_".join(order)
print("  拼出:", flag)
assert flag == EXPECT
print("  PASS route-trace-ctf")
'''

DOC["exif-trio-ctf"] = dict(
    lesson="p37 图片题顺序（EXIF 优先） · 参考 MoeCTF「星走路的旅程 level1」",
    summary="一张照片里同时躺着型号、坐标、拍摄时间 —— 三样各取一段拼起来才是 flag。",
    steps=[
        ("一次看清全部字段", "```bash\nexiftool files/photo.jpg\n```\n\n"
                       "关键三行：\n```\nMake / Model  : Xiaomi / Xiaomi 14 Ultra\n"
                       "GPS Position  : 39 deg 54' 59.00\" N, 116 deg 23' 50.00\" E\n"
                       "Modify Date   : 2026:07:14 17:26:00\n```"),
        ("坐标 → 城市", "```\n39.91639, 116.39722 → 北京 · 故宫\n```"),
        ("型号 → 品牌", "`Xiaomi 14 Ultra` 的品牌取小写：`xiaomi`。"),
        ("时间 → UTC", "```\n2026-07-14 17:26:00 (UTC+8)  − 8h  =  09:26 (UTC)\n```"),
        ("拼接", "```\nflag{城市拼音_品牌小写_UTC时刻}\nflag{beijing_xiaomi_0926}\n```"),
    ],
    why="这就是「星走路的旅程 level1」的三件套思路（机场三字码 + 手机品牌 + UTC 时间）。\n"
        "三个参数分别藏在 EXIF 的不同字段里，取哪个、怎么换算，都要自己判断。\n"
        "最容易错的是时间：EXIF 记当地时间，flag 要 UTC。",
    defend="这三个字段全是相机自动写的，你什么都没做就交出去了。\n发图前 `exiftool -all= 图.jpg`，或关掉相机的「记录位置」。",
)

DOC["route-trace-ctf"] = dict(
    lesson="p37 EXIF · p10 Recon · 参考 MoeCTF「星走路的旅程」多要素拼接",
    summary="三张照片、三个城市、三段拍摄时间。按时间排序，把路线写出来。",
    steps=[
        ("先批量读出来", "```bash\nfor f in files/*.jpg; do echo \"== $f\"; exiftool -GPS:all -ModifyDate \"$f\"; done\n```\n\n"
                    "或者一次跑整个目录：`exiftool files/`"),
        ("把坐标换成城市", "```\nIMG_1001.jpg  31.240,121.497 → 上海\n"
                    "IMG_1002.jpg  29.566,106.576 → 重庆\n"
                    "IMG_1003.jpg  29.332,110.426 → 湖南张家界\n```"),
        ("按时间排序", "```\nIMG_1001  2024:10:07 08:41:30\n"
                   "IMG_1003  2024:10:09 09:12:07\n"
                   "IMG_1002  2024:10:11 21:05:11\n```\n\n"
                   "文件名是乱的，真正的顺序在 EXIF 里。"),
        ("拼出路线", "```\nflag{shanghai_zhangjiajie_chongqing}\n```"),
    ],
    why="文件名可以随便改，**拍摄时间是相机写的**，改不了（除非有人专门改）。\n"
        "所以「复现一个人的行程」靠的不是文件夹顺序，而是元数据里的时间轴 —— 这是取证的常规手法。\n"
        "三张图各自只给一个点，串成时间线才有意义。",
    defend="同一件事反过来看：只要你不清元数据，别人就能画出你的行程图。\n"
           "分享旅行照前批量清一遍：`exiftool -all= -r 相册目录/`。",
)


# ============================ 孪生题：简化前置 ============================
VERIFY["b64-only-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：readme 里那串 base64 直接解。"""
import base64, os, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
raw = open(os.path.join(HERE, "files", "readme.txt"), encoding="utf-8-sig").read()
blob = max(re.findall(r"[A-Za-z0-9+/=]{20,}", raw), key=len)
flag = base64.b64decode(blob).decode()
print("  取到 base64:", blob[:32], "...")
print("  解出:", flag)
assert flag == EXPECT
print("  PASS b64-only-ctf")
'''

VERIFY["hex-only-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：readme 里那串十六进制直接转字节。"""
import binascii, os, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
raw = open(os.path.join(HERE, "files", "readme.txt"), encoding="utf-8-sig").read()
blob = max(re.findall(r"[0-9a-fA-F]{20,}", raw), key=len)
flag = binascii.unhexlify(blob).decode()
print("  取到 hex:", blob[:32], "...")
print("  解出:", flag)
assert flag == EXPECT
print("  PASS hex-only-ctf")
'''

VERIFY["ext-lie-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：.docx 其实是 ZIP，直接当 zip 打开。"""
import io as _io, os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
p = os.path.join(HERE, "files", "作业.docx")
raw = open(p, "rb").read()
print("  前 4 字节:", raw[:4].hex(), "(504b0304 = ZIP)")
assert raw[:4] == b"PK\x03\x04", "不是 zip，构造有误"
flag = zipfile.ZipFile(_io.BytesIO(raw)).read("flag.txt").decode().strip()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS ext-lie-ctf")
'''

VERIFY["zip-comment-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：flag 在 ZIP 的注释字段（EOF 之后）里。"""
import os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
z = zipfile.ZipFile(os.path.join(HERE, "files", "handin.zip"))
comment = z.comment.decode("utf-8")
names = z.namelist()
print("  包内文件:", names)
for n in names:
    assert EXPECT.encode() not in z.read(n), "flag 不该在文件内容里"
print("  注释:", comment)
assert comment == EXPECT
print("  PASS zip-comment-ctf")
'''

VERIFY["lsb-easy-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：只读红色通道最低位。"""
import os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"
im = Image.open(os.path.join(HERE, "files", "photo.png")).convert("RGB")
px = im.load()
bits = []
for y in range(im.height):
    for x in range(im.width):
        bits.append(px[x, y][0] & 1)
out = bytearray()
for i in range(0, len(bits) - 7, 8):
    v = 0
    for k in range(8):
        v = (v << 1) | bits[i + k]
    if v == 0:
        break
    out.append(v)
flag = out.decode("utf-8", "replace")
print("  解出:", flag)
assert flag == EXPECT
print("  PASS lsb-easy-ctf")
'''

# ============================ 流量三连 ============================
VERIFY["traffic-chunked-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：按 seq 重排服务器载荷 → 解 chunked → 拼出 flag。"""
import os, re, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"

import struct

def frames_of(p):
    data = open(p, "rb").read()
    off, out = 24, []
    while off + 16 <= len(data):
        _, _, cap, _ = struct.unpack_from("<IIII", data, off)
        off += 16
        out.append(data[off:off + cap])
        off += cap
    return out

def ip_proto(fr):
    if fr[12:14] != b"\x08\x00":
        return None, None
    ihl = (fr[14] & 0x0F) * 4
    tot = struct.unpack_from(">H", fr, 16)[0]
    return fr[14 + 9], fr[14 + ihl:14 + tot]

def tcp_of(fr):
    proto, ip = ip_proto(fr)
    if proto != 6:
        return None
    sport, dport, seq = struct.unpack_from(">HHI", ip, 0)
    doff = (ip[12] >> 4) * 4
    return sport, dport, seq, ip[doff:]

def udp_of(fr):
    proto, ip = ip_proto(fr)
    if proto != 17:
        return None
    sport, dport, length = struct.unpack_from(">HHH", ip, 0)
    return sport, dport, ip[8:length]

def dns_qname(dns):
    i, labels = 12, []
    while i < len(dns) and dns[i]:
        n = dns[i]
        labels.append(dns[i + 1:i + 1 + n].decode("latin-1"))
        i += 1 + n
    return ".".join(labels)

p = os.path.join(HERE, "files", "capture.pcap")
segs = {}
for fr in frames_of(p):
    t = tcp_of(fr)
    if not t:
        continue
    sport, dport, seq, payload = t
    if sport == 80 and payload:
        segs[seq] = payload
stream = b"".join(segs[k] for k in sorted(segs))
print("  重组后 %d 字节" % len(stream))
head, _, body = stream.partition(b"\r\n\r\n")
print("  响应头:", head.split(b"\r\n")[0].decode("latin-1"))
assert b"chunked" in head.lower(), "不是分块传输"
parts = []
while True:
    line, _, rest = body.partition(b"\r\n")
    n = int((line.strip() or b"0"), 16)
    if n == 0:
        break
    chunk = rest[:n]
    body = rest[n + 2:]
    if not chunk.startswith(b"-- "):        # 丢掉日志噪声块
        parts.append(chunk)
data = b"".join(parts)
print("  有效分块:", len(parts), "块")
m = re.search(rb"flag\{[^}]+\}", data)
assert m, "分块内容里没有 flag"
flag = m.group().decode()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS traffic-chunked-ctf")
'''

VERIFY["traffic-dns-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：取 .t.itns.local 下的编号子域，按序号拼 base32 再解码。"""
import base64, os, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"

import struct

def frames_of(p):
    data = open(p, "rb").read()
    off, out = 24, []
    while off + 16 <= len(data):
        _, _, cap, _ = struct.unpack_from("<IIII", data, off)
        off += 16
        out.append(data[off:off + cap])
        off += cap
    return out

def ip_proto(fr):
    if fr[12:14] != b"\x08\x00":
        return None, None
    ihl = (fr[14] & 0x0F) * 4
    tot = struct.unpack_from(">H", fr, 16)[0]
    return fr[14 + 9], fr[14 + ihl:14 + tot]

def tcp_of(fr):
    proto, ip = ip_proto(fr)
    if proto != 6:
        return None
    sport, dport, seq = struct.unpack_from(">HHI", ip, 0)
    doff = (ip[12] >> 4) * 4
    return sport, dport, seq, ip[doff:]

def udp_of(fr):
    proto, ip = ip_proto(fr)
    if proto != 17:
        return None
    sport, dport, length = struct.unpack_from(">HHH", ip, 0)
    return sport, dport, ip[8:length]

def dns_qname(dns):
    i, labels = 12, []
    while i < len(dns) and dns[i]:
        n = dns[i]
        labels.append(dns[i + 1:i + 1 + n].decode("latin-1"))
        i += 1 + n
    return ".".join(labels)

p = os.path.join(HERE, "files", "capture.pcap")
names = []
for fr in frames_of(p):
    u = udp_of(fr)
    if not u:
        continue
    sport, dport, payload = u
    if dport == 53:
        names.append(dns_qname(payload))
print("  共 %d 条 DNS 查询" % len(names))
for n in names[:4]:
    print("    " + n)
pat = re.compile(r"^(\d+)\.([A-Z2-7]+)\.t\.itns\.local$")
hits = []
for n in names:
    m = pat.match(n)
    if m:
        hits.append((int(m.group(1)), m.group(2)))
hits.sort()
print("  命中隧道分片: %d 片" % len(hits))
b32 = "".join(h[1] for h in hits)
b32 += "=" * (-len(b32) % 8)
flag = base64.b32decode(b32).decode()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS traffic-dns-ctf")
'''

VERIFY["traffic-proto-ctf"] = r'''# -*- coding: utf-8 -*-
"""自校验：重排 TCP 流 → 按 ITNS 帧解析 → XOR 0x5A → 拼 flag。"""
import os, re, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "%(flag)s"

import struct

def frames_of(p):
    data = open(p, "rb").read()
    off, out = 24, []
    while off + 16 <= len(data):
        _, _, cap, _ = struct.unpack_from("<IIII", data, off)
        off += 16
        out.append(data[off:off + cap])
        off += cap
    return out

def ip_proto(fr):
    if fr[12:14] != b"\x08\x00":
        return None, None
    ihl = (fr[14] & 0x0F) * 4
    tot = struct.unpack_from(">H", fr, 16)[0]
    return fr[14 + 9], fr[14 + ihl:14 + tot]

def tcp_of(fr):
    proto, ip = ip_proto(fr)
    if proto != 6:
        return None
    sport, dport, seq = struct.unpack_from(">HHI", ip, 0)
    doff = (ip[12] >> 4) * 4
    return sport, dport, seq, ip[doff:]

def udp_of(fr):
    proto, ip = ip_proto(fr)
    if proto != 17:
        return None
    sport, dport, length = struct.unpack_from(">HHH", ip, 0)
    return sport, dport, ip[8:length]

def dns_qname(dns):
    i, labels = 12, []
    while i < len(dns) and dns[i]:
        n = dns[i]
        labels.append(dns[i + 1:i + 1 + n].decode("latin-1"))
        i += 1 + n
    return ".".join(labels)

p = os.path.join(HERE, "files", "capture.pcap")
segs = {}
for fr in frames_of(p):
    t = tcp_of(fr)
    if not t:
        continue
    sport, dport, seq, payload = t
    if sport == 9001 and payload:
        segs[seq] = payload
s = b"".join(segs[k] for k in sorted(segs))
print("  重组 %d 字节" % len(s))
i, out = 0, b""
while i + 11 <= len(s):
    if s[i:i + 4] != b"ITNS":
        i += 1
        continue
    typ = s[i + 4]
    seqn, plen = struct.unpack_from(">HH", s, i + 5)
    body = s[i + 9:i + 9 + plen]
    i += 9 + plen + 2
    print("    帧 type=%d seq=%d len=%d" % (typ, seqn, plen))
    out += bytes(b ^ 0x5A for b in body)
print("  还原:", out)
m = re.search(rb"flag\{[^}]+\}", out)
assert m, "帧内容里没有 flag"
flag = m.group().decode()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS traffic-proto-ctf")
'''



# ============================ 孪生题：简化前置 ============================
DOC["b64-only-ctf"] = dict(
    lesson="p12 Encoding",
    summary="最原始的编码题：一串 base64，解开就是 flag。",
    steps=[
        ("看字符集", "全是 `A-Z a-z 0-9 + /`，结尾还有 `=` —— 这是 base64 唯一的特征。"),
        ("解", "```bash\ncat files/readme.txt\npython -c \"import base64;print(base64.b64decode('那串').decode())\"\n```"),
        ("对照一下", "同一道题的上一级是「接力」（base64 → hex → rot13），这一题只有一层。"),
    ],
    why="base64 把 3 个字节变成 4 个可打印字符，所以长度总是 4 的倍数、以 `=` 补齐。\n它**不是加密**，没有密钥，任何人都能解 —— 只是换了个能在邮箱/URL 里安全传递的外壳。",
    defend="别把 base64 当加密用。要保密就用 AES + 密钥，要传输就用 HTTPS。",
)

DOC["hex-only-ctf"] = dict(
    lesson="p12 Encoding · p27 十六进制视角",
    summary="一串 0-9a-f，两个字符一个字节，转回去就是明文。",
    steps=[
        ("看特征", "只有 `0-9 a-f`，长度是偶数 —— 十六进制。"),
        ("转字节", "```bash\npython -c \"print(bytes.fromhex('那串').decode())\"\n```"),
        ("和 base64 的区别", "base64 是 3 字节 → 4 字符；hex 是 1 字节 → 2 字符。hex 更长，但一眼能对应到字节。"),
    ],
    why="hex 是把每个字节拆成两个十六进制字符，长度翻倍但完全可逆。\n你在 `xxd`、Wireshark 里看到的都是它 —— 记住 **两个字符 = 一个字节**。",
    defend="hex 也只是表示法，不提供任何保密性，等同于明文。",
)

# ============================ 文件类型 ============================
DOC["ext-lie-ctf"] = dict(
    lesson="p27 十六进制视角 · p32 文件头速查",
    summary="后缀写 .docx，其实是个 ZIP —— 看一眼前 4 个字节就知道。",
    steps=[
        ("别双击，先看字节", "```bash\nxxd files/作业.docx | head -2\n```\n\n```\n00000000: 504b 0304 1400 0000 0800 ...   PK..\n```\n\n`50 4B 03 04` 就是 ZIP 的签名。"),
        ("改后缀", "把 `.docx` 改成 `.zip`，双击就能看到里面的 `flag.txt`。"),
        ("Windows 里的小技巧", "如果看不后缀：资源管理器 → 查看 → 勾上「文件扩展名」。"),
    ],
    why="**文件类型由内容决定，不由后缀决定**。Word 的 .docx 本质就是个 zip（装着 XML）。\n所以「改后缀」这一步在真机上一样成立：服务端如果只看后缀就放行，那就等着出事。",
    defend="上传接口必须按**内容**判类型（读魔数），不能信 Content-Type 和后缀。",
)

# ============================ ZIP 结构 ============================
DOC["zip-comment-ctf"] = dict(
    lesson="p33 附加数据 · ZIP 结构",
    summary="包里没有 flag，但压缩包自己带着一个备注字段。",
    steps=[
        ("先老老实实解压", "```bash\nunzip -l files/handin.zip\nunzip files/handin.zip -d out/\n```\n\n只有一个 `readme.txt`，它说 flag 不在包里。"),
        ("看压缩包注释", "```bash\nunzip -z files/handin.zip\n```\n\n7-Zip 里则是右键 → 属性，最下面一栏「注释」。\nPython：`zipfile.ZipFile('handin.zip').comment`"),
        ("读 flag", "注释里就是 flag。"),
    ],
    why="ZIP 末尾的 EOCD 记录里有个**注释长度**字段，后面最多能挂 64 KB 任意字节。\n它不属于任何文件，所以 `unzip -l` 看不到 —— 但压缩包确实带着它。\n这跟下一题（伪加密）是同一个思路：**ZIP 里除了文件，还有一堆字段**。",
    defend="收到别人的压缩包，`unzip -z` 看一眼注释；自己打包时也别把密码、内网地址写进备注。",
)

# ============================ LSB ============================
DOC["lsb-easy-ctf"] = dict(
    lesson="p14 Stego",
    summary="单通道 LSB 的入门版：只动红色通道，从左上角按行读。",
    steps=[
        ("按题意读红色通道", "```python\nfrom PIL import Image\nim = Image.open('files/photo.png').convert('RGB')\npx = im.load()\nbits = []\nfor y in range(im.height):\n    for x in range(im.width):\n        bits.append(px[x, y][0] & 1)\n```"),
        ("8 位拼一个字符", "```python\nout = bytearray()\nfor i in range(0, len(bits) - 7, 8):\n    v = 0\n    for k in range(8):\n        v = (v << 1) | bits[i + k]\n    if v == 0:\n        break\n    out.append(v)\nprint(out.decode())\n```"),
        ("也可以偷懒", "```bash\nzsteg files/photo.png\n```"),
    ],
    why="一个像素的红色值改动 1/255，肉眼完全看不出 —— 但它是**确定的比特**。\n单通道版只能藏 宽×高 位（900×600 的图约 67 KB 容量），所以是入门题；\n下一题把三个通道都用上，容量翻三倍，还要处理通道交织顺序。",
    defend="社交平台重压缩会破坏 LSB。真要用隐写，先确认对方拿到的是原图（无损）。",
)

# ============================ 流量三连 ============================
DOC["traffic-chunked-ctf"] = dict(
    lesson="p18 Traffic · p36 流量五步",
    summary="响应用了 chunked 编码，flag 被切三段夹在日志行里；TCP 段还乱序、重传过一次。",
    steps=[
        ("先过滤出 HTTP", "```\nhttp.response\n```\n\n能看到 `Transfer-Encoding: chunked` —— 说明**没有 Content-Length**，body 是一块一块发过来的。"),
        ("跟着 TCP 流看", "右键 → Follow → TCP Stream。Wireshark 会按 seq 自动重排，乱序和重传都不是障碍。"),
        ("读懂分块格式", "```\n<长度(十六进制)>\n<数据>\n<长度(十六进制)>\n<数据>\n0\n\n```\n\n每块开头那行是**十六进制长度**（比如 `1a` = 26 字节），最后一块长度是 `0`。"),
        ("筛掉噪声再拼", "分块是交替的：一块 flag 片段、一块 `-- log line N -- 日志行`。\n"
                 "**丢掉以 `-- ` 开头的块**，剩下的按顺序接起来就是完整 flag。\n"
                 "Wireshark 里可以右键 → Export Objects，或自己写二十行脚本。"),
    ],
    why="chunked 是 HTTP/1.1 的流式传输：服务器不知道总长度时，就一块块发，末尾用 0 长度收尾。\n初学者常在包里直接 `strings` 找，看到半截就以为没有 —— **必须把分块拆干净再拼**。\n另外这题故意让 TCP 段乱序加重传：**抓包顺序不等于真实顺序**，要按 seq 排。",
    defend="日志类接口别用 chunked 传敏感数据；代理/网关要能正确重组后再做内容检测。",
)

DOC["traffic-dns-ctf"] = dict(
    lesson="p18 Traffic · p36 流量五步 · 要查 DNS 记录类型",
    summary="几十条 DNS 查询，只有一组编号子域是真隧道，把分片拼起来 base32 解码。",
    steps=[
        ("先看协议分布", "```\ndns\n```\n\n会发现大量形如 `01.XXXX.t.itns.local` 的查询 —— 正常业务不会这么查。"),
        ("分辨真假", "`www` / `api` / `mail` 是噪声，另有 `NN.XXXX.cdn.itns.local` 是干扰组。\n真正的隧道是 **`NN.<分片>.t.itns.local`** 这一族（后缀是 `t.`）。"),
        ("按编号排序拼起来", "```python\nimport re, base64\npat = re.compile(r'^(\\d+)\\.([A-Z2-7]+)\\.t\\.itns\\.local$')\nhits = sorted((int(m.group(1)), m.group(2)) for m in (pat.match(n) for n in names) if m)\nb32 = ''.join(h[1] for h in hits)\nb32 += '=' * (-len(b32) % 8)\nprint(base64.b32decode(b32).decode())\n```"),
        ("为什么是 base32", "DNS 标签只允许 `A-Z 0-9 -`，base64 里的 `+ / =` 会被拒 —— 所以隧道一般用 **base32** 编码。"),
    ],
    why="DNS 隧道是真实存在的隐蔽信道：把数据编码进子域名，由内网机器不断发起解析，数据就从防火墙的 53 端口漏出去。\n**识别特征**：某域名下大量随机化、等长、带编号的子域；查询量大但响应极小。\n可以查一下 `iodine`、`dnscat2` 两个工具。",
    defend="出口 DNS 做白名单 + 长度/熵检测；内网禁止绕过内部解析器直连外部 DNS。",
)

DOC["traffic-proto-ctf"] = dict(
    lesson="p18 Traffic · 协议逆向（要查资料 + 写解析器）",
    summary="非标端口上的私有协议：帧头要自己猜出来，payload 还做了异或。",
    steps=[
        ("别用 HTTP 视角看", "端口 9001，Follow TCP Stream 一看就是乱码 —— 不是文本协议。"),
        ("找规律", "```bash\ntshark -r files/capture.pcap -q -z follow,tcp,raw,1\n```\n\n或者导出 raw 后 `xxd`：会反复看到 `49 54 4e 53`（ASCII 的 `ITNS`）—— 这是**魔数**，每一帧都从它开始。"),
        ("猜帧结构", "```\nITNS | type(1) | seq(2,BE) | len(2,BE) | payload(len) | crc16(2,BE)\n```\n\n验证方法：`len` 字段和下一个 `ITNS` 的间距对得上；末尾两字节用 CRC16-CCITT 能算出同样的值。"),
        ("payload 还异或了", "字节分布明显偏离文本（`flag` 的 `66 6c 61 67` 变成 `3c 36 3b 3d`）→ 试单字节异或 `0x5A`。"),
        ("写解析器", "```python\ni, out = 0, b''\nwhile i + 11 <= len(s):\n    if s[i:i+4] != b'ITNS':\n        i += 1\n        continue\n    plen = struct.unpack_from('>H', s, i + 7)[0]\n    body = s[i+9:i+9+plen]\n    out += bytes(b ^ 0x5A for b in body)\n    i += 9 + plen + 2\nprint(out)\n```"),
    ],
    why="这是最接近**真实逆向**的一类流量题：没有文档，只能从字节里找规律。\n套路固定：①找重复出现的魔数 ②量字段宽度（2 字节长度？大端还是小端？）③校验字段试常见算法（CRC16 / CRC32 / 求和）④内容不像明文就试单字节异或。\nCRC16 的常见变体（CCITT / IBM / MODBUS）都能在网上查到参数表，逐个试。",
    defend="自己设计协议也要加魔数 + 长度 + 校验；但**别指望私有协议当安全**，抓包就能逆。",
)

# ---------------------------------------------------------------------------
# 渲染
# ---------------------------------------------------------------------------
def render_readme(key, cfg):
    d = DOC[key]
    files = "\n".join("| `%s` | 附件 |" % f for f in cfg["dirs"])
    steps = []
    for i, (h, body) in enumerate(d["steps"], 1):
        steps.append("### Step %d｜%s\n\n%s\n\n" % (i, h, body))
    tpl = """# 题解：%(title)s

> 对应第三课 %(lesson)s

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | %(tags)s |
| 难度 | %(diff)s / 3 |
| 分值 | %(points)s |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

%(summary)s

### 附件

| 文件 | 说明 |
|---|---|
%(files)s

## 二、手把手解题

%(steps)s
## 三、为什么能成（原理）

%(why)s

## 四、怎么防（延伸）

%(defend)s

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `%(flag)s`。
"""
    vals = {
        "title": cfg["title"],
        "lesson": d["lesson"],
        "tags": "、".join(cfg["tags"]),
        "diff": cfg["difficulty"],
        "points": cfg["points"],
        "summary": d["summary"],
        "files": files,
        "steps": "".join(steps),
        "why": d["why"],
        "defend": d["defend"],
        "flag": cfg["flag"],
    }
    for k, v in vals.items():
        tpl = tpl.replace("%(" + k + ")s", str(v))
    return tpl


def photo_table():
    """按题目列出用到的照片与授权信息。"""
    import json as _json
    credits_path = os.path.join(ROOT, "_build", "_assets", "photos", "credits.json")
    credits = {}
    if os.path.exists(credits_path):
        credits = _json.load(open(credits_path, encoding="utf-8"))
    use = {
        "02 file-header": ["p_greatwall.jpg"],
        "03 append-zip": ["user_ctf_meme.png"],
        "05 lsb-stego": ["p_yangshuo.jpg"],
        "06 exif-meta": ["p_greatwall_cc0.jpg"],
        "10 exif-gps": ["p_gugong_turtle.jpg", "p_karst.jpg", "p_zjj_gps.jpg",
                        "p_guilin.jpg", "p_mutianyu.jpg"],
        "11 exif-thumb": ["p_gugong_turtle2.jpg"],
        "12 exif-erase": ["p_xihu_pano.jpg"],
        "13 png-text": ["p_garden_wangshi.jpg"],
        "08 geo-hongyadong": ["(geo) hongyadong.jpg"],
        "09 geo-nanjinglu": ["(geo) nanjing_road.jpg"],
    }
    extra = {"(geo) hongyadong.jpg": ("洪崖洞夜景（**保留 EXIF**：GPS + 拍摄时间）",
                                    "CC BY 4.0", "Lianguanlun"),
             "(geo) nanjing_road.jpg": ("南京东路夜景（元数据已清空）", "CC BY-SA 4.0", "EditQ")}
    lines = ["| 题 | 文件 | 内容 | 许可 | 作者 |", "|---|---|---|---|---|"]
    for chal, files in use.items():
        for f in files:
            if f in extra:
                note, lic, artist = extra[f]
            else:
                c = credits.get(f, {})
                note, lic, artist = c.get("note", ""), c.get("license", "?"), c.get("artist", "?")
            lines.append("| %s | `%s` | %s | %s | %s |" % (chal, f, note, lic, artist))
    return "\n".join(lines)


def main():
    for key, cfg in META.items():
        base = os.path.join(ROOT, cfg.get("group") or "", cfg.get("path") or key)
        md = render_readme(key, cfg)
        write(os.path.join(base, "README.md"), md)
        chk = (CHECKER.replace("%(title)s", cfg["title"])
                      .replace("%(name)s", key)
                      .replace("%(flag)s", cfg["flag"]))
        write(os.path.join(base, "checker", "main.rx"), chk)
        code = VERIFY[key].replace("%(flag)s", cfg["flag"]).replace("%%", "%")
        write(os.path.join(base, "verify.py"), code)
        print("  [%s] README + checker + verify 已写入" % key)

    # 总索引
    rows, series_rows = [], []
    for key, cfg in sorted(META.items(), key=lambda kv: kv[1]["no"]):
        sub = cfg.get("path") or key
        line = "| %02d | [`%s/`](%s%s/) | %s | %s | %s | %s |" % (
            cfg["no"], sub, (cfg.get("group") + "/") if cfg.get("group") else "",
            sub, cfg["title"], "、".join(cfg["tags"]),
            cfg["difficulty"], cfg["points"])
        (series_rows if cfg.get("group") == "starwalk-series" else rows).append(line)
    index = """# misc-ctf · MISC 入门题集（%(count)s 题）

配套课件：`..\\..\\学校文件\\网安社\\晚自习\\第三课\\第三课.pptx`（38 页）

全部为**附件型**题目：下载附件 → 本地拆 → 提交 flag。不需要 Docker，也不需要联网跑服务。

## 题目一览

### 基础题（%(n_base)s 题）

| # | 目录 | 题名 | 考点 | 难度 | 分值 |
|---|---|---|---|---|---|
%(rows)s

### starwalk 系列 Level-6（%(n_series)s 题）· 参考 MoeCTF「星走路的旅程」

多要素拼 flag：坐标、时间、招牌都要读对，拼起来才是答案。

| # | 目录 | 题名 | 考点 | 难度 | 分值 |
|---|---|---|---|---|---|
%(series_rows)s

## 目录结构（每题一致）

```
<题目>/
├── files/          # 发给选手的附件
├── README.md       # 题解（手把手 + 原理 + 防御）
├── checker/main.rx # ret2shell 评测脚本（静态 flag）
└── verify.py       # 自校验：按题解跑一遍并断言 flag
```

## 一键校验

```bash
python verify_all.py
```

会对每道题跑 `verify.py`，全部通过才算题目可用。

## 与课件对应

| 课件页 | 主题 | 对应题目 |
|---|---|---|
| p12 / p13 | 编码、编码≠加密 | 01 编码接力 |
| p27 / p32 | 十六进制视角、文件头速查 | 02 被抹掉的文件头 |
| p14 / p33 | 隐写、附加数据 | 03 图片里有个压缩包 |
| p33 | 伪加密 | 04 没有密码的加密包 |
| p14 | LSB | 05 最低有效位 |
| p37 | EXIF 描述、base64 | 06 照片会说话 |
| p37 / p14 | EXIF 缩略图（IFD1） | 07 被打码的照片 |
| p37 / p33 | EXIF 抹除、APP1 段 | 08 抹掉之前先备份 |
| p31 / p27 | PNG 块结构、tEXt 注释 | 09 注释里的悄悄话 |
| p18 / p36 | 流量分析 | 10 流量里的那一次请求 |
| p37 / p10 | EXIF + 时间换算，多要素拼 flag（starwalk 系列） | 11 属性三件套 · 13 游客照 · 14 三层线索 |
| p37 | 批量 EXIF、景区定位 | 12 五张照片 |
| p37 / p10 | 按时间轴复现路线 | 15 三张照片的路线 |

## 造题脚本

`_build/make_challenges.py`（造附件）+ `_build/make_docs.py`（写文档/checker/verify）。
`_build/challenges.json` 是题目元数据（题号、flag、分值、考点）。
改完重跑两个脚本即可重建全部题目与附件。

## 工具

### 动态附件生成器（平台用）

`动态附件生成器/` 下有一套独立工具，把任意一题做成人手一份的「动态附件」：

```bash
cd 动态附件生成器
python make_mapped.py                 # 交互：直接输入 <题目名> <份数>
python make_mapped.py demo-01 3       # 命令行：生成后自动校验
```

产出 `out/<目标>/mapped/*.zip` 与 `checker/flag.json`，分别上传到
`$BUCKET/mapped/` 和 `$BUCKET/checker/`，配合平台的 `mapped.rx` 使用。
细节见 `动态附件生成器/README.md`（含上传步骤与四个坑）。

## 素材来源

%(photos)s

> 除 03 题为你提供的动漫截图外，其余照片均来自 Wikimedia Commons（CC 授权）。
> 所有图都做了缩放/重新编码；附件的元数据按题目需要写入或清除。
> 原图存放在 `_build/_assets/photos/`（附 `credits.json`），重跑 `make_challenges.py` 可复现。

""" % {"count": len(META), "rows": "\n".join(rows),
       "series_rows": "\n".join(series_rows),
       "n_base": len(rows), "n_series": len(series_rows),
       "photos": photo_table()}
    write(os.path.join(ROOT, "README.md"), index)

    # verify_all
    write(os.path.join(ROOT, "verify_all.py"), '''# -*- coding: utf-8 -*-
"""跑一遍全部题目的 verify.py，汇总结果。"""
import os, subprocess, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ROOT = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable
names = []
for d in sorted(os.listdir(ROOT)):
    if d.startswith("_"):
        continue
    p = os.path.join(ROOT, d)
    if not os.path.isdir(p):
        continue
    if os.path.exists(os.path.join(p, "verify.py")):
        names.append(d)
    else:                                   # 系列子目录
        for sub in sorted(os.listdir(p)):
            sp = os.path.join(p, sub)
            if os.path.isdir(sp) and os.path.exists(os.path.join(sp, "verify.py")):
                names.append("%s/%s" % (d, sub))
ok = 0
for n in names:
    print("== %s" % n)
    r = subprocess.run([PY, os.path.join(ROOT, n, "verify.py")],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (r.stdout or "") + (r.stderr or "")
    for line in out.strip().splitlines():
        print("   " + line)
    if r.returncode == 0:
        ok += 1
    else:
        print("   [FAIL] exit=%d" % r.returncode)
print()
print("汇总：%d / %d 通过" % (ok, len(names)))
sys.exit(0 if ok == len(names) else 1)
''')
    print("  总索引 + verify_all.py 已写入")


if __name__ == "__main__":
    main()
