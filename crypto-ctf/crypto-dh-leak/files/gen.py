#!/usr/bin/env python3
# crypto-dh-leak / 出题脚本
# reproduce output.txt: FLAG=<the flag> python gen.py > output.txt
import os

if not os.environ.get("FLAG"):
    raise SystemExit("set FLAG env first, e.g. FLAG=<the flag> python gen.py")
FLAG = os.environ["FLAG"].encode()

P = 80018670274883150409862411783418126359407430999869082268806452448243214366441
G = 5
ALICE_PRIVATE = 20521497233047711021177104428502843167809486412815022554383655353799053978850
BOB_PRIVATE = 14920202176872147577582805973723132573192169997039402662387549960665990753322


def main() -> None:
    alice_public = pow(G, ALICE_PRIVATE, P)
    bob_public = pow(G, BOB_PRIVATE, P)
    shared = pow(bob_public, ALICE_PRIVATE, P)
    key = shared % 256
    ciphertext = [b ^ key for b in FLAG]

    print(f"p = {P}")
    print(f"g = {G}")
    print(f"alice_public = {alice_public}")
    print(f"bob_public = {bob_public}")
    print(f"alice_private = {ALICE_PRIVATE}")
    print(f"ciphertext = {ciphertext}")


if __name__ == "__main__":
    main()
