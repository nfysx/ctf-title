# -*- coding: utf-8 -*-
import io, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{f1l3_n3v3r_l13s}"
p = os.path.join(HERE, "files", "实验报告.pdf")
data = open(p, "rb").read()
assert data[:8] == b"\x89PNG\r\n\x1a\n", "演示文件不是 PNG"
im = Image.open(io.BytesIO(data)); im.load()
print("  后缀 .pdf，实际是 %s %dx%d" % (im.format, im.width, im.height))
assert im.format == "PNG"
print("  预期 flag:", EXPECT)
print("  PASS demo-02（内容需人工看图确认）")
