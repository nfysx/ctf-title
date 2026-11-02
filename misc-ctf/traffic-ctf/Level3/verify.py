# -*- coding: utf-8 -*-
"""自校验：取 .t.itns.local 下的编号子域，按序号拼 base32 再解码。"""
import base64, os, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{dn5_1s_4_5n34ky_ch4nn3l}"

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
names = []
for fr in frames_of(p):
    u = udp_of(fr)
    if not u:
        continue
    sport, dport, payload = u
    if dport == 53:
        names.append(dns_qname(payload))
print("  共 %d 条 DNS 查询" % len(names))
for n in names[:4]:
    print("    " + n)
pat = re.compile(r"^(\d+)\.([A-Z2-7]+)\.t\.itns\.local$")
hits = []
for n in names:
    m = pat.match(n)
    if m:
        hits.append((int(m.group(1)), m.group(2)))
hits.sort()
print("  命中隧道分片: %d 片" % len(hits))
b32 = "".join(h[1] for h in hits)
b32 += "=" * (-len(b32) % 8)
flag = base64.b32decode(b32).decode()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS traffic-dns-ctf")
