# 题解：被打码的照片

> 对应第三课 p37 图片题顺序 · p14 隐写（容器类）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | EXIF、缩略图、IFD1 |
| 难度 | 2 / 3 |
| 分值 | 180 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

一张真实的故宫铜龟照，主图被打码了，但相机顺手存的一张缩略图没打码。

### 附件

| 文件 | 说明 |
|---|---|
| `files/photo.jpg` | 附件 |

## 二、手把手解题

### Step 1｜先确认主图真的没东西

```bash
xxd files/photo.jpg | head -3
# 能看到 Exif 标志
```

### Step 2｜看 IFD1

JPEG 的 EXIF 里有两条 IFD 链：IFD0 是主信息，**IFD1 是缩略图**。
IFD1 里两个关键标签：`0x0201 ThumbnailOffset`（缩略图从哪开始）、`0x0202 ThumbnailLength`（多长）。

一条命令直接取：
```bash
exiftool -b -ThumbnailImage files/photo.jpg > thumb.jpg
```

### Step 3｜打开缩略图

`thumb.jpg` 里就是 flag。


## 三、为什么能成（原理）

相机/手机为了在相册里快速预览，会把一张小图塞进 EXIF（IFD1）。
**对主图做的任何处理（打码、裁剪、加水印）都不会自动同步到缩略图** —— 
所以「打了码」的图常常还能从缩略图里看到原貌。这是真实存在过的隐私事故。

## 四、怎么防（延伸）

发图前不仅要打码，还要**清空元数据**（`exiftool -all=`），否则缩略图会出卖你。
社交平台一般会重压缩图片并剥离 EXIF，这正是它顺手做的一层保护。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{thumbn41l_n3v3r_l13s}`。
