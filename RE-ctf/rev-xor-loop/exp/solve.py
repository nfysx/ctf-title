#!/usr/bin/env python3
"""rev-xor-loop: ENC[i] = flag[i] ^ KEY[i%6] ^ (i & 0xff)。

KEY 是 6 字节常量 'K3y!26'，密文是 28 字节数组，两者都在 .rodata。
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
KEY = b"K3y!26"
LEN = 28

data = open(EXE, "rb").read()
for off in range(len(data) - LEN):
    cand = bytes(data[off + i] ^ KEY[i % 6] ^ i for i in range(LEN))
    if cand.startswith(b"flag{") and cand.endswith(b"}"):
        print("[+] ENC @", hex(off), " key @", hex(data.find(KEY)))
        print(cand.decode())
        break
