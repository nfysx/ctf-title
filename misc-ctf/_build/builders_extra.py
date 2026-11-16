# -*- coding: utf-8 -*-
"""动态附件 builders —— misc-ctf 缺的 6 道题

接线（动态附件生成器/make_mapped.py）：
    sys.path.insert(0, _build)
    import builders_extra
    builders_extra.BUILDERS["<题目目录名>"](cfg)

cfg 约定：
    cfg["dir"]   输出根目录；附件一律写进 <dir>/files/
    cfg["flag"]  本次变体的 flag（字符串）

三条硬约束：
  1. 确定性：同一 flag 跑两次产出逐字节相同。不用 random / 时间戳；
     需要“随机”的地方（PBE 的 salt）由 flag 派生。
  2. 离线：只用 Pillow + numpy + qrcode + pycryptodome + 标准库。
  3. 自检：每个 builder 写完文件后，按该题的解题逻辑把 flag 反解一遍，
     与 cfg["flag"] 逐字比对，不符直接抛 AssertionError。

基准素材在 _build/_assets/dyn/（原图 / 原流量），见各 builder 注释。
"""
import base64
import hashlib
import io
import os
import re
import struct
import zlib

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "_assets", "dyn")

FONT_MONO_BOLD = r"C:\Windows\Fonts\consolab.ttf"
FONT_MONO = r"C:\Windows\Fonts\consola.ttf"
FONT_CN = r"C:\Windows\Fonts\msyh.ttc"
FONT_CN_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"

PNG_SIG = b"\x89PNG\r\n\x1a\n"


# ===========================================================================
# 通用小工具
# ===========================================================================
def _write(path, data):
    d = os.path.dirname(path)
    if d:
        os.makedirs(d, exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)
    return path


def _files(cfg):
    d = os.path.join(cfg["dir"], "files")
    os.makedirs(d, exist_ok=True)
    return d


def _font(path, size):
    for p in (path, FONT_MONO, FONT_CN):
        try:
            return ImageFont.truetype(p, size)
        except Exception:
            continue
    return ImageFont.load_default(size)


def _fit_font(text, max_w, max_h, start, path=FONT_MONO_BOLD):
    """从 start 往上找第一个宽高都能装下的字号（逐像素递减，结果确定）"""
    size = start
    while size > 8:
        f = _font(path, size)
        bb = f.getbbox(text)
        if bb[2] - bb[0] <= max_w and bb[3] - bb[1] <= max_h:
            return f, bb
        size -= 1
    raise ValueError("字号放不下：%r" % (text,))


# --- 像素级“反解”：按同一字体重新栅格化，与图上的墨迹做 IoU 比对 -------------
def _ink_mask(im, box, dark_on_light):
    reg = im.convert("L").crop(box)
    a = np.asarray(reg, dtype=np.uint8)
    return (a < 128) if dark_on_light else (a > 128)


def _assert_text_present(im, text, font, xy, dark_on_light=True, min_iou=0.70):
    """确认这串字真的画在图上：模板匹配 + 墨量比对。

    返回 (iou, ink_img, ink_ref)。文字缺失 / 内容不同会让 IoU 掉到 0.3 以下。
    """
    bb = font.getbbox(text)
    w, h = bb[2] - bb[0], bb[3] - bb[1]
    ref = Image.new("L", (w + 8, h + 8), 0)
    ImageDraw.Draw(ref).text((4 - bb[0], 4 - bb[1]), text, font=font, fill=255)
    ref_m = np.asarray(ref, dtype=np.uint8)[4:4 + h, 4:4 + w] > 127

    box = (int(xy[0] + bb[0]), int(xy[1] + bb[1]),
           int(xy[0] + bb[0]) + w, int(xy[1] + bb[1]) + h)
    assert 0 <= box[0] and box[2] <= im.width and 0 <= box[1] and box[3] <= im.height,         "文字区域超出画布：%r vs %r" % (box, im.size)
    img_m = _ink_mask(im, box, dark_on_light)

    inter = int((img_m & ref_m).sum())
    union = int((img_m | ref_m).sum())
    iou = inter / union if union else 0.0
    ink_img, ink_ref = int(img_m.sum()), int(ref_m.sum())
    ok_ink = ink_ref > 0 and abs(ink_img - ink_ref) / ink_ref < 0.25
    assert iou >= min_iou and ok_ink, (
        "图上没找到这串字：iou=%.3f ink_img=%d ink_ref=%d text=%r"
        % (iou, ink_img, ink_ref, text))
    return iou, ink_img, ink_ref


# ===========================================================================
# 1) misc-hello-strings —— NOTI 容器 + 明文正文
# ===========================================================================
NOTICE_BODY = (
    "NOTICE 2026-11-03\r\n"
    "本周社团活动改到周四 19:00，教室 B203。\r\n"
    "签到凭据（提交到平台）：\r\n"
    "{flag}\r\n"
    "请转发给没看到通知的同学。\r\n"
)


def _strings(data, n=4):
    """等价于 GNU strings 的默认规则：连续 >=n 个可打印 ASCII 算一条"""
    out, run = [], b""
    for b in list(data) + [0]:
        if 0x20 <= b < 0x7F:
            run += bytes([b])
        else:
            if len(run) >= n:
                out.append(run)
            run = b""
    return out


def build_misc_hello_strings(cfg):
    """files/notice.bin = 'NOTI' + 小端 uint32 正文长度 + UTF-8 正文（flag 明文）"""
    flag = cfg["flag"]
    body = NOTICE_BODY.format(flag=flag).encode("utf-8")
    data = b"NOTI" + struct.pack("<I", len(body)) + body
    p = _write(os.path.join(_files(cfg), "notice.bin"), data)

    # ---- 自检：按题目解法把 flag 捞回来 ----
    raw = open(p, "rb").read()
    assert raw[:4] == b"NOTI", "魔数写错"
    (blen,) = struct.unpack_from("<I", raw, 4)
    assert blen == len(raw) - 8, "长度字段与正文不一致"
    hits = _strings(raw[8:8 + blen], 4)
    got = [h.decode() for h in hits if h.startswith(b"flag{")]
    assert len(got) == 1, "strings 捞到的 flag 条数异常：%r" % (got,)
    assert got[0] == flag, "反解出的 flag 不一致：%r != %r" % (got[0], flag)
    print("    [hello-strings] %d B，strings 捞到 %d 条，flag=%s"
          % (len(raw), len(hits), got[0]))
    return p


# ===========================================================================
# 2) misc-hello-hexdump —— 12 字节头 + 每 2 字节插 0x00 + DEADBEEF
# ===========================================================================
def build_misc_hello_hexdump(cfg):
    """files/blob.bin：a5 5a 01 00 | 正文长(le32) | 每组字节(le32) | 正文 | de ad be ef"""
    flag = cfg["flag"].encode("utf-8")
    chunk = 2
    groups = [flag[i:i + chunk] for i in range(0, len(flag), chunk)]
    payload = b"".join(g + b"\x00" for g in groups)
    data = (b"\xa5\x5a" + bytes([1, 0])
            + struct.pack("<II", len(payload), chunk)
            + payload + b"\xde\xad\xbe\xef")
    p = _write(os.path.join(_files(cfg), "blob.bin"), data)

    # ---- 自检：按 00 的分组位置拼回来 ----
    raw = open(p, "rb").read()
    assert raw[:2] == b"\xa5\x5a" and raw[2] == 1
    (plen,) = struct.unpack_from("<I", raw, 4)
    (cpb,) = struct.unpack_from("<I", raw, 8)
    assert cpb == chunk and raw[-4:] == b"\xde\xad\xbe\xef"
    payload2 = raw[12:12 + plen]
    assert plen == len(payload2)
    assert payload2 == payload, "正文与预期字节不一致"
    # 题解视角：每 2 字节 + 1 个 00 分隔 → 把分隔符去掉就是原文（奇数字长也成立）
    got = b"".join(payload2.split(b"\x00")).decode("utf-8")
    assert got == cfg["flag"], "拼回的 flag 不一致：%r" % (got,)
    # 默认阈值 4 必须捞不到东西（本题考点）；阈值降到 2 能逐组吐出来
    assert _strings(raw, 4) == [], "strings 默认阈值不该有输出"
    assert len(_strings(payload2, 2)) == sum(1 for g in groups if len(g) >= cpb)
    print("    [hello-hexdump] %d B，%d 组 × %d 字节，flag=%s"
          % (len(raw), len(groups), cpb, got))
    return p


# ===========================================================================
# 3) magic-bytes-ctf —— 两个装成 .docx / .txt 的 JPEG
# ===========================================================================
def _white_bottom(im, thr=235):
    """从底往上扫，返回第一行「整行都近白」的 y（底部白边的起点）"""
    a = np.asarray(im.convert("RGB"), dtype=np.uint8)
    rowmin = a.min(axis=(1, 2))
    y = im.height - 1
    while y > 0 and rowmin[y] > thr:
        y -= 1
    return y + 1


def build_magic_bytes(cfg):
    """ikun.docx(JPEG 1156x1289，flag 画在底部白边) + mjq.txt(JPEG 1151x1103 对照样本)"""
    flag = cfg["flag"]
    base = os.path.join(ASSETS, "ikun_base.jpg")
    im = Image.open(base)
    im.load()
    im = im.convert("RGB")
    W, H = im.size

    top = _white_bottom(im)
    band = H - top
    font, bb = _fit_font(flag, W - 100, band - 30, start=160, path=FONT_MONO_BOLD)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]

    d = ImageDraw.Draw(im)
    d.rectangle([0, top, W - 1, H - 1], fill=(255, 255, 255))
    x = (W - tw) // 2 - bb[0]
    y = top + (band - th) // 2 - bb[1]
    d.text((x, y), flag, font=font, fill=(0, 0, 0))

    out1 = os.path.join(_files(cfg), "ikun.docx")
    im.save(out1, "JPEG", quality=92, optimize=True, subsampling=0, progressive=False)

    out2 = os.path.join(_files(cfg), "mjq.txt")
    _write(out2, open(os.path.join(ASSETS, "mjq_base.jpg"), "rb").read())

    # ---- 自检：当成图片解码（不看扩展名），再按像素模板把字反解出来 ----
    head = open(out1, "rb").read(16)
    assert head[:4] == b"\xff\xd8\xff\xe0" and head[6:11] == b"JFIF\x00", "不是 JFIF"
    im2 = Image.open(out1)
    im2.load()
    assert im2.format == "JPEG" and im2.size == (W, H), im2.format
    iou, ink_img, ink_ref = _assert_text_present(im2, flag, font, (x, y))
    im3 = Image.open(out2)
    im3.load()
    assert im3.format == "JPEG" and im3.size == (1151, 1103), im3.format
    assert flag.encode() not in open(out2, "rb").read(), "mjq 里不该有明文 flag"
    print("    [magic-bytes] ikun %d B %dx%d 字号 %d，IoU=%.3f（墨量 %d/%d）；"
          "mjq %d B %dx%d" % (os.path.getsize(out1), W, H, font.size, iou, ink_img, ink_ref,
                              os.path.getsize(out2), im3.width, im3.height))
    return [out1, out2]


# ===========================================================================
# 4) png-ihdr-ctf —— IHDR 高度写小 + 重算 CRC，flag 画在隐藏区
# ===========================================================================
IHDR_REAL_H = 1716
IHDR_FAKE_H = 1400
IHDR_PLATE = (1490, 1574, 40, 1104)      # y0, y1, x0, x1


def _png_blocks(data):
    i, out, idat = 8, [], b""
    while i + 8 <= len(data):
        (n,) = struct.unpack_from(">I", data, i)
        t = bytes(data[i + 4:i + 8])
        out.append((t, n, i))
        if t == b"IDAT":
            idat += bytes(data[i + 8:i + 8 + n])
        if t == b"IEND":
            break
        i += 12 + n
    return out, idat


def _png_set_height(data, h, fix_crc=True):
    """IHDR.height 在偏移 20；IHDR 的 CRC 在偏移 29 = crc32(类型+数据)"""
    b = bytearray(data)
    struct.pack_into(">I", b, 20, h)
    if fix_crc:
        struct.pack_into(">I", b, 29, zlib.crc32(bytes(b[12:29])) & 0xFFFFFFFF)
    return bytes(b)


def build_png_ihdr(cfg):
    """files/第六题.png：1144x1716 的海报，IHDR 高度改成 1400，flag 画在 1490~1574 的暗条上"""
    flag = cfg["flag"]
    im = Image.open(os.path.join(ASSETS, "ihdr_base.png"))
    im.load()
    im = im.convert("RGB")
    W, H = im.size
    assert (W, H) == (1144, IHDR_REAL_H), im.size

    y0, y1, x0, x1 = IHDR_PLATE
    font, bb = _fit_font(flag, (x1 - x0) - 60, (y1 - y0) - 18, start=64, path=FONT_MONO_BOLD)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]

    d = ImageDraw.Draw(im)
    d.rectangle([x0, y0, x1, y1], fill=(14, 14, 20))
    x = x0 + ((x1 - x0) - tw) // 2 - bb[0]
    y = y0 + ((y1 - y0) - th) // 2 - bb[1]
    d.text((x, y), flag, font=font, fill=(255, 255, 255))

    buf = io.BytesIO()
    im.save(buf, "PNG", optimize=True)
    png = _png_set_height(buf.getvalue(), IHDR_FAKE_H, fix_crc=True)
    out = _write(os.path.join(_files(cfg), "第六题.png"), png)

    # ---- 自检：按题解用 zlib 长度反推高度 → 修高度 → 图上把字反解出来 ----
    raw = open(out, "rb").read()
    assert raw[:8] == PNG_SIG
    (decl_h,) = struct.unpack_from(">I", raw, 20)
    assert decl_h == IHDR_FAKE_H, decl_h
    blocks, idat = _png_blocks(raw)
    w, hh, bd, ct = struct.unpack(">IIBB", raw[16:26])
    bpp = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ct] * (bd // 8)
    stride = w * bpp + 1
    n = len(zlib.decompress(idat))
    real_h, rem = divmod(n, stride)
    assert rem == 0 and real_h == IHDR_REAL_H, (n, stride, real_h, rem)
    # 未修高度时 Pillow 只能看到 1400 行
    assert Image.open(io.BytesIO(raw)).size == (1144, IHDR_FAKE_H)
    fixed = _png_set_height(raw, real_h, fix_crc=True)
    im2 = Image.open(io.BytesIO(fixed))
    im2.load()
    assert im2.size == (1144, IHDR_REAL_H)
    iou, ink_img, ink_ref = _assert_text_present(im2, flag, font, (x, y), dark_on_light=False)
    assert y > IHDR_FAKE_H, "flag 没落在隐藏区：y=%d" % y
    print("    [png-ihdr] %d B｜IHDR 声明 1144x%d｜IDAT 反推 %d 行（余 %d）｜"
          "flag 在 y=%d（隐藏区）｜IoU=%.3f" % (len(raw), decl_h, real_h, rem, y, iou))
    return out


# ===========================================================================
# 5) png-hidden-qr-ctf —— 前半段画在图里，后半段是 IEND 后的空白二维码
# ===========================================================================
QR_MODULE_PX = 4
QR_SPLIT = 19           # 图上画 flag[:19]，其余进二维码（与原题的分法一致）


def _qr_tail_bytes(text):
    """二维码矩阵 → 每模块 4px → 空格=黑 / Tab=白，行优先拼成字节串"""
    import qrcode
    from qrcode.constants import ERROR_CORRECT_L
    q = qrcode.QRCode(error_correction=ERROR_CORRECT_L, box_size=1, border=0)
    q.add_data(text)
    q.make(fit=True)
    m = q.get_matrix()                     # True = 黑模块
    N = len(m)
    out = bytearray()
    for row in m:
        line = b"".join((b" " if v else b"\t") * QR_MODULE_PX for v in row)
        out += line * QR_MODULE_PX
    return bytes(out), N


def build_png_hidden_qr(cfg):
    """files/release.png：500x419（真实 500x500，IHDR 的 CRC 故意不修）+ IEND 后二维码尾"""
    flag = cfg["flag"]
    k = QR_SPLIT if len(flag) - QR_SPLIT >= 4 else max(1, len(flag) // 2)   # 太短就对半切
    head_s, tail_s = flag[:k], flag[k:]
    assert head_s and tail_s, "flag 太短，切不出两半"

    im = Image.open(os.path.join(ASSETS, "qr_base.png"))
    im.load()
    im = im.convert("RGBA")                # 保持 colortype=6（题解按 4 字节/像素推高度）
    W, H = im.size
    d = ImageDraw.Draw(im)

    f_big, bb = _fit_font(head_s, 470, 46, start=46, path=FONT_CN_BOLD)
    x = 19 - bb[0]
    y = 452 - bb[1]                        # 整块落在 y=452~497（全部在隐藏区里）
    d.text((x, y), head_s, font=f_big, fill=(0, 0, 0))

    note = "还有一半flag 在文件末尾"          # 提示行：右对齐放在前半段上方
    f_small = _font(FONT_CN, 18)
    nb = f_small.getbbox(note)
    nx = 498 - (nb[2] - nb[0]) - nb[0]
    ny = 424 - nb[1]
    d.text((nx, ny), note, font=f_small, fill=(0, 0, 0))

    buf = io.BytesIO()
    im.save(buf, "PNG", optimize=True)
    png = _png_set_height(buf.getvalue(), 419, fix_crc=False)     # CRC 故意写坏
    tail, N = _qr_tail_bytes(tail_s)
    out = _write(os.path.join(_files(cfg), "release.png"), png + tail)

    # ---- 自检 ----
    raw = open(out, "rb").read()
    (decl_h,) = struct.unpack_from(">I", raw, 20)
    assert decl_h == 419
    stored = struct.unpack_from(">I", raw, 29)[0]
    calc = zlib.crc32(raw[12:29]) & 0xFFFFFFFF
    assert stored != calc, "IHDR 的 CRC 应该被写坏（本题 Step1 的判定依据）"
    iend = raw.rfind(b"IEND")
    assert iend > 0
    got_tail = raw[iend + 8:]
    assert len(got_tail) == (QR_MODULE_PX * N) ** 2, "尾部字节数与矩阵不符"
    assert set(got_tail) == {0x20, 0x09}, "尾部只允许空格/Tab"

    # 二维码：重排成位图 → 与 qrcode 生成矩阵逐模块比对 → 再用 OpenCV 独立解一遍
    side = QR_MODULE_PX * N
    img = np.zeros((side, side), np.uint8)
    for i, b in enumerate(got_tail):                    # 行优先：numpy 下标是 [row, col]
        img[i // side, i % side] = 0 if b == 0x20 else 255
    mods = (img.reshape(N, QR_MODULE_PX, N, QR_MODULE_PX).mean(axis=(1, 3)) < 128)
    q = __import__("qrcode").QRCode(error_correction=__import__("qrcode.constants",
                    fromlist=["ERROR_CORRECT_L"]).ERROR_CORRECT_L, box_size=1, border=0)
    q.add_data(tail_s)
    q.make(fit=True)
    assert (np.array(q.get_matrix()) == mods).all(), "二维码矩阵与预期不一致"
    try:
        import cv2
        big = cv2.resize(img, (side * 4, side * 4), interpolation=cv2.INTER_NEAREST)
        qz = 4 * QR_MODULE_PX
        canvas = np.full((big.shape[0] + 2 * qz, big.shape[1] + 2 * qz), 255, np.uint8)
        canvas[qz:qz + big.shape[0], qz:qz + big.shape[1]] = big
        txt, _pts, _ = cv2.QRCodeDetector().detectAndDecode(canvas)
        assert txt == tail_s, "OpenCV 解出的二维码内容不一致：%r" % (txt,)
    except ImportError:
        txt = "(cv2 未安装，跳过独立解码)"

    # 图上的前半段
    fixed = _png_set_height(raw, 500, fix_crc=True)
    fixed = fixed[:iend] + fixed[iend:]            # 保持尾部不影响解码
    im2 = Image.open(io.BytesIO(fixed))
    im2.load()
    assert im2.size == (500, 500), im2.size
    iou, ink_img, ink_ref = _assert_text_present(im2, head_s, f_big, (x, y))
    joined = head_s + tail_s
    assert joined == flag, "两半拼不回原 flag"
    print("    [png-hidden-qr] %d B｜IHDR 声明 500x419（CRC 坏）｜二维码 %d×%d 模块 = %dpx"
          "｜尾部 %d B｜前半段 IoU=%.3f｜QR=%r"
          % (len(raw), N, N, side, len(got_tail), iou, txt))
    return out


# ===========================================================================
# 6) pcap-file-read-ctf —— 改写 HTTP 响应体里的 PBE 密文
# ===========================================================================
PBE_PASSWORD = "SePassWordLen23SUCT"     # /proc/self/cmdline 里读到的启动参数
PBE_ITERS = 1000
PBE_ALGO = "PBEWithMD5AndDES"

# ---------------- PBES1（Java PBEWithMD5AndDES） ----------------
def pbe_encrypt(plain, password, salt, iters=PBE_ITERS):
    from Crypto.Cipher import DES
    key = hashlib.md5(password.encode() + salt).digest()
    for _ in range(iters - 1):
        key = hashlib.md5(key).digest()
    key = key[:8]
    pad = 8 - (len(plain) % 8)
    data = plain + bytes([pad]) * pad              # PKCS#5
    return salt + DES.new(key, DES.MODE_CBC, key).encrypt(data)


def pbe_decrypt(blob, password, iters=PBE_ITERS):
    from Crypto.Cipher import DES
    salt, ct = blob[:8], blob[8:]
    key = hashlib.md5(password.encode() + salt).digest()
    for _ in range(iters - 1):
        key = hashlib.md5(key).digest()
    key = key[:8]
    pt = DES.new(key, DES.MODE_CBC, key).decrypt(ct)
    pad = pt[-1]
    assert 0 < pad <= 8 and pt[-pad:] == bytes([pad]) * pad, "PKCS#5 填充不对"
    return pt[:-pad]


# ---------------- pcapng / 以太 / IP / TCP ----------------
def _pcapng_chunks(d):
    chunks, off = [], 0
    while off + 8 <= len(d):
        t, ln = struct.unpack_from("<II", d, off)
        chunks.append((t, ln, off))
        off += ln
    assert off == len(d), "pcapng 块长没有铺满文件"
    return chunks


def _l4(data):
    """Ethernet+IPv4+TCP → dict（含走位与校验和状态）"""
    if len(data) < 34 or data[12:14] != b"\x08\x00":
        return None
    ip = data[14:]
    if ip[9] != 6:
        return None
    ihl = (ip[0] & 15) * 4
    tot = struct.unpack_from("!H", ip, 2)[0]
    tcp = ip[ihl:tot]
    if len(tcp) < 20:
        return None
    doff = (tcp[12] >> 4) * 4
    sport, dport, seq, ack = struct.unpack_from("!HHII", tcp, 0)
    return dict(eth=data[:14], ip=ip[:ihl], src=ip[12:16], dst=ip[16:20],
                tot=tot, ihl=ihl, tcp=tcp[:doff], sport=sport, dport=dport,
                seq=seq, ack=ack, flags=tcp[13], payload=tcp[doff:])


def _cksum(data):
    if len(data) % 2:
        data += b"\x00"
    s = 0
    for i in range(0, len(data), 2):
        s += (data[i] << 8) + data[i + 1]
    while s >> 16:
        s = (s & 0xFFFF) + (s >> 16)
    return (~s) & 0xFFFF


def _patch_frame(old_frame, rec, payload=None, seq=None, ack=None):
    """保留原包的以太/IP/TCP 头字段（ident、ttl、tos、窗口、选项都不动），
    只替换载荷并把 IP 总长、IP 校验和、TCP 校验和算对；需要时顺带挪 seq/ack。"""
    ip_hdr = bytearray(rec["ip"])
    tcp_hdr = bytearray(rec["tcp"])
    if seq is not None:
        struct.pack_into("!I", tcp_hdr, 4, seq & 0xFFFFFFFF)
    if ack is not None:
        struct.pack_into("!I", tcp_hdr, 8, ack & 0xFFFFFFFF)
    pl = rec["payload"] if payload is None else payload

    tcp = bytes(tcp_hdr) + pl
    t = bytearray(tcp)
    t[16:18] = b"\x00\x00"
    pseudo = rec["src"] + rec["dst"] + struct.pack("!BBH", 0, 6, len(t))
    t[16:18] = struct.pack("!H", _cksum(pseudo + bytes(t)))

    struct.pack_into("!H", ip_hdr, 2, len(ip_hdr) + len(t))
    struct.pack_into("!H", ip_hdr, 10, 0)
    struct.pack_into("!H", ip_hdr, 10, _cksum(bytes(ip_hdr)))
    return old_frame[:14] + bytes(ip_hdr) + bytes(t)


def _epb(iface, tsh, tsl, frame, orig_len=None):
    cap = len(frame)
    pad = (-cap) % 4
    body = frame + b"\x00" * pad
    total = 28 + cap + pad + 4
    return (struct.pack("<II", 6, total)
            + struct.pack("<IIIII", iface, tsh, tsl, cap, orig_len if orig_len else cap)
            + body + struct.pack("<I", total))


def _find_output_packet(chunks, d):
    """定位响应体里带 OUTPUT= 的那个 EPB"""
    for idx, (t, ln, off) in enumerate(chunks):
        if t != 6:
            continue
        cap = struct.unpack_from("<I", d, off + 20)[0]
        rec = _l4(d[off + 28: off + 28 + cap])
        if not rec:
            continue
        pl = rec["payload"]
        if pl.startswith(b"HTTP/1.1 200") and b"OUTPUT=" in pl:
            return idx, off, ln, rec
    raise AssertionError("基准 pcapng 里没找到 OUTPUT 响应包")


def build_pcap_file_read(cfg):
    """files/SU_Checkin.pcapng：把 application.properties 响应体里的 PBE 密文换成新 flag 的"""
    flag = cfg["flag"]
    base = open(os.path.join(ASSETS, "pcap_base.pcapng"), "rb").read()
    chunks = _pcapng_chunks(base)
    idx, off, ln, rec = _find_output_packet(chunks, base)

    # 基准包自证：口令 / 迭代次数 / 算法确实能解开原密文
    old_payload = rec["payload"]
    head, _, old_body = old_payload.partition(b"\r\n\r\n")
    m = re.search(rb"OUTPUT=([A-Za-z0-9+/=]+)", old_body)
    assert m, "基准包里的 OUTPUT 行不见了"
    old_flag = pbe_decrypt(base64.b64decode(m.group(1)), PBE_PASSWORD)
    assert old_flag.startswith(b"flag{") and old_flag.endswith(b"}"), old_flag

    salt = base64.b64decode(m.group(1))[:8]              # 沿用基准的 salt：
    blob = pbe_encrypt(flag.encode(), PBE_PASSWORD, salt)  # 同 flag → 同密文
    b64 = base64.b64encode(blob)

    new_body = re.sub(rb"OUTPUT=[A-Za-z0-9+/=]+", b"OUTPUT=" + b64, old_body, count=1)
    assert new_body != old_body or flag.encode() == old_flag
    new_head = re.sub(rb"(?im)^Content-Length:\s*\d+",
                      b"Content-Length: " + str(len(new_body)).encode(), head, count=1)
    new_payload = new_head + b"\r\n\r\n" + new_body
    delta = len(new_payload) - len(old_payload)

    tsh, tsl = struct.unpack_from("<II", base, off + 12)
    iface = struct.unpack_from("<I", base, off + 8)[0]
    old_frame = base[off + 28: off + 28 + struct.unpack_from("<I", base, off + 20)[0]]
    frame = _patch_frame(old_frame, rec, payload=new_payload)

    out = bytearray()
    for i, (t, ln_i, off_i) in enumerate(chunks):
        if i < idx:
            out += base[off_i: off_i + ln_i]
        elif i == idx:
            out += _epb(iface, tsh, tsl, frame)
        else:
            raw = base[off_i: off_i + ln_i]
            if t == 6 and delta:
                raw = _shift_packet(raw, rec, delta)
            out += raw

    p = _write(os.path.join(_files(cfg), "SU_Checkin.pcapng"), bytes(out))

    # ---- 自检 ----
    chk = open(p, "rb").read()
    chunks2 = _pcapng_chunks(chk)                       # 块长必须铺满文件
    assert len(chunks2) == len(chunks)
    idx2, off2, ln2, rec2 = _find_output_packet(chunks2, chk)
    h2, _, b2 = rec2["payload"].partition(b"\r\n\r\n")
    cl = int(re.search(rb"(?im)^Content-Length:\s*(\d+)", h2).group(1))
    assert cl == len(b2), "Content-Length 与实际正文不符"
    got = pbe_decrypt(base64.b64decode(re.search(rb"OUTPUT=([A-Za-z0-9+/=]+)", b2).group(1)),
                      PBE_PASSWORD)
    assert got.decode() == flag, "解密结果不一致：%r" % (got,)

    # 结构自洽：IP 总长 / TCP 校验和 / 流内 seq 连续
    bad_ip = bad_ck = 0
    for t, lni, offi in chunks2:
        if t != 6:
            continue
        _if, _tsh, _tsl, cap, orig = struct.unpack_from("<IIIII", chk, offi + 8)
        assert lni == 28 + cap + (-cap) % 4 + 4, "EPB 块长与 cap_len 不符"
        fr = chk[offi + 28: offi + 28 + cap]
        r = _l4(fr)
        if not r:
            continue
        # 以太网最小帧会被补到 60 字节，所以只要求 IP 总长 <= 抓到的字节数
        if r["tot"] > len(fr) - 14:
            bad_ip += 1
        tcp = bytearray(r["tcp"]) + r["payload"]
        ck = struct.unpack_from("!H", tcp, 16)[0]
        tcp[16:18] = b"\x00\x00"
        pseudo = r["src"] + r["dst"] + struct.pack("!BBH", 0, 6, len(tcp))
        if _cksum(pseudo + bytes(tcp)) != ck:
            bad_ck += 1
    assert bad_ip == 0 and bad_ck == 0, (bad_ip, bad_ck)
    _assert_flow_seq(chk, chunks2, rec2)
    base_bad = _flow_ack_mismatch(base, chunks, rec)
    out_bad = _flow_ack_mismatch(chk, chunks2, rec2)
    assert out_bad == base_bad, "ACK 与对端字节位置不一致：%d（基准 %d）" % (out_bad, base_bad)

    print("    [pcap-file-read] %d B（基准 %d B，Δ=%+d）｜OUTPUT 密文 %d B → %d B b64｜"
          "IP/校验和/seq 全部自洽" % (len(chk), len(base), len(chk) - len(base),
                                  len(m.group(1)), len(b64)))
    return p


def _shift_packet(raw, target, delta):
    """同一个 TCP 连接里，被改包之后的包要同步挪 seq/ack，否则 Wireshark 会判乱序"""
    t, ln = struct.unpack_from("<II", raw, 0)
    if t != 6:
        return raw
    iface, tsh, tsl, cap, orig = struct.unpack_from("<IIIII", raw, 8)
    frame = raw[28:28 + cap]
    r = _l4(frame)
    if not r:
        return raw
    srv, cli = target["src"], target["dst"]
    sport, dport = target["sport"], target["dport"]
    forward = (r["src"] == srv and r["dst"] == cli and r["sport"] == sport and r["dport"] == dport)
    reverse = (r["src"] == cli and r["dst"] == srv and r["sport"] == dport and r["dport"] == sport)
    if not (forward or reverse):
        return raw
    if forward:                                  # 服务器 → 客户端：数据变长，后续 seq 往后挪
        frame = _patch_frame(frame, r, seq=r["seq"] + delta)
    else:                                        # 客户端 → 服务器：它 ACK 的是服务端的字节，ack 跟着挪
        frame = _patch_frame(frame, r, ack=r["ack"] + delta)
    return _epb(iface, tsh, tsl, frame, orig)


def _flow_packets(d, chunks, target):
    """按文件顺序取出目标连接两个方向的包，附带方向标记"""
    srv, cli, sport, dport = target["src"], target["dst"], target["sport"], target["dport"]
    out = []
    for t, ln, off in chunks:
        if t != 6:
            continue
        cap = struct.unpack_from("<I", d, off + 20)[0]
        r = _l4(d[off + 28: off + 28 + cap])
        if not r:
            continue
        if r["src"] == srv and r["dst"] == cli and r["sport"] == sport and r["dport"] == dport:
            out.append(("srv", r))
        elif r["src"] == cli and r["dst"] == srv and r["sport"] == dport and r["dport"] == sport:
            out.append(("cli", r))
    return out


def _flow_ack_mismatch(d, chunks, target):
    """统计「ACK 指向的字节位置 != 对端已发送到的位置」的包数。

    改包长之后这里必须和基准完全相等 —— 只查 seq 连续性查不出 ACK 没跟着挪。
    """
    pkts = _flow_packets(d, chunks, target)
    expect = {}
    bad = 0
    for key, r in pkts:
        other = "cli" if key == "srv" else "srv"
        if r["flags"] & 0x10 and other in expect and r["ack"] != expect[other]:
            bad += 1
        adv = len(r["payload"])
        if r["flags"] & 0x02:
            adv += 1
        if r["flags"] & 0x01:
            adv += 1
        expect[key] = (r["seq"] + adv) & 0xFFFFFFFF
    return bad


def _assert_flow_seq(d, chunks, target):
    """校验目标连接两个方向的序号连续性（SYN/FIN 各占 1）"""
    srv, cli, sport, dport = target["src"], target["dst"], target["sport"], target["dport"]
    streams = {}
    for t, ln, off in chunks:
        if t != 6:
            continue
        cap = struct.unpack_from("<I", d, off + 20)[0]
        r = _l4(d[off + 28: off + 28 + cap])
        if not r:
            continue
        if r["src"] == srv and r["dst"] == cli and r["sport"] == sport and r["dport"] == dport:
            key = "srv"
        elif r["src"] == cli and r["dst"] == srv and r["sport"] == dport and r["dport"] == sport:
            key = "cli"
        else:
            continue
        streams.setdefault(key, []).append(r)
    assert streams, "目标连接没解析出来"
    for key, pkts in streams.items():
        expect = None
        for r in pkts:
            if expect is not None:
                assert r["seq"] == expect, "%s 方向 seq 断链：%d != %d" % (key, r["seq"], expect)
            adv = len(r["payload"]) + (1 if r["flags"] & 0x03 else 0) + (1 if r["flags"] & 0x01 else 0)
            expect = (r["seq"] + adv) & 0xFFFFFFFF
    return True


# ===========================================================================
BUILDERS = {
    "misc-hello-strings":   build_misc_hello_strings,
    "misc-hello-hexdump":   build_misc_hello_hexdump,
    "magic-bytes-ctf":      build_magic_bytes,
    "png-ihdr-ctf":         build_png_ihdr,
    "png-hidden-qr-ctf":    build_png_hidden_qr,
    "pcap-file-read-ctf":   build_pcap_file_read,
}

# 各题现有 checkers 里的静态 flag（自测用；生产由 make_mapped 传变体 flag）
STATIC_FLAGS = {
    "misc-hello-strings": "flag{strings_shows_everything}",
    "misc-hello-hexdump": "flag{hex_is_just_another_view}",
    "magic-bytes-ctf":    "flag{magic_bytes_reveal_the_real_type}",
    "png-ihdr-ctf":       "flag{png_ihdr_height_holds_the_flag}",
    "png-hidden-qr-ctf":  "flag{png_height_and_trailing_whitespace_qr}",
    "pcap-file-read-ctf": "flag{pcap_arbitrary_file_read_then_pbe_decrypt}",
}


if __name__ == "__main__":
    import json
    import shutil
    import sys
    import tempfile

    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    keys = list(BUILDERS) if len(sys.argv) < 2 else sys.argv[1:]
    for k in keys:
        tmp = tempfile.mkdtemp(prefix="chk_")
        try:
            cfg = {"dir": tmp, "flag": STATIC_FLAGS[k]}
            BUILDERS[k](cfg)
            print("  %-22s OK  %s" % (k, sorted(os.listdir(os.path.join(tmp, "files")))))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
