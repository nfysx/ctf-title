# -*- coding: utf-8 -*-
"""自校验：按 RGB 顺序读最低位，遇 0xFF 结束。"""
import os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{lsb_1s_1n_th3_l4st_b1t}"
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
