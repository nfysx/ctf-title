# -*- coding: utf-8 -*-
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
print("  修复后: %s  %dx%d" % (im.format, im.width, im.height))
assert im.format == "PNG" and im.width == 1280 and im.height > 500
print("  修复图已写出:", out)
print("  预期 flag:", "flag{h3ader_1s_th3_truth}")
print("  PASS file-header-ctf（图片内容需人工看图确认）")
