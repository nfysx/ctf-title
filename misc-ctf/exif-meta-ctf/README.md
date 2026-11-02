# 题解：照片会说话

> 对应第三课 p37 图片题顺序（EXIF 优先） · p10 Recon

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | EXIF、元数据、base64 |
| 难度 | 1 / 3 |
| 分值 | 120 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

像素里什么都没有，答案在相机写下的元数据里，还是 base64。

### 附件

| 文件 | 说明 |
|---|---|
| `files/photo.jpg` | 附件 |

## 二、手把手解题

### Step 1｜先看元数据

```bash
exiftool files/photo.jpg
```

或 Windows：右键 → 属性 → 详细信息。

`ImageDescription` 里是一长串字符，形如 `ZmxhZ3ttM3Q0ZDR0NF90NGxrcyE=`。

### Step 2｜认出 base64

结尾的 `=`、只有字母数字和 `+/` —— 直接解码：

```bash
python -c "import base64;print(base64.b64decode('ZmxhZ3ttM3Q0ZDR0NF90NGxrcyE=').decode())"
```

### Step 3｜注意干扰项

`UserComment` 里写着 `nothing here, keep looking` —— 别被它带跑。


## 三、为什么能成（原理）

JPEG 的 EXIF 是**相机/软件自动写入**的信息：机型、时间、甚至 GPS 坐标。
上传图片时不清理元数据，等于把拍摄地点和时间一起交出去 —— 这正是 p10 讲的那条：
「你发出去的每张照片，都在替你说出位置」。

## 四、怎么防（延伸）

发图前清元数据：`exiftool -all= photo.jpg`，或截图/重压缩。
服务端也应该在接收图片时剥离 EXIF，避免用户位置被间接泄露。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{m3t4d4t4_t4lks}`。
