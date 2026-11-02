# -*- coding: utf-8 -*-
"""自校验：只读红色通道最低位。"""
import os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{0n3_ch4nn3l_15_3n0ugh}"
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
