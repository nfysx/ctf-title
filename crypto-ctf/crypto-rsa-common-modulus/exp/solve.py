#!/usr/bin/env python3
# crypto-rsa-common-modulus / 解答脚本
import os

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")


def egcd(a: int, b: int):
    """返回 (g, x, y) 使 a*x + b*y = g = gcd(a, b)。"""
    if b == 0:
        return a, 1, 0
    g, x1, y1 = egcd(b, a % b)
    return g, y1, x1 - (a // b) * y1


def spow(base: int, exp: int, mod: int) -> int:
    if exp < 0:
        base = pow(base, -1, mod)
        exp = -exp
    return pow(base, exp, mod)


def main() -> None:
    vals = {}
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if "=" in line and not line.strip().startswith("#"):
                k, v = line.split("=", 1)
                vals[k.strip()] = int(v.strip())

    n, e1, e2, c1, c2 = vals["n"], vals["e1"], vals["e2"], vals["c1"], vals["c2"]
    g, a, b = egcd(e1, e2)                       # a*e1 + b*e2 = 1
    assert g == 1, "e1, e2 must be coprime"

    m = (spow(c1, a, n) * spow(c2, b, n)) % n
    print(m.to_bytes((m.bit_length() + 7) // 8, "big").decode())


if __name__ == "__main__":
    main()
