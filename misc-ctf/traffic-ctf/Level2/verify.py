# -*- coding: utf-8 -*-
"""自校验：按 seq 重排服务器载荷 → 解 chunked → 拼出 flag。"""
import os, re, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{chunk3d_4nd_r34553mbl3d}"

import struct

def frames_of(p):
    data = open(p, "rb").read()
    off, out = 24, []
    while off + 16 <= len(data):
        _, _, cap, _ = struct.unpack_from("<IIII", data, off)
        off += 16
        out.append(data[off:off + cap])
        off += cap
    return out

def ip_proto(fr):
    if fr[12:14] != b"\x08\x00":
        return None, None
    ihl = (fr[14] & 0x0F) * 4
    tot = struct.unpack_from(">H", fr, 16)[0]
    return fr[14 + 9], fr[14 + ihl:14 + tot]

def tcp_of(fr):
    proto, ip = ip_proto(fr)
    if proto != 6:
        return None
    sport, dport, seq = struct.unpack_from(">HHI", ip, 0)
    doff = (ip[12] >> 4) * 4
    return sport, dport, seq, ip[doff:]

def udp_of(fr):
    proto, ip = ip_proto(fr)
    if proto != 17:
        return None
    sport, dport, length = struct.unpack_from(">HHH", ip, 0)
    return sport, dport, ip[8:length]

def dns_qname(dns):
    i, labels = 12, []
    while i < len(dns) and dns[i]:
        n = dns[i]
        labels.append(dns[i + 1:i + 1 + n].decode("latin-1"))
        i += 1 + n
    return ".".join(labels)

p = os.path.join(HERE, "files", "capture.pcap")
segs = {}
for fr in frames_of(p):
    t = tcp_of(fr)
    if not t:
        continue
    sport, dport, seq, payload = t
    if sport == 80 and payload:
        segs[seq] = payload
stream = b"".join(segs[k] for k in sorted(segs))
print("  重组后 %d 字节" % len(stream))
head, _, body = stream.partition(b"\r\n\r\n")
print("  响应头:", head.split(b"\r\n")[0].decode("latin-1"))
assert b"chunked" in head.lower(), "不是分块传输"
parts = []
while True:
    line, _, rest = body.partition(b"\r\n")
    n = int((line.strip() or b"0"), 16)
    if n == 0:
        break
    chunk = rest[:n]
    body = rest[n + 2:]
    if not chunk.startswith(b"-- "):        # 丢掉日志噪声块
        parts.append(chunk)
data = b"".join(parts)
print("  有效分块:", len(parts), "块")
m = re.search(rb"flag\{[^}]+\}", data)
assert m, "分块内容里没有 flag"
flag = m.group().decode()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS traffic-chunked-ctf")
