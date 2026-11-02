# 题解：注释里的悄悄话

> 对应第三课 p31 附加数据 · p27 字节视角（PNG 的块结构）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | PNG 结构、tEXt 注释块、strings |
| 难度 | 2 / 3 |
| 分值 | 180 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

照片本身没问题，flag 藏在 PNG 的 tEXt 文本块里 —— 肉眼看不见，字符串能看见。

### 附件

| 文件 | 说明 |
|---|---|
| `files/photo.png` | 附件 |

## 二、手把手解题

### Step 1｜先常规检查

```bash
file files/photo.png
exiftool files/photo.png
binwalk files/photo.png
```

图片正常、没有附加数据、EXIF 里也没有东西。

### Step 2｜看字符串

```bash
strings files/photo.png | head -20
```

会看到一行形如 `Comment` + 一串 base64（`ZmxhZ3t0M3h0X2NodW5rXzFzXzRfdGgxbm...`）。
PNG 的 tEXt 块是**不压缩的明文字段**，`strings` 直接就能扫出来。

### Step 3｜按块结构读

```python
import struct
raw = open('files/photo.png','rb').read()
off = 8
while off < len(raw):
    ln = struct.unpack_from('>I', raw, off)[0]
    typ = raw[off+4:off+8]
    if typ == b'tEXt':
        print(raw[off+8:off+8+ln])
    off += 12 + ln
    if typ == b'IEND': break
```

### Step 4｜base64 解码

```bash
python -c "import base64;print(base64.b64decode('<那串>').decode())"
```


## 三、为什么能成（原理）

PNG 除了像素数据（IDAT），还有若干**元数据块**：`tEXt`（纯文本）、`iTXt`（UTF-8）、`zTXt`（压缩）。
它们不参与显示，所以「图片看着完全正常」；但它们是**明文**存在文件里的，`strings` 一扫就露。
注意还有 `Software` / `Author` 两个干扰项 —— 别拿到就以为是答案。

## 四、怎么防（延伸）

发图前除了清 EXIF，也要清 PNG 的文本块：`exiftool -all= 图.png` 或 `pngcrush -rem text`。
很多「截图打码」的翻车，正是漏了这些看不见的字段。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{t3xt_chunk_1s_4_th1ng}`。
