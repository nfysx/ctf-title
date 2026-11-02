# -*- coding: utf-8 -*-

"""自校验：读出 EXIF 的 GPS 与拍摄时间，按题解换算 UTC 并拼出 flag。"""
import datetime, os, sys
from PIL import Image
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{chongqing_hongyadong_20241009_1325}"
PREFIX = "chongqing_hongyadong"

ex = Image.open(os.path.join(HERE, "files", "photo.jpg")).getexif()
g = ex.get_ifd(0x8825)
assert g, "EXIF 里没有 GPS —— 题目要求保留元数据"
one = lambda x: (x[0] / x[1]) if isinstance(x, tuple) else float(x)
deg = lambda v: one(v[0]) + one(v[1]) / 60 + one(v[2]) / 3600
lat, lon = deg(g[2]), deg(g[4])
dt_raw = ex.get(0x0132)
print("  GPS: %.6f, %.6f" % (lat, lon))
print("  拍摄时间(本地):", dt_raw)
assert abs(lat - 29.566714) < 0.005 and abs(lon - 106.575697) < 0.005, "坐标不对"
assert dt_raw, "没有拍摄时间"
local = datetime.datetime.strptime(dt_raw, "%Y:%m:%d %H:%M:%S")
utc = local - datetime.timedelta(hours=8)
flag = "flag{%s_%s_%s}" % (PREFIX, local.strftime("%Y%m%d"), utc.strftime("%H%M"))
print("  UTC 时刻:", utc.strftime("%H%M"))
print("  拼出:", flag)
assert flag == EXPECT
print("  PASS geo-hongyadong-ctf（EXIF 已保留，含时间换算）")
