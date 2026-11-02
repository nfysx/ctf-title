# -*- coding: utf-8 -*-
"""自校验：readme 里那串十六进制直接转字节。"""
import binascii, os, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{h3x_15_e4sy_t00}"
raw = open(os.path.join(HERE, "files", "readme.txt"), encoding="utf-8-sig").read()
blob = max(re.findall(r"[0-9a-fA-F]{20,}", raw), key=len)
flag = binascii.unhexlify(blob).decode()
print("  取到 hex:", blob[:32], "...")
print("  解出:", flag)
assert flag == EXPECT
print("  PASS hex-only-ctf")
