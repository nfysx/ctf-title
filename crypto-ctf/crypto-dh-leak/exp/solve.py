#!/usr/bin/env python3
# crypto-dh-leak / 解答脚本
import os

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")


def main() -> None:
    vals = {}
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if "=" in line:
                k, v = line.split("=", 1)
                vals[k.strip()] = v.strip()

    p = int(vals["p"])
    bob_public = int(vals["bob_public"])
    alice_private = int(vals["alice_private"])            # 泄露的私钥
    ciphertext = [int(x) for x in vals["ciphertext"].strip("[]").split(",")]

    shared = pow(bob_public, alice_private, p)            # 有私钥即可算共享秘密
    key = shared % 256
    print(bytes(c ^ key for c in ciphertext).decode())


if __name__ == "__main__":
    main()
