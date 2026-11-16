#!/usr/bin/env python3
"""rev-smc: smcx（校验函数）与 smcd（异或密钥 TK）在磁盘上都是 XOR 密文，
main() 先用 8 字节密钥 SMK 就地解开（mprotect 改成 RWX）再调用。

静态还原（不依赖反汇编库）：
  1) 解析 ELF 节表，取出 smcx / smcd 的原始字节；
  2) 暴力试 SMK（文件里所有 8 字节窗口），用它解 smcd 得到 TK；
  3) 明文固定以 "flag{" 开头，于是 ENC[0..4] = "flag{" ^ TK[0..4]；
     到文件里搜这 5 个字节，命中处就是目标串 ENC；
  4) ENC[i] ^ TK[i & 7] 即 flag。
"""
import os
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
PREFIX = b"flag{"


def elf_sections(d):
    e_shoff = struct.unpack_from("<Q", d, 0x28)[0]
    e_shentsize = struct.unpack_from("<H", d, 0x3A)[0]
    e_shnum = struct.unpack_from("<H", d, 0x3C)[0]
    e_shstrndx = struct.unpack_from("<H", d, 0x3E)[0]
    stroff = struct.unpack_from("<Q", d, e_shoff + e_shstrndx * e_shentsize + 0x18)[0]
    out = {}
    for i in range(e_shnum):
        o = e_shoff + i * e_shentsize
        name_off = struct.unpack_from("<I", d, o)[0]
        end = d.index(b"\x00", stroff + name_off)
        name = d[stroff + name_off:end].decode()
        sh_flags, sh_addr, sh_off, sh_size = struct.unpack_from("<QQQQ", d, o + 8)
        out[name] = dict(off=sh_off, size=sh_size, addr=sh_addr, flags=sh_flags)
    return out


data = open(EXE, "rb").read()
secs = elf_sections(data)
smcx = secs["smcx"]
smcd = secs["smcd"]
print("[+] smcx off=0x%x size=%d   smcd off=0x%x size=%d"
      % (smcx["off"], smcx["size"], smcd["off"], smcd["size"]))
blob = data[smcd["off"]:smcd["off"] + smcd["size"]]

for k in range(len(data) - 8):
    key = data[k:k + 8]
    tk = bytes(b ^ key[i & 7] for i, b in enumerate(blob))
    want = bytes(c ^ tk[i & 7] for i, c in enumerate(PREFIX))
    pos = data.find(want)
    if pos < 0:
        continue
    dec = bytes(data[pos + i] ^ tk[i & 7] for i in range(64))
    if not dec.startswith(PREFIX):
        continue
    end = dec.find(b"}")
    if end < 0:
        continue
    flag = dec[:end + 1].decode()
    if flag.isprintable():
        print("[+] SMK @ 0x%x  TK=%s  ENC @ 0x%x" % (k, tk.hex(), pos))
        print(flag)
        break
