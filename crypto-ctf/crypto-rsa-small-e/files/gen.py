#!/usr/bin/env python3
# crypto-rsa-small-e / 出题脚本
# reproduce output.txt: FLAG=<the flag> python gen.py > output.txt
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"].encode()

N = 137501818638697334315872743810235481644063740776962046117735817908208739816103701278353173797438899826658912870912979246040719821596277825183493696722407400007619028810644529946686583424615743873465549989977841816810547434201548221211111415754960757542486322385853412402237184634076943052800788704868061519223
E = 3


def main() -> None:
    m = int.from_bytes(FLAG, "big")
    assert m ** 3 < N, "flag too long for this n"
    c = pow(m, E, N)
    print("n =", N)
    print("e =", E)
    print("c =", c)


if __name__ == "__main__":
    main()
