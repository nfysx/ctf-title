#!/usr/bin/env python3
# crypto-xor-known / 解答脚本
import os

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")


def main() -> None:
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if line.startswith("ciphertext"):
                ct = bytes.fromhex(line.split("=", 1)[1].strip())

    key = ct[0] ^ ord("f")                      # 已知明文首字节反推密钥
    print(bytes(b ^ key for b in ct).decode())


if __name__ == "__main__":
    main()
