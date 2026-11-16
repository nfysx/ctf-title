#!/usr/bin/env python3
# crypto-rsa-common-modulus / 出题脚本
# reproduce output.txt: FLAG=<the flag> python gen.py > output.txt
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"].encode()

N = 91918120914838391268811113193639385364674662696683988956563166535849021503137991749915655438220016545890844396501177866334881373491327710233104581819118837827017589307954350429288516880674289277634687484572338183976998378029334542137299909487209920251527309112255173407965011799974375235860645417410808399813
E1 = 65537
E2 = 17


def main() -> None:
    m = int.from_bytes(FLAG, "big")
    c1 = pow(m, E1, N)
    c2 = pow(m, E2, N)
    print("# same n, two exponents, same message")
    print("n =", N)
    print("e1 =", E1)
    print("e2 =", E2)
    print("c1 =", c1)
    print("c2 =", c2)


if __name__ == "__main__":
    main()
