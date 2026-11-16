#!/usr/bin/env python3
# crypto-rsa-wiener / 出题脚本
# 私钥参数不随附件发布，生成时由环境变量传入：
#   RSA_P=... RSA_Q=... RSA_D=... FLAG=<the flag> python gen.py
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"].encode()
P = int(os.environ["RSA_P"])
Q = int(os.environ["RSA_Q"])
D = int(os.environ["RSA_D"])

N = P * Q
PHI = (P - 1) * (Q - 1)
E = pow(D, -1, PHI)
HERE = os.path.dirname(os.path.abspath(__file__))


def main() -> None:
    m = int.from_bytes(FLAG, "big")
    c = pow(m, E, N)
    with open(os.path.join(HERE, "output.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("# RSA, d 比 n^0.25/3 小\n")
        fh.write("n = " + str(N) + "\n")
        fh.write("e = " + str(E) + "\n")
        fh.write("c = " + str(c) + "\n")


if __name__ == "__main__":
    main()
