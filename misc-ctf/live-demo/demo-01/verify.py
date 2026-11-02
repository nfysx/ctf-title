# -*- coding: utf-8 -*-
import io, os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{3xt3n510n_15_4_l13}"
p = os.path.join(HERE, "files", "作业.jpg")
data = open(p, "rb").read()
assert data[:4] == b"PK\x03\x04", "演示文件不是 zip —— 后缀戏法失效"
flag = zipfile.ZipFile(io.BytesIO(data)).read("flag.txt").decode().strip()
print("  伪装成 jpg 的 zip，解出:", flag)
assert flag == EXPECT
print("  PASS demo-01（改后缀就能解）")
