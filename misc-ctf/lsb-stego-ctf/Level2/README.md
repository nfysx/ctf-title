# 题解：最低有效位

> 对应第三课 p14 Stego · p27 字节视角

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | LSB、图片隐写 |
| 难度 | 2 / 3 |
| 分值 | 150 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

一张真实的阳朔村庄照，flag 藏在每个像素 RGB 的最低位里。

### 附件

| 文件 | 说明 |
|---|---|
| `files/photo.png` | 附件 |

## 二、手把手解题

### Step 1｜先排除元数据和附件

```bash
exiftool files/photo.png
binwalk files/photo.png
```

两者都干净 —— 那就在**像素**里。

### Step 2｜看最低位

每个像素有 R/G/B 三个 0-255 的值。把每个值的最后一位取出来，串成一串 bit，
每 8 bit 拼一个字节 —— 这就是 LSB 隐写。

```python
from PIL import Image
im = Image.open('files/photo.png').convert('RGB')
px = im.load(); bits = []
for y in range(im.height):
    for x in range(im.width):
        r, g, b = px[x, y]
        bits += [r & 1, g & 1, b & 1]
```

拼字节时遇到 `0xFF` 就停（这是约定的结束符）。

### Step 3｜工具也可以

```bash
zsteg files/photo.png      # 会自动试各种位平面
stegsolve                    # 图形界面，切 Bit Plane
```


## 三、为什么能成（原理）

改掉最低位，颜色只变化 1/255 —— 人眼完全看不出，但信息已经存进去了。
一张 640×400 的图有 768000 个 RGB 字节，理论能藏 96 KB 文本。

## 四、怎么防（延伸）

隐写检测（steganalysis）靠统计特征：LSB 平面应当接近随机噪声，若呈现规整结构就说明被写过。
社交平台通常会**重压缩图片**，这会破坏 LSB —— 这也是平台方顺手做的一层防护。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{lsb_1s_1n_th3_l4st_b1t}`。
