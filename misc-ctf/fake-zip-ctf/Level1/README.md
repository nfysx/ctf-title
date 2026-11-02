# 题解：压缩包的备注

> 对应第三课 p33 附加数据 · ZIP 结构

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | ZIP 结构、注释字段 |
| 难度 | 1 / 3 |
| 分值 | 80 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

包里没有 flag，但压缩包自己带着一个备注字段。

### 附件

| 文件 | 说明 |
|---|---|
| `files/handin.zip` | 附件 |

## 二、手把手解题

### Step 1｜先老老实实解压

```bash
unzip -l files/handin.zip
unzip files/handin.zip -d out/
```

只有一个 `readme.txt`，它说 flag 不在包里。

### Step 2｜看压缩包注释

```bash
unzip -z files/handin.zip
```

7-Zip 里则是右键 → 属性，最下面一栏「注释」。
Python：`zipfile.ZipFile('handin.zip').comment`

### Step 3｜读 flag

注释里就是 flag。


## 三、为什么能成（原理）

ZIP 末尾的 EOCD 记录里有个**注释长度**字段，后面最多能挂 64 KB 任意字节。
它不属于任何文件，所以 `unzip -l` 看不到 —— 但压缩包确实带着它。
这跟下一题（伪加密）是同一个思路：**ZIP 里除了文件，还有一堆字段**。

## 四、怎么防（延伸）

收到别人的压缩包，`unzip -z` 看一眼注释；自己打包时也别把密码、内网地址写进备注。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{c0mm3n7_15_4_f13ld}`。
