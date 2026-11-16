#!/usr/bin/env python3
# crypto-lcg-recover / 出题脚本
# 参数不随附件发布，生成时由环境变量传入：
#   LCG_M=... LCG_A=... LCG_C=... LCG_S0=... FLAG=<the flag> python gen.py
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"].encode()
M = int(os.environ["LCG_M"])
A = int(os.environ["LCG_A"])
C = int(os.environ["LCG_C"])
S0 = int(os.environ["LCG_S0"])

COUNT = 12
HERE = os.path.dirname(os.path.abspath(__file__))


def main() -> None:
    x = S0
    states = []
    for _ in range(COUNT):
        states.append(x)
        x = (A * x + C) % M

    ks = b""
    while len(ks) < len(FLAG):
        ks += bytes([x & 0xFF])
        x = (A * x + C) % M

    ct = bytes(a ^ b for a, b in zip(FLAG, ks))
    with open(os.path.join(HERE, "output.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("# x[0..11] = 连续 12 个 LCG 输出，x[n+1] = (a*x[n] + c) mod m\n")
        fh.write("# 模数 m、乘数 a、增量 c 均未知；flag 用 x[12] 起的每个状态最低字节做流密钥加密\n")
        fh.write("states = [" + ", ".join(str(s) for s in states) + "]\n")
        fh.write("ciphertext = " + ct.hex() + "\n")


if __name__ == "__main__":
    main()
