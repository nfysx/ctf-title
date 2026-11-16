#!/usr/bin/env python3
# crypto-xor-known / 出题脚本
# reproduce output.txt: FLAG=<the flag> python gen.py > output.txt
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"].encode()
KEY = 0x6B


def main() -> None:
    ct = bytes(b ^ KEY for b in FLAG)
    print("# single byte xor with a one byte key; the plaintext is a flag, so byte 0 is 'f' (0x66)")
    print("ciphertext =", ct.hex())


if __name__ == "__main__":
    main()
