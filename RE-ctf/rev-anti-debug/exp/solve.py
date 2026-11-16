#!/usr/bin/env python3
"""rev-anti-debug: 程序用 ptrace(PTRACE_TRACEME) 自检，被调试就直接退出。

真校验逐字节可逆：
  v = in[i] ^ K[i%8];  v = rol4(v);  v = (v + i*0x13) & 0xff;  与 T[i] 比
逆：v = (T[i] - i*0x13) & 0xff; v = ror4(v); in[i] = v ^ K[i%8]

不需要调试器，也不需要改二进制：K 与 T 都在文件里，静态就能算出 flag。
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
LEN = 36


def ror4(v):
    return ((v >> 4) | (v << 4)) & 0xFF


data = open(EXE, "rb").read()
flag = None
for t_off in range(len(data) - LEN):
    T = data[t_off:t_off + LEN]
    for k_off in range(max(0, t_off - 128), min(len(data) - 8, t_off + 128)):
        if (ror4(T[0]) ^ data[k_off]) != ord("f"):
            continue
        K = data[k_off:k_off + 8]
        out = bytearray()
        for i in range(LEN):
            out.append(ror4((T[i] - i * 0x13) & 0xFF) ^ K[i % 8])
        if out.startswith(b"flag{") and out.endswith(b"}"):
            print("[+] T @ 0x%x  K @ 0x%x" % (t_off, k_off))
            flag = out.decode()
            break
    if flag:
        break

print(flag)
