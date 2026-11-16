#!/usr/bin/env python3
"""rev-math-check: 四轮变换串起来，倒着做一遍就还原。

  1) a[i] = in[i] ^ 0xa7
  2) b[PERM[i]] = a[i]          置换
  3) c[i] = (b[i] * 0x6b + i*7 + 0x11) & ff
  4) T[i] = rol8(c[i], 3)
逆：反 rol -> 乘 0x6b 的模逆 -> 反置换 -> 反 xor。
PERM 是 0..35 的排列，用它自己在文件里定位；T 是另一段 36 字节。
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
LEN = 36
MUL, ADD, ROL = 0x6B, 0x11, 3
INV = pow(MUL, -1, 256)


def ror8(v, s):
    return ((v >> s) | (v << (8 - s))) & 0xFF


def decode(perm, T):
    c = [ror8(t, ROL) for t in T]
    b = [((c[i] - i * 7 - ADD) * INV) & 0xFF for i in range(LEN)]
    return bytes(b[perm[i]] ^ 0xA7 for i in range(LEN))


data = open(EXE, "rb").read()
flag = None
for off in range(len(data) - LEN):
    if sorted(data[off:off + LEN]) != list(range(LEN)):
        continue
    perm = list(data[off:off + LEN])
    for t_off in range(len(data) - LEN):
        if abs(t_off - off) < LEN:
            continue
        out = decode(perm, data[t_off:t_off + LEN])
        if out.startswith(b"flag{") and out.endswith(b"}"):
            print("[+] PERM @ 0x%x  T @ 0x%x" % (off, t_off))
            flag = out.decode()
            break
    if flag:
        break

print(flag)
