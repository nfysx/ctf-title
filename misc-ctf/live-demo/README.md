# live-demo · 现场演示题（6 道）

配合课件 `第三课.pptx`（38 页）在讲台上表演：**每题 1.5–3 分钟**，
一道题 = 一个视觉冲击 + 一句话总结。和 `misc-ctf/` 里那 12 道练习题互补：

- `misc-ctf/`：发给学生自己拆，有完整题解
- `live-demo/`：讲师台上做，有**分秒脚本 + 台词 + 翻车预案**

## 演示顺序（建议按课件顺序插讲）

| # | 目录 | 题名 | 对应课件页 | 时长 |
|---|---|---|---|---|
| 01 | [`demo-01/`](demo-01/) | 改个后缀就露馅 | p5「后缀是名字，不是事实」 | 1.5 |
| 02 | [`demo-02/`](demo-02/) | file 一句话拆穿 | p7 / p29「拿到附件先做五件事」 | 2 |
| 03 | [`demo-03/`](demo-03/) | 把 PNG 拖进解压软件 | p31「大佬，压缩包打不开啊…」 | 2 |
| 04 | [`demo-04/`](demo-04/) | 六位密码，四秒破 | p32「压缩包有密码？先想清楚哪一种」 | 3 |
| 05 | [`demo-05/`](demo-05/) | 照片自带坐标 | p35「图片类」/ p10 Recon | 3 |
| 06 | [`demo-06/`](demo-06/) | 打码了也白打 | p35「图片类」· EXIF 缩略图 | 2.5 |

## 每题目录

```
<demo>/
├── files/            # 台上打开的那几个文件（已按演示场景命名）
├── LIVE.md           # ⭐ 表演脚本：分秒 + 台词 + 预期画面 + 翻车预案
├── flag.txt          # 答案
├── 爆破.py           # 只有 demo-04 有
├── verify.py         # 自校验
└── checker/main.rx   # 可选：放平台时用
```

## 一键校验

```bash
python verify_all.py
```

## 课前 5 分钟检查清单

```
□ 资源管理器已勾「文件扩展名」（demo-01 全靠它）
□ 装了 7-Zip（demo-03）、exiftool（demo-05/06）、Python（demo-04）
□ 地图能在浏览器打开，或提前截好落点图（demo-05）
□ 所有 files/ 复制到桌面一份（别在 U 盘/网络盘上演示，会卡）
□ demo-04 的爆破脚本先自己跑一遍，确认 2 秒内出结果
```

## 素材来源（真实照片）

| 用于 | 文件 | 来源 | 许可 |
|---|---|---|---|
| demo-02 `实验报告.pdf` | 桂林山水 | Wikimedia Commons（同 misc-ctf 的 `p_guilin.jpg`） | CC BY-SA 4.0 |
| demo-03 `风景.png` | 西湖（三潭印月） | Wikimedia Commons `West Lake, Hangzhou, China.jpg` | 见文件页 |
| demo-05 `合影.jpg` | 黄浦江拍的东方明珠 | Wikimedia Commons `Shanghai Oriental Pearl Tower-20150516-RM-123453.jpg` | CC BY-SA 4.0 |
| demo-06 `马赛克.jpg` | 苏州狮子林（打码） | Wikimedia Commons（同 misc-ctf 的 `p_garden_shizilin.jpg`） | CC BY-SA 3.0 |

- 原片存放在 `_build/_assets/live/`，重跑 `make_live_demo.py` 可复现
- demo-03 转 PNG 时元数据自然丢失；demo-05 的 GPS **用的是原图 EXIF 里的真实值**
  （31.242222, 121.491111），不是编的
- 两张都做了缩放，演示时省显存

## 与练习题的分工

| | live-demo | misc-ctf |
|---|---|---|
| 谁做 | 讲师 | 学生 |
| 时长 | 1.5–3 分钟 | 10–40 分钟 |
| 目的 | 制造「原来是这么回事」 | 自己动手踩坑 |
| 演练内容 | 01 改后缀 · 02 file · 03 附加数据 · 04 爆破 · 05 GPS · 06 缩略图 | 12 题覆盖全部考点 |
