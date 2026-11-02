# -*- coding: utf-8 -*-
"""生成 misc-ctf 全部题目（附件 + 题解 + checker + verify）。

设计约束
--------
* 只用标准库 + Pillow，不需要 7z / exiftool / tshark / binwalk
* 所有 flag 静态写在附件里（checker 里留了切动态的注释）
* 每题一个 verify.py，本脚本末尾会跑一遍全部 verify 自检
* 内容对得上第三课：编码链 / 文件头 / 附加数据 / 伪加密 / LSB / EXIF / 流量 / 图寻

运行：python make_challenges.py [--check]
"""
import base64
import hashlib
import io
import os
import struct
import subprocess
import sys
import zipfile

from PIL import Image, ImageDraw, ImageFont, PngImagePlugin

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # …\misc-ctf
PY = sys.executable
FONT_CN = r"C:\Windows\Fonts\msyh.ttc"
FONT_MONO = r"C:\Windows\Fonts\consola.ttf"

SEED = 20261027


# ---------------------------------------------------------------------------
# 通用小工具
# ---------------------------------------------------------------------------
def ensure_dir(p):
    os.makedirs(p, exist_ok=True)
    return p


def write_bytes(path, data):
    ensure_dir(os.path.dirname(path))
    with open(path, "wb") as f:
        f.write(data)
    return len(data)


def write_text(path, text, bom=True):
    """txt 附件用 utf-8-sig，保证旧版记事本也不乱码。"""
    ensure_dir(os.path.dirname(path))
    enc = "utf-8-sig" if bom else "utf-8"
    with open(path, "w", encoding=enc, newline="\r\n") as f:
        f.write(text)
    return len(text)


def write_md(path, text):
    ensure_dir(os.path.dirname(path))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def png_bytes(img):
    b = io.BytesIO()
    img.save(b, "PNG", optimize=True)
    return b.getvalue()


def zip_bytes(entries, compress=zipfile.ZIP_DEFLATED):
    b = io.BytesIO()
    with zipfile.ZipFile(b, "w", compress) as z:
        for name, data in entries:
            z.writestr(name, data)
    return b.getvalue()


# --- 伪加密：同时改本地文件头与中央目录的 bit0 --------------------------------
def zip_set_encrypted_bit(data, on=True):
    raw = bytearray(data)
    for sig, off in ((b"PK\x03\x04", 6), (b"PK\x01\x02", 8)):
        i = raw.find(sig)
        while i != -1:
            (fb,) = struct.unpack_from("<H", raw, i + off)
            fb = (fb | 1) if on else (fb & ~1)
            struct.pack_into("<H", raw, i + off, fb)
            if sig == b"PK\x03\x04":
                nlen, xlen = struct.unpack_from("<HH", raw, i + 26)
                (clen,) = struct.unpack_from("<I", raw, i + 18)
                nxt = i + 30 + nlen + xlen + clen
            else:
                nlen, xlen, clen = struct.unpack_from("<HHH", raw, i + 28)
                nxt = i + 46 + nlen + xlen + clen
            i = raw.find(sig, nxt)
    return bytes(raw)


# --- LSB：把消息写进 RGB 最低位，末尾放 0xFF 分隔 -----------------------------
LSB_END = "1111111111111110"          # 0xFF 的二进制 + 1 位


def lsb_embed(img, message):
    img = img.convert("RGB")
    px = img.load()
    bits = "".join(format(b, "08b") for b in message) + LSB_END
    k = 0
    for y in range(img.height):
        for x in range(img.width):
            ch = list(px[x, y])
            for c in range(3):
                if k < len(bits):
                    ch[c] = (ch[c] & 0xFE) | int(bits[k])
                    k += 1
            px[x, y] = tuple(ch)
            if k >= len(bits):
                return img
    raise ValueError("图片太小，放不下消息")


# --- 一个干净的 pcap（Ethernet + IPv4 + TCP/UDP），校验和算对 ------------------
def _cksum(data):
    if len(data) % 2:
        data += b"\x00"
    s = 0
    for i in range(0, len(data), 2):
        s += (data[i] << 8) + data[i + 1]
    while s >> 16:
        s = (s & 0xFFFF) + (s >> 16)
    return (~s) & 0xFFFF


def _ip4(src, dst, proto, payload):
    total = 20 + len(payload)
    hdr = struct.pack("!BBHHHBBH4s4s", 0x45, 0, total, 0x1234, 0x4000, 64, proto, 0, src, dst)
    hdr = hdr[:10] + struct.pack("!H", _cksum(hdr)) + hdr[12:]
    return hdr + payload


def _tcp(sport, dport, seq, ack, flags, payload=b""):
    hdr = struct.pack("!HHIIBBHHH", sport, dport, seq, ack, 5 << 4, flags, 8192, 0, 0)
    pseudo = struct.pack("!4s4sBBH", b"\x0a\x00\x00\x02", b"\x0a\x00\x00\x01", 0, 6, len(hdr) + len(payload))
    ck = _cksum(pseudo + hdr + payload)
    hdr = hdr[:16] + struct.pack("!H", ck) + hdr[18:]
    return hdr + payload


def _udp(sport, dport, payload):
    pseudo = struct.pack("!4s4sBBH", b"\x0a\x00\x00\x02", b"\x0a\x00\x00\x01", 0, 17, 8 + len(payload))
    hdr = struct.pack("!HHHH", sport, dport, 8 + len(payload), 0)
    ck = _cksum(pseudo + hdr + payload) or 0xFFFF
    return struct.pack("!HHHH", sport, dport, 8 + len(payload), ck) + payload


def _eth(payload):
    return b"\xaa\xbb\xcc\x11\x22\x33" + b"\xaa\xbb\xcc\x44\x55\x66" + b"\x08\x00" + payload


def _dns_query(name):
    q = b"".join(bytes([len(p)]) + p.encode() for p in name.split(".")) + b"\x00"
    return (struct.pack("!HHHHHH", 0x1A2B, 0x0100, 1, 0, 0, 0) + q
            + struct.pack("!HH", 1, 1))


def build_pcap(frames):
    out = io.BytesIO()
    out.write(struct.pack("<IHHiIII", 0xA1B2C3D4, 2, 4, 0, 0, 65535, 1))
    ts = 1761600000
    for i, frame in enumerate(frames):
        out.write(struct.pack("<IIII", ts + i, i * 1000, len(frame), len(frame)))
        out.write(frame)
    return out.getvalue()



# ---------------------------------------------------------------------------
# 真实照片工具：全部来自 _assets/photos/（Wikimedia CC 授权）
# ---------------------------------------------------------------------------
HERE = os.path.dirname(os.path.abspath(__file__))
PHOTOS = os.path.join(HERE, "_assets", "photos")


def load_photo(name, width=1280):
    im = Image.open(os.path.join(PHOTOS, name)).convert("RGB")
    if im.width > width:
        im = im.resize((width, int(im.height * width / im.width)), Image.LANCZOS)
    return im


def caption_strip(img, text, sub=None, bar=0.17, fs=0.058):
    """照片底部压一条说明条（把 flag 写在照片上）"""
    im = img.copy()
    d = ImageDraw.Draw(im, "RGBA")
    h = int(im.height * bar)
    top = im.height - h
    d.rectangle([0, top, im.width, im.height], fill=(10, 10, 12, 210))
    d.rectangle([0, top, 9, im.height], fill=(146, 208, 80, 255))
    try:
        f1 = ImageFont.truetype(FONT_MONO, max(16, int(im.height * fs)))
        f2 = ImageFont.truetype(FONT_CN, max(12, int(im.height * fs * 0.50)))
    except Exception:
        f1 = f2 = ImageFont.load_default(size=max(14, int(im.height * fs * 0.6)))
    d.text((int(im.width * 0.028), top + int(h * 0.16)), text, font=f1, fill=(244, 244, 238))
    if sub:
        d.text((int(im.width * 0.028), top + int(h * 0.62)), sub, font=f2, fill=(198, 202, 208))
    return im


def mosaic(img, y0=0.28, y1=0.97, x0=0.05, x1=0.95, cell=26):
    """真照片 + 一块马赛克"""
    im = img.copy()
    d = ImageDraw.Draw(im)
    a, b = int(im.height * y0), int(im.height * y1)
    c, e = int(im.width * x0), int(im.width * x1)
    small = im.crop((c, a, e, b))
    small = small.resize((max(1, small.width // cell), max(1, small.height // cell)), Image.BILINEAR)
    small = small.resize((e - c, b - a), Image.NEAREST)
    im.paste(small, (c, a))
    d.rectangle([c, a, e, b], outline=(230, 230, 230), width=2)
    return im



# --- ZIP 注释（EOCD 尾部） ---------------------------------------------------
def zip_with_comment(entries, comment, compress=zipfile.ZIP_DEFLATED):
    data = bytearray(zip_bytes(entries, compress))
    eocd = data.rfind(b"PK\x05\x06")
    assert eocd > 0
    cb = comment if isinstance(comment, bytes) else comment.encode("utf-8")
    struct.pack_into("<H", data, eocd + 20, len(cb))
    return bytes(data) + cb


# --- 单通道 LSB（只动 R 的最低位） --------------------------------------------
def lsb_embed_r(img, message):
    im = img.convert("RGB").copy()
    px = im.load()
    bits = []
    for ch in message:
        bits.extend((ch >> k) & 1 for k in range(7, -1, -1))
    bits.extend([0] * 8)
    if len(bits) > im.width * im.height:
        raise ValueError("消息太长，单通道装不下")
    i = 0
    for y in range(im.height):
        for x in range(im.width):
            if i >= len(bits):
                return im
            r, g, b = px[x, y]
            px[x, y] = ((r & 0xFE) | bits[i], g, b)
            i += 1
    return im


# --- CRC16-CCITT（私有协议用） ----------------------------------------------
def crc16(data):
    crc = 0xFFFF
    for b in data:
        crc ^= b << 8
        for _ in range(8):
            crc = ((crc << 1) ^ 0x1021) & 0xFFFF if crc & 0x8000 else (crc << 1) & 0xFFFF
    return crc


# ---------------------------------------------------------------------------
# 手搓 JPEG APP1(EXIF)：Pillow 不能写 GPS IFD 和缩略图，这里自己拼 TIFF 结构
# ---------------------------------------------------------------------------
def build_exif_tiff(make, model, desc, gps=None, thumb_jpeg=None, software=b"ITNS Editor 1.0",
                    datetime_orig=None, artist=None):
    """返回 APP1 中 Exif\0\0 之后的 TIFF 字节。

    gps: ((lat_dms, "N"), (lon_dms, "E")) ，dms 是 ((度,分,秒)) 三个整数元组
    thumb_jpeg: 缩略图 JPEG 字节；写进 IFD1 的 0x0201/0x0202
    """
    n_ifd0 = (5 + (1 if gps is not None else 0)
              + (1 if datetime_orig else 0) + (1 if artist else 0))
    n_gps = 4
    n_ifd1 = 3 if thumb_jpeg else 0
    ifd0_off = 8
    ifd0_size = 2 + n_ifd0 * 12 + 4
    gps_off = ifd0_off + ifd0_size
    gps_size = (2 + n_gps * 12 + 4) if gps else 0
    ifd1_off = gps_off + gps_size
    ifd1_size = (2 + n_ifd1 * 12 + 4) if thumb_jpeg else 0
    data_off = ifd1_off + ifd1_size

    blobs = b""

    def put(b):
        nonlocal blobs
        off = data_off + len(blobs)
        blobs += b + (b"\x00" if len(b) % 2 else b"")
        return off

    def ent(tag, typ, cnt, val):
        head = struct.pack("<HHI", tag, typ, cnt)
        if isinstance(val, int):
            return head + struct.pack("<I", val)
        return head + val.ljust(4, b"\x00")

    e = [ent(0x010F, 2, len(make) + 1, put(make + b"\x00")),
         ent(0x0110, 2, len(model) + 1, put(model + b"\x00")),
         ent(0x010E, 2, len(desc) + 1, put(desc + b"\x00")),
         ent(0x0131, 2, len(software) + 1, put(software + b"\x00")),
         ent(0x0112, 3, 1, 1)]
    if gps is not None:
        e.append(ent(0x8825, 4, 1, gps_off))
    if artist:
        a = artist.encode("ascii")
        e.append(ent(0x013B, 2, len(a) + 1, put(a + b"\x00")))
    if datetime_orig:                                   # 0x0132 DateTime：YYYY:MM:DD HH:MM:SS
        dt = datetime_orig.encode("ascii")
        e.append(ent(0x0132, 2, len(dt) + 1, put(dt + b"\x00")))
    e.sort(key=lambda x: struct.unpack_from("<H", x, 0)[0])
    ifd0 = struct.pack("<H", len(e)) + b"".join(e) + struct.pack("<I", ifd1_off if thumb_jpeg else 0)

    gpsb = b""
    if gps is not None:
        (lat, lref), (lon, lref2) = gps
        rat = lambda ps: b"".join(struct.pack("<II", n, d) for n, d in ps)
        g = [ent(0x0001, 2, 2, (lref.encode() + b"\x00").ljust(4, b"\x00")),
             ent(0x0002, 5, 3, put(rat(lat))),
             ent(0x0003, 2, 2, (lref2.encode() + b"\x00").ljust(4, b"\x00")),
             ent(0x0004, 5, 3, put(rat(lon)))]
        g.sort(key=lambda x: struct.unpack_from("<H", x, 0)[0])
        gpsb = struct.pack("<H", len(g)) + b"".join(g) + struct.pack("<I", 0)

    ifd1b = b""
    if thumb_jpeg:
        toff = data_off + len(blobs)
        blobs += thumb_jpeg + (b"\x00" if len(thumb_jpeg) % 2 else b"")
        f = [ent(0x0103, 3, 1, 6),
             ent(0x0201, 4, 1, toff),
             ent(0x0202, 4, 1, len(thumb_jpeg))]
        f.sort(key=lambda x: struct.unpack_from("<H", x, 0)[0])
        ifd1b = struct.pack("<H", len(f)) + b"".join(f) + struct.pack("<I", 0)

    return b"II*\x00" + struct.pack("<I", ifd0_off) + ifd0 + gpsb + ifd1b + blobs


def jpeg_with_exif(img, tiff, tail=b"", quality=90):
    """把 EXIF 段插在 SOI 之后；tail 会接在 APP1 段内部（EXIF 数据之后）。"""
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality=quality)
    raw = buf.getvalue()
    body = b"Exif\x00\x00" + tiff + tail
    app1 = b"\xff\xe1" + struct.pack(">H", len(body) + 2) + body
    return raw[:2] + app1 + raw[2:]


def jpeg_strip_app1(raw):
    """模拟「抹除 EXIF」：把 APP1 整段摘掉，其余字节保持不动。"""
    if raw[:2] != b"\xff\xd8":
        return raw
    out = bytearray(raw[:2])
    i = 2
    while i < len(raw) - 1:
        if raw[i] != 0xFF:
            out += raw[i:]
            break
        mk = raw[i + 1]
        if mk in (0xD8, 0xD9) or 0xD0 <= mk <= 0xD7:
            out += raw[i:i + 2]
            i += 2
            continue
        ln = struct.unpack_from(">H", raw, i + 2)[0]
        seg = raw[i:i + 2 + ln]
        if not (mk == 0xE1 and seg[4:10] == b"Exif\x00\x00"):
            out += seg
        i += 2 + ln
        if mk == 0xDA:
            out += raw[i:]
            break
    return bytes(out)


def ifd1_thumbnail(raw):
    """从 JPEG 字节里按 IFD1 取出缩略图（没有就返回 None）。"""
    i = raw.find(b"Exif\x00\x00")
    if i < 0:
        return None
    t = raw[i + 6:]
    first = struct.unpack_from("<I", t, 4)[0]
    n = struct.unpack_from("<H", t, first)[0]
    nxt = struct.unpack_from("<I", t, first + 2 + n * 12)[0]
    if not nxt:
        return None
    m = struct.unpack_from("<H", t, nxt)[0]
    tags = {}
    for k in range(m):
        tag = struct.unpack_from("<H", t, nxt + 2 + k * 12)[0]
        tags[tag] = struct.unpack_from("<I", t, nxt + 2 + k * 12 + 8)[0]
    off, ln = tags.get(0x0201), tags.get(0x0202)
    if not off or not ln:
        return None
    return t[off:off + ln]


def gps_from_exif(raw):
    """手工解析 GPS IFD，返回 (lat, lon) 或 None。"""
    i = raw.find(b"Exif\x00\x00")
    if i < 0:
        return None
    t = raw[i + 6:]
    first = struct.unpack_from("<I", t, 4)[0]
    n = struct.unpack_from("<H", t, first)[0]
    gps_off = None
    for k in range(n):
        tag = struct.unpack_from("<H", t, first + 2 + k * 12)[0]
        if tag == 0x8825:
            gps_off = struct.unpack_from("<I", t, first + 2 + k * 12 + 8)[0]
    if not gps_off:
        return None
    m = struct.unpack_from("<H", t, gps_off)[0]
    vals = {}
    for k in range(m):
        tag, typ, cnt = struct.unpack_from("<HHI", t, gps_off + 2 + k * 12)
        raw4 = t[gps_off + 2 + k * 12 + 8: gps_off + 2 + k * 12 + 12]
        if typ == 5 and cnt == 3:
            off = struct.unpack("<I", raw4)[0]
            pairs = [struct.unpack_from("<II", t, off + j * 8) for j in range(3)]
            vals[tag] = pairs
        else:
            vals[tag] = raw4[0:1].decode("ascii", "replace")
    if 2 not in vals or 4 not in vals:
        return None
    deg = lambda ps: sum(n / d / (60 ** j) for j, (n, d) in enumerate(ps))
    lat, lon = deg(vals[2]), deg(vals[4])
    if vals.get(1) == "S":
        lat = -lat
    if vals.get(3) == "W":
        lon = -lon
    return lat, lon

# ---------------------------------------------------------------------------
# 各题构造
# ---------------------------------------------------------------------------
def build_enc_chain(cfg):
    """三层编码：base64 → hex → rot13"""
    import codecs
    flag = cfg["flag"]
    rot = codecs.encode(flag, "rot13")
    hexs = rot.encode().hex()
    outer = base64.b64encode(hexs.encode()).decode()
    body = """ITNS · MISC 练习 · 编码接力

有人把 flag 藏在一段文字里，走的是「编码」而不是「加密」。
你已经知道：编码是可逆的，不需要钥匙。

下面是题目给的全部内容：

%s

提示：
  1. 看字符集和长度，先认出最外面那一层
  2. 拆掉一层，把结果贴回来看它变成什么样
  3. 一共三层，拆到能读懂为止

第三课 p12 / p13：base 家族、十六进制、ROT13，以及「编码 ≠ 加密」。
""" % outer
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"), body)


def build_file_header(cfg):
    """真实长城照 + 底部只写 flag；PNG 前 4 字节被清零"""
    im = load_photo("p_greatwall.jpg", 1280)
    im = caption_strip(im, cfg["flag"], None, bar=0.13, fs=0.060)
    data = bytearray(png_bytes(im))
    data[0:4] = b"\x00\x00\x00\x00"
    write_bytes(os.path.join(cfg["dir"], "files", "logo.png"), bytes(data))

def build_append_zip(cfg):
    """用户提供的动漫截图，PNG 的 IEND 之后接一个只含 flag.txt 的 zip"""
    src = os.path.join(PHOTOS, "user_ctf_meme.png")
    im = Image.open(src)
    if im.mode != "RGB":
        im = im.convert("RGB")
    inner = zip_bytes([("flag.txt", (cfg["flag"] + "\n").encode())])
    write_bytes(os.path.join(cfg["dir"], "files", "cover.png"), png_bytes(im) + inner)

def build_fake_zip(cfg):
    """伪加密 zip：加密位被置上，其实没有密码；包内只有 flag.txt"""
    plain = zip_bytes([("flag.txt", (cfg["flag"] + "\n").encode())])
    write_bytes(os.path.join(cfg["dir"], "files", "secret.zip"), zip_set_encrypted_bit(plain, True))

def build_lsb(cfg):
    """真实阳朔村庄照，flag 写进 RGB 最低位"""
    img = lsb_embed(load_photo("p_yangshuo.jpg", 1200), cfg["flag"].encode())
    write_bytes(os.path.join(cfg["dir"], "files", "photo.png"), png_bytes(img))

def build_exif(cfg):
    """真实长城照（CC0），EXIF ImageDescription 里放 base64(flag)"""
    im = load_photo("p_greatwall_cc0.jpg", 1200)
    ex = im.getexif()
    ex[0x010E] = base64.b64encode(cfg["flag"].encode()).decode()
    ex[0x010F] = "Apple"
    ex[0x0110] = "iPhone 13"
    ex[0x0131] = "iOS 17.4.1"
    ex[0x9286] = "ASCII\x00\x00\x00nothing here, keep looking"
    write_bytes(os.path.join(cfg["dir"], "files", "photo.jpg"), _jpeg_with_exif(im, ex))

def _jpeg_with_exif(img, ex):
    b = io.BytesIO()
    img.save(b, "JPEG", exif=ex.tobytes(), quality=92)
    return b.getvalue()


def build_traffic(cfg):
    """pcap：DNS 噪声 + 一条完整 HTTP 会话（握手/响应齐全）+ TLS 噪声。

    flag 在第二个请求的 query 里（base64）。序列号按真实握手推进，
    保证 Wireshark 能把它当成正常会话解析出来。
    """
    flag = cfg["flag"]
    payload_b64 = base64.b64encode(flag.encode()).decode()
    cli = b"\x0a\x00\x00\x02"          # 客户端 10.0.0.2
    srv = b"\x0a\x00\x00\x01"          # 服务器 10.0.0.1
    frames = []

    def push(src, dst, tcp):
        frames.append(_eth(_ip4(src, dst, 6, tcp)))

    # --- DNS 噪声 ---------------------------------------------------------
    dns = _dns_query("files.itns.local")
    frames.append(_eth(_ip4(cli, srv, 17, _udp(5353, 53, dns))))
    dns_r = struct.pack("!HHHHHH", 0x1A2B, 0x8180, 1, 1, 0, 0) + dns[12:]
    frames.append(_eth(_ip4(srv, cli, 17, _udp(53, 5353, dns_r))))

    # --- HTTP 会话（49812 → 80）-------------------------------------------
    cseq, sseq = 1000, 5000
    push(cli, srv, _tcp(49812, 80, cseq, 0, 0x02))                    # SYN
    push(srv, cli, _tcp(80, 49812, sseq, cseq + 1, 0x12))             # SYN/ACK
    cseq += 1
    push(cli, srv, _tcp(49812, 80, cseq, sseq + 1, 0x10))             # ACK
    sseq += 1

    def http_pair(req, resp):
        nonlocal cseq, sseq
        push(cli, srv, _tcp(49812, 80, cseq, sseq, 0x18, req.encode()))
        cseq += len(req)
        push(srv, cli, _tcp(80, 49812, sseq, cseq, 0x10))             # ACK
        push(srv, cli, _tcp(80, 49812, sseq, cseq, 0x18, resp.encode()))
        sseq += len(resp)
        push(cli, srv, _tcp(49812, 80, cseq, sseq, 0x10))             # ACK

    http_pair(
        "GET /index.html HTTP/1.1\r\n"
        "Host: files.itns.local\r\n"
        "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64)\r\n"
        "Accept: text/html\r\n\r\n",
        "HTTP/1.1 200 OK\r\n"
        "Server: nginx/1.24.0\r\n"
        "Content-Type: text/html; charset=utf-8\r\n"
        "Content-Length: 46\r\n\r\n"
        "<html><body><h1>ITNS log viewer</h1></body></html>",
    )
    http_pair(
        "GET /api/log?q=%s HTTP/1.1\r\n"
        "Host: files.itns.local\r\n"
        "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64)\r\n"
        "Accept: application/json\r\n"
        "Referer: http://files.itns.local/index.html\r\n\r\n" % payload_b64,
        "HTTP/1.1 200 OK\r\n"
        "Server: nginx/1.24.0\r\n"
        "Content-Type: application/json\r\n"
        "Content-Length: 27\r\n\r\n"
        '{"status":"logged","ok":true}',
    )

    # --- TLS 噪声（49813 → 443）-------------------------------------------
    hello = bytes.fromhex("160301004a01000046 0303") + bytes(
        (i * 37 + 11) % 256 for i in range(0x46)
    )
    push(cli, srv, _tcp(49813, 443, 7000, 0, 0x02))
    push(srv, cli, _tcp(443, 49813, 9000, 7001, 0x12))
    push(cli, srv, _tcp(49813, 443, 7001, 9001, 0x18, hello))

    write_bytes(os.path.join(cfg["dir"], "files", "capture.pcap"), build_pcap(frames))


def _geo_prepare(src_path, dst_path, max_w=1400):
    """图寻题照片：缩放 + 抹掉全部元数据（避免 GPS/描述直接泄题）"""
    im = Image.open(src_path)
    im = im.convert("RGB")
    if im.width > max_w:
        im = im.resize((max_w, int(im.height * max_w / im.width)), Image.LANCZOS)
    data = io.BytesIO()
    im.save(data, "JPEG", quality=90)      # 不带 exif 参数 = 不写元数据
    write_bytes(dst_path, data.getvalue())
    return im.size


def build_geo_hongyadong(cfg):
    """洪崖洞真实照片，**保留 EXIF**：GPS 29.566714/106.575697 + 拍摄时间 + 作者

    flag 要用「地名 + 拍摄日期的 UTC 时刻」，所以坐标和时间都得读出来再换算。
    """
    src = os.path.join(HERE, "_assets", "geo", "hongyadong.jpg")
    im = Image.open(src).convert("RGB")
    if im.width > 1200:
        im = im.resize((1200, int(im.height * 1200 / im.width)), Image.LANCZOS)
    lat = ((29, 1), (34, 1), (0, 1))        # 29°34'00"  ≈ 29.566714
    lon = ((106, 1), (34, 1), (33, 1))      # 106°34'33" ≈ 106.575697
    tiff = build_exif_tiff(b"", b"", b"", gps=((lat, "N"), (lon, "E")),
                           software=b"", datetime_orig="2024:10:09 21:25:22",
                           artist="Lianguanlun")
    write_bytes(os.path.join(cfg["dir"], "files", "photo.jpg"),
                jpeg_with_exif(im, tiff, quality=90))
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"),
               "一张游客照，直接发的原图，元数据一点没清。\n\n"
               "任务：从文件里读出拍摄地点和拍摄时间，定位到那片建筑。\n"
               "提交格式：flag{城市拼音_建筑名拼音_拍摄日期(YYYYMMDD)_UTC时刻(HHMM)}\n"
               "例：flag{beijing_tiananmen_20240101_0230}\n\n"
               "注意：文件里记的是北京时间（UTC+8），flag 要的是 UTC。\n", bom=True)

def build_geo_nanjinglu(cfg):
    src = os.path.join(ROOT, "_build", "_assets", "geo", "nanjing_road.jpg")
    _geo_prepare(src, os.path.join(cfg["dir"], "files", "photo.jpg"), max_w=1500)
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"),
               "一张夜里的街景照片。三个线索拼起来才能定位：\n"
               "  1. 左边那栋亮着的老百货，招牌上写着它的名字\n"
               "  2. 它背后那栋高塔，顶上也有名字\n"
               "  3. 这条街本身是某座城市最有名的步行街\n"
               "提交格式：flag{城市拼音_街道名拼音_建筑名拼音}\n"
               "例：flag{beijing_wangfujing_dongfanguangchang}\n", bom=True)


# --- EXIF 查询（批量）：五张照片只有一张带 GPS ------------------------------
def build_exif_gps(cfg):
    """五张真实旅行照，只有张家界那张带 GPS（原图真实坐标 29.332338, 110.426066）"""
    lat = ((29, 1), (19, 1), (56, 1))       # 29°19'56"  ≈ 29.332338
    lon = ((110, 1), (25, 1), (34, 1))      # 110°25'34" ≈ 110.426066
    scenes = [
        ("IMG_2031.jpg", "p_gugong_turtle.jpg", b"Apple", b"iPhone 13",
         "morning in the palace", "2026:07:11 09:12:40", None),
        ("IMG_2032.jpg", "p_karst.jpg", b"Apple", b"iPhone 13",
         "foggy hills", "2026:07:12 06:48:03", None),
        ("IMG_2033.jpg", "p_zjj_gps.jpg", b"Canon", b"Canon EOS 6D",
         "a quick shot", "2026:07:14 09:26:00", ((lat, "N"), (lon, "E"))),
        ("IMG_2034.jpg", "p_guilin.jpg", b"Apple", b"iPhone 13",
         "river cruise", "2026:07:13 17:05:22", None),
        ("IMG_2035.jpg", "p_mutianyu.jpg", b"Apple", b"iPhone 13",
         "climbing up", "2026:07:15 11:30:11", None),
    ]
    outdir = ensure_dir(os.path.join(cfg["dir"], "files", "photos"))
    for name, photo, make, model, exif_desc, dt, gps in scenes:
        im = load_photo(photo, 1280)
        tiff = build_exif_tiff(make, model, exif_desc.encode("ascii"), gps=gps,
                               software=b"iOS 17.4.1", datetime_orig=dt)
        write_bytes(os.path.join(outdir, name), jpeg_with_exif(im, tiff, quality=90))
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"),
               "有人在朋友圈发了五张旅行照。\n"
               "其中只有一张，把拍摄地点一起带了出来。\n\n"
               "任务：找出那张照片，把经纬度换算出来，定位到那片景区。\n"
               "提交格式：flag{省份拼音_景区名拼音}（例：flag{anhui_huangshan}）\n\n"
               "提示：exiftool 支持一次跑一整个目录。\n", bom=True)

def build_exif_thumb(cfg):
    """真实故宫铜龟照：主图打码，缩略图没打码并写着 flag（只有 flag，无说明）"""
    main = mosaic(load_photo("p_gugong_turtle2.jpg", 1200))
    tiff = None
    for w, q, fs, bar in ((560, 70, 0.090, 0.26), (460, 66, 0.100, 0.28),
                          (380, 60, 0.110, 0.30), (300, 55, 0.120, 0.32)):
        th = caption_strip(load_photo("p_gugong_turtle2.jpg", w), cfg["flag"], None,
                           bar=bar, fs=fs)
        tb = io.BytesIO()
        th.save(tb, "JPEG", quality=q)
        cand = build_exif_tiff(b"Apple", b"iPhone 13", b"blurred on purpose",
                               thumb_jpeg=tb.getvalue(), software=b"iOS 17.4.1")
        if len(cand) + 10 < 65535:
            tiff = cand
            print("    缩略图 %dpx q%d → APP1 %d 字节" % (w, q, len(cand) + 10))
            break
    if tiff is None:
        raise RuntimeError("缩略图压不到 64KB 以内")
    write_bytes(os.path.join(cfg["dir"], "files", "photo.jpg"),
                jpeg_with_exif(main, tiff, quality=88))

def build_exif_erase(cfg):
    """真实西湖全景：APP1 段里藏 zip；after.jpg 是「抹除之后」的版本"""
    im = load_photo("p_xihu_pano.jpg", 1600)
    inner = zip_bytes([
        ("flag.txt", (cfg["flag"] + "\n").encode()),
        ("readme.txt", "这层压缩包藏在 EXIF 段里，抹除 EXIF 就一起没了。\n".encode("utf-8")),
    ])
    tiff = build_exif_tiff(b"Apple", b"iPhone 13", b"West Lake, Hangzhou", software=b"iOS 17.4.1")
    before = jpeg_with_exif(im, tiff, tail=inner, quality=90)
    write_bytes(os.path.join(cfg["dir"], "files", "before.jpg"), before)
    write_bytes(os.path.join(cfg["dir"], "files", "after.jpg"), jpeg_strip_app1(before))


def build_png_text(cfg):
    """真实苏州园林照，flag 藏在 PNG 的 tEXt 注释块里"""
    im = load_photo("p_garden_wangshi.jpg", 1280)
    info = PngImagePlugin.PngInfo()
    info.add_text("Comment", base64.b64encode(cfg["flag"].encode()).decode())
    info.add_text("Software", "ITNS Photo Editor 1.0")   # 干扰项
    info.add_text("Author", "anonymous")                 # 干扰项
    buf = io.BytesIO()
    im.save(buf, "PNG", pnginfo=info, optimize=True)
    write_bytes(os.path.join(cfg["dir"], "files", "photo.png"), buf.getvalue())



def build_exif_trio(cfg):
    """星走路的第一关：型号 + 坐标 + 时间，三件套拼 flag"""
    im = load_photo("p_gugong_turtle2.jpg", 1200)
    lat = ((39, 1), (54, 1), (59, 1))       # 39°54'59"  ≈ 39.91639（故宫）
    lon = ((116, 1), (23, 1), (50, 1))      # 116°23'50" ≈ 116.39722
    tiff = build_exif_tiff(b"Xiaomi", b"Xiaomi 14 Ultra", b"palace courtyard",
                           gps=((lat, "N"), (lon, "E")), software=b"HyperOS 1.0",
                           datetime_orig="2026:07:14 17:26:00")
    write_bytes(os.path.join(cfg["dir"], "files", "photo.jpg"),
                jpeg_with_exif(im, tiff, quality=90))
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"),
               "一张游客照，元数据完整。\n\n"
               "任务：从文件里凑出三样东西 —— 拍摄地点（城市）、手机品牌、拍摄时刻。\n"
               "提交格式：flag{城市拼音_品牌小写_UTC时刻(HHMM)}\n"
               "例：flag{nanjing_vivo_1130}\n\n"
               "注意：文件里记的是北京时间（UTC+8）。\n", bom=True)


def build_route_trace(cfg):
    """星走路的第五关：三张不同城市的照片，按时间排序得出路线"""
    # (文件名, 照片, 经度DMS, 纬度DMS, 参考点, 拍摄时间)
    scenes = [
        ("IMG_1002.jpg", os.path.join(HERE, "_assets", "geo", "hongyadong.jpg"),
         ((106, 1), (34, 1), (33, 1)), ((29, 1), (34, 1), (0, 1)), "2024:10:11 21:05:11"),
        ("IMG_1001.jpg", "p_pearl_gps.jpg",
         ((121, 1), (29, 1), (49, 1)), ((31, 1), (14, 1), (25, 1)), "2024:10:07 08:41:30"),
        ("IMG_1003.jpg", "p_zjj_gps.jpg",
         ((110, 1), (25, 1), (34, 1)), ((29, 1), (19, 1), (56, 1)), "2024:10:09 09:12:07"),
    ]
    outdir = ensure_dir(os.path.join(cfg["dir"], "files"))
    for name, photo, lon, lat, dt in scenes:
        path = photo if os.path.isabs(photo) else os.path.join(PHOTOS, photo)
        im = Image.open(path).convert("RGB")
        if im.width > 1000:
            im = im.resize((1000, int(im.height * 1000 / im.width)), Image.LANCZOS)
        tiff = build_exif_tiff(b"Apple", b"iPhone 13", b"trip", gps=((lat, "N"), (lon, "E")),
                               software=b"iOS 17.4.1", datetime_orig=dt)
        write_bytes(os.path.join(outdir, name), jpeg_with_exif(im, tiff, quality=90))
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"),
               "一次旅行，三张照片，文件名是乱的。\n\n"
               "任务：按**拍摄时间**排好顺序，认出三座城市，写出这趟路线。\n"
               "提交格式：flag{起点城市_中间城市_终点城市}（全小写拼音，用下划线）\n"
               "例：flag{beijing_xian_chengdu}\n", bom=True)


# ===========================================================================
# 孪生题：每组的简化前置
# ===========================================================================
def build_b64_only(cfg):
    """Level-1-1：只有一层 base64"""
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"),
               "值班的同学在群里发了这串东西，说「解开就是 flag」。\n\n"
               + base64.b64encode(cfg["flag"].encode()).decode() + "\n", bom=True)


def build_hex_only(cfg):
    """Level-1-2：只有一层十六进制"""
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"),
               "抓包导出来的一段，看着像乱码。\n\n"
               + cfg["flag"].encode().hex() + "\n", bom=True)


def build_ext_lie(cfg):
    """Level-2-1：后缀骗人（.docx 其实是 zip）"""
    inner = zip_bytes([("flag.txt", (cfg["flag"] + "\n").encode())])
    write_bytes(os.path.join(cfg["dir"], "files", "作业.docx"), inner)


def build_zip_comment(cfg):
    """Level-3-1：flag 在压缩包的注释字段里"""
    entries = [("readme.txt", "flag 不在包里任何一个文件中。\n"
                              "但压缩包本身还带着一个字段，你可能从来没点开过。\n".encode("utf-8"))]
    write_bytes(os.path.join(cfg["dir"], "files", "handin.zip"),
                zip_with_comment(entries, cfg["flag"]))


def build_lsb_easy(cfg):
    """Level-4-1：只动红色通道最低位"""
    img = lsb_embed_r(load_photo("p_yangshuo.jpg", 900), cfg["flag"].encode())
    write_bytes(os.path.join(cfg["dir"], "files", "photo.png"), png_bytes(img))
    write_text(os.path.join(cfg["dir"], "files", "readme.txt"),
               "这张图里藏了 flag，藏法很朴素：\n"
               "每个像素的**红色通道**最低位，从左上角开始按行读，每 8 位一个字符。\n"
               "（不需要动绿、蓝通道。）\n", bom=True)


# ===========================================================================
# 流量三连：分块传输 / DNS 隧道 / 私有协议
# ===========================================================================
def build_traffic_chunked(cfg):
    """Level-5-2：HTTP 分块传输，flag 被切成三段夹在日志行里"""
    flag = cfg["flag"]
    cli, srv = b"\x0a\x00\x00\x02", b"\x0a\x00\x00\x01"
    frames = []

    def push(src, dst, tcp):
        frames.append(_eth(_ip4(src, dst, 6, tcp)))

    q = _dns_query("files.itns.local")
    frames.append(_eth(_ip4(cli, srv, 17, _udp(5353, 53, q))))
    frames.append(_eth(_ip4(srv, cli, 17, _udp(53, 5353,
                     struct.pack("!HHHHHH", 0x1A2B, 0x8180, 1, 1, 0, 0) + q[12:]))))

    cseq, sseq = 2000, 8000
    push(cli, srv, _tcp(50021, 80, cseq, 0, 0x02))
    push(srv, cli, _tcp(80, 50021, sseq, cseq + 1, 0x12))
    cseq += 1
    sseq += 1
    push(cli, srv, _tcp(50021, 80, cseq, sseq, 0x10))
    req = ("GET /download/log.bin HTTP/1.1\r\nHost: files.itns.local\r\n"
           "User-Agent: Mozilla/5.0\r\nAccept: */*\r\n\r\n").encode()
    push(cli, srv, _tcp(50021, 80, cseq, sseq, 0x18, req))
    cseq += len(req)

    parts = [flag[:13], flag[13:26], flag[26:]]
    body = b""
    for i, p in enumerate(parts):
        pb = p.encode()
        body += ("%x\r\n" % len(pb)).encode() + pb + b"\r\n"
        junk = ("-- log line %d: ok --\n" % i).encode()
        body += ("%x\r\n" % len(junk)).encode() + junk + b"\r\n"
    body += b"0\r\n\r\n"
    head = ("HTTP/1.1 200 OK\r\nServer: nginx/1.24.0\r\n"
            "Content-Type: application/octet-stream\r\n"
            "Transfer-Encoding: chunked\r\n\r\n").encode()
    resp = head + body

    step = 110
    segs = [resp[i:i + step] for i in range(0, len(resp), step)]
    base = sseq
    order = list(range(len(segs)))
    if len(order) > 5:
        order[2], order[5] = order[5], order[2]          # 乱序到达
    for idx in order:
        push(srv, cli, _tcp(80, 50021, base + idx * step, cseq, 0x18, segs[idx]))
    if len(segs) > 3:                                     # 一次重传
        push(srv, cli, _tcp(80, 50021, base + 3 * step, cseq, 0x18, segs[3]))
    push(cli, srv, _tcp(50021, 80, cseq, base + len(resp), 0x10))
    write_bytes(os.path.join(cfg["dir"], "files", "capture.pcap"), build_pcap(frames))


def build_traffic_dns(cfg):
    """Level-5-3：DNS 隧道，base32 分片藏在子域名里"""
    flag = cfg["flag"]
    b32 = base64.b32encode(flag.encode()).decode().rstrip("=")
    chunks = [b32[i:i + 12] for i in range(0, len(b32), 12)]
    cli, srv = b"\x0a\x00\x00\x05", b"\x0a\x00\x00\x01"
    frames = []

    def be(name, sid=40000, answer=False):
        q = _dns_query(name)
        frames.append(_eth(_ip4(cli, srv, 17, _udp(sid, 53, q))))
        if answer:
            r = struct.pack("!HHHHHH", 0x2222, 0x8180, 1, 1, 0, 0) + q[12:]
            frames.append(_eth(_ip4(srv, cli, 17, _udp(53, sid, r))))

    for n in ("www.itns.local", "api.itns.local", "mail.itns.local"):
        be(n)
    for i, ch in enumerate(chunks, 1):                   # 真正的那一组
        be("%02d.%s.t.itns.local" % (i, ch), 41000 + i, True)
    noise = base64.b32encode(b"decoy-decoy-decoy-").decode().rstrip("=")
    for i in range(1, 4):                                # 干扰组（.cdn.）
        be("%02d.%s.cdn.itns.local" % (i, noise[(i - 1) * 12:i * 12]), 42000 + i)
    write_bytes(os.path.join(cfg["dir"], "files", "capture.pcap"), build_pcap(frames))


def build_traffic_proto(cfg):
    """Level-5-4：私有协议，帧头要自己逆出来，payload 还做了 XOR"""
    flag = cfg["flag"]
    cli, srv = b"\x0a\x00\x00\x07", b"\x0a\x00\x00\x01"
    parts = [flag[:10], flag[10:20], flag[20:]]
    blob = b""
    for i, p in enumerate(parts, 1):
        body = bytes(b ^ 0x5A for b in p.encode())
        head = b"ITNS" + bytes([0x01]) + struct.pack(">HH", i, len(body))
        blob += head + body + struct.pack(">H", crc16(head + body))
    blob += b"ITNS" + bytes([0x02]) + struct.pack(">HH", 0, 4) + bytes(b ^ 0x5A for b in b"bye!")

    frames = []

    def push(src, dst, tcp):
        frames.append(_eth(_ip4(src, dst, 6, tcp)))

    push(cli, srv, _tcp(50111, 9001, 3000, 0, 0x02))
    push(srv, cli, _tcp(9001, 50111, 9000, 3001, 0x12))
    push(cli, srv, _tcp(50111, 9001, 3001, 9001, 0x10))
    hello = b"ITNS" + bytes([0x00]) + struct.pack(">HH", 0, 5) + bytes(b ^ 0x5A for b in b"HELLO")
    push(cli, srv, _tcp(50111, 9001, 3001, 9001, 0x18, hello))
    step = 40
    segs = [blob[i:i + step] for i in range(0, len(blob), step)]
    for idx, sg in enumerate(segs):
        push(srv, cli, _tcp(9001, 50111, 9001 + idx * step, 3001 + len(hello), 0x18, sg))
    write_bytes(os.path.join(cfg["dir"], "files", "capture.pcap"), build_pcap(frames))

BUILDERS = {
    "enc-chain-ctf": build_enc_chain,
    "file-header-ctf": build_file_header,
    "append-zip-ctf": build_append_zip,
    "fake-zip-ctf": build_fake_zip,
    "lsb-stego-ctf": build_lsb,
    "exif-meta-ctf": build_exif,
    "traffic-ctf": build_traffic,
    "geo-hongyadong-ctf": build_geo_hongyadong,
    "geo-nanjinglu-ctf": build_geo_nanjinglu,
    "exif-gps-ctf": build_exif_gps,
    "exif-thumb-ctf": build_exif_thumb,
    "exif-erase-ctf": build_exif_erase,
    "png-text-ctf": build_png_text,
    "exif-trio-ctf": build_exif_trio,
    "route-trace-ctf": build_route_trace,

    "b64-only-ctf": build_b64_only,
    "hex-only-ctf": build_hex_only,
    "ext-lie-ctf": build_ext_lie,
    "zip-comment-ctf": build_zip_comment,
    "lsb-easy-ctf": build_lsb_easy,
    "traffic-chunked-ctf": build_traffic_chunked,
    "traffic-dns-ctf": build_traffic_dns,
    "traffic-proto-ctf": build_traffic_proto,
}

if __name__ == "__main__":
    # 题目元数据由 make_docs.py 统一写入；这里只负责附件
    import json
    cfg_path = os.path.join(ROOT, "_build", "challenges.json")
    with open(cfg_path, encoding="utf-8") as f:
        meta = json.load(f)
    total = 0
    for key, cfg in meta.items():
        base = os.path.join(ROOT, cfg.get("group") or "", cfg.get("path") or key)
        d = ensure_dir(os.path.join(base, "files"))
        cfg["dir"] = base
        BUILDERS[key](cfg)
        gens = os.listdir(d)
        print("  [%s] 生成 %d 个附件: %s" % (key, len(gens), ", ".join(sorted(gens))))
        total += len(gens)
    print("附件合计 %d 个文件" % total)
