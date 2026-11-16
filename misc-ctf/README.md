# misc-ctf · MISC 入门题集（35 题）

> 全部是**附件型**题目：下载附件 → 本地拆 → 提交 flag。不需要 Docker，也不需要联网跑服务。
> 覆盖四条主线：**编码与文件格式 → 图片隐写与元数据 → 流量分析 → 地理定位**。
> 前 20 题对应 `..\..\学校文件\网安社\晚自习\第三课\第三课.pptx`（38 页）。

---

## 题目一览（35 题）

### 基础题（24 题）

| # | 目录 | 题名 | 考点 | 难度 | 分值 |
|---|---|---|---|---|---|
| 01 | [`enc-chain-ctf/Level1/`](<enc-chain-ctf/Level1/>) | 只有 base64 | base64、编码 | 1 | 80 |
| 02 | [`enc-chain-ctf/Level2/`](<enc-chain-ctf/Level2/>) | 只有十六进制 | 十六进制、编码 | 1 | 80 |
| 03 | [`enc-chain-ctf/Level3/`](<enc-chain-ctf/Level3/>) | 编码接力 | 编码、base64、十六进制、ROT13 | 1 | 100 |
| 04 | [`file-header-ctf/Level1/`](<file-header-ctf/Level1/>) | 改个后缀就露馅 | 文件类型、魔数、改后缀 | 1 | 80 |
| 05 | [`file-header-ctf/Level2/`](<file-header-ctf/Level2/>) | 被抹掉的文件头 | 文件头、魔数、十六进制 | 2 | 150 |
| 06 | [`fake-zip-ctf/Level1/`](<fake-zip-ctf/Level1/>) | 压缩包的备注 | ZIP 结构、注释字段 | 1 | 80 |
| 07 | [`fake-zip-ctf/Level2/`](<fake-zip-ctf/Level2/>) | 没有密码的加密包 | 伪加密、ZIP 结构、标志位 | 2 | 150 |
| 08 | [`lsb-stego-ctf/Level1/`](<lsb-stego-ctf/Level1/>) | 红色通道的悄悄话 | LSB、单通道、图片隐写 | 1 | 100 |
| 09 | [`lsb-stego-ctf/Level2/`](<lsb-stego-ctf/Level2/>) | 最低有效位 | LSB、图片隐写 | 2 | 150 |
| 10 | [`append-zip-ctf/`](<append-zip-ctf/>) | 图片里有个压缩包 | 附加数据、binwalk、PNG 结构 | 2 | 150 |
| 11 | [`exif-meta-ctf/`](<exif-meta-ctf/>) | 照片会说话 | EXIF、元数据、base64 | 1 | 120 |
| 12 | [`exif-thumb-ctf/`](<exif-thumb-ctf/>) | 被打码的照片 | EXIF、缩略图、IFD1 | 2 | 180 |
| 13 | [`exif-erase-ctf/`](<exif-erase-ctf/>) | 抹掉之前先备份 | EXIF、抹除、APP1 段、对比 | 3 | 220 |
| 14 | [`png-text-ctf/`](<png-text-ctf/>) | 注释里的悄悄话 | PNG 结构、tEXt 注释块、strings | 2 | 180 |
| 15 | [`traffic-ctf/Level1/`](<traffic-ctf/Level1/>) | 流量里的那一次请求 | 流量分析、pcap、base64 | 2 | 200 |
| 16 | [`traffic-ctf/Level2/`](<traffic-ctf/Level2/>) | 分块传输的日志 | HTTP、chunked、TCP 重组 | 2 | 220 |
| 17 | [`traffic-ctf/Level3/`](<traffic-ctf/Level3/>) | DNS 里的悄悄话 | DNS、隧道、base32、过滤 | 3 | 260 |
| 18 | [`traffic-ctf/Level4/`](<traffic-ctf/Level4/>) | 自己逆的私有协议 | 协议逆向、帧结构、CRC、XOR | 3 | 300 |
| 19 | [`misc-hello-strings/`](<misc-hello-strings/>) | 万物皆可 strings ☆ | `file` → `strings` → `grep` 三连 | 1 | 80 |
| 20 | [`misc-hello-hexdump/`](<misc-hello-hexdump/>) | 换个角度看字节 ☆ | `xxd` / `hexdump`、十六进制视角 | 1 | 80 |
| 21 | [`magic-bytes-ctf/`](<magic-bytes-ctf/>) | 改错的扩展名 | 文件魔数 ≠ 扩展名，改名即可打开 | 1 | 100 |
| 22 | [`png-ihdr-ctf/`](<png-ihdr-ctf/>) | 被裁掉的图 | IHDR 高度篡改 + 重算 CRC32 | 1 | 100 |
| 23 | [`png-hidden-qr-ctf/`](<png-hidden-qr-ctf/>) | 图里有两半 | 高度篡改 + IEND 后空白隐写 → 二维码 | 2 | 180 |
| 24 | [`pcap-file-read-ctf/`](<pcap-file-read-ctf/>) | 那一次入侵 | pcap 任意文件读取 → Java PBEWithMD5AndDES 解密 | 3 | 300 |

> ☆ = 引入题。`misc-hello-strings` 教 `file` / `strings` / `grep` 三连，
> `misc-hello-hexdump` 教 `xxd` 视角，这两道是后面所有题的地基。

### starwalk 系列（5 题）· 参考 MoeCTF「星走路的旅程」

多要素拼 flag：坐标、时间、招牌都要读对，拼起来才是答案。

| # | 目录 | 题名 | 考点 | 难度 | 分值 |
|---|---|---|---|---|---|
| 25 | [`starwalk-series/Level1/`](<starwalk-series/Level1/>) | 属性三件套 | EXIF、GPS、型号、时间换算 | 1 | 100 |
| 26 | [`starwalk-series/Level2/`](<starwalk-series/Level2/>) | 五张照片，一张带坐标 | EXIF、GPS、批量查询、景区定位 | 2 | 200 |
| 27 | [`starwalk-series/Level3/`](<starwalk-series/Level3/>) | 这张游客照在哪拍的 | EXIF、GPS、时间换算、地标识别 | 1 | 150 |
| 28 | [`starwalk-series/Level4/`](<starwalk-series/Level4/>) | 三层线索拼出一条街 | 信息搜集、招牌识别、地图比对 | 3 | 250 |
| 29 | [`starwalk-series/Level5/`](<starwalk-series/Level5/>) | 三张照片的路线 | EXIF、GPS、时间排序、多步推理 | 3 | 250 |

### live-demo（6 题 · 现场演示用）

`live-demo/demo-01 … demo-06` 是讲课时的现场演示脚本（分秒 + 台词 + 预期画面），
每题都有 checker，说明统一写在 [`live-demo/README.md`](live-demo/README.md)，未单独拆分题解。

## 跑起来

附件题不用起服务，本地拆就行：

```bash
cd misc-ctf/png-text-ctf
strings files/photo.png | head            # 第一课
binwalk files/photo.png                   # 看有没有附加数据

cd ../png-hidden-qr-ctf
python -c "print(open('files/release.png','rb').read()[-10000:])"   # 看 IEND 之后
```

每道题都有自校验脚本（在 `exp/` 下，**不对外发布**）：

```bash
python exp/verify.py
```

## 一键校验

```bash
cd misc-ctf
python verify_all.py
```

会对每道题跑 `exp/verify.py`（兼容根目录与 `exp/` 两种位置），全部通过才算题目可用。
当前状态：**31 / 31 通过**。

## 平台上传要点

35 道全是附件题，**不占端口、不需要镜像**。

- 附件放各自的 `files/` 下，原样下发
- `checker/main.rx` 是静态模板，`FLAG` 与附件实际能提取出的串一致（每题都实测过）
- `exp/`（含 `verify.py`）**不要下发**
- 系列题的每级是**独立一题**（各自有 `checker/` 与 `files/`），不是一道题的多关卡

## 这个方向的常见坑

| 现象 | 原因 |
|---|---|
| `strings` 什么都看不到 | 文件里是非 ASCII 数据（LSB、二进制字段），换 `xxd` 看 |
| 改扩展名后图片打不开 | 不只是改后缀，文件头也可能被抹了（`file-header-ctf/Level2`） |
| ZIP 要密码 | 大概率是伪加密：改掉通用位标记第 0 位即可 |
| `exiftool` 看不到 GPS | 元数据被抹了，要比对原始图的重编码差异 |
| LSB 读出来是乱码 | 通道/位序选错：先试单通道 LSB，再试 RGB 三位平面 |
| pcapng 打不开 | 老版本 Wireshark 不认，或文件是 XX 压缩包套壳 |

## 目录结构

```text
misc-ctf/
├── <题目>/
│   ├── files/           # 发给选手的附件
│   ├── checker/main.rx  # ret2shell 评测脚本（静态 flag）
│   ├── exp/verify.py    # 自校验：按题解跑一遍并断言 flag（不对外）
│   └── README.md        # 题解
├── verify_all.py        # 一键跑全部 verify.py
├── _build/              # 造题脚本与素材（不是题）
└── 动态附件生成器/        # 把任意一题做成「人手一份动态附件」（不是题）
```

## 与课件对应

| 课件页 | 主题 | 对应题目 |
|---|---|---|
| p12 / p13 | 编码、编码≠加密 | 01 编码接力 |
| p27 / p32 | 十六进制视角、文件头速查 | 02 被抹掉的文件头 |
| p14 / p33 | 隐写、附加数据 | 03 图片里有个压缩包 |
| p33 | 伪加密 | 04 没有密码的加密包 |
| p14 | LSB | 05 最低有效位 |
| p37 | EXIF 描述、base64 | 06 照片会说话 |
| p37 / p14 | EXIF 缩略图（IFD1） | 07 被打码的照片 |
| p37 / p33 | EXIF 抹除、APP1 段 | 08 抹掉之前先备份 |
| p31 / p27 | PNG 块结构、tEXt 注释 | 09 注释里的悄悄话 |
| p18 / p36 | 流量分析 | 10 流量里的那一次请求 |
| p37 / p10 | EXIF + 时间换算，多要素拼 flag | 21 属性三件套 · 23 游客照 · 24 三层线索 |
| p37 | 批量 EXIF、景区定位 | 22 五张照片 |
| p37 / p10 | 按时间轴复现路线 | 25 三张照片的路线 |

## 造题脚本

`_build/make_challenges.py`（造附件）+ `_build/make_docs.py`（写文档 / checker / verify）。
`_build/challenges.json` 是题目元数据（题号、flag、分值、考点）。改完重跑两个脚本即可重建全部题目与附件。

## 工具：动态附件生成器（平台用）

`动态附件生成器/` 下有一套独立工具，把任意一题做成人手一份的「动态附件」：

```bash
cd 动态附件生成器
python make_mapped.py                 # 交互：输入 <题目名> <份数>
python make_mapped.py demo-01 3       # 命令行：生成后自动校验
```

产出 `out/<目标>/mapped/*.zip` 与 `checker/flag.json`，分别上传到 `$BUCKET/mapped/` 与 `$BUCKET/checker/`，
配合平台的 `mapped.rx` 使用。细节见 `动态附件生成器/README.md`。

## 素材来源

| 题 | 文件 | 内容 | 许可 | 作者 |
|---|---|---|---|---|
| 02 file-header | `p_greatwall.jpg` | 慕田峪长城俯拍 | CC BY 2.0 | xiquinhosilva |
| 03 append-zip | `user_ctf_meme.png` | 用户提供的动漫截图（字幕「老实打CTF，很枯燥吧」） | 用户提供 | - |
| 05 lsb-stego | `p_yangshuo.jpg` | 阳朔村庄 | CC BY 2.0 | McKay Savage |
| 06 exif-meta | `p_greatwall_cc0.jpg` | 长城 | CC0 | Velatrix |
| 10 exif-gps | `p_gugong_turtle.jpg` | 故宫铜龟 | CC BY-SA 3.0 | CEphoto, Uwe Aranas |
| 10 exif-gps | `p_karst.jpg` | 喀斯特山水 | CC BY 3.0 | Vyacheslav Argenberg |
| 10 exif-gps | `p_zjj_gps.jpg` | 张家界国家森林公园 | CC BY 2.0 | Kuruman |
| 10 exif-gps | `p_guilin.jpg` | 桂林山水 | CC BY-SA 4.0 | G41rn8 |
| 10 exif-gps | `p_mutianyu.jpg` | 慕田峪长城垛口 | CC BY 2.0 | Arian Zwegers |
| 11 exif-thumb | `p_gugong_turtle2.jpg` | 故宫铜龟特写 | CC BY 2.0 | Dave Proffer |
| 12 exif-erase | `p_xihu_pano.jpg` | 西湖全景航拍 | CC BY 4.0 | Wanderingchina |
| 13 png-text | `p_garden_wangshi.jpg` | 网师园 | CC BY-SA 3.0 | Chinatravelsavvy |
| 08 geo-hongyadong | `(geo) hongyadong.jpg` | 洪崖洞夜景（保留 EXIF） | CC BY 4.0 | Lianguanlun |
| 09 geo-nanjinglu | `(geo) nanjing_road.jpg` | 南京东路夜景（元数据已清空） | CC BY-SA 4.0 | EditQ |

> 除 03 题为用户提供的截图外，其余照片均来自 Wikimedia Commons（CC 授权）。
> 所有图都做了缩放/重新编码；附件元数据按题目需要写入或清除。
> 原图在 `_build/_assets/photos/`（附 `credits.json`），重跑 `make_challenges.py` 可复现。
