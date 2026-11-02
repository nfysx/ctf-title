# -*- coding: utf-8 -*-
import os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{y0ur_ph0t0_t4lk5}"
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
