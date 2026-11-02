# 题解：只有 base64

> 对应第三课 p12 Encoding

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | base64、编码 |
| 难度 | 1 / 3 |
| 分值 | 80 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

最原始的编码题：一串 base64，解开就是 flag。

### 附件

| 文件 | 说明 |
|---|---|
| `files/readme.txt` | 附件 |

## 二、手把手解题

### Step 1｜看字符集

全是 `A-Z a-z 0-9 + /`，结尾还有 `=` —— 这是 base64 唯一的特征。

### Step 2｜解

```bash
cat files/readme.txt
python -c "import base64;print(base64.b64decode('那串').decode())"
```

### Step 3｜对照一下

同一道题的上一级是「接力」（base64 → hex → rot13），这一题只有一层。


## 三、为什么能成（原理）

base64 把 3 个字节变成 4 个可打印字符，所以长度总是 4 的倍数、以 `=` 补齐。
它**不是加密**，没有密钥，任何人都能解 —— 只是换了个能在邮箱/URL 里安全传递的外壳。

## 四、怎么防（延伸）

别把 base64 当加密用。要保密就用 AES + 密钥，要传输就用 HTTPS。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{b4s364_15_ju57_4_5t3p}`。
