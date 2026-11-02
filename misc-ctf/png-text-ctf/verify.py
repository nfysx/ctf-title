# -*- coding: utf-8 -*-
"""自校验：读 PNG 的 tEXt 块，取出 base64 并解码。"""
import base64, os, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{t3xt_chunk_1s_4_th1ng}"
raw = open(os.path.join(HERE, "files", "photo.png"), "rb").read()
assert raw[:8] == b"\x89PNG\r\n\x1a\n", "不是 PNG"
off, texts = 8, {}
while off < len(raw):
    ln = struct.unpack_from(">I", raw, off)[0]
    typ = raw[off + 4:off + 8]
    data = raw[off + 8:off + 8 + ln]
    if typ == b"tEXt":
        k, _, v = data.partition(b"\x00")
        texts[k.decode("latin-1")] = v.decode("latin-1")
    off += 12 + ln
    if typ == b"IEND":
        break
print("  tEXt 块:", {k: (v[:24] + "..." if len(v) > 24 else v) for k, v in texts.items()})
assert "Comment" in texts, "没有 Comment 文本块"
flag = base64.b64decode(texts["Comment"]).decode()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS png-text-ctf")
