# 题解：编码接力

> 对应第三课 p12 Encoding · p13 编码/加密/哈希

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 编码、base64、十六进制、ROT13 |
| 难度 | 1 / 3 |
| 分值 | 100 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

三层编码套娃，一层一层剥。考的是「认出字符集」而不是「会写解密脚本」。

### 附件

| 文件 | 说明 |
|---|---|
| `files/readme.txt` | 附件 |

## 二、手把手解题

### Step 1｜先看字符集

打开 `files/readme.txt`，正文里有一大段字符：

```
看起来像这样：TjNkMWJqTXdNak0zTkRrME1UTTVPVEk1T1M0eE5ERTBPVGd1TVRZNU1USTVMakUyT1RFeU5UUTVMakV6TVRrek5qUTVMakV6TVRrek5qUTVMakV6TVRrek5qUTVMakV6TVRrek5qUTVM...
```

只有大小写字母和数字、长度是 4 的倍数、结尾可能有 `=` —— 这些特征指向 **base64**。

### Step 2｜第一层：base64

```bash
python -c "import base64,sys;print(base64.b64decode(open('readme.txt',encoding='utf-8-sig').read().split('下面是题目给的全部内容：')[1].split('提示')[0].strip()).decode())"
```

结果是一串 **全是 0-9a-f 的十六进制** —— 下一层是 hex。

### Step 3｜第二层：十六进制

```bash
python -c "print(bytes.fromhex('<上一步的结果>').decode())"
```

结果是一段能读的英文，但单词是乱的：`syn0q1at` 这种 —— 典型 **ROT13**。

### Step 4｜第三层：ROT13

```bash
python -c "import codecs;print(codecs.decode('<上一步的结果>','rot13'))"
```

得到 flag。也可以直接把字符串贴进 CyberChef，用 Magic 一键识别。


## 三、为什么能成（原理）

每一层都是**可逆的编码**，没有密钥：base64 只是换字符集、hex 只是换进制、ROT13 只是字母位移 13 位。
所以「看到乱码先别慌」—— 先看字符集判断是哪一类编码，再决定用哪个工具拆。

## 四、怎么防（延伸）

真实系统里编码不是加密，**把敏感数据 base64 一下就当成「加密」是常见错误**（比如把密码、token 塞进 URL 参数）。
防御：敏感数据必须用真正的加密（AES 等）+ 传输层保护（HTTPS），而不是编码。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{3nc0d1ng_1s_n0t_3ncrypt10n}`。
