# 题解：抹掉之前先备份

> 对应第三课 p37 EXIF · p33 附加数据（同一个思路换了容器）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | EXIF、抹除、APP1 段、对比 |
| 难度 | 3 / 3 |
| 分值 | 220 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

有人把 EXIF 清掉了，还说「清完就干净了」。对比两张图，你会发现他清掉了别的东西。

### 附件

| 文件 | 说明 |
|---|---|
| `files/before.jpg` | 附件 |
| `files/after.jpg` | 附件 |

## 二、手把手解题

### Step 1｜先对比两张图

```bash
ls -l files/before.jpg files/after.jpg
cmp -l files/before.jpg files/after.jpg | head
```

`after.jpg` 比 `before.jpg` **小了一百多字节** —— 少的就是那段 EXIF。

### Step 2｜看 APP1 段结构

JPEG 的元数据在 `FFE1` 段里：

```
FF E1 | 长度(2字节) | 「Exif\0\0」 | TIFF 结构 ...
```

```bash
xxd files/before.jpg | head -6
```

### Step 3｜把 APP1 段整个 dump 出来

```python
raw = open('files/before.jpg','rb').read()
i = raw.find(b'\xff\xe1')
ln = int.from_bytes(raw[i+2:i+4], 'big')
blk = raw[i:i+2+ln]
open('app1.bin','wb').write(blk)
```

### Step 4｜从里面切出 ZIP

APP1 段里 `PK\x03\x04` 之后就是压缩包：

```python
off = blk.find(b'PK\x03\x04')
open('inner.zip','wb').write(blk[off:])
```

解压 → `flag.txt`。


## 三、为什么能成（原理）

APP1 段只规定了「长度 + 开头是 Exif 标记」，**中间的字节没有任何强制含义** ——
所以只要长度字段写对，就能在里面塞任意数据：ZIP、文本、甚至另一张图。
抹除 EXIF 工具的做法是「整段丢掉」，于是这些私货跟着一起消失（对比 `after.jpg`）。

**结论：抹除之前先备份，或者先 dump 出来看一眼。**

## 四、怎么防（延伸）

收到可疑图片：先 `exiftool -b -all:all` 或按 APP1 段 dump 备份，再做处理。
自己做「抹除」时，也不要以为「清掉 EXIF 就等于清干净」——
附加数据（p33）、缩略图（上一题）、LSB（p14）都不在 EXIF 里。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{3r4s3_3xif_4ft3r_dump}`。
