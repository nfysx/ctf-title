# 题解：编码不是加密（crypto-lesson-encoding）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 1（引导题，第一课） |
| 考点 | base64 / hex / URL 百分号编码的识别与还原；编码与加密的区别 |
| 形态 | 纯引导题，无容器 |
| 附件 | `files/task.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

## 题面（直接复制到平台「简介」）

三段数据各用一种常见编码写成，哪一段是哪种要靠字符集自己判断。每段解出来只是一段纯文本，按 SEG1、SEG2、SEG3 的顺序拼接才是 flag。附件只有 `files/task.txt`。

**提示**：编码不需要密钥，也不需要密钥就能还原；看字符集决定用哪个解码器。

## 三、解题步骤

### 第 1 步：认字符集

```
SEG1  ZmxhZ3tlbmNvZGluZ19pc18=        -> 大小写字母+数字，末尾有 "=" -> base64
SEG2  6e6f745f                        -> 全是 0-9a-f，长度 8 是偶数   -> hex
SEG3  %65%6e%63%72%79%70%74%69%6f%6e%7d -> 全是 %XX 三元组            -> URL 百分号编码
```

### 第 2 步：逐段解开（命令可直接复制）

```bash
# SEG1 base64
python3 -c "import base64; print(base64.b64decode('ZmxhZ3tlbmNvZGluZ19pc18=').decode())"

# SEG2 hex
python3 -c "print(bytes.fromhex('6e6f745f').decode())"

# SEG3 URL
python3 -c "import urllib.parse; print(urllib.parse.unquote('%65%6e%63%72%79%70%74%69%6f%6e%7d'))"
```

> Windows 上若没有 `python3` 这个别名（Microsoft Store 的占位别名会报 "Python was not found"），把命令里的 `python3` 换成 `python`。

Linux / macOS / WSL 下还有对应命令行工具，效果一样：

```bash
echo ZmxhZ3tlbmNvZGluZ19pc18= | base64 -d        # base64
echo 6e6f745f | xxd -r -p                        # hex
python3 -c "import urllib.parse; print(urllib.parse.unquote('%65%6e%63%72%79%70%74%69%6f%6e%7d'))"   # URL 没有标准命令行工具
```

三段输出分别是 `flag{encoding_is_`、`not_`、`encryption}`（顺序对应 SEG1/2/3）。

### 第 3 步：自己动手

把三段输出**按顺序**接起来（不要加空格、不要加换行），得到一个 32 字符的字符串，那就是 flag，提交即可。

出题人验证：

```
$ python3 -c "import base64; print(base64.b64decode('ZmxhZ3tlbmNvZGluZ19pc18=').decode())"
flag{encoding_is_
$ python3 -c "print(bytes.fromhex('6e6f745f').decode())"
not_
$ python3 -c "import urllib.parse; print(urllib.parse.unquote('%65%6e%63%72%79%70%74%69%6f%6e%7d'))"
encryption}
$ # 手工拼接三段
flag{encoding_is_not_encryption}
```

## 四、原理

**base64 为什么是 4/3 倍。** 它把输入按 3 字节（24 bit）分组，再切成 4 个 6 bit 的片段，
每个片段查表映射到 64 个可打印字符之一。24 bit 正好被 6 整除，所以不跨组浪费；
输入长度不是 3 的倍数时用 `=` 补齐，这也是「长度必为 4 的倍数」的来源。
它解决的问题是：把任意二进制塞进只允许可打印 ASCII 的通道（邮件、HTTP 头、JSON 字段）。
解码是确定性的查表逆运算，没有任何密钥参与。

**hex 为什么是 2 倍。** 一个字节 8 bit，用两个十六进制数字表示（每数字 4 bit），
字节与文本一一对应，没有分组、没有补齐、没有任何歧义。它是最「笨」也最不容易出错的表示法，
所以 `xxd`、哈希值、内存 dump 都用它。

**URL 百分号编码为什么变长。** URI 里 `?`、`&`、`=`、`/`、`#` 有语法含义，
空格在报文里也容易出问题，所以这些字节要写成 `%XX`。百分号编码可以把**任意**字节写成 `%XX`，
不必只编码特殊字符，附件里那段就是全量编码，解码器照样能还原。

三者共同的结论：**变换是公开的、确定的、可逆的，没有密钥**。拿到密文的人只要有工具就能还原，
所以编码只提供「格式兼容」，不提供「机密性」。把它当加密用是 CTF 里最常见的入门误解。

## 五、防御建议

- 配置文件、日志、数据库里用 base64「藏」密码或 token 等于明文存放：`base64 -d` 一行就还原。要存就存哈希（带盐、慢哈希），要传就用密钥管理/KMS。
- 看到 `eyJ...` 开头的串要意识到那是 base64 的 JSON（JWT 的头部与载荷），**JWT 的载荷是可读的**，签名只保证完整性，不保证机密性。
- 传输层用 TLS，不要指望编码隐藏内容；需要机密性就用 AEAD（AES-GCM、ChaCha20-Poly1305）。
- 日志脱敏要在写入前做，而不是「编码一下再写」。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/task.txt` | 三段待解码数据（base64 / hex / URL 各一段）+ 拼接说明 |
