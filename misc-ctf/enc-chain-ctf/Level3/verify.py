# -*- coding: utf-8 -*-
"""自校验：按题解的三层解码跑一遍，必须解出 flag。"""
import base64, codecs, os, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{3nc0d1ng_1s_n0t_3ncrypt10n}"
raw = open(os.path.join(HERE, "files", "readme.txt"), encoding="utf-8-sig").read()
blob = max(re.findall(r"[A-Za-z0-9+/=]{40,}", raw), key=len)
inner_hex = base64.b64decode(blob).decode()          # 第 1 层：base64
rot = bytes.fromhex(inner_hex).decode()              # 第 2 层：hex
flag = codecs.decode(rot, "rot13")                   # 第 3 层：rot13
print("  解出:", flag)
assert flag == EXPECT, "解码结果与预期不符"
print("  PASS enc-chain-ctf")
