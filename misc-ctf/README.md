# misc-ctf · MISC 入门题集（23 题）

配套课件：`..\..\学校文件\网安社\晚自习\第三课\第三课.pptx`（38 页）

全部为**附件型**题目：下载附件 → 本地拆 → 提交 flag。不需要 Docker，也不需要联网跑服务。

## 题目一览

### 基础题（18 题）

| # | 目录 | 题名 | 考点 | 难度 | 分值 |
|---|---|---|---|---|---|
| 01 | [`enc-chain-ctf/Level1/`](enc-chain-ctf/Level1/) | 只有 base64 | base64、编码 | 1 | 80 |
| 02 | [`enc-chain-ctf/Level2/`](enc-chain-ctf/Level2/) | 只有十六进制 | 十六进制、编码 | 1 | 80 |
| 03 | [`enc-chain-ctf/Level3/`](enc-chain-ctf/Level3/) | 编码接力 | 编码、base64、十六进制、ROT13 | 1 | 100 |
| 04 | [`file-header-ctf/Level1/`](file-header-ctf/Level1/) | 改个后缀就露馅 | 文件类型、魔数、改后缀 | 1 | 80 |
| 05 | [`file-header-ctf/Level2/`](file-header-ctf/Level2/) | 被抹掉的文件头 | 文件头、魔数、十六进制 | 2 | 150 |
| 06 | [`fake-zip-ctf/Level1/`](fake-zip-ctf/Level1/) | 压缩包的备注 | ZIP 结构、注释字段 | 1 | 80 |
| 07 | [`fake-zip-ctf/Level2/`](fake-zip-ctf/Level2/) | 没有密码的加密包 | 伪加密、ZIP 结构、标志位 | 2 | 150 |
| 08 | [`lsb-stego-ctf/Level1/`](lsb-stego-ctf/Level1/) | 红色通道的悄悄话 | LSB、单通道、图片隐写 | 1 | 100 |
| 09 | [`lsb-stego-ctf/Level2/`](lsb-stego-ctf/Level2/) | 最低有效位 | LSB、图片隐写 | 2 | 150 |
| 10 | [`append-zip-ctf/`](append-zip-ctf/) | 图片里有个压缩包 | 附加数据、binwalk、PNG 结构 | 2 | 150 |
| 11 | [`exif-meta-ctf/`](exif-meta-ctf/) | 照片会说话 | EXIF、元数据、base64 | 1 | 120 |
| 12 | [`exif-thumb-ctf/`](exif-thumb-ctf/) | 被打码的照片 | EXIF、缩略图、IFD1 | 2 | 180 |
| 13 | [`exif-erase-ctf/`](exif-erase-ctf/) | 抹掉之前先备份 | EXIF、抹除、APP1 段、对比 | 3 | 220 |
| 14 | [`png-text-ctf/`](png-text-ctf/) | 注释里的悄悄话 | PNG 结构、tEXt 注释块、strings | 2 | 180 |
| 15 | [`traffic-ctf/Level1/`](traffic-ctf/Level1/) | 流量里的那一次请求 | 流量分析、pcap、base64 | 2 | 200 |
| 16 | [`traffic-ctf/Level2/`](traffic-ctf/Level2/) | 分块传输的日志 | HTTP、chunked、TCP 重组 | 2 | 220 |
| 17 | [`traffic-ctf/Level3/`](traffic-ctf/Level3/) | DNS 里的悄悄话 | DNS、隧道、base32、过滤 | 3 | 260 |
| 18 | [`traffic-ctf/Level4/`](traffic-ctf/Level4/) | 自己逆的私有协议 | 协议逆向、帧结构、CRC、XOR | 3 | 300 |

### starwalk 系列 Level-6（5 题）· 参考 MoeCTF「星走路的旅程」

多要素拼 flag：坐标、时间、招牌都要读对，拼起来才是答案。

| # | 目录 | 题名 | 考点 | 难度 | 分值 |
|---|---|---|---|---|---|
| 19 | [`Level1/`](starwalk-series/Level1/) | 属性三件套 | EXIF、GPS、型号、时间换算 | 1 | 100 |
| 20 | [`Level2/`](starwalk-series/Level2/) | 五张照片，一张带坐标 | EXIF、GPS、批量查询、景区定位 | 2 | 200 |
| 21 | [`Level3/`](starwalk-series/Level3/) | 这张游客照在哪拍的 | EXIF、GPS、时间换算、地标识别 | 1 | 150 |
| 22 | [`Level4/`](starwalk-series/Level4/) | 三层线索拼出一条街 | 信息搜集、招牌识别、地图比对 | 3 | 250 |
| 23 | [`Level5/`](starwalk-series/Level5/) | 三张照片的路线 | EXIF、GPS、时间排序、多步推理 | 3 | 250 |

## 目录结构（每题一致）

```
<题目>/
├── files/          # 发给选手的附件
├── README.md       # 题解（手把手 + 原理 + 防御）
├── checker/main.rx # ret2shell 评测脚本（静态 flag）
└── verify.py       # 自校验：按题解跑一遍并断言 flag
```

## 一键校验

```bash
python verify_all.py
```

会对每道题跑 `verify.py`，全部通过才算题目可用。

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
| p37 / p10 | EXIF + 时间换算，多要素拼 flag（starwalk 系列） | 11 属性三件套 · 13 游客照 · 14 三层线索 |
| p37 | 批量 EXIF、景区定位 | 12 五张照片 |
| p37 / p10 | 按时间轴复现路线 | 15 三张照片的路线 |

## 造题脚本

`_build/make_challenges.py`（造附件）+ `_build/make_docs.py`（写文档/checker/verify）。
`_build/challenges.json` 是题目元数据（题号、flag、分值、考点）。
改完重跑两个脚本即可重建全部题目与附件。

## 工具

### 动态附件生成器（平台用）

`动态附件生成器/` 下有一套独立工具，把任意一题做成人手一份的「动态附件」：

```bash
cd 动态附件生成器
python make_mapped.py                 # 交互：直接输入 <题目名> <份数>
python make_mapped.py demo-01 3       # 命令行：生成后自动校验
```

产出 `out/<目标>/mapped/*.zip` 与 `checker/flag.json`，分别上传到
`$BUCKET/mapped/` 和 `$BUCKET/checker/`，配合平台的 `mapped.rx` 使用。
细节见 `动态附件生成器/README.md`（含上传步骤与四个坑）。

## 素材来源

| 题 | 文件 | 内容 | 许可 | 作者 |
|---|---|---|---|---|
| 02 file-header | `p_greatwall.jpg` | 慕田峪长城俯拍 | CC BY 2.0 | xiquinhosilva |
| 03 append-zip | `user_ctf_meme.png` | 用户提供的动漫截图（字幕「老实打CTF，很枯燥吧」） | 用户提供（版权自行确认） | - |
| 05 lsb-stego | `p_yangshuo.jpg` | 阳朔村庄 | CC BY 2.0 | McKay Savage from London, UK |
| 06 exif-meta | `p_greatwall_cc0.jpg` | 长城(CC0) | CC0 | Velatrix |
| 10 exif-gps | `p_gugong_turtle.jpg` | 故宫铜龟 | CC BY-SA 3.0 | CEphoto, Uwe Aranas |
| 10 exif-gps | `p_karst.jpg` | 喀斯特山水 | CC BY 3.0 | Vyacheslav Argenberg Vascoplanet Com |
| 10 exif-gps | `p_zjj_gps.jpg` | 张家界国家森林公园 | CC BY 2.0 | Kuruman from Tokyo, Japan |
| 10 exif-gps | `p_guilin.jpg` | 桂林山水 | CC BY-SA 4.0 | G41rn8 |
| 10 exif-gps | `p_mutianyu.jpg` | 慕田峪长城垛口 | CC BY 2.0 | Arian Zwegers |
| 11 exif-thumb | `p_gugong_turtle2.jpg` | 故宫铜龟特写 | CC BY 2.0 | Dave Proffer |
| 12 exif-erase | `p_xihu_pano.jpg` | 西湖全景航拍 | CC BY 4.0 | Wanderingchina |
| 13 png-text | `p_garden_wangshi.jpg` | 网师园 | CC BY-SA 3.0 | Chinatravelsavvy |
| 08 geo-hongyadong | `(geo) hongyadong.jpg` | 洪崖洞夜景（**保留 EXIF**：GPS + 拍摄时间） | CC BY 4.0 | Lianguanlun |
| 09 geo-nanjinglu | `(geo) nanjing_road.jpg` | 南京东路夜景（元数据已清空） | CC BY-SA 4.0 | EditQ |

> 除 03 题为你提供的动漫截图外，其余照片均来自 Wikimedia Commons（CC 授权）。
> 所有图都做了缩放/重新编码；附件的元数据按题目需要写入或清除。
> 原图存放在 `_build/_assets/photos/`（附 `credits.json`），重跑 `make_challenges.py` 可复现。

