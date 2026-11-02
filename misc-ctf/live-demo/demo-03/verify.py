# -*- coding: utf-8 -*-
import io, os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{dr4g_1t_1nt0_7z1p}"
data = open(os.path.join(HERE, "files", "风景.png"), "rb").read()
iend = data.find(b"IEND")
off = data.find(b"PK\x03\x04", iend)
assert iend > 0 and off > 0, "IEND 之后没有 ZIP"
print("  PNG 结束于 %d，ZIP 从 %d 开始（附加 %d 字节）" % (iend + 8, off, len(data) - off))
flag = zipfile.ZipFile(io.BytesIO(data[off:])).read("flag.txt").decode().strip()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS demo-03（7-Zip 可直接打开）")
