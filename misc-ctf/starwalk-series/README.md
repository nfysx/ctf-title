# starwalk-series · 星走路的旅程（5 关）

仿 MoeCTF 2026《星走路的旅程》的思路做的迷你系列：**flag 不是一个词，而是几个字段拼出来的**。
每关都要先从图/文件里挖出若干事实，再换算、交叉验证，最后拼成答案。

| 关 | 目录 | 题名 | 难度 | 分值 | 拼法 |
|---|---|---|---|---|---|
| Level1 | [`Level1/`](Level1/) | 属性三件套 | 1 | 100 | 型号 + 坐标 + 时间各取一段 → `flag{beijing_xiaomi_0926}` |
| Level2 | [`Level2/`](Level2/) | 五张照片，一张带坐标 | 2 | 200 | 批量扫目录找唯一带 GPS 的 → `flag{hunan_zhangjiajie}` |
| Level3 | [`Level3/`](Level3/) | 没清元数据的游客照 | 2 | 150 | GPS + 拍摄时间，**要换算 UTC** → `flag{chongqing_hongyadong_20241009_1325}` |
| Level4 | [`Level4/`](Level4/) | 三层线索拼一条街 | 3 | 250 | 招牌 + 高塔 + 街名互相印证 → `flag{shanghai_nanjingdonglu_yongan}` |
| Level5 | [`Level5/`](Level5/) | 三张照片的路线 | 3 | 250 | 按拍摄时间排序复现路线 → `flag{shanghai_zhangjiajie_chongqing}` |

## 递进关系

- **Level1** 教「三件套」：一个文件里同时有型号、坐标、时间
- **Level2** 加「筛选」：五张里只有一张带 GPS，要批量比
- **Level3** 加「换算」：EXIF 记的是本地时间，flag 要 UTC
- **Level4** 换「推理」：从图里读招牌，靠多条线索互相印证
- **Level5** 加「排序」：三个点按时间轴串成一条路线

## 依赖

```
exiftool（必需）
任意地图（高德 / Google Maps）
```

## 一键校验

```bash
python verify_all.py
```

## 与 MoeCTF 的对应

| MoeCTF | 本系列 |
|---|---|
| level1：机场三字码 + 手机品牌 + UTC 时间 | Level1 属性三件套 |
| level2：商铺搜索 → 省市路名 | Level4 三层线索 |
| level3：注册号 → 航线 → 出发/到达机场 | Level5 路线复现 |

> 照片全部来自 Wikimedia Commons（CC 授权），明细见上级 `README.md` 的「素材来源」表。
