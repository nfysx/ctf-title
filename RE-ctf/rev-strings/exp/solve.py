#!/usr/bin/env python3
"""rev-strings: flag 明文存在 ELF 的 .rodata 里，直接搜。"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")

data = open(EXE, "rb").read()
m = re.search(rb"flag\{[a-z0-9_]+\}", data)
print("[+] offset:", hex(m.start()))
print(m.group().decode())
