# -*- coding: utf-8 -*-
"""自校验：解析 pcap，从 HTTP 请求行里取出 base64 并解码。"""
import base64, os, re, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{pcap_n3v3r_l13s}"
data = open(os.path.join(HERE, "files", "capture.pcap"), "rb").read()
magic = struct.unpack_from("<I", data, 0)[0]
assert magic in (0xA1B2C3D4, 0xD4C3B2A1), "不是 pcap"
off, n, payloads = 24, 0, []
while off + 16 <= len(data):
    _, _, cap, orig = struct.unpack_from("<IIII", data, off)
    off += 16
    payloads.append(data[off:off + cap])
    off += cap
    n += 1
text = b"".join(payloads).decode("latin-1")
m = re.search(r"GET /api/log\?q=([A-Za-z0-9+/=]+)", text)
assert m, "没找到关键请求"
flag = base64.b64decode(m.group(1)).decode()
print("  共解析 %d 个包" % n)
print("  请求行:", text[m.start():m.start() + 46].replace("\r\n", "\\r\\n"))
print("  解出:", flag)
assert flag == EXPECT
print("  PASS traffic-ctf")
