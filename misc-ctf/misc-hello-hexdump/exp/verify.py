# -*- coding: utf-8 -*-
"""自校验：按 README 的字节视角把被 0x00 分隔的字符拼回来。"""
import os, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
# 兼容放在 exp/ 下的情况：本层没有 files/ 就退回上一层
if not os.path.isdir(os.path.join(HERE, "files")):
    HERE = os.path.dirname(HERE)
EXPECT = "flag{hex_is_just_another_view}"
p = os.path.join(HERE, "files", "blob.bin")
raw = open(p, "rb").read()

# 1) 12 字节头
magic = raw[0:2]
ver, keep = raw[2], raw[3]
(plen,) = struct.unpack_from("<I", raw, 4)
(chunk,) = struct.unpack_from("<I", raw, 8)
assert magic == b"\xa5\x5a" and ver == 1, "魔数/版本不对，附件构造有误"
assert chunk == 2, "分组大小应为 2"
payload = raw[12:12 + plen]
print("  文件 %d 字节｜魔数 %s｜版本 %d｜正文 %d 字节｜每组 %d 字节"
      % (len(raw), magic.hex(" "), ver, plen, chunk))

# 2) strings（阈值 4）应当抓不到；阈值 2 能逐组吐出来
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

assert strings(raw) == [], "strings 默认阈值本应抓不到东西"
groups = strings(payload, 2)
print("  strings -n 2 得到 %d 组：" % len(groups))
for g in groups:
    print("    " + g.decode())

# 3) 拼回来：每 3 字节取前 2 字节，跳过 00
parts = []
for i in range(0, plen, chunk + 1):
    seg = payload[i:i + chunk + 1]
    assert seg[chunk] == 0, "第 %d 组后面的分隔符不是 00" % (i // (chunk + 1))
    parts.append(seg[:chunk])
flag = b"".join(parts).decode()
print("  拼接结果:", flag)
assert flag == EXPECT, "解出的 flag 与预期不符"
print("  PASS misc-hello-hexdump")
