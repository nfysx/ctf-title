#!/usr/bin/env python3
# crypto-classic / 解答脚本
import os

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")

ALPHA = "abcdefghijklmnopqrstuvwxyz{}_"
SHIFT = 5
REV = {".-": "A", "-...": "B"}


def main() -> None:
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if line.startswith("ciphertext"):
                morse = line.split("=", 1)[1].strip()

    bits = "".join(REV[tok] for tok in morse.split("/"))          # 莫尔斯 -> A/B
    assert len(bits) % 5 == 0
    shifted = "".join(ALPHA[int(bits[i:i + 5].replace("A", "0").replace("B", "1"), 2)]
                      for i in range(0, len(bits), 5))            # 培根 -> 凯撒后的密文
    flag = "".join(ALPHA[(ALPHA.index(ch) - SHIFT) % len(ALPHA)] for ch in shifted)
    print(flag)


if __name__ == "__main__":
    main()
