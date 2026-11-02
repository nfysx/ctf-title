# -*- coding: utf-8 -*-
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
