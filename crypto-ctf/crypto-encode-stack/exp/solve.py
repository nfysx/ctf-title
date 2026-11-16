#!/usr/bin/env python3
# crypto-encode-stack / 解答脚本
import base64
import codecs
import os

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")


def main() -> None:
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if line.startswith("data"):
                blob = line.split("=", 1)[1].strip()

    step3 = base64.b64decode(blob).decode()   # base64
    step2 = bytes.fromhex(step3).decode()     # hex
    step1 = codecs.decode(step2, "rot13")     # rot13
    print(step1[::-1])                        # 反序


if __name__ == "__main__":
    main()
