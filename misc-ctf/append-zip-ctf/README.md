# 题解：图片里有个压缩包

> 对应第三课 p14 Stego · p33 附加数据

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 附加数据、binwalk、PNG 结构 |
| 难度 | 2 / 3 |
| 分值 | 150 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

一张动漫截图，能正常打开；但 IEND 之后还接着一个压缩包。

### 附件

| 文件 | 说明 |
|---|---|
| `files/cover.png` | 附件 |

## 二、手把手解题

### Step 1｜确认它真的是 PNG

```bash
file files/cover.png
xxd files/cover.png | head -2
```

### Step 2｜看尾部

```bash
xxd files/cover.png | tail -3
```

PNG 正常以 `IEND` 结尾，但这里 `IEND` 之后还有数据，且能看到 `50 4b 03 04`（`PK`，ZIP 的开头）。

### Step 3｜切出来

```bash
binwalk -e files/cover.png
# 或者手工按偏移切：
python -c "d=open('files/cover.png','rb').read();i=d.find(b'PK\x03\x04');open('inner.zip','wb').write(d[i:])"
```

### Step 4｜解压拿 flag

```bash
unzip inner.zip
cat flag.txt
```


## 三、为什么能成（原理）

很多文件格式都有**明确的结束标记**（PNG 的 `IEND`、JPEG 的 `FFD9`），解析器读完标记就不再看了。
于是「在结束标记后面追加数据」既能藏东西，又不影响原文件正常打开 —— 这就是附加数据隐写。

## 四、怎么防（延伸）

接外部文件时按格式规范校验：PNG 必须 IEND 结束、大小与像素数匹配；
上传/下载链路要检查是否存在尾随数据（`binwalk` 一扫就能发现）。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{4ppend3d_d4t4_4fter_1end}`。
