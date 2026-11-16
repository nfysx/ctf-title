#!/usr/bin/env python3
# crypto-otp-reuse / 出题脚本
# reproduce files: FLAG=<the flag> python gen.py
import hashlib
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"].encode()

SEED = b"ret2shell-otp-demo-seed-0001"
OFFSET = 61          # 秘密文件的密文从密钥流第 61 字节开始
BLOCKS = 32
HERE = os.path.dirname(os.path.abspath(__file__))

PUBLIC = (
    b"The monthly ops notice is public and archived. All hands meeting moved to "
    b"Thursday 15:00 in the main hall, remote link stays the same as last time."
)


def keystream(n: int) -> bytes:
    out = b""
    for i in range(BLOCKS):
        out += hashlib.sha256(SEED + i.to_bytes(4, "big")).digest()
    return out[:n]


def main() -> None:
    ks = keystream(256)
    c_public = bytes(a ^ b for a, b in zip(PUBLIC, ks))
    c_flag = bytes(a ^ b for a, b in zip(FLAG, ks[OFFSET:]))

    with open(os.path.join(HERE, "known_message.txt"), "wb") as fh:
        fh.write(PUBLIC + b"\n")

    with open(os.path.join(HERE, "output.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("# 同一个一次性密码本加密了两份文件：公开通知（known_message.txt，密钥流偏移 0）\n")
        fh.write("# 和含 flag 的秘密文件（起始偏移未知，不在偏移 0）\n")
        fh.write("c_public = " + c_public.hex() + "\n")
        fh.write("c_secret = " + c_flag.hex() + "\n")


if __name__ == "__main__":
    main()
