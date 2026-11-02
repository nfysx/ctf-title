# 题解：你好啊，预言家（反射型 XSS）

> 对应课件 p33「XSS」。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 反射型 XSS、用 JavaScript 读 Cookie、**「偷到凭据」≠「能用凭据」** |
| 难度 | 1 |
| 端口 | 8081 |
| Flag 格式 | `flag{...}` |

一句话：`name` 参数被原样拼进 HTML，所以你能让它执行 JS；
但 Cookie 里**没有 flag**，只有一个服务端签发的会话标识 `sid` ——
得让页面里的 `claim()` 拿它去换 flag。

## 二、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd xss-reflect-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8081/
```

## 三、手把手解题

### Step 1｜确认注入点

输入框随便输点东西，提交后出现：

```
（你输入的内容） 不是你的名字
```

源代码里是这么拼的：

```php
$greeting = '<pre>' . $_GET['name'] . ' 不是你的名字</pre>';
```

**没有任何转义** → 试试脚本。

### Step 2｜先看看 Cookie 里有什么（这一步会让你「白跑一次」）

按 F12 → Application → Cookies，你会看到：

```
sid = 094a563e107488a6d3f2e63c1af1e163f9234e5b6d2390d6a8d102f58ccbad20
```

**只有一串 64 位十六进制，没有 flag** —— 它不是 base64(flag)，
你把它 base64 解码、反转、hash 都出不来 flag。

这就引出本题真正的考点：**偷到凭据 ≠ 拿到东西**，凭据得拿去用。

页面底部的提示也写了：

> 你的会话标识 `sid` 就在 Cookie 里 —— 但它本身只是一串随机数，换不到任何东西。
> 页面里藏了个 `claim()` 函数，它懂得拿 sid 去服务端领东西。

### Step 3｜让 claim() 跑起来

页面里的 `claim()` 会自己读 Cookie 里的 `sid`，再请求 `/claim.php?sid=...` 换 flag。
所以要做的就是**让它执行一次**。payload：

```
http://127.0.0.1:8081/?name=<script>claim()</script>
```

（URL 编码后：`?name=%3Cscript%3Eclaim()%3C%2Fscript%3E`）

页面中间的框里会直接出现 `flag{...}`。

也可以先在控制台验证一下：

```javascript
claim()        // 控制台直接敲也能看到 flag
```

### Step 4｜为什么不能「抄 Cookie 直接换」

你可能想：既然 sid 在 Cookie 里，那我手动打开 `/claim.php?sid=<抄来的 sid>` 不就行了？

试试 —— 会被拒：

```
这个地址不能直接打开 —— 得让页面里的 claim() 帮你请求
```

因为 `/claim.php` 有三道门槛：

| 门槛 | 挡掉什么 |
|---|---|
| sid 必须带服务端 HMAC 签名 | 自己编一串 hex 过不了 |
| 请求里的 sid 必须和当前 Cookie 一致 | 拿别人的 sid 来过不了 |
| 必须带 `Referer`（从页面发起） | **在地址栏直接敲过不了** |

所以「F12 抄 Cookie → 粘贴到地址栏」这条捷径走不通，
你必须让浏览器**真的执行一次 JS** —— 这就是 XSS。

## 四、为什么能成（原理）

服务端把用户输入**原样拼进 HTML**：

```html
<pre><script>claim()</script> 不是你的名字</pre>
```

浏览器解析到 `<script>` 就执行 —— 数据变成了代码。这类漏洞叫 **反射型 XSS**：
payload 在 URL 里、**不持久化**，必须骗受害者点你的链接才会中招。

本题的第二层设计是「凭据无害化」：

```
Cookie 里：sid        —— 服务端签发、HMAC 签名、与 flag 无关
页面里  ：claim()     —— 唯一知道怎么把 sid 换成 flag 的东西
服务端  ：/claim.php  —— 验签名 + 验 Cookie 一致 + 验 Referer
```

于是：**F12 能看、能抄，但抄走也换不到东西**，除非让浏览器执行 JS。

这正是真实 XSS 攻击的两个步骤：**先偷到凭据，再用凭据去调接口**。

## 五、怎么修（防御）

- 输出到 HTML 时按上下文转义：`htmlspecialchars($s, ENT_QUOTES, 'UTF-8')`
- 加 `Content-Security-Policy`（禁 inline script）
- **Cookie 加 `HttpOnly`** —— 这一步能让 JS 完全读不到 sid，是最直接的止血
  （注意：`HttpOnly` 挡的是 JS，**挡不住 F12 的 Cookies 面板**；本题故意没用它来代替题目设计）
- 会话标识要能**服务端作废**；换 flag 这类操作要有 CSRF token + 二次确认

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`（**明文**），未注入则首次请求自动生成随机 flag
并缓存到容器 `/tmp/.web_ctf_flag`。sid 的签名密钥缓存在 `/tmp/.web_ctf_sid_secret`。

验证：

```bash
# 首页只发 sid，不发 flag
curl.exe -i http://127.0.0.1:8081/ | findstr "Set-Cookie"
# 直接访问 claim.php 会被拒
curl.exe -i http://127.0.0.1:8081/claim.php
```

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```
