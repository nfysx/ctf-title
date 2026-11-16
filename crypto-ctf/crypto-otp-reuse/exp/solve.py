#!/usr/bin/env python3
# crypto-otp-reuse / 解答脚本
import os

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "files", "output.txt")
KNOWN = os.path.join(HERE, "..", "files", "known_message.txt")


def main() -> None:
    vals = {}
    with open(DATA, "r", encoding="utf-8") as fh:
        for line in fh:
            if "=" in line and not line.strip().startswith("#"):
                k, v = line.split("=", 1)
                vals[k.strip()] = bytes.fromhex(v.strip())
    public = open(KNOWN, "rb").read().strip()
    c_public, c_secret = vals["c_public"], vals["c_secret"]

    # 已知明文 + 密文 = 密钥流（偏移 0 起的那一段）
    ks = bytes(a ^ b for a, b in zip(public, c_public))

    # crib: 秘密文件明文以 flag{ 开头，用它把偏移对齐出来
    crib = b"flag{"
    hits = [t for t in range(len(ks) - len(c_secret) + 1)
            if all(c_secret[i] ^ ks[t + i] == crib[i] for i in range(len(crib)))]
    assert len(hits) == 1, hits
    t = hits[0]

    flag = bytes(c_secret[i] ^ ks[t + i] for i in range(len(c_secret)))
    print("offset =", t)
    print(flag.decode())


if __name__ == "__main__":
    main()
