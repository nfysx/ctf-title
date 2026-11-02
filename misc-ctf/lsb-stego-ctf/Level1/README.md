# 题解：红色通道的悄悄话

> 对应第三课 p14 Stego

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | LSB、单通道、图片隐写 |
| 难度 | 1 / 3 |
| 分值 | 100 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

单通道 LSB 的入门版：只动红色通道，从左上角按行读。

### 附件

| 文件 | 说明 |
|---|---|
| `files/photo.png` | 附件 |
| `files/readme.txt` | 附件 |

## 二、手把手解题

### Step 1｜按题意读红色通道

```python
from PIL import Image
im = Image.open('files/photo.png').convert('RGB')
px = im.load()
bits = []
for y in range(im.height):
    for x in range(im.width):
        bits.append(px[x, y][0] & 1)
```

### Step 2｜8 位拼一个字符

```python
out = bytearray()
for i in range(0, len(bits) - 7, 8):
    v = 0
    for k in range(8):
        v = (v << 1) | bits[i + k]
    if v == 0:
        break
    out.append(v)
print(out.decode())
```

### Step 3｜也可以偷懒

```bash
zsteg files/photo.png
```


## 三、为什么能成（原理）

一个像素的红色值改动 1/255，肉眼完全看不出 —— 但它是**确定的比特**。
单通道版只能藏 宽×高 位（900×600 的图约 67 KB 容量），所以是入门题；
下一题把三个通道都用上，容量翻三倍，还要处理通道交织顺序。

## 四、怎么防（延伸）

社交平台重压缩会破坏 LSB。真要用隐写，先确认对方拿到的是原图（无损）。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{0n3_ch4nn3l_15_3n0ugh}`。
