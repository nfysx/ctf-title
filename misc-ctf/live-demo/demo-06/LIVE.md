# 现场演示 06 · 打码了也白打

> 对应课件 **p35「图片类」· EXIF 缩略图** · 时长 **2.5 分钟** · 需要：下列任选一种：本机 exiftool / 在线 ExifTool 网页 / Python（脚本已备好）

最有说服力的一道：打码的那张是假的，没打码的那张一直在文件里。

## 台上脚本

| 时间 | 你做什么 | 屏幕上会出现 | 你说什么 |
|---|---|---|---|
| 0:00–0:40 | 打开 `files\马赛克.jpg` | 画面被一大块马赛克盖住 | 「已经打码了，安全了吧？」 |
| 0:40–1:20 | 取缩略图（三种方式任选，见下节） | 得到一个 `t.jpg` | 「我从这张图里，把另一张图拿出来了。」 |
| 1:20–2:00 | 打开 `t.jpg` | 缩略图清清楚楚，写着 flag | 「打码只改了主图，缩略图没动。」 |
| 2:00–2:30 | 收尾 | — | 「发图前先清元数据：`exiftool -all= 图.jpg`。」 |

## 取缩略图的三种方式（重要：在线版的坑）

### ❌ 最容易错的一种

在**网页版 ExifTool** 的 `Flags` 框里直接粘这一整行：

```
exiftool -b -ThumbnailImage 马赛克.jpg > t.jpg
```

会得到：

```
File not found -> 
File not found - t.jpg
```

**原因**：网页版不是 shell。它把你的输入当参数解析，于是
* `马赛克.jpg` 被当成「另一个输入文件名」→ 工作目录里没有 → `File not found`
* `>` 被当成一个文件名 token → `File not found ->`
* `t.jpg` 同理 → `File not found - t.jpg`
（重定向 `>` 是 shell 的功能，`exiftool` 本身没有这个参数）

### ✅ 网页版正确填法

1. `Flags` 框里**只填选项**：`-b -ThumbnailImage`
2. 文件用 **Browse… / 拖拽 / 粘贴** 载入（不要写进 Flags）
3. `-b` 输出的是**二进制**，"Full Text Output" 里会是乱码 —— 别慌，这是对的
4. 点 **Download Output 下载输出结果**，存成 `t.jpg`，双击打开

> 如果上传后仍报文件名错误，先把 `马赛克.jpg` 改名成 `a.jpg` 再传（某些在线工具对中文名不友好）。

### ✅ 本机命令（真 shell，`>` 才有意义）

```bash
exiftool -b -ThumbnailImage 马赛克.jpg > t.jpg
```

### ✅ 不装 exiftool 也行：本目录的 `取缩略图.py`

```bash
python 取缩略图.py
```

会把缩略图导出到同目录的 `t.jpg`（只用标准库 + Pillow）。

## 翻车预案

- `-ThumbnailImage` 报 unknown tag → 换 `-ThumbnailImage` 或 `-AllThumbnails`；老版本用 `exiftool -b -ThumbnailImage -- 马赛克.jpg`。
- 网页版下载下来的文件没有扩展名 → 手动改成 `t.jpg` 再打开。
- 实在不行用 010 Editor：搜 `FFD8` 的**第二次**出现，从那里框选到文件末尾另存。

## 答案

```
flag{thumbn41l_b3tr4y5_y0u}
```

## 自校验

```bash
python verify.py
```

## 放进平台（可选）

`checker/main.rx` 是静态 flag 版，`check()` 比对答案、`environ()` 返回空对象。
演示题一般不上平台，留着只是备用。
