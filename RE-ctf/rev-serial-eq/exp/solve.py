#!/usr/bin/env python3
"""rev-serial-eq: 每 4 个字符一组，y = M * x (mod 256)，M 是 4x4 常量矩阵。

定位：M 的 16 字节在前，T 在别处。用已知前缀 "flag" 反推
  y0 = M * ("flag") (mod 256)
再到文件里搜这 4 个字节，命中处就是 T 的起点。
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
BLOCKS = 11
PREFIX = b"flag"


def det3(a):
    return (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1])
            - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
            + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))


def det4(m):
    s = 0
    for j in range(4):
        sub = [[m[r][c] for c in range(4) if c != j] for r in range(1, 4)]
        s += (-1) ** j * m[0][j] * det3(sub)
    return s


def matvec(m, x):
    return [sum(m[r][c] * x[c] for c in range(4)) & 0xFF for r in range(4)]


def solve_block(m, y):
    d = det4(m) % 256
    if d % 2 == 0:
        return None
    inv = pow(d, -1, 256)
    cof = [[0] * 4 for _ in range(4)]
    for i in range(4):
        for j in range(4):
            sub = [[m[r][c] for c in range(4) if c != j] for r in range(4) if r != i]
            cof[i][j] = (-1) ** (i + j) * det3(sub)
    adj = [[cof[j][i] % 256 for j in range(4)] for i in range(4)]
    return [sum(adj[i][j] * y[j] for j in range(4)) * inv % 256 for i in range(4)]


data = open(EXE, "rb").read()
flag = None
for off in range(len(data) - 16):
    m = [list(data[off + 4 * r: off + 4 * r + 4]) for r in range(4)]
    if det4(m) % 2 == 0:
        continue
    y0 = bytes(matvec(m, list(PREFIX)))
    pos = data.find(y0)
    if pos < 0 or pos + 4 * BLOCKS > len(data):
        continue
    T = data[pos:pos + 4 * BLOCKS]
    out = []
    for b in range(BLOCKS):
        x = solve_block(m, list(T[4 * b: 4 * b + 4]))
        if x is None:
            out = None
            break
        out += x
    if not out:
        continue
    cand = bytes(out).rstrip(b"\x00")
    if cand.startswith(b"flag{") and cand.endswith(b"}"):
        print("[+] M @ 0x%x  T @ 0x%x" % (off, pos))
        flag = cand.decode()
        break

print(flag)
