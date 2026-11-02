# 题解：内网管理入口（请求头伪造）

> 对应课件 p17「请求详解」——每一行请求头都是输入。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | HTTP 请求头、`X-Forwarded-For` 可伪造、`User-Agent` 可伪造 |
| 难度 | 1 |
| 端口 | 8087 |
| Flag 格式 | `flag{...}` |

一句话：页面靠两个请求头判断身份，而这两个头**都是客户端随手能改的**。

> 它连你是不是**真人**都没验过 —— 在它眼里，身份就是两行字，这就是全部的**含人量**。

## 二、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd header-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8087/
```

## 三、手把手解题

### Step 1｜页面把判断依据全写出来了

打开首页会看到两个条件（实时显示服务端看到的值）：

```
来源 IP 判定（X-Forwarded-For）：不通过   当前值：(未提供)
客户端标识判定（AdminBot）：不通过        当前值：Mozilla/5.0 ...
```

**服务器把「它要看哪些头」直接告诉你了** —— 这就是白给。

### Step 2｜补两个请求头（curl 一行）

```bash
curl -s http://127.0.0.1:8087/ \
     -H "X-Forwarded-For: 127.0.0.1" \
     -H "User-Agent: AdminBot/1.0"
```

两个条件变绿 → 页面输出 Flag。

> **`/1.0` 不是必须的** —— 服务端用的是 `stripos($ua, 'AdminBot')`（子串 + 忽略大小写），
> 所以写 `AdminBot` 就够了。`/1.0` 只是 **User-Agent 的惯例格式**：`产品名/版本号`。
> 真实爬虫都长这样：`Googlebot/2.1`、`Baiduspider/2.0`、`python-requests/2.31.0`、`curl/8.16.0`。
> 你也可以写 `AdminBot/99.9` —— **版本号是自报的，没有任何校验**，这本身就是本题想讲的东西。
>
> 附带一个真实的坑：子串匹配意味着 `NotAdminBot`、`AdminBotX` 也会通过。
> 真实反爬如果这么写，绕起来更简单 —— 严格点应该用
> `preg_match('/^AdminBot(\/[\d.]+)?$/i', $ua)`。

### Step 3｜Burp 方式

拦下 `GET /`，在头部加：

```
X-Forwarded-For: 127.0.0.1
User-Agent: AdminBot/1.0
```

放行即可。

### Step 4｜只补一个会怎样

```bash
curl -s "http://127.0.0.1:8087/" -H "X-Forwarded-For: 127.0.0.1"
```

只过一个条件，**不给 flag** —— 验证了「两个都要」。

## 四、为什么能成（原理）

```php
$xff = $_SERVER['HTTP_X_FORWARDED_FOR'];   // ← 客户端写的字符串
$ua  = $_SERVER['HTTP_USER_AGENT'];        // ← 客户端写的字符串
$ok = strpos($xff, '127.0.0.1') !== false && stripos($ua, 'AdminBot') !== false;
```

任何以 `X-` 开头的头都不是 HTTP 标准语义，只是**约定**，中间件/客户端都能随意加。

真实场景里的两个经典用法：

| 场景 | 伪造 |
|---|---|
| 「仅内网可访问」 | `X-Forwarded-For: 127.0.0.1` |
| 简易反爬（只挡普通浏览器） | `User-Agent: 某搜索引擎爬虫` |
| 短信/登录频率限制 | 每次换一个 `X-Forwarded-For` 绕过阈值 |

## 五、怎么修（防御）

- **`X-Forwarded-For` 只能在最外层入口写**，内网链路要逐跳清洗；应用侧取「可信代理链」里最后一个
- 真正的来源 IP 用 `REMOTE_ADDR`，配合可信代理白名单
- `User-Agent` **永远不能当身份凭据**
- 内网限制用网络层（防火墙 / 内网 ACL），不要用请求头

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`；未注入则自动随机生成并缓存到 `/tmp/.web_ctf_flag`。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```
