#!/usr/bin/env python3
# crypto-aes-ecb-reorder / 远端 exp：走 TCP，只用服务端当加密 oracle 做块重排
# usage: python exp.py [host] [port]     默认 127.0.0.1:9201
import re
import socket
import sys

HOST = sys.argv[1] if len(sys.argv) > 2 else "127.0.0.1"
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 9201

PROMPT = b"cmd> "


class Conn:
    def __init__(self) -> None:
        self.s = socket.create_connection((HOST, PORT), timeout=10)
        self.buf = b""

    def recv_until(self, marker: bytes) -> bytes:
        while marker not in self.buf:
            chunk = self.s.recv(4096)
            if not chunk:
                break
            self.buf += chunk
        idx = self.buf.find(marker)
        if idx < 0:
            out, self.buf = self.buf, b""
            return out
        out, self.buf = self.buf[:idx], self.buf[idx + len(marker):]
        return out

    def send_line(self, data: bytes) -> None:
        self.s.sendall(data + b"\n")

    def encrypt(self, name: bytes) -> bytes:
        self.send_line(b"1")
        self.recv_until(b"name(hex)> ")
        self.send_line(name.hex().encode())
        out = self.recv_until(PROMPT)
        return bytes.fromhex(re.search(rb"ct = ([0-9a-f]+)", out).group(1).decode())

    def submit(self, token: bytes) -> bytes:
        self.send_line(b"2")
        self.recv_until(b"token(hex)> ")
        self.send_line(token.hex().encode())
        return self.recv_until(PROMPT)


def main() -> None:
    c = Conn()

    # 第 1 步：name 填 10 个 A，让 "name=" + name + "&" 正好占满 16 字节
    ct_a = c.encrypt(b"A" * 10)
    block0 = ct_a[0:16]

    # 第 2 步：name 填 11 个 C，让第 1 块正好是 "admin=1" + 0x09 * 9
    ct_b = c.encrypt(b"C" * 11 + b"admin=1" + b"\x09" * 9)
    block1 = ct_b[16:32]

    print("name=AAAAAAAAAA      ->", ct_a.hex())
    print("  block0 (块内明文 name=AAAAAAAAAA&):", block0.hex())
    print("name=CCCCCCCCCCC+admin=1+0x09*9 ->", ct_b.hex())
    print("  block1 (块内明文 admin=1+0x09*9):", block1.hex())

    crafted = block0 + block1
    print("crafted token        ->", crafted.hex())
    out = c.submit(crafted)
    print("server:", out.decode(errors="replace").strip())

    m = re.search(r"flag\{[^}]*\}", out.decode(errors="replace"))
    if m:
        print("flag:", m.group(0))
    else:
        raise SystemExit("attack failed")


if __name__ == "__main__":
    main()
