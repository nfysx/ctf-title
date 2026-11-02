# 题解：三张照片的路线

> 对应第三课 p37 EXIF · p10 Recon · 参考 MoeCTF「星走路的旅程」多要素拼接

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | EXIF、GPS、时间排序、多步推理 |
| 难度 | 3 / 3 |
| 分值 | 250 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

三张照片、三个城市、三段拍摄时间。按时间排序，把路线写出来。

### 附件

| 文件 | 说明 |
|---|---|
| `files/IMG_1001.jpg` | 附件 |
| `files/IMG_1002.jpg` | 附件 |
| `files/IMG_1003.jpg` | 附件 |
| `files/readme.txt` | 附件 |

## 二、手把手解题

### Step 1｜先批量读出来

```bash
for f in files/*.jpg; do echo "== $f"; exiftool -GPS:all -ModifyDate "$f"; done
```

或者一次跑整个目录：`exiftool files/`

### Step 2｜把坐标换成城市

```
IMG_1001.jpg  31.240,121.497 → 上海
IMG_1002.jpg  29.566,106.576 → 重庆
IMG_1003.jpg  29.332,110.426 → 湖南张家界
```

### Step 3｜按时间排序

```
IMG_1001  2024:10:07 08:41:30
IMG_1003  2024:10:09 09:12:07
IMG_1002  2024:10:11 21:05:11
```

文件名是乱的，真正的顺序在 EXIF 里。

### Step 4｜拼出路线

```
flag{shanghai_zhangjiajie_chongqing}
```


## 三、为什么能成（原理）

文件名可以随便改，**拍摄时间是相机写的**，改不了（除非有人专门改）。
所以「复现一个人的行程」靠的不是文件夹顺序，而是元数据里的时间轴 —— 这是取证的常规手法。
三张图各自只给一个点，串成时间线才有意义。

## 四、怎么防（延伸）

同一件事反过来看：只要你不清元数据，别人就能画出你的行程图。
分享旅行照前批量清一遍：`exiftool -all= -r 相册目录/`。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{shanghai_zhangjiajie_chongqing}`。
