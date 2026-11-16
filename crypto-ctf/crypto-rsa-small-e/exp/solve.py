#!/usr/bin/env python3
# crypto-rsa-small-e / 解答脚本
import os

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")


def iroot(x: int, k: int) -> int:
    """整数 k 次根，向下取整。"""
    lo, hi = 0, 1 << ((x.bit_length() + k - 1) // k + 1)
    while lo < hi:
        mid = (lo + hi + 1) // 2
        if mid ** k <= x:
            lo = mid
        else:
            hi = mid - 1
    return lo


def main() -> None:
    vals = {}
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if "=" in line:
                k, v = line.split("=", 1)
                vals[k.strip()] = int(v.strip())

    m = iroot(vals["c"], vals["e"])            # m^3 < n，c 就是完全立方数
    print(m.to_bytes((m.bit_length() + 7) // 8, "big").decode())


if __name__ == "__main__":
    main()
