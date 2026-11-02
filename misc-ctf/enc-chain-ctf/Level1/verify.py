# -*- coding: utf-8 -*-
"""自校验：readme 里那串 base64 直接解。"""
import base64, os, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{b4s364_15_ju57_4_5t3p}"
raw = open(os.path.join(HERE, "files", "readme.txt"), encoding="utf-8-sig").read()
blob = max(re.findall(r"[A-Za-z0-9+/=]{20,}", raw), key=len)
flag = base64.b64decode(blob).decode()
print("  取到 base64:", blob[:32], "...")
print("  解出:", flag)
assert flag == EXPECT
print("  PASS b64-only-ctf")
