# -*- coding: utf-8 -*-
"""自校验：型号 + 坐标 + 时间，三件套拼出 flag。"""
import datetime, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{beijing_xiaomi_0926}"

ex = Image.open(os.path.join(HERE, "files", "photo.jpg")).getexif()
g = ex.get_ifd(0x8825)
assert g, "没有 GPS"
one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
lat, lon = deg(g[2]), deg(g[4])
model = ex.get(0x0110) or ""
dt_raw = ex.get(0x0132)
print("  Model:", model)
print("  GPS: %.5f, %.5f" % (lat, lon))
print("  拍摄时间(本地):", dt_raw)
assert abs(lat - 39.9163) < 0.01 and abs(lon - 116.3972) < 0.01, "坐标不在故宫附近"
assert "xiaomi" in model.lower(), "型号不是小米"
local = datetime.datetime.strptime(dt_raw, "%Y:%m:%d %H:%M:%S")
utc = (local - datetime.timedelta(hours=8)).strftime("%H%M")
flag = "flag{%s_%s_%s}" % ("beijing", "xiaomi", utc)
print("  UTC 时刻:", utc)
print("  拼出:", flag)
assert flag == EXPECT
print("  PASS exif-trio-ctf")
