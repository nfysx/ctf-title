# -*- coding: utf-8 -*-
"""自校验：before 的 APP1 段里有 ZIP；after 抹掉后就没了。"""
import io, os, struct, sys, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{3r4s3_3xif_4ft3r_dump}"
before = open(os.path.join(HERE, "files", "before.jpg"), "rb").read()
after = open(os.path.join(HERE, "files", "after.jpg"), "rb").read()

def app1_span(raw):
    i = raw.find(b"\xff\xe1")
    assert i > 0, "没有 APP1"
    ln = struct.unpack_from(">H", raw, i + 2)[0]
    return i, i + 2 + ln

b0, b1 = app1_span(before)
blk = before[b0:b1]
print("  before: %d 字节, APP1 段 [%d, %d) 共 %d 字节" % (len(before), b0, b1, b1 - b0))
print("  after : %d 字节, 还含 Exif 标记: %s" % (len(after), b"Exif\x00\x00" in after))
assert b"Exif\x00\x00" in blk, "before 的 APP1 不是 EXIF"
assert b"PK\x03\x04" not in after, "after 应当已经把 EXIF 抹干净"
off = blk.find(b"PK\x03\x04")
assert off > 0, "before 的 EXIF 段里没有 ZIP —— 题目不对"
z = zipfile.ZipFile(io.BytesIO(blk[off:]))
flag = z.read("flag.txt").decode().strip()
print("  从 APP1 尾部取出 ZIP，读 flag:", flag)
assert flag == EXPECT
assert len(before) > len(after), "抹除后应当更小"
print("  PASS exif-erase-ctf")
