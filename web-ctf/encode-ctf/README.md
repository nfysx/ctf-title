# 题解：两层门（编码链）

> 对应课件 p48「编码、加密、哈希，是三件事」。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | Base64、十六进制、编码 ≠ 加密 |
| 难度 | 1 |
| 端口 | 8089 |
| Flag 格式 | `flag{...}` |

一句话：页面给一串 base64，解出来是 hex，再转文本就是一条路径，访问它拿 flag。

> 编码是**预制**的，没有巧思，一层层剥就完事 —— 顺序别乱。

## 二、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd encode-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8089/
```

## 三、手把手解题

### Step 1｜读谜面

页面给出一长串：

```
NzU2ZTZjNmY2MzZiMmU3MDY4NzAzZjYzNmY2NDY1M2Q2MjZjNzU2NTYyNjk3MjY0
```

页面已经提示了两层：`base64` → `16 进制`。

### Step 2｜第一层：base64 解码

```bash
echo 'NzU2ZTZjNmY2MzZiMmU3MDY4NzAzZjYzNmY2NDY1M2Q2MjZjNzU2NTYyNjk3MjY0' | base64 -d
```

得到：

```
756e6c6f636b2e7068703f636f64653d626c756562697264
```

（PowerShell 控台用：`[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("..."))`）

### Step 3｜第二层：hex 转文本

Linux / Git Bash：

```bash
echo '756e6c6f636b2e7068703f636f64653d626c756562697264' | xxd -r -p
```

Python（跨平台都能用）：

```python
print(bytes.fromhex("756e6c6f636b2e7068703f636f64653d626c756562697264").decode())
```

得到：

```
unlock.php?code=bluebird
```

### Step 4｜访问它

```
http://127.0.0.1:8089/unlock.php?code=bluebird
```

页面「门开了」，输出 Flag。

> 参数 `code` 不对会显示「门是锁的」—— 说明路径本身对，只是口令错。

### Step 5｜一条命令走完（偷懒版）

```bash
P='NzU2ZTZjNmY2MzZiMmU3MDY4NzAzZjYzNmY2NDY1M2Q2MjZjNzU2NTYyNjk3MjY0'
curl -s "http://127.0.0.1:8089/$(echo $P | base64 -d | xxd -r -p)"
```

## 四、为什么能成（原理）

**编码不是加密**：编码只换「表示形式」，不需要密钥、谁都能解；加密才需要密钥。

| 类型 | 目的 | 需要密钥 | 例子 |
|---|---|---|---|
| 编码 | 换种形式传输 | 否 | Base64、URL 编码、hex |
| 加密 | 保密 | **是** | AES、RSA |
| 哈希 | 校验/存密码 | 否（不可逆） | MD5、SHA-256 |

真实 CTF 里常见的连环编码：

```
base64 → hex → urlencode → 反转 → 凯撒位移 → …
```

判断技巧：`=` 结尾且只有 `A-Za-z0-9+/` → base64；只有 `0-9a-f` 且偶数长度 → hex；
有 `%XX` → URL 编码。

## 五、怎么修（防御）

- 别把「编码」当「加密」用 —— base64 一个密码等于明文
- 谜面类逻辑别把关键路径写在可解码数据里（本题是教学，故意如此）
- 真实系统里：密码存**哈希**（bcrypt/argon2），传输用 **TLS**，敏感数据用**真加密**

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`；未注入则自动随机生成并缓存到 `/tmp/.web_ctf_flag`。
谜面（编码链）是静态的，**flag 不是** —— 每实例都不同。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```
