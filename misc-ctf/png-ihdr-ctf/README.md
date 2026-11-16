# 被裁掉的图

## 一、题目与考点

| 项目 | 内容 |
| --- | --- |
| 考点 | PNG 文件结构（IHDR / IDAT）、用 zlib 流长度反推真实图像高度、CRC32 校验 |
| 难度 | 1 |
| 附件 | `files/第六题.png`（双击打开显示 1144×1400，3,484,307 字节） |
| Flag | `flag{png_ihdr_height_holds_the_flag}` |

## 题面（直接复制到平台「简介」）

下载到的是一张竖版海报 `files/第六题.png`，双击能正常打开，属性里写着 1144×1400。海报看起来是完整的，但排版明显没画完，底部被切掉了半截。目标：找到被切掉的那部分画面，flag 就写在里面。

**提示**：一张图需要多少行数据，可以从文件本身算出来，不必相信头里写的数字。

## 三、解题步骤

### Step 1 先确认文件的客观事实

```powershell
cd 附件
certutil -hashfile 第六题.png SHA256
```

再用 Python 读像素尺寸（Windows 控制台中文易乱码，统一加 `sys.stdout.reconfigure(encoding="utf-8")`）：

```python
from PIL import Image
im = Image.open("第六题.png")
print(im.size, im.mode)
```

输出：

```
(1144, 1400) RGB
```

### Step 2 拆块，把 IHDR 和 IDAT 分开看

PNG 是「8 字节签名 + 一串块」，每块结构固定：`长度(4) 类型(4) 数据(n) CRC(4)`，全部大端。

```python
import struct, zlib, sys
sys.stdout.reconfigure(encoding="utf-8")

data = open("第六题.png", "rb").read()
assert data[:8] == b"\x89PNG\r\n\x1a\n"

i, idat = 8, b""
while i < len(data):
    n = struct.unpack(">I", data[i:i+4])[0]
    t = data[i+4:i+8]
    if t == b"IHDR":
        ihdr = data[i+8:i+8+n]
    if t == b"IDAT":
        idat += data[i+8:i+8+n]
    i += 12 + n

w, h, bd, ct = struct.unpack(">IIBB", ihdr[:10])
print("IHDR 声明: %dx%d bitdepth=%d colortype=%d" % (w, h, bd, ct))
print("IDAT 压缩数据总长: %d" % len(idat))
```

输出：

```
IHDR 声明: 1144x1400 bitdepth=8 colortype=2
IDAT 压缩数据总长: 3483614
```

### Step 3 用 zlib 反推真实高度

IDAT 是一整条（可由多块拼接的）zlib 流，解压后是逐行的扫描线：每行开头 1 字节 filter 类型，后面是像素数据。行宽由 **宽度 × 每像素字节数** 决定，而每像素字节数只由 colortype + bitdepth 决定：

| colortype | 含义 | bitdepth=8 时每像素字节 |
| --- | --- | --- |
| 0 | 灰度 | 1 |
| 2 | 真彩色 RGB | 3 |
| 3 | 调色板索引 | 1 |
| 4 | 灰度 + Alpha | 2 |
| 6 | RGBA | 4 |

本题 colortype=2、bitdepth=8 → 每像素 3 字节，每行 `1144 × 3 + 1 = 3433` 字节。

```python
bpp = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ct] * (bd // 8)
rowbytes = w * bpp + 1
raw = zlib.decompress(idat)
print("解压后字节数: %d  每行字节: %d" % (len(raw), rowbytes))
print("真实高度: %d  (余数 %d)" % (len(raw) // rowbytes, len(raw) % rowbytes))
```

输出：

```
解压后字节数: 5891028  每行字节: 3433
真实高度: 1716  (余数 0)
```

余数为 0 说明是整除，1716 就是被藏起来的真实行数；IHDR 里写的 1400 是假的，有 316 行画面被"声明"掉了。

### Step 4 改 IHDR 高度，并重算 IHDR 的 CRC32

IHDR 数据区共 13 字节：`width(4) height(4) bitdepth(1) colortype(1) compression(1) filter(1) interlace(1)`。
height 在文件偏移 `8(签名) + 4(长度) + 4(类型) + 4(width) = 20`，4 字节大端。

块的 CRC 覆盖 **类型 + 数据**（不含长度字段），所以改完 height 必须重算。完整脚本：

```python
import struct, zlib

data = bytearray(open("第六题.png", "rb").read())
REAL_H = 1716

struct.pack_into(">I", data, 20, REAL_H)                                   # IHDR.height
struct.pack_into(">I", data, 8 + 8 + 13,
                 zlib.crc32(bytes(data[12:12 + 4 + 13])) & 0xffffffff)     # IHDR.crc = crc32(类型 + 数据)

open("fixed.png", "wb").write(bytes(data))

from PIL import Image
im = Image.open("fixed.png")
im.load()
print("修复后尺寸:", im.size)
im.crop((0, 1460, im.width, 1600)).save("flag_area.png")
```

输出：

```
修复后尺寸: (1144, 1716)
```

不重算就打不开，可以自己验证：删掉第二行 `struct.pack_into` 再跑，Pillow 直接抛 `UnidentifiedImageError: cannot identify image file`。

### Step 5 看图拿 flag

打开 `flag_area.png`，或直接看 `fixed.png` 底部：

```
flag{png_ihdr_height_holds_the_flag}
```

flag 是白色等宽字，画在 y≈1498–1554 的深色横条上，正好落在被隐藏的 316 行（y = 1400–1715）之中。

### Step 6 不写脚本的路径：TweakPNG

Windows 上的 GUI 做法：用 TweakPNG 打开附件 → 在块列表里双击 `IHDR` → 把 `Height` 从 1400 改成 1716 → `Save`。TweakPNG 保存时会自动重算被改块的 CRC，效果与 Step 4 的脚本一致。

## 四、原理

1. **PNG 的尺寸只有一个来源。** 解码器只认 IHDR 里的 width/height；IDAT 里的数据没有行号，也不带总行数。于是"IHDR 说几行就画几行，剩下的解压数据丢掉"是绝大多数实现的自然行为：藏数据不需要任何漏洞，只要把高度改小。

2. **zlib 流长度是硬约束，反过来就是尺子。** 非隔行 PNG 的解压结果长度恒等于 `height × (width × bpp + 1)`（+1 是每行 filter 字节）。width、bitdepth、colortype 都能从 IHDR 读到，IDAT 解压长度能直接算出来，真实高度由此唯一确定：`height = len(decompressed) // (width × bpp + 1)`。本题 5,891,028 ÷ 3,433 = 1,716，余 0，没有歧义。

3. **CRC32 在这里是门锁，不是检测器。** 每块的 CRC 覆盖"类型 + 数据"，libpng / Pillow 在读 IHDR 时就会校验；改了字段不重算 CRC，文件立刻变成"无法识别的图像"（实测抛 `UnidentifiedImageError`）。这个失败发生在解码之前，所以它不会给你"图片被改过但仍能显示"的提示；也正是本题文件看起来完全正常的原因：作者改高度时正确地重算了 CRC。

4. **本题的实际数据。** 原海报 1144×1716，作者把 IHDR 高度改成 1400 并重算 CRC，IDAT 仍是完整的 1716 行。可见部分（前 1400 行）与真实图像逐字节相同，所以"看起来正常"；被藏起来的 316 行里画着 flag。

## 五、防御建议

1. **不要转发"没解码过的图"。** 图片进入系统（上传、抓取、CDN 回源）后一律走「解码 → 按需缩放 → 重新编码」。重新编码只写出解码器真正看到的像素，多余的行、尾随数据、polyglot 附加物会全部消失，这是把本类隐写一次性清零的最省事做法。

2. **断言 IHDR 与 IDAT 一致。** 解析时检查 `len(zlib.decompress(idat)) == height × (width × bpp + 1)`，不整除就拒收；同时用解码后的 `im.size` 再判一次真实尺寸，因为声明值本身可以造假。

3. **校验每一块的 CRC，包括 IHDR。** 有些自研解析器为了"容错"跳过 CRC，等于把 IHDR 变成可随便伪造的字段；而 Pillow / libpng 是校验的，两种实现不一致本身就是风险面。

4. **体积限制要针对解压后数据。** 只限文件大小挡不住 zlib 炸弹，要同时限制解压总字节数与真实解码像素数。

5. **预览、缩略图、审核图都从重新编码的副本生成。** 禁止直接把用户原件透传给下游，也不要用"只改文件头"的方式做裁剪。

## 六、动态 Flag（仅 Web 题）

不适用：本题是静态附件题，flag 固定写死在图片像素里，无服务端动态下发。

## 七、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/第六题.png` | 3,484,307 字节 | `f4a67b8bac0b52b170d725463a1f9a74eb37356992e3e72871af9875e9a5a80f` |

文件属性：PNG，实际 1144×1716（IHDR 声明 1144×1400），8 bit，colortype 2（真彩色），非隔行，54 个 IDAT 块，解压后 5,891,028 字节。

---


