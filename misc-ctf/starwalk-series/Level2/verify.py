# -*- coding: utf-8 -*-
"""自校验：五张照片里找出带 GPS 的那张，换算坐标并断言落点。"""
import io, os, struct, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{hunan_zhangjiajie}"
TARGET = (29.332338, 110.426066)          # 东方明珠

def gps_of(path):
    ex = Image.open(path).getexif()
    g = ex.get_ifd(0x8825)
    if not g or 2 not in g or 4 not in g:
        return None
    one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
    deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
    lat, lon = deg(g[2]), deg(g[4])
    return (-lat if g.get(1) == "S" else lat), (-lon if g.get(3) == "W" else lon)

d = os.path.join(HERE, "files", "photos")
found = []
for name in sorted(os.listdir(d)):
    p = os.path.join(d, name)
    g = gps_of(p)
    print("  %-14s %s" % (name, ("%s" % (g,)) if g else "无 GPS"))
    if g:
        found.append((name, g))
assert len(found) == 1, "带 GPS 的照片应当只有一张，实际 %d 张" % len(found)
name, (lat, lon) = found[0]
print("  命中: %s  %.5f, %.5f" % (name, lat, lon))
assert abs(lat - TARGET[0]) < 0.01 and abs(lon - TARGET[1]) < 0.01, "坐标不在预期地标附近"
print("  预期答案:", EXPECT)
print("  PASS exif-gps-ctf")
