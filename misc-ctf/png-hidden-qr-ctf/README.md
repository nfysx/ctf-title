# 图里有两半

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | PNG 分块结构（IHDR/IDAT/IEND）、高度篡改、CRC32 校验、文件尾部空白隐写（空格/Tab）、二维码识别 |
| 类型 | Misc · 图片隐写 |
| 难度 | 2 / 5 |
| 附件 | `release.png`（60,222 B） |
| Flag | `flag{png_height_and_trailing_whitespace_qr}` |

## 题面（直接复制到平台「简介」）

神秘人入侵了公司某系统，并留下了一张图片 `release.png`：它打开只有上半截画面，下半截像是被裁掉了，但文件本身能正常显示、不报错。目标：把被裁掉的部分还原出来，flag 分两半放着。

**提示**：尺寸是文件里声明出来的，解码器实际解出的数据量才是事实。

## 三、解题步骤

### Step 1｜先看它为什么"打不开"

用看图工具或 Pillow 打开会直接失败：

```bash
python -c "from PIL import Image; Image.open('release.png')"
# UnidentifiedImageError: cannot identify image file
```

这不是文件损坏那么简单：逐块核对 CRC 就能看出是谁被动过：

```python
import struct, zlib
d = open('release.png','rb').read()
i = 8
while i < len(d):
    ln = struct.unpack('>I', d[i:i+4])[0]; typ = d[i+4:i+8]
    stored = struct.unpack('>I', d[i+8+ln:i+12+ln])[0]
    calc   = zlib.crc32(d[i+4:i+8+ln]) & 0xffffffff
    print(typ.decode(), ln, 'CRC OK' if stored == calc else 'CRC BAD')
    i += 12 + ln
    if typ == b'IEND': break
```

只有 `IHDR` 的 CRC 是坏的 → **有人改过宽高但没重算 CRC**。

### Step 2｜用 IDAT 长度反推真实高度

解码器只认 IHDR 里的宽高，IDAT 里没有行号，多余的行会被直接丢掉。所以先用解压后的字节数当尺子：

```python
import struct, zlib
d = open('release.png','rb').read()
i, idat = 8, b''
while i < len(d):
    ln = struct.unpack('>I', d[i:i+4])[0]; typ = d[i+4:i+8]
    if typ == b'IDAT': idat += d[i+8:i+8+ln]
    i += 12 + ln
    if typ == b'IEND': break

W, COLORTYPE, BITDEPTH = 500, 6, 8
bpp    = 4                      # colortype 6 = RGBA，每像素 4 字节
stride = W*bpp + 1              # +1 是每行的滤波字节
raw = zlib.decompress(idat)
print(len(raw), '÷', stride, '=', len(raw)//stride)   # 1000500 ÷ 2001 = 500
```

**真实高度是 500，IHDR 却写着 419**，被裁掉的 81 行就是藏东西的地方。

### Step 3｜改回高度并重算 CRC

```python
import struct, zlib
d = bytearray(open('release.png','rb').read())
d[20:24] = struct.pack('>I', 500)                                  # 改 height
d[29:33] = struct.pack('>I', zlib.crc32(bytes(d[12:29])) & 0xffffffff)  # 重算 IHDR 的 CRC
open('fixed.png','wb').write(bytes(d))
```

打开 `fixed.png`，图片底部露出前半段：`flag{png_height_and`，旁边一行小字：「另一半在文件末尾的空白里」。

### Step 4｜尾部 10,000 字节的空白

IEND 之后还跟着 **10,000 字节**，并且只有两种字节值：

```python
d = open('fixed.png','rb').read()
trail = d[d.rfind(b'IEND')+8:]
print(len(trail), sorted(set(trail)))   # 10000 [9, 32]
```

`0x20`（空格）和 `0x09`（Tab），这就是空白隐写。10,000 = **100 × 100**，正好是个正方形：
把空格当黑、Tab 当白按 100 列排开，就是一张 **25 × 25 模块（4 像素/模块）的二维码**。

```python
from PIL import Image
img = Image.new('1', (100, 100)); px = img.load()
for i, b in enumerate(trail):
    px[i % 100, i // 100] = 0 if b == 0x20 else 1     # 空格=黑
img.resize((400, 400), Image.NEAREST).save('qr.png')
```

扫码（或按 QR 规范手工解：定位符 → 读格式信息得掩码 → 反掩码 → 之字形取字节段）得到：

`_trailing_whitespace_qr}`

### Step 5｜拼起来

`flag{png_height_and` + `_trailing_whitespace_qr}` = **`flag{png_height_and_trailing_whitespace_qr}`**

## 四、原理

| 机制 | 说明 |
|---|---|
| 尺寸只有一个来源 | 解码器只读 IHDR 的 width/height；IDAT 是按行连续压缩的码流，没有行号也没有总行数。把 height 改小，多出来的行就是"不可见"的 |
| zlib 长度就是尺子 | 非隔行 PNG 解压后的字节数恒等于 `height × (width×bpp + 1)`，bpp 由 colortype + bitdepth 唯一决定（本图 RGBA/8 → 4）。除一下就能反推真实高度 |
| CRC32 是门锁不是检测器 | IHDR 的 CRC 覆盖"类型名 + 数据"，改了 height 不重算，整个文件直接不可识别：失败发生在解码之前，不会提示"哪里被改过" |
| 空白隐写 | 空格/Tab 在渲染上几乎不可见，却是合法可区分的字符；按位读出来就是一串二进制。载体可以是文件尾部、行尾、HTML 注释 |
| 二维码 | 10000 = 100×100 的整数约束直接提示"这是个位图"；4 像素/模块使 25 模块的 Version 2 铺满 100 像素 |

## 五、防御建议

- **不要把"文件头里的字段"当可信**：上传/转发的图片一律用完整解码 → 重新编码（re-encode），而不是只读文件头或拷贝字节流。
- **校验要和重算同时做**：任何"改一个字段"的操作都必须连带更新 CRC/哈希，否则等于给攻击者留了个"这里被动过"的标记。
- **隐写检测**：对可疑文件做尾部/行尾的字符频率分析、位平面分析；生产环境更简单：剥掉元数据与 EOF 之后的全部字节。
- 本题这类"图片里藏东西"的载体本身无害，真正的风险是同一套手法被用在**配置文件、日志、工单附件**里偷渡数据。

## 六、附件清单

| 文件 | 大小 | SHA-256 |
|---|---|---|
| `release.png` | 60,222 B | `41b54102a447322daa9a631200e30a7f6cb14b975a14ce82af7b703575b301e7` |

---


