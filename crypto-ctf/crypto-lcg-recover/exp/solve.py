#!/usr/bin/env python3
# crypto-lcg-recover / 解答脚本
# 1) 用相邻差分的二次关系 gcd 出模数 m；2) 解出 a、c；3) 往后推状态生成密钥流
import os
from math import gcd

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")


def main() -> None:
    vals = {}
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if "=" in line and not line.strip().startswith("#"):
                k, v = line.split("=", 1)
                vals[k.strip()] = v.strip()
    states = [int(x) for x in vals["states"].strip("[]").split(",")]
    ct = bytes.fromhex(vals["ciphertext"])

    diff = [states[i + 1] - states[i] for i in range(len(states) - 1)]
    # 每个 t_i = d[i+1]*d[i-1] - d[i]^2 都是 m 的倍数，取 gcd 还原 m
    m = 0
    for i in range(1, len(diff) - 1):
        m = gcd(m, abs(diff[i + 1] * diff[i - 1] - diff[i] * diff[i]))
    assert m > max(states), "modulus recovery failed"
    print("m =", m)

    a = (diff[1] * pow(diff[0], -1, m)) % m
    c = (states[1] - a * states[0]) % m
    assert all((a * states[i] + c) % m == states[i + 1] for i in range(len(states) - 1))
    print("a =", a)
    print("c =", c)

    x = states[-1]
    ks = b""
    while len(ks) < len(ct):
        x = (a * x + c) % m
        ks += bytes([x & 0xFF])
    print("flag =", bytes(p ^ q for p, q in zip(ct, ks)).decode())


if __name__ == "__main__":
    main()
