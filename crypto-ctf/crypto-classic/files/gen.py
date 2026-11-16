#!/usr/bin/env python3
# crypto-classic / 出题脚本
# reproduce output.txt: FLAG=<the flag> python gen.py > output.txt
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"].lower()

ALPHA = "abcdefghijklmnopqrstuvwxyz{}_"   # 29 个符号，5 bit 装得下
SHIFT = 5
MORSE = {"A": ".-", "B": "-..."}


def caesar(text: str, shift: int) -> str:
    return "".join(ALPHA[(ALPHA.index(ch) + shift) % len(ALPHA)] for ch in text)


def bacon(text: str) -> str:
    bits = ""
    for ch in text:
        idx = ALPHA.index(ch)
        bits += format(idx, "05b")
    return bits.replace("0", "A").replace("1", "B")


def to_morse(bits: str) -> str:
    return "/".join(MORSE[b] for b in bits)


def main() -> None:
    step1 = caesar(FLAG, SHIFT)
    step2 = bacon(step1)
    step3 = to_morse(step2)
    print("# pipeline: caesar(+5 over a-z{}_) -> bacon(29 symbols, 5 bit) -> morse(A=.- B=-...) joined by /")
    print("ciphertext =", step3)


if __name__ == "__main__":
    main()
