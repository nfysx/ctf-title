# -*- coding: utf-8 -*-
"""自校验：按 README 的三步跑一遍——认类型、捞可打印串、过滤。"""
import os, re, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
# 兼容放在 exp/ 下的情况：本层没有 files/ 就退回上一层
if not os.path.isdir(os.path.join(HERE, "files")):
    HERE = os.path.dirname(HERE)
EXPECT = "flag{strings_shows_everything}"
p = os.path.join(HERE, "files", "notice.bin")
raw = open(p, "rb").read()

# 1) 容器头：魔数 NOTI + 小端 uint32 正文长度
magic = raw[:4]
(blen,) = struct.unpack_from("<I", raw, 4)
assert magic == b"NOTI", "魔数不对，附件构造有误"
assert blen == len(raw) - 8, "长度字段与正文实际长度不一致"
body = raw[8:8 + blen]
print("  文件 %d 字节｜魔数 %r｜正文 %d 字节" % (len(raw), magic, blen))

# 2) 模拟 strings：连续 >=4 个可打印 ASCII 字节算一条
def strings(data, n=4):
    out, run = [], b""
    for b in data + b"\x00":
        if 0x20 <= b < 0x7f:
            run += bytes([b])
        else:
            if len(run) >= n:
                out.append(run)
            run = b""
    return out

hits = strings(body)
print("  strings 捞出 %d 条：" % len(hits))
for h in hits:
    print("    " + h.decode())

# 3) grep flag
flag = [h for h in hits if h.startswith(b"flag{")][-1].decode()
print("  过滤后取到:", flag)
assert flag == EXPECT, "解出的 flag 与预期不符"
print("  PASS misc-hello-strings")
