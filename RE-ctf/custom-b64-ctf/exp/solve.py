#!/usr/bin/env python3
"""从 ELF 里取出自定义字母表和目标密文，反解出 flag。"""
import re

STD = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
DATA = "challenge"

d = open(DATA, "rb").read()

# 1) 64 个基表字符的连续串 = 自定义字母表
m = re.search(rb"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{64}(?![A-Za-z0-9+/])", d)
assert m, "没找到字母表"
tbl = m.group().decode()
span = m.span()

# 2) 另一段形如 Base64 的常量 = 目标密文（排除字母表所在区间）
ct = None
for mm in re.finditer(rb"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{16,}={0,2}(?![A-Za-z0-9+/])", d):
    s, e = mm.span()
    if e <= span[0] or s >= span[1]:
        ct = mm.group().decode()
        break
assert ct, "没找到密文"

print("字母表:", tbl)
print("密文  :", ct)

# 3) 自定义表下标 -> 标准表字符 -> 标准 Base64 解码
std_b64 = "".join("=" if c == "=" else STD[tbl.index(c)] for c in ct)
print("标准表:", std_b64)
print("flag  :", __import__("base64").b64decode(std_b64).decode())
