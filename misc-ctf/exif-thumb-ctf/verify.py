# -*- coding: utf-8 -*-
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
print("  缩略图: %d 字节  %s %s" % (ln, im.format, im.size))
print("  已导出到:", out)
print("  预期 flag:", "flag{thumbn41l_n3v3r_l13s}")
print("  PASS exif-thumb-ctf（缩略图内容需人工看图确认）")
