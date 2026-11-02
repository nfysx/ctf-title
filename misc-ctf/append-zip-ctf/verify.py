# -*- coding: utf-8 -*-
"""自校验：从 PNG 尾部切出 zip，读出 flag。"""
import io, os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{4ppend3d_d4t4_4fter_1end}"
data = open(os.path.join(HERE, "files", "cover.png"), "rb").read()
iend = data.find(b"IEND")
assert iend != -1, "不是 PNG"
off = data.find(b"PK\x03\x04", iend)
assert off != -1, "IEND 之后没有 ZIP —— 题目不对"
print("  PNG 长度: %d, ZIP 偏移: %d, 附加字节: %d" % (iend + 8, off, len(data) - off))
z = zipfile.ZipFile(io.BytesIO(data[off:]))
flag = z.read("flag.txt").decode().strip()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS append-zip-ctf")
