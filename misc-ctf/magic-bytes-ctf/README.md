# 改错的扩展名

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 改错的扩展名 |
| 考点 | 文件头（Magic Bytes）识别；扩展名与真实类型不符时的还原 |
| 难度 | 1 |
| 附件 | `ikun.docx`、`mjq.txt`（扩展名是错的，真实类型均为 JPEG） |
| Flag | `flag{magic_bytes_reveal_the_real_type}` |

## 题面（直接复制到平台「简介」）

同事转来两个文件：`ikun.docx` 和 `mjq.txt`。一个 Word 说文件已损坏打不开，一个用记事本打开是一屏乱码，文件本身没坏，只是名字起错了。目标：判断它们到底是什么类型，把里面的 flag 找出来。

**提示**：名字可以改，文件开头的那几个字节改不了。

## 三、解题步骤

### Step 1　看文件的头几个字节

文件类型由内容决定，不由扩展名决定，所以先看头部：

```powershell
Format-Hex -Path .\ikun.docx -Count 16
```

```
0000000000000000 FF D8 FF E0 00 10 4A 46 49 46 00 01 01 00 00 01  ÿØÿà..JFIF......
```

换 Python 看两个文件（十六进制逐字节输出）：

```powershell
python -c "print(open('ikun.docx','rb').read(16).hex(' ')); print(open('mjq.txt','rb').read(16).hex(' '))"
```

```
ff d8 ff e0 00 10 4a 46 49 46 00 01 01 00 00 01
ff d8 ff e0 00 10 4a 46 49 46 00 01 01 00 00 01
```

### Step 2　对照文件头表

`FF D8 FF` 是 JPEG 的 SOI 标记，紧跟的 `4A 46 49 46` 是 ASCII `JFIF`。两个文件都是 JPEG 图片。常见文件头对照表见第四节。

顺手排除一个常见误判：`.docx` 的真实格式是 OOXML，本质是 ZIP，必须以 `50 4B 03 04`（`PK\x03\x04`）开头。这两个文件不是：

```powershell
python -c "import zipfile;zipfile.ZipFile('ikun.docx')"
```

```
zipfile.BadZipFile: File is not a zip file
```

用 PowerShell 解压同样失败（`System.IO.FileFormatException`）：

```powershell
Expand-Archive -Path .\ikun.docx -DestinationPath .\out
```

### Step 3　改扩展名，直接打开

```powershell
Copy-Item .\ikun.docx .\ikun.jpg
Copy-Item .\mjq.txt  .\mjq.jpg
```

双击 `ikun.jpg`（Windows 照片 / 任意看图软件均可）。flag 印在图片**最底部的白色区域**，黑色单行文字，字号 50px：

```
flag{magic_bytes_reveal_the_real_type}
```

实测位置与尺寸（原图 1156×1289）：文字占 x 67–1087、y 1198–1244，字高 47px，居中排布，整行清晰可读。

`mjq.jpg`（1151×1103）也是一张正常的 JPEG 图片，但图里没有 flag，它是同一批打包里的对照样本，用来确认"两个文件的处理方式完全一样"。

### Step 4　不改扩展名也能读（可选，验证"内容才是类型"）

解码器按内容嗅探，不看扩展名，所以直接拿错名字的文件去解码也能成功：

```powershell
python -c "from PIL import Image; im=Image.open('ikun.docx'); im.load(); print(im.format, im.size, im.mode)"
python -c "from PIL import Image; im=Image.open('mjq.txt'); im.load(); print(im.format, im.size, im.mode)"
```

```
JPEG (1156, 1289) RGB
JPEG (1151, 1103) RGB
```

Windows 自带的 GDI+ 解码器结论一致：

```powershell
Add-Type -AssemblyName System.Drawing
$b=[System.Drawing.Image]::FromFile("$PWD\ikun.docx"); "{0}x{1} {2}" -f $b.Width,$b.Height,$b.RawFormat
```

```
1156x1289 Jpeg
```

### Step 5　取 flag

`flag{magic_bytes_reveal_the_real_type}`

## 四、原理

### 1. 扩展名只是一张标签

Windows 双击文件时，只做一件事：拿扩展名去 `HKEY_CLASSES_ROOT` 查关联程序，然后把文件交给它。之后是否"能打开"，完全由该程序**按文件内容**解析的结果决定。扩展名不参与文件内容的任何判断，改它不会破坏一个字节的数据。

### 2. 解码器看的是魔数与结构签名

- JPEG：以 `FF D8`（SOI，Start Of Image）开始，随后是 `FF E0` 段 + `JFIF` 标识；以 `FF D9`（EOI）结束。libjpeg 找不到 SOI 就直接报"不是 JPEG 文件"。
- 本题两个文件头是 `FF D8 FF E0 00 10 4A 46 49 46`，即 `SOI + APP0(长度 0x0010) + "JFIF\0"`，是标准 JFIF 头，所以 Pillow 与 GDI+ 都能在 `.docx` / `.txt` 的名字下正常解码（见 Step 4 实测）。

### 3. 为什么 Word 说文件损坏

`.docx` 是 OOXML，容器是 ZIP，规范要求 `50 4B 03 04` 开头，内部必须有 `[Content_Types].xml`、`word/document.xml` 等条目。本题文件头是 `FF D8 FF`，Word 连 ZIP 目录都读不出来，于是报"文件已损坏，无法打开"。同理记事本把 JPEG 的二进制当文本渲染，得到乱码。

### 4. 常见文件头对照表（以下取值均在本机实测生成/抓取）

| 真实类型 | 十六进制头 | ASCII / 说明 | 本机来源 |
| --- | --- | --- | --- |
| JPEG | `FF D8 FF E0` | `\xFF\xD8\xFF\xE0`（SOI + APP0），第 7 字节起为 `JFIF` | Pillow 保存 |
| PNG | `89 50 4E 47 0D 0A 1A 0A` | `.PNG....` | Pillow 保存 |
| GIF | `47 49 46 38 37 61` | `GIF87a`（亦有 `GIF89a`） | Pillow 保存 |
| BMP | `42 4D` | `BM` | Pillow 保存 |
| ZIP / DOCX / XLSX / JAR / APK | `50 4B 03 04` | `PK\x03\x04` | zipfile 生成 |
| GZIP | `1F 8B 08` | — | gzip 生成 |
| TAR | 偏移 257 处 `75 73 74 61 72` | `ustar` | tarfile 生成 |
| PDF | `25 50 44 46 2D` | `%PDF-` | Pillow 保存 |
| PE / EXE / DLL | `4D 5A` | `MZ` | `C:\mingw64\bin\objdump.exe` |
| ELF | `7F 45 4C 46` | `\x7FELF`；objdump -f 显示 `file format elf64-x86-64` | 手工构造 64 字节 ELF 头后由 objdump 识别 |

### 5. "改扩展名即可打开"的操作要点

1. 先把文件头读成十六进制（`Format-Hex`、`xxd`、Python `read(16).hex()` 都行），别靠"看起来像"。
2. 在对照表里定位真实类型，注意有些格式要看到偏移 257（TAR）或第 4–8 字节（JPEG 的 `JFIF`）才能确认。
3. 复制一份并加上正确扩展名（`Copy-Item a.docx a.jpg`），保留原件不动，避免反复改名。
4. 图片类用看图软件直接看；压缩/文档类改回 `.zip`/`.docx` 再看。

本题走第 4 条：改成 `.jpg` 后图片正常显示，flag 直接画在图上，不需要进一步分析。

## 五、防御建议

真实世界里"扩展名 ≠ 类型"是被反复利用的入口（上传 getshell、钓鱼附件、绕过内容审查），修法如下：

1. **上传校验按内容，不按扩展名。** 服务端读前若干字节比对白名单魔数，再做一次强校验：图片用 Pillow/ImageMagick 完整解码一遍并重新编码（顺带洗掉图里的多余数据和尾部附加内容），Office/压缩包用 zip 库解析并检查必备条目，两步都通过才落盘。
2. **落盘时重命名。** 存储名用随机串 + 由**嗅探结果**决定的扩展名，用户提供的原文件名只入库不进路径，避免 `.php.jpg`、`..%2f` 之类绕过。
3. **回显时用服务端嗅探出的 `Content-Type`**，非图片/媒体一律 `Content-Disposition: attachment`，禁止浏览器按客户端声明去渲染。
4. **文件名规范化**：拒绝多扩展名、末尾空白与点、Unicode 双向控制符（RLO）和全角点（`evil。jpg.exe`）。
5. **客户端侧**：Windows 默认"隐藏已知文件类型的扩展名"，正好让 `发票.pdf.jpg` 这类双扩展名伪装成立；在资源管理器 → 查看里取消该选项，是成本最低的一道防线。

## 六、动态 Flag

不适用。本题为非 Web 题，flag 静态固定在附件 `ikun.docx`（实为 JPEG）内，没有服务端下发逻辑。

## 七、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/ikun.docx` | 139,310 B | `af2e26fc2d45090d46269173f0c5acaf419b0cacf1510f8e9fde3b0bdcd739df` |
| `files/mjq.txt` | 322,176 B | `d95da5e57b5f9a103f1832de9146a961e25cfee0100497cce550b68425a4c539` |

两个文件的头部均为 `FF D8 FF E0 00 10 4A 46 49 46`（JPEG/JFIF），结尾均为 `FF D9`，EOI 之后 0 字节附加数据；用 Pillow 与 Windows GDI+ 两条独立路径解码，尺寸分别为 1156×1289 与 1151×1103。

**关于原有的 24 字节尾巴**：原始打包文件在 JPEG 的 `FF D9` 之后还跟着 24 字节：8 字节固定头 `1336060100000000` + 16 字节该 JPEG 主体的 MD5。这是当初打包工具留下的完整性校验尾，不是题目考点。重新编码会改变主体字节，原 MD5 必然失配，因此本附件已将这 24 字节移除，两个文件都以 `FF D9` 正常结束。

---


