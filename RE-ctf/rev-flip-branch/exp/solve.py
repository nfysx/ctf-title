#!/usr/bin/env python3
"""rev-flip-branch: 校验分支写反了（strcmp 相等反而走 Wrong）。

A) 静态：ENC[i] = flag[i] ^ 0x5a，XOR 回去就是 flag；
B) 运行：把 0x1193 处的 je(74) 改成 jne(75)，再喂 flag 就打印 Good!。
   （.text 的 VMA 与文件偏移都是 0x1100 起，所以 VA 0x1193 == 文件偏移 0x1193）
"""
import os
import shutil
import subprocess
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
PATCH_OFF = 0x1193
data = open(EXE, "rb").read()

# ---------- A. 从常量反推 ----------
flag = None
for off in range(len(data) - 29):
    dec = bytes(b ^ 0x5A for b in data[off:off + 29])
    if dec.startswith(b"flag{") and dec.endswith(b"}"):
        flag = dec.decode()
        print("[+] ENC @ 0x%x -> %s" % (off, flag))
        break

# ---------- B. patch 分支后运行 ----------
print("[+] patch @ 0x%x: %02x -> 75 (je -> jne)" % (PATCH_OFF, data[PATCH_OFF]))
assert data[PATCH_OFF] == 0x74, "偏移不对，先 objdump 定位 je"

tmp = tempfile.mkdtemp()
dst = os.path.join(tmp, "patched")
patched = bytearray(data)
patched[PATCH_OFF] = 0x75
open(dst, "wb").write(patched)
os.chmod(dst, 0o755)
p = subprocess.run([dst], input=flag.encode(), capture_output=True)
print("[+] patched run:", p.returncode, p.stdout.decode(errors="replace").strip())
shutil.rmtree(tmp, ignore_errors=True)

print(flag)
