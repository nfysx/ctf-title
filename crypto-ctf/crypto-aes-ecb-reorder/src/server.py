#!/usr/bin/env python3
"""ECB profile service.

pt = b"name=" + <name> + b"&admin=0"，PKCS7 填充后整块 AES-128-ECB 加密。
明文以 admin=1 结尾时返回 flag。密钥只存在于镜像里的 /app/key。
"""
import sys

from Crypto.Cipher import AES
from Crypto.Util.Padding import pad, unpad

KEY = open("/app/key", "rb").read()
BANNER = (
    b"ECB profile service\n"
    b"pt = b'name=' + <your name> + b'&admin=0', AES-128-ECB + PKCS7\n"
    b"1) encrypt name (hex)\n"
    b"2) submit token (hex)\n"
)


def encrypt_name(name: bytes) -> bytes:
    pt = b"name=" + name + b"&admin=0"
    return AES.new(KEY, AES.MODE_ECB).encrypt(pad(pt, 16))


def decrypt_token(token: bytes):
    if len(token) % 16 or not 0 < len(token) <= 512:
        return None
    try:
        return unpad(AES.new(KEY, AES.MODE_ECB).decrypt(token), 16)
    except ValueError:
        return None


def reply(text: bytes) -> None:
    sys.stdout.buffer.write(text)
    sys.stdout.buffer.flush()


def read_hex() -> bytes | None:
    line = sys.stdin.buffer.readline()
    if not line:
        return None
    try:
        return bytes.fromhex(line.decode("ascii", "replace").strip())
    except ValueError:
        return None


def main() -> None:
    reply(BANNER)
    while True:
        reply(b"cmd> ")
        line = sys.stdin.buffer.readline()
        if not line:
            return
        cmd = line.strip()

        if cmd == b"1":
            reply(b"name(hex)> ")
            name = read_hex()
            if name is None:
                reply(b"bad hex\n")
                continue
            if len(name) > 256:
                reply(b"name too long\n")
                continue
            reply(b"ct = " + encrypt_name(name).hex().encode() + b"\n")

        elif cmd == b"2":
            reply(b"token(hex)> ")
            token = read_hex()
            if token is None:
                reply(b"bad hex\n")
                continue
            pt = decrypt_token(token)
            if pt is not None and pt.endswith(b"admin=1"):
                reply(b"result = " + open("/app/flag", "rb").read().strip() + b"\n")
            else:
                reply(b"result = denied\n")

        else:
            reply(b"unknown command\n")


if __name__ == "__main__":
    main()
