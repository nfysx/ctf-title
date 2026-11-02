# -*- coding: utf-8 -*-
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
