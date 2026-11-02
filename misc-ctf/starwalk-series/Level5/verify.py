# -*- coding: utf-8 -*-
"""自校验：三张照片按拍摄时间排序 → 路线。"""
import datetime, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{shanghai_zhangjiajie_chongqing}"

def city_of(lon):
    if lon > 118:
        return "shanghai"
    if lon > 108:
        return "zhangjiajie"
    return "chongqing"

rows = []
for name in sorted(os.listdir(os.path.join(HERE, "files"))):
    if not name.lower().endswith(".jpg"):
        continue
    ex = Image.open(os.path.join(HERE, "files", name)).getexif()
    g = ex.get_ifd(0x8825)
    assert g, name + " 没有 GPS"
    one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
    deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
    lat, lon = deg(g[2]), deg(g[4])
    dt = datetime.datetime.strptime(ex.get(0x0132), "%Y:%m:%d %H:%M:%S")
    rows.append((dt, name, lat, lon, city_of(lon)))
    print("  %-14s %s  %.3f,%.3f → %s" % (name, dt, lat, lon, city_of(lon)))

rows.sort()
order = [r[4] for r in rows]
print("  按时间排序:", " → ".join(order))
flag = "flag{%s}" % "_".join(order)
print("  拼出:", flag)
assert flag == EXPECT
print("  PASS route-trace-ctf")
