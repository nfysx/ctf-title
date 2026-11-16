#!/usr/bin/env python3
# crypto-encode-stack / 出题脚本
# reproduce output.txt: FLAG=<the flag> python gen.py > output.txt
import base64
import codecs
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"]


def main() -> None:
    step1 = FLAG[::-1]                                    # 反序
    step2 = codecs.encode(step1, "rot13")                 # rot13
    step3 = step2.encode().hex()                          # hex
    step4 = base64.b64encode(step3.encode()).decode()     # base64
    print("# pipeline: reverse -> rot13 -> hex -> base64")
    print("data =", step4)


if __name__ == "__main__":
    main()
