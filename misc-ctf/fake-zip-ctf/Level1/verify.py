# -*- coding: utf-8 -*-
"""自校验：flag 在 ZIP 的注释字段（EOF 之后）里。"""
import os, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{c0mm3n7_15_4_f13ld}"
z = zipfile.ZipFile(os.path.join(HERE, "files", "handin.zip"))
comment = z.comment.decode("utf-8")
names = z.namelist()
print("  包内文件:", names)
for n in names:
    assert EXPECT.encode() not in z.read(n), "flag 不该在文件内容里"
print("  注释:", comment)
assert comment == EXPECT
print("  PASS zip-comment-ctf")
