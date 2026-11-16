#!/usr/bin/env python3
# crypto-rsa-wiener / 解答脚本
# e/n 的连分数收敛子逐项当作 k/d 试，命中即恢复 d
import os
from math import isqrt

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")


def continued_fraction(num: int, den: int):
    while den:
        a = num // den
        yield a
        num, den = den, num - a * den


def convergents(num: int, den: int):
    h0, h1 = 0, 1          # 分子
    k0, k1 = 1, 0          # 分母
    for a in continued_fraction(num, den):
        h0, h1 = h1, a * h1 + h0
        k0, k1 = k1, a * k1 + k0
        yield h1, k1


def main() -> None:
    vals = {}
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if "=" in line and not line.strip().startswith("#"):
                k, v = line.split("=", 1)
                vals[k.strip()] = int(v.strip())
    n, e, c = vals["n"], vals["e"], vals["c"]

    for k_num, d in convergents(e, n):
        if k_num == 0:
            continue
        if (e * d - 1) % k_num:
            continue
        phi = (e * d - 1) // k_num
        s = n - phi + 1
        disc = s * s - 4 * n
        if disc < 0:
            continue
        r = isqrt(disc)
        if r * r != disc or (s + r) % 2:
            continue
        p, q = (s + r) // 2, (s - r) // 2
        if p * q != n:
            continue
        m = pow(c, d, n)
        print(m.to_bytes((m.bit_length() + 7) // 8, "big").decode())
        return
    raise SystemExit("no convergent hit")


if __name__ == "__main__":
    main()
