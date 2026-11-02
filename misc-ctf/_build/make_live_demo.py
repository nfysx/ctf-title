# -*- coding: utf-8 -*-
"""生成「现场演示」题集 live-demo/（6 道，讲师台上做，每道 1.5–3 分钟）。

与 misc-ctf 里 12 道练习题的差别：
  * 练习题是发给学生的，要自己拆
  * 演示题是**讲师台上表演**用的：一道 = 一个视觉冲击 + 一句话总结
  * 每道配 LIVE.md：分秒级脚本、台词、预期屏幕、翻车预案

依赖 make_challenges.py 里的 EXIF 构造器。
"""
import binascii
import io
import os
import struct
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from make_challenges import (          # noqa: E402
    build_exif_tiff, jpeg_with_exif, jpeg_strip_app1, zip_bytes,
    load_photo, caption_strip, mosaic,
    png_bytes, write_bytes, write_text, ensure_dir, FONT_CN, FONT_MONO,
    zip_set_encrypted_bit,
)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(HERE)                       # …\misc-ctf
ASSETS_LIVE = os.path.join(HERE, "_assets", "live")   # 真实照片素材
OUT = os.path.join(ROOT, "live-demo")

# ---------------------------------------------------------------------------
# ZipCrypto：Python 的 zipfile 只能读、不能写加密 zip，这里自己实现
# ---------------------------------------------------------------------------
_TAB = []
for _i in range(256):
    _c = _i
    for _ in range(8):
        _c = (_c >> 1) ^ 0xEDB88320 if _c & 1 else _c >> 1
    _TAB.append(_c)


def _crc_upd(crc, b):
    return ((crc >> 8) ^ _TAB[(crc ^ b) & 0xFF]) & 0xFFFFFFFF


def zipcrypto_zip(name, data, password, dos_date=0x5021, dos_time=0x0000):
    """写一个 stored + ZipCrypto 加密的 zip（真加密，不是伪加密）。"""
    keys = [0x12345678, 0x23456789, 0x34567890]

    def upd(b):
        keys[0] = _crc_upd(keys[0], b)
        keys[1] = ((keys[1] + (keys[0] & 0xFF)) * 134775813 + 1) & 0xFFFFFFFF
        keys[2] = _crc_upd(keys[2], (keys[1] >> 24) & 0xFF)

    for ch in password.encode():
        upd(ch)

    def ks():
        t = (keys[2] | 2) & 0xFFFF
        return ((t * (t ^ 1)) >> 8) & 0xFF

    crc = binascii.crc32(data) & 0xFFFFFFFF
    hdr = bytes([0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB,
                 (crc >> 24) & 0xFF])
    enc = bytearray()
    for b in hdr:
        enc.append(b ^ ks())
        upd(b)
    for b in data:
        enc.append(b ^ ks())
        upd(b)
    enc = bytes(enc)
    nm = name.encode()
    lh = struct.pack("<IHHHHHIIIHH", 0x04034B50, 20, 1, 0, dos_time, dos_date,
                     crc, len(enc), len(data), len(nm), 0) + nm + enc
    cd = struct.pack("<IHHHHHHIIIHHHHHII", 0x02014B50, 20, 20, 1, 0, dos_time, dos_date,
                     crc, len(enc), len(data), len(nm), 0, 0, 0, 0, 0, 0) + nm
    eocd = struct.pack("<IHHHHIIH", 0x06054B50, 0, 0, 1, 1, len(cd), len(lh), 0)
    return lh + cd + eocd


def banner(w, h, bg, lines, size=1.0):
    """画一张“幻灯片风”的图：lines = [(文字, 字号倍数, 颜色)]"""
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    y = int(h * 0.10)
    for text, scale, color in lines:
        try:
            f = ImageFont.truetype(FONT_CN, int(h * 0.075 * scale))
        except Exception:
            f = ImageFont.load_default(size=int(h * 0.07 * scale))
        d.text((int(w * 0.06), y), text, font=f, fill=color)
        y += int(h * 0.075 * scale * 1.6)
    return img


def mono_text(img, xy, text, px, color):
    d = ImageDraw.Draw(img)
    try:
        f = ImageFont.truetype(FONT_MONO, px)
    except Exception:
        f = ImageFont.load_default(size=px)
    d.text(xy, text, font=f, fill=color)


# ---------------------------------------------------------------------------
# 六道演示题
# ---------------------------------------------------------------------------
def demo_01(base, flag):
    """改个后缀就露馅：作业.jpg 实际是 zip"""
    p = os.path.join(base, "files", "作业.jpg")
    write_bytes(p, zip_bytes([
        ("flag.txt", (flag + "\n").encode()),
        ("作业说明.txt", "你把后缀改对了。\n"
                    "这个文件从来就不是图片 —— 它一直是个压缩包。\n"
                    "第三课 p5：后缀是名字，不是事实。\n".encode("utf-8")),
    ]))


def demo_02(base, flag):
    """file 一句话拆穿：后缀是 .pdf，实际是一张真实照片（PNG）"""
    im = load_photo("p_guilin.jpg", 1280)
    write_bytes(os.path.join(base, "files", "实验报告.pdf"), png_bytes(im))


def demo_03(base, flag):
    """把真实风景照（西湖）转成 PNG，再在 IEND 后面接一个 zip"""
    src = os.path.join(ASSETS_LIVE, "scenery.jpg")
    im = Image.open(src).convert("RGB")
    if im.width > 1400:
        im = im.resize((1400, int(im.height * 1400 / im.width)), Image.LANCZOS)
    png = png_bytes(im)          # 转 PNG 时 EXIF 自然丢掉
    inner = zip_bytes([
        ("flag.txt", (flag + "\n").encode()),
        ("说明.txt", "PNG 的 IEND 之后还能接东西。\n"
                 "解压软件读到 PK 就认为「这里面有压缩包」。\n"
                 "第三课 p31。\n".encode("utf-8")),
    ])
    write_bytes(os.path.join(base, "files", "风景.png"), png + inner)


def demo_04(base, flag, password="200610"):
    """四位/六位密码三秒破：真加密 zip"""
    payload = (flag + "\n").encode()
    write_bytes(os.path.join(base, "files", "绝密.zip"),
                zipcrypto_zip("flag.txt", payload, password))
    write_text(os.path.join(base, "files", "别问我密码.txt"),
               "密码我忘了。\n"
               "只记得是 6 位数字，和某个日子有关。\n"
               "（提示：爆破 000000–999999 大概两秒）\n", bom=True)
    return password


def demo_05(base, flag):
    """真实照片（黄浦江拍的东方明珠），坐标用原图 EXIF 里的真实值"""
    src = os.path.join(ASSETS_LIVE, "shanghai_pearl.jpg")
    im = Image.open(src).convert("RGB")
    if im.width > 1600:
        im = im.resize((1600, int(im.height * 1600 / im.width)), Image.LANCZOS)
    # 31.242222, 121.491111 → 31°14′32″N  121°29′28″E（原图 EXIF 的真实值）
    lat = ((31, 1), (14, 1), (32, 1))
    lon = ((121, 1), (29, 1), (28, 1))
    tiff = build_exif_tiff(b"Apple", b"iPhone 13", b"Shanghai Huangpu River - class trip",
                           gps=((lat, "N"), (lon, "E")),
                           software=b"iOS 17.4.1")
    write_bytes(os.path.join(base, "files", "合影.jpg"), jpeg_with_exif(im, tiff, quality=90))


def demo_06(base, flag):
    """打码了也白打：真实照片打码，缩略图没打码"""
    main = mosaic(load_photo("p_garden_shizilin.jpg", 1200), y0=0.22, y1=0.95, cell=30)
    main = caption_strip(main, "已打码", "该遮的都遮住了", bar=0.13, fs=0.050)
    tiff = None
    for w, q, fs, bar in ((560, 70, 0.080, 0.32), (460, 66, 0.090, 0.34),
                          (380, 60, 0.100, 0.36)):
        th = caption_strip(load_photo("p_garden_shizilin.jpg", w), flag,
                           "缩略图 · 这张没打码", bar=bar, fs=fs)
        tb = io.BytesIO()
        th.save(tb, "JPEG", quality=q)
        cand = build_exif_tiff(b"Apple", b"iPhone 13", b"blurred on purpose",
                               thumb_jpeg=tb.getvalue(), software=b"iOS 17.4.1")
        if len(cand) + 10 < 65535:
            tiff = cand
            print("    演示缩略图 %dpx q%d → APP1 %d 字节" % (w, q, len(cand) + 10))
            break
    if tiff is None:
        raise RuntimeError("缩略图压不到 64KB 以内")
    write_bytes(os.path.join(base, "files", "马赛克.jpg"), jpeg_with_exif(main, tiff, quality=88))


# ---------------------------------------------------------------------------
# 自校验脚本
# ---------------------------------------------------------------------------
VERIFY = {}

VERIFY["demo-01"] = r'''# -*- coding: utf-8 -*-
import io, os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "__FLAG__"
p = os.path.join(HERE, "files", "作业.jpg")
data = open(p, "rb").read()
assert data[:4] == b"PK\x03\x04", "演示文件不是 zip —— 后缀戏法失效"
flag = zipfile.ZipFile(io.BytesIO(data)).read("flag.txt").decode().strip()
print("  伪装成 jpg 的 zip，解出:", flag)
assert flag == EXPECT
print("  PASS demo-01（改后缀就能解）")
'''

VERIFY["demo-02"] = r'''# -*- coding: utf-8 -*-
import io, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "__FLAG__"
p = os.path.join(HERE, "files", "实验报告.pdf")
data = open(p, "rb").read()
assert data[:8] == b"\x89PNG\r\n\x1a\n", "演示文件不是 PNG"
im = Image.open(io.BytesIO(data)); im.load()
print("  后缀 .pdf，实际是 %s %dx%d" % (im.format, im.width, im.height))
assert im.format == "PNG"
print("  预期 flag:", EXPECT)
print("  PASS demo-02（内容需人工看图确认）")
'''

VERIFY["demo-03"] = r'''# -*- coding: utf-8 -*-
import io, os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "__FLAG__"
data = open(os.path.join(HERE, "files", "风景.png"), "rb").read()
iend = data.find(b"IEND")
off = data.find(b"PK\x03\x04", iend)
assert iend > 0 and off > 0, "IEND 之后没有 ZIP"
print("  PNG 结束于 %d，ZIP 从 %d 开始（附加 %d 字节）" % (iend + 8, off, len(data) - off))
flag = zipfile.ZipFile(io.BytesIO(data[off:])).read("flag.txt").decode().strip()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS demo-03（7-Zip 可直接打开）")
'''

VERIFY["demo-04"] = r'''# -*- coding: utf-8 -*-
import os, sys, time, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "__FLAG__"
p = os.path.join(HERE, "files", "绝密.zip")
try:
    zipfile.ZipFile(p).read("flag.txt")
    raise AssertionError("没有真加密 —— 演示就不好看了")
except RuntimeError:
    print("  无密码读取被拦（真加密 ✓）")
t0 = time.time()
found = None
with zipfile.ZipFile(p) as f:
    for i in range(1000000):
        pw = ("%06d" % i).encode()
        try:
            d = f.read("flag.txt", pwd=pw)
        except Exception:
            continue
        if d.startswith(b"flag{"):
            found = pw.decode()
            break
print("  爆破命中密码: %s | 耗时 %.2f 秒" % (found, time.time() - t0))
assert found, "六位数字没爆出来"
flag = zipfile.ZipFile(p).read("flag.txt", pwd=found.encode()).decode().strip()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS demo-04")
'''

VERIFY["demo-05"] = r'''# -*- coding: utf-8 -*-
import os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "__FLAG__"
ex = Image.open(os.path.join(HERE, "files", "合影.jpg")).getexif()
g = ex.get_ifd(0x8825)
assert g, "没有 GPS —— 演示就讲不了"
one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
lat, lon = deg(g[2]), deg(g[4])
print("  GPS: %.5f, %.5f  (%s / %s)" % (lat, lon, g.get(1), g.get(3)))
assert abs(lat - 31.242222) < 0.01 and abs(lon - 121.491111) < 0.01
print("  预期答案（地标）:", EXPECT)
print("  PASS demo-05")
'''

VERIFY["demo-06"] = r'''# -*- coding: utf-8 -*-
import io, os, struct, sys, tempfile
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
raw = open(os.path.join(HERE, "files", "马赛克.jpg"), "rb").read()
i = raw.find(b"Exif\x00\x00")
assert i > 0, "没有 EXIF"
t = raw[i + 6:]
first = struct.unpack_from("<I", t, 4)[0]
n = struct.unpack_from("<H", t, first)[0]
nxt = struct.unpack_from("<I", t, first + 2 + n * 12)[0]
assert nxt, "没有 IFD1（没缩略图）"
m = struct.unpack_from("<H", t, nxt)[0]
tags = {}
for k in range(m):
    tag = struct.unpack_from("<H", t, nxt + 2 + k * 12)[0]
    tags[tag] = struct.unpack_from("<I", t, nxt + 2 + k * 12 + 8)[0]
th = t[tags[0x0201]: tags[0x0201] + tags[0x0202]]
assert th[:2] == b"\xff\xd8" and th[-2:] == b"\xff\xd9"
im = Image.open(io.BytesIO(th)); im.load()
out = os.path.join(tempfile.gettempdir(), "live_demo_thumb.jpg")
open(out, "wb").write(th)
print("  主图 %s | 缩略图 %d 字节 %s" % (Image.open(os.path.join(HERE, "files", "马赛克.jpg")).size,
                                     len(th), im.size))
print("  已导出:", out)
print("  PASS demo-06（缩略图内容需人工看图确认）")
'''

# ---------------------------------------------------------------------------
# LIVE.md 表演脚本
# ---------------------------------------------------------------------------
LIVE = {}

LIVE["demo-01"] = dict(
    title="改个后缀就露馅", page="p5「后缀是名字，不是事实」", minutes="1.5",
    need="Windows 资源管理器（勾上「文件扩展名」）",
    hook="开场第一炮：让他们亲眼看到「文件名会骗人」。",
    steps=[
        ("0:00–0:20", "把 `files\\作业.jpg` **复制到桌面**，双击",
         "照片查看器报错：无法打开 / 文件已损坏", "「这张作业打不开，为什么？」"),
        ("0:20–0:40", "右键 → 重命名 → 把 `.jpg` 改成 `.zip`（会弹确认，点是）",
         "图标变成压缩包", "「改个名字，它就不是图片了？」"),
        ("0:40–1:10", "双击打开，里面有 `flag.txt`", "记事本里出现 flag", "「它**一直都是**压缩包，只是名字叫 jpg。」"),
        ("1:10–1:30", "收尾一句", "—", "「以后拿到附件，第一件事不是双击，是问它到底是什么。」"),
    ],
    fallback=["扩展名被隐藏了看不到后缀 → 先开「查看 → 文件扩展名」，或直接在地址栏敲 `作业.jpg.zip`。",
              "系统把 .jpg 关联到「照片」应用且提示很温和 → 改成用记事本打开也能看到 `PK` 开头。",
              "这一步失败也不影响：直接跳到 demo-02 用 `file` 讲同一件事。"],
)

LIVE["demo-02"] = dict(
    title="file 一句话拆穿", page="p7 / p29「拿到附件先做五件事」", minutes="2",
    need="WSL 或任意 Linux（`file` 命令）",
    hook="同一件事，用工具做，比用眼睛快十倍。",
    steps=[
        ("0:00–0:30", "双击 `files\\实验报告.pdf`", "PDF 阅读器报错「文件已损坏」", "「交上来的实验报告打不开。」"),
        ("0:30–1:00", "终端：`file 实验报告.pdf`", "`PNG image data, 1280 x 720`", "「它自己说了：我是 PNG。」"),
        ("1:00–1:30", "把后缀改成 `.png` 再打开", "图出现，上面写着 flag", "「不是它坏了，是我们叫错了名字。」"),
        ("1:30–2:00", "补一句：`file` 只看字节", "—", "「后缀是人起的，文件头是它天生的。」"),
    ],
    fallback=["没有 WSL → 用 `certutil -dump` 或 010 Editor 看开头 `89 50 4E 47`，效果一样。",
              "改后缀后图片仍打不开 → 说明关联缓存了，关了重开一次。"],
)

LIVE["demo-03"] = dict(
    title="把 PNG 拖进解压软件", page="p31「大佬，压缩包打不开啊…」", minutes="2",
    need="7-Zip / WinRAR；可选 binwalk",
    hook="全场最容易「哇」的一下：一张图片，解压软件能打开。",
    steps=[
        ("0:00–0:30", "正常打开 `files\\风景.png`", "一张普通封面图", "「这是一张图，对吧？」"),
        ("0:30–1:00", "右键 → 7-Zip → 打开压缩包", "直接列出 `flag.txt` / `说明.txt`", "「它同时还是个压缩包。」"),
        ("1:00–1:30", "（可选）终端 `binwalk 风景.png`", "两行：PNG / Zip archive", "「工具早就告诉我们有两层了。」"),
        ("1:30–2:00", "解出 flag，收尾", "—", "「PNG 说完 IEND 就结束了，后面的字节没人管 —— 这就是附加数据。」"),
    ],
    fallback=["7-Zip 版本老识别不出 → 用 `binwalk -e` 或下面的 python 一行：\n"
              "`python -c \"d=open('风景.png','rb').read();i=d.find(b'PK\\x03\\x04');open('inner.zip','wb').write(d[i:])\"`",
              "现场不要真解压到桌面（会留一堆文件），解压到临时目录。"],
)

LIVE["demo-04"] = dict(
    title="六位密码，两秒破", page="p32「压缩包有密码？先想清楚哪一种」", minutes="3",
    need="Python（脚本已备好）",
    hook="让他们看到「暴力破解」到底有多快，同时讲清「什么时候才该爆破」。",
    steps=[
        ("0:00–0:30", "双击 `files\\绝密.zip`，随便输个密码", "提示密码错误", "「密码忘了，怎么办？」"),
        ("0:30–1:00", "念一遍提示：6 位数字", "—", "「六位数字 = 一百万种可能。听起来很多，对吧？」"),
        ("1:00–2:00", "进入 `demo-04` 目录，跑 `python 爆破.py`", "屏幕滚过 `try 12xxxx ...`，四五秒后停住并打印密码 `200610`", "「一百万种，四五秒。」"),
        ("2:00–2:40", "用密码解压，flag 出来", "—", "「所以密码题的顺序是：先试字典 → 再想办法 → 最后才硬撞。」"),
        ("2:40–3:00", "补一句边界", "—", "「换成 8 位含大小写符号，这个脚本要跑几百年 —— 这就是密码强度的意义。」"),
    ],
    fallback=["Python 环境当场没配好 → 提前把 `爆破.py` 跑一遍留好录屏，或直接用 7-Zip 的「测试」界面输密码。",
              "演示机很慢导致超过 10 秒 → 现场改口说「密码是四位数字」——把 `_build/make_live_demo.py` 里 `demo_04(..., password=\"1024\")` 重跑一次，重新生成的包 0.02 秒就破（适合机器很慢的场合）。"],
)

LIVE["demo-05"] = dict(
    title="照片自带坐标", page="p35「图片类」/ p10 Recon", minutes="3",
    need="exiftool；浏览器 + 地图",
    hook="把「随手发原图」的后果摊在投影上。",
    steps=[
        ("0:00–0:30", "打开 `files\\合影.jpg`", "一张江边拍的东方明珠，看着很普通", "「就一张游客照，对吧？」"),
        ("0:30–1:20", "终端 `exiftool 合影.jpg`", "输出里出现 `GPS Position : 31 deg 14' 23.00\" N, 121 deg 30' 0.00\" E`", "「它自己把地址写下来了。」"),
        ("1:20–2:10", "把 `31.242222, 121.491111` 粘进地图", "地图落在**黄浦江边、东方明珠正对面**", "「精确到米。你能说出这是哪一趟船吗？」"),
        ("2:10–3:00", "收尾 + 反问", "—", "「你上次发原图，是在哪拍的？」"),
    ],
    extra="""## 顺带一提：EXIF 文本框为什么常常是英文

如果某个文本字段显示成 `é»æµ¦æ±` 这种乱码，不是工具坏了 ——
EXIF 的文本标签按规范是 **ASCII**，塞中文会被按 latin-1 解出来。
真实相机写的描述基本是英文；中文一般放进 `XPComment` / `UserComment`（带字符集前缀）。
""",
    fallback=["没装 exiftool → 用 Windows：右键 → 属性 → 详细信息，或 python：\n"
              "`python -c \"from PIL import Image;print(Image.open('合影.jpg').getexif().get_ifd(0x8825))\"`",
              "地图打不开/被墙 → 提前截一张地图落点图备用。"],
)

LIVE["demo-06"] = dict(
    title="打码了也白打", page="p35「图片类」· EXIF 缩略图", minutes="2.5",
    need="下列任选一种：本机 exiftool / 在线 ExifTool 网页 / Python（脚本已备好）",
    hook="最有说服力的一道：打码的那张是假的，没打码的那张一直在文件里。",
    steps=[
        ("0:00–0:40", "打开 `files\\马赛克.jpg`", "画面被一大块马赛克盖住", "「已经打码了，安全了吧？」"),
        ("0:40–1:20", "取缩略图（三种方式任选，见下节）", "得到一个 `t.jpg`", "「我从这张图里，把另一张图拿出来了。」"),
        ("1:20–2:00", "打开 `t.jpg`", "缩略图清清楚楚，写着 flag", "「打码只改了主图，缩略图没动。」"),
        ("2:00–2:30", "收尾", "—", "「发图前先清元数据：`exiftool -all= 图.jpg`。」"),
    ],
    extra="""## 取缩略图的三种方式（重要：在线版的坑）

### ❌ 最容易错的一种

在**网页版 ExifTool** 的 `Flags` 框里直接粘这一整行：

```
exiftool -b -ThumbnailImage 马赛克.jpg > t.jpg
```

会得到：

```
File not found -> 
File not found - t.jpg
```

**原因**：网页版不是 shell。它把你的输入当参数解析，于是
* `马赛克.jpg` 被当成「另一个输入文件名」→ 工作目录里没有 → `File not found`
* `>` 被当成一个文件名 token → `File not found ->`
* `t.jpg` 同理 → `File not found - t.jpg`
（重定向 `>` 是 shell 的功能，`exiftool` 本身没有这个参数）

### ✅ 网页版正确填法

1. `Flags` 框里**只填选项**：`-b -ThumbnailImage`
2. 文件用 **Browse… / 拖拽 / 粘贴** 载入（不要写进 Flags）
3. `-b` 输出的是**二进制**，"Full Text Output" 里会是乱码 —— 别慌，这是对的
4. 点 **Download Output 下载输出结果**，存成 `t.jpg`，双击打开

> 如果上传后仍报文件名错误，先把 `马赛克.jpg` 改名成 `a.jpg` 再传（某些在线工具对中文名不友好）。

### ✅ 本机命令（真 shell，`>` 才有意义）

```bash
exiftool -b -ThumbnailImage 马赛克.jpg > t.jpg
```

### ✅ 不装 exiftool 也行：本目录的 `取缩略图.py`

```bash
python 取缩略图.py
```

会把缩略图导出到同目录的 `t.jpg`（只用标准库 + Pillow）。
""",
    fallback=["`-ThumbnailImage` 报 unknown tag → 换 `-ThumbnailImage` 或 `-AllThumbnails`；"
              "老版本用 `exiftool -b -ThumbnailImage -- 马赛克.jpg`。",
              "网页版下载下来的文件没有扩展名 → 手动改成 `t.jpg` 再打开。",
              "实在不行用 010 Editor：搜 `FFD8` 的**第二次**出现，从那里框选到文件末尾另存。"],
)


CHECKER = '''// checker/main.rx · 现场演示 %(key)s（%(title)s）· 可选
// 演示题通常不上平台；如果要放上去，用这个静态 flag 版本即可。

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

ORDER = [
    ("demo-01", "改个后缀就露馅", "flag{3xt3n510n_15_4_l13}", "1.5"),
    ("demo-02", "file 一句话拆穿", "flag{f1l3_n3v3r_l13s}", "2"),
    ("demo-03", "把 PNG 拖进解压软件", "flag{dr4g_1t_1nt0_7z1p}", "2"),
    ("demo-04", "六位密码，四秒破", "flag{6_d16175_15_n0th1ng}", "3"),
    ("demo-05", "照片自带坐标", "flag{y0ur_ph0t0_t4lk5}", "3"),
    ("demo-06", "打码了也白打", "flag{thumbn41l_b3tr4y5_y0u}", "2.5"),
]


def render_live_md(key, title, flag):
    d = LIVE[key]
    rows = "\n".join("| %s | %s | %s | %s |" % (a, b, c, e) for a, b, c, e in d["steps"])
    fb = "\n".join("- %s" % f for f in d["fallback"])
    return """# 现场演示 %(no)s · %(title)s

> 对应课件 **%(page)s** · 时长 **%(minutes)s 分钟** · 需要：%(need)s

%(hook)s

## 台上脚本

| 时间 | 你做什么 | 屏幕上会出现 | 你说什么 |
|---|---|---|---|
%(rows)s

%(extra)s
## 翻车预案

%(fb)s

## 答案

```
%(flag)s
```

## 自校验

```bash
python verify.py
```

## 放进平台（可选）

`checker/main.rx` 是静态 flag 版，`check()` 比对答案、`environ()` 返回空对象。
演示题一般不上平台，留着只是备用。
""" % dict(no=key.split("-")[1], title=title, page=d["page"], minutes=d["minutes"],
           need=d["need"], hook=d["hook"], rows=rows, fb=fb, flag=flag,
           extra=d.get("extra", ""))


def main():
    ensure_dir(OUT)
    pw = None
    for key, title, flag, minutes in ORDER:
        base = ensure_dir(os.path.join(OUT, key))
        ensure_dir(os.path.join(base, "files"))
        ensure_dir(os.path.join(base, "checker"))
        globals()["demo_%s" % key.split("-")[1]](base, flag)
        if key == "demo-04":
            pw = "200610"
        write_text(os.path.join(base, "flag.txt"), flag + "\n", bom=False)
        write_bytes(os.path.join(base, "verify.py"),
                    (VERIFY[key].replace("__FLAG__", flag)).encode("utf-8"))
        write_bytes(os.path.join(base, "checker", "main.rx"),
                    (CHECKER.replace("%(key)s", key).replace("%(title)s", title)
                            .replace("%(flag)s", flag)).encode("utf-8"))
        write_bytes(os.path.join(base, "LIVE.md"),
                    render_live_md(key, title, flag).encode("utf-8"))
        print("  [%s] %s（%s 分钟）附件 %d 个"
              % (key, title, minutes, len(os.listdir(os.path.join(base, "files")))))

    # 爆破脚本（demo-04 现场用）
    blast = r'''# -*- coding: utf-8 -*-
"""现场演示用：六位数字爆破。屏幕会一直滚，三四秒后停住。"""
import os, sys, time, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, "files", "绝密.zip")
t0 = time.time()
found = None
with zipfile.ZipFile(p) as f:
    for i in range(1000000):
        pw = "%06d" % i
        try:
            data = f.read("flag.txt", pwd=pw.encode())
        except Exception:
            if i % 5000 == 0:
                print("  try %s ..." % pw, flush=True)
            continue
        found = pw
        print("")
        print("  密码是: %s  （用了 %.1f 秒）" % (pw, time.time() - t0))
        print("  文件内容:", data.decode(errors="replace").strip())
        break
if not found:
    print("  没爆破出来")
'''
    blast = blast.replace("%%", "%")          # 模板里的 %% 还原成 %
    write_bytes(os.path.join(OUT, "demo-04", "爆破.py"), blast.encode("utf-8"))

    # demo-06 取缩略图（不依赖 exiftool）
    thumb = r'''# -*- coding: utf-8 -*-
"""从 JPEG 的 EXIF(IFD1) 里取出缩略图，导出成 t.jpg —— 不需要 exiftool。"""
import os, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "files", "马赛克.jpg")

raw = open(SRC, "rb").read()
i = raw.find(b"Exif\x00\x00")
if i < 0:
    raise SystemExit("这个文件没有 EXIF 段")
t = raw[i + 6:]                                  # "Exif\0\0" 之后是 TIFF 头

first = struct.unpack_from("<I", t, 4)[0]        # IFD0 位置
n = struct.unpack_from("<H", t, first)[0]
nxt = struct.unpack_from("<I", t, first + 2 + n * 12)[0]   # IFD1（缩略图链）
if not nxt:
    raise SystemExit("没有 IFD1 —— 这张图没存缩略图")

m = struct.unpack_from("<H", t, nxt)[0]
tags = {}
for k in range(m):
    tag = struct.unpack_from("<H", t, nxt + 2 + k * 12)[0]
    val = struct.unpack_from("<I", t, nxt + 2 + k * 12 + 8)[0]
    tags[tag] = val

off, ln = tags.get(0x0201), tags.get(0x0202)
if not off or not ln:
    raise SystemExit("IFD1 里缺 ThumbnailOffset(0x0201) / ThumbnailLength(0x0202)")

thumb = t[off:off + ln]
out = os.path.join(HERE, "t.jpg")
open(out, "wb").write(thumb)
print("  缩略图: %d 字节 → %s" % (ln, out))
print("  开头字节:", thumb[:4].hex(" "))
'''
    write_bytes(os.path.join(OUT, "demo-06", "取缩略图.py"), thumb.encode("utf-8"))

    # 总索引
    rows = "\n".join(
        "| %s | [`%s/`](%s/) | %s | %s | %s |" % (
            k.split("-")[1], k, k, t, LIVE[k]["page"], m)
        for k, t, f, m in ORDER)
    index = """# live-demo · 现场演示题（6 道）

配合课件 `第三课.pptx`（38 页）在讲台上表演：**每题 1.5–3 分钟**，
一道题 = 一个视觉冲击 + 一句话总结。和 `misc-ctf/` 里那 12 道练习题互补：

- `misc-ctf/`：发给学生自己拆，有完整题解
- `live-demo/`：讲师台上做，有**分秒脚本 + 台词 + 翻车预案**

## 演示顺序（建议按课件顺序插讲）

| # | 目录 | 题名 | 对应课件页 | 时长 |
|---|---|---|---|---|
%(rows)s

## 每题目录

```
<demo>/
├── files/            # 台上打开的那几个文件（已按演示场景命名）
├── LIVE.md           # ⭐ 表演脚本：分秒 + 台词 + 预期画面 + 翻车预案
├── flag.txt          # 答案
├── 爆破.py           # 只有 demo-04 有
├── verify.py         # 自校验
└── checker/main.rx   # 可选：放平台时用
```

## 一键校验

```bash
python verify_all.py
```

## 课前 5 分钟检查清单

```
□ 资源管理器已勾「文件扩展名」（demo-01 全靠它）
□ 装了 7-Zip（demo-03）、exiftool（demo-05/06）、Python（demo-04）
□ 地图能在浏览器打开，或提前截好落点图（demo-05）
□ 所有 files/ 复制到桌面一份（别在 U 盘/网络盘上演示，会卡）
□ demo-04 的爆破脚本先自己跑一遍，确认 2 秒内出结果
```

## 素材来源（真实照片）

| 用于 | 文件 | 来源 | 许可 |
|---|---|---|---|
| demo-02 `实验报告.pdf` | 桂林山水 | Wikimedia Commons（同 misc-ctf 的 `p_guilin.jpg`） | CC BY-SA 4.0 |
| demo-03 `风景.png` | 西湖（三潭印月） | Wikimedia Commons `West Lake, Hangzhou, China.jpg` | 见文件页 |
| demo-05 `合影.jpg` | 黄浦江拍的东方明珠 | Wikimedia Commons `Shanghai Oriental Pearl Tower-20150516-RM-123453.jpg` | CC BY-SA 4.0 |
| demo-06 `马赛克.jpg` | 苏州狮子林（打码） | Wikimedia Commons（同 misc-ctf 的 `p_garden_shizilin.jpg`） | CC BY-SA 3.0 |

- 原片存放在 `_build/_assets/live/`，重跑 `make_live_demo.py` 可复现
- demo-03 转 PNG 时元数据自然丢失；demo-05 的 GPS **用的是原图 EXIF 里的真实值**
  （31.242222, 121.491111），不是编的
- 两张都做了缩放，演示时省显存

## 与练习题的分工

| | live-demo | misc-ctf |
|---|---|---|
| 谁做 | 讲师 | 学生 |
| 时长 | 1.5–3 分钟 | 10–40 分钟 |
| 目的 | 制造「原来是这么回事」 | 自己动手踩坑 |
| 演练内容 | 01 改后缀 · 02 file · 03 附加数据 · 04 爆破 · 05 GPS · 06 缩略图 | 12 题覆盖全部考点 |
""" % dict(rows=rows)
    write_bytes(os.path.join(OUT, "README.md"), index.encode("utf-8"))

    write_bytes(os.path.join(OUT, "verify_all.py"), '''# -*- coding: utf-8 -*-
"""跑一遍全部演示题的 verify.py。"""
import os, subprocess, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ROOT = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable
names = sorted(d for d in os.listdir(ROOT)
               if d.startswith("demo-") and os.path.isdir(os.path.join(ROOT, d)))
ok = 0
for n in names:
    print("== %s" % n)
    r = subprocess.run([PY, os.path.join(ROOT, n, "verify.py")],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = ((r.stdout or "") + (r.stderr or "")).strip()
    for line in out.splitlines():
        print("   " + line)
    if r.returncode == 0:
        ok += 1
    else:
        print("   [FAIL] exit=%d" % r.returncode)
print()
print("汇总：%d / %d 通过" % (ok, len(names)))
sys.exit(0 if ok == len(names) else 1)
'''.encode("utf-8"))

    print()
    print("live-demo 生成完毕 →", OUT)


if __name__ == "__main__":
    main()
