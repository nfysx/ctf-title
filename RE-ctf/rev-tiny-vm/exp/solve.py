#!/usr/bin/env python3
"""rev-tiny-vm: 一段字节码 + 一个极简解释器，校验逻辑全在字节码里。

字节码以 CMP_LEN(LEN) 开头，扫描 0x0c ?? 0x02 即可定位；然后用同一套
操作码语义模拟执行，逐条算出每个位置上期望的字符。
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
LEN = 26

HALT_FAIL, HALT_OK, LD, XOR, ADD, SUB, MUL, ROL, ROR, CMP_BYTE, JMP, JZ, CMP_LEN, NOT = range(14)


def rol8(v, s):
    return ((v << s) | (v >> (8 - s))) & 0xFF


def ror8(v, s):
    return ((v >> s) | (v << (8 - s))) & 0xFF


def emulate(code):
    r, pc, out = 0, 0, bytearray()
    while pc < len(code):
        op = code[pc]
        pc += 1
        if op == HALT_FAIL:
            return None
        if op == HALT_OK:
            return bytes(out)
        if op == LD:
            r = code[pc]; pc += 1
        elif op == XOR:
            r ^= code[pc]; pc += 1
        elif op == ADD:
            r = (r + code[pc]) & 0xFF; pc += 1
        elif op == SUB:
            r = (r - code[pc]) & 0xFF; pc += 1
        elif op == MUL:
            r = (r * code[pc]) & 0xFF; pc += 1
        elif op == ROL:
            r = rol8(r, code[pc] & 7); pc += 1
        elif op == ROR:
            r = ror8(r, code[pc] & 7); pc += 1
        elif op == CMP_BYTE:
            out.append(r)
        elif op == JMP:
            d = code[pc]; pc += 1
            pc += d if d < 0x80 else d - 256
        elif op == JZ:
            d = code[pc]; pc += 1
            if r == 0:
                pc += d if d < 0x80 else d - 256
        elif op == CMP_LEN:
            pc += 1
        elif op == NOT:
            r = (~r) & 0xFF
        else:
            return None
    return None


data = open(EXE, "rb").read()
for off in range(len(data) - 4):
    if data[off] == CMP_LEN and data[off + 1] == LEN and data[off + 2] == LD:
        flag = emulate(data[off:off + 400])
        if flag and flag.startswith(b"flag{"):
            print("[+] bytecode @", hex(off))
            print(flag.decode())
            break
