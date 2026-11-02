# -*- coding: utf-8 -*-
"""自校验：从 EXIF ImageDescription 取 base64 并解码。"""
import base64, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{m3t4d4t4_t4lks}"
ex = Image.open(os.path.join(HERE, "files", "photo.jpg")).getexif()
desc = ex.get(0x010E)
assert desc, "ImageDescription 为空"
flag = base64.b64decode(desc).decode()
print("  ImageDescription:", desc[:40] + ("…" if len(desc) > 40 else ""))
print("  解出:", flag)
assert flag == EXPECT
print("  PASS exif-meta-ctf")
