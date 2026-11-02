# -*- coding: utf-8 -*-
"""自校验：重排 TCP 流 → 按 ITNS 帧解析 → XOR 0x5A → 拼 flag。"""
import os, re, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{r3v3r53_th3_fr4m3}"

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
    if sport == 9001 and payload:
        segs[seq] = payload
s = b"".join(segs[k] for k in sorted(segs))
print("  重组 %d 字节" % len(s))
i, out = 0, b""
while i + 11 <= len(s):
    if s[i:i + 4] != b"ITNS":
        i += 1
        continue
    typ = s[i + 4]
    seqn, plen = struct.unpack_from(">HH", s, i + 5)
    body = s[i + 9:i + 9 + plen]
    i += 9 + plen + 2
    print("    帧 type=%d seq=%d len=%d" % (typ, seqn, plen))
    out += bytes(b ^ 0x5A for b in body)
print("  还原:", out)
m = re.search(rb"flag\{[^}]+\}", out)
assert m, "帧内容里没有 flag"
flag = m.group().decode()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS traffic-proto-ctf")
