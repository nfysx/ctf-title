# -*- coding: utf-8 -*-
"""自校验：.docx 其实是 ZIP，直接当 zip 打开。"""
import io as _io, os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{r3n4m3_1t_4nd_5e3}"
p = os.path.join(HERE, "files", "作业.docx")
raw = open(p, "rb").read()
print("  前 4 字节:", raw[:4].hex(), "(504b0304 = ZIP)")
assert raw[:4] == b"PK\x03\x04", "不是 zip，构造有误"
flag = zipfile.ZipFile(_io.BytesIO(raw)).read("flag.txt").decode().strip()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS ext-lie-ctf")
