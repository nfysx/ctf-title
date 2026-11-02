# 题解：五张照片，一张带坐标

> 对应第三课 p37 图片题顺序（EXIF 优先） · p10 Recon · 参考 MoeCTF「星走路的旅程」

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | EXIF、GPS、批量查询、景区定位 |
| 难度 | 2 / 3 |
| 分值 | 200 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

五张旅行照，只有一张把坐标和拍摄时间一起带了出来。读 EXIF、换算、定位、拼 flag。

### 附件

| 文件 | 说明 |
|---|---|
| `files/photos/IMG_2031.jpg` | 附件 |
| `files/photos/IMG_2032.jpg` | 附件 |
| `files/photos/IMG_2033.jpg` | 附件 |
| `files/photos/IMG_2034.jpg` | 附件 |
| `files/photos/IMG_2035.jpg` | 附件 |
| `files/readme.txt` | 附件 |

## 二、手把手解题

### Step 1｜批量查元数据

```bash
exiftool files/photos/
```

五张里只有 `IMG_2033.jpg` 同时有 `GPS Position` 和 `Create Date`。

### Step 2｜只挖那一张

```bash
exiftool -GPS:all -CreateDate files/photos/IMG_2033.jpg
```

`GPS Position : 29 deg 19' 56.00" N, 110 deg 25' 34.00" E`
`Create Date  : 2026:07:14 09:26:00`

### Step 3｜换算并定位

```
29°19′56″N → 29.33222
110°25′34″E → 110.42611
```

贴进地图 —— 落在**湖南张家界国家森林公园**（砂岩峰林）。

### Step 4｜拼出答案

省份 + 景区：

```
flag{hunan_zhangjiajie}
```


## 三、为什么能成（原理）

相机拍照时把 GPS 写进 EXIF 的 GPS IFD（0x8825），单位是「度/分/秒」的分数形式，
要自己算成小数才能贴进地图。批量题的关键是**先用一条命令横向比一遍**，
谁是那个「多出两个字段」的文件，一眼就看出来。

## 四、怎么防（延伸）

发图前清元数据：`exiftool -all= photo.jpg`；手机相机里关掉「保存位置信息」更彻底。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{hunan_zhangjiajie}`。
