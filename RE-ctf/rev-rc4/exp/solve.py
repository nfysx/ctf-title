#!/usr/bin/env python3
"""rev-rc4: 输入先过 RC4(key)，再和 26 字节密文逐字节比。

key（8 字节）与密文都在 .rodata，位置不固定。
做法：先算每个候选 key 的第一个密钥流字节，用它去匹配
  ks0 = 密文[0] ^ 'f'
只对命中的候选做完整解密。
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
LEN = 26


def ksa_first_byte(key):
    s = list(range(256))
    j = 0
    for i in range(256):
        j = (j + s[i] + key[i % len(key)]) & 0xFF
        s[i], s[j] = s[j], s[i]
    i, j = 1, s[1]
    s[1], s[j] = s[j], s[1]
    return s[(s[1] + s[j]) & 0xFF]


def rc4(key, data):
    s = list(range(256))
    j = 0
    for i in range(256):
        j = (j + s[i] + key[i % len(key)]) & 0xFF
        s[i], s[j] = s[j], s[i]
    out = bytearray()
    i = j = 0
    for b in data:
        i = (i + 1) & 0xFF
        j = (j + s[i]) & 0xFF
        s[i], s[j] = s[j], s[i]
        out.append(b ^ s[(s[i] + s[j]) & 0xFF])
    return bytes(out)


data = open(EXE, "rb").read()
table = {}
for k in range(len(data) - 8):
    table.setdefault(ksa_first_byte(data[k:k + 8]), []).append(k)

for c in range(len(data) - LEN):
    for k in table.get(data[c] ^ ord("f"), []):
        pt = rc4(data[k:k + 8], data[c:c + LEN])
        if pt.startswith(b"flag{") and pt.endswith(b"}"):
            print("[+] KEY @ 0x%x  ENC @ 0x%x" % (k, c))
            print(pt.decode())
            raise SystemExit
