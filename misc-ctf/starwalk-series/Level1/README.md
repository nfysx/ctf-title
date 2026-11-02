# 题解：属性三件套

> 对应第三课 p37 图片题顺序（EXIF 优先） · 参考 MoeCTF「星走路的旅程 level1」

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | EXIF、GPS、型号、时间换算 |
| 难度 | 1 / 3 |
| 分值 | 100 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

一张照片里同时躺着型号、坐标、拍摄时间 —— 三样各取一段拼起来才是 flag。

### 附件

| 文件 | 说明 |
|---|---|
| `files/photo.jpg` | 附件 |
| `files/readme.txt` | 附件 |

## 二、手把手解题

### Step 1｜一次看清全部字段

```bash
exiftool files/photo.jpg
```

关键三行：
```
Make / Model  : Xiaomi / Xiaomi 14 Ultra
GPS Position  : 39 deg 54' 59.00" N, 116 deg 23' 50.00" E
Modify Date   : 2026:07:14 17:26:00
```

### Step 2｜坐标 → 城市

```
39.91639, 116.39722 → 北京 · 故宫
```

### Step 3｜型号 → 品牌

`Xiaomi 14 Ultra` 的品牌取小写：`xiaomi`。

### Step 4｜时间 → UTC

```
2026-07-14 17:26:00 (UTC+8)  − 8h  =  09:26 (UTC)
```

### Step 5｜拼接

```
flag{城市拼音_品牌小写_UTC时刻}
flag{beijing_xiaomi_0926}
```


## 三、为什么能成（原理）

这就是「星走路的旅程 level1」的三件套思路（机场三字码 + 手机品牌 + UTC 时间）。
三个参数分别藏在 EXIF 的不同字段里，取哪个、怎么换算，都要自己判断。
最容易错的是时间：EXIF 记当地时间，flag 要 UTC。

## 四、怎么防（延伸）

这三个字段全是相机自动写的，你什么都没做就交出去了。
发图前 `exiftool -all= 图.jpg`，或关掉相机的「记录位置」。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{beijing_xiaomi_0926}`。
