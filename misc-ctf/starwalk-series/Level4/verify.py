# -*- coding: utf-8 -*-
"""自校验：照片存在、可解码、元数据已清空。"""
import os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
ANSWER = "flag{shanghai_nanjingdonglu_yongan}"
p = os.path.join(HERE, "files", "photo.jpg")
im = Image.open(p); im.load()
print("  照片: %s  %dx%d" % (im.format, im.width, im.height))
ex = im.getexif()
leak = {k: v for k, v in ex.items() if k in (0x010E, 0x0132, 0x8825, 0x9003)}
assert not leak, "元数据没清干净: %r" % leak
print("  元数据: 已清空")
print("  预期答案:", ANSWER)
print("  PASS geo-nanjinglu-ctf（招牌文字由人工看图确认）")
