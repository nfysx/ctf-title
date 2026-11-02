# -*- coding: utf-8 -*-
"""自校验：先确认「假装加密」，清除标志位后读出 flag。"""
import os, struct, sys, tempfile, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{f4k3_3ncrypt10n_b1t}"
p = os.path.join(HERE, "files", "secret.zip")
raw = open(p, "rb").read()

def clear_bit(data):
    out = bytearray(data)
    for sig, off in ((b"PK\x03\x04", 6), (b"PK\x01\x02", 8)):
        i = out.find(sig)
        while i != -1:
            (fb,) = struct.unpack_from("<H", out, i + off)
            struct.pack_into("<H", out, i + off, fb & ~1)
            if sig == b"PK\x03\x04":
                nlen, xlen = struct.unpack_from("<HH", out, i + 26)
                (clen,) = struct.unpack_from("<I", out, i + 18)
                nxt = i + 30 + nlen + xlen + clen
            else:
                nlen, xlen, clen = struct.unpack_from("<HHH", out, i + 28)
                nxt = i + 46 + nlen + xlen + clen
            i = out.find(sig, nxt)
    return bytes(out)

try:
    zipfile.ZipFile(p).read("flag.txt")
    raise AssertionError("没有加密位，题目不对")
except RuntimeError as e:
    print("  原文件确实「要求密码」:", str(e)[:60])
fixed = clear_bit(raw)
out = os.path.join(tempfile.gettempdir(), "misc_fake_zip_fixed.zip")
open(out, "wb").write(fixed)
flag = zipfile.ZipFile(out).read("flag.txt").decode().strip()
print("  清除标志位后解出:", flag)
assert flag == EXPECT
print("  PASS fake-zip-ctf")
