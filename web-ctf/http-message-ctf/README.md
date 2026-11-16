# 题解：预言家，我知道你为何而来（HTTP 报文三要素）

> 对应课件 p17「请求详解」、p48「Burp」。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | HTTP 报文结构：请求行 / 请求头 / 请求体 / URL 参数 |
| 难度 | 2 |
| 端口 | 8080 |
| Flag 格式 | `flag{...}`（页面输出的是 **Base64 编码后的** flag） |

一句话：三道关卡分别藏在**Cookie**、**请求方法+请求体**、**URL 参数**里，三关必须在**同一个请求**里同时满足。

## 题面（直接复制到平台「简介」）

一个自称「预言家」的页面，它给的三条线索分别落在同一份报文的不同部位。
你要同时满足三个条件：带了什么 Cookie、用了什么方法、传了什么参数。
目标是在同一次请求里三关全过：浏览器打开 http://<地址>:8080/ 。

**提示**：F12 的 Network 里逐项对照它要什么；三个条件必须凑在同一个请求上，分开试永远差一关。

## 二、启动环境

> **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd http-message-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8080/
```

## 三、手把手解题

### Step 1｜先看页面在说什么

浏览器打开首页，页面提示「预言家，没有饼干了，我需要 …」并配了一张数学题的图。

刷新时用 Burp / F12 Network 抓一下请求，会看到：

```
GET / HTTP/1.1
Host: 127.0.0.1:8080
User-Agent: Mozilla/5.0 ...
```

**没有 Cookie**：提示里的「饼干」就是 Cookie，数学题的解是 `78`。

### Step 2｜补上 Cookie

浏览器控制台执行：

```javascript
document.cookie = "cookie=78; path=/";
location.reload();
```

或者 Burp 里手动加一行请求头：

```
Cookie: cookie=78
```

页面变成第二关，提示「现在的交流方法不太安全，改成一个安全的方式吧……别忘用英语写在数据里」。

- 「不太安全的方式」= GET，要换成 **POST**
- 「用英语写在数据里」= 请求体里写 `priestess=prophet`（女祭司=priestess，预言家=prophet）

### Step 3｜改成 POST + 请求体

```bash
curl -i -X POST "http://127.0.0.1:8080/" \
     -H "Cookie: cookie=78" \
     -d "priestess=prophet"

curl -i -X POST "http://127.0.0.1:8080/" -H "Cookie: cookie=78" -d "priestess=prophet"
```

Burp 里则是：`GET` 改 `POST`，头部补 `Content-Type: application/x-www-form-urlencoded`，body 写 `priestess=prophet`。

第三关提示：链接里少了个参数，「管理员的值不为一」。

### Step 4｜拼上 URL 参数，一发送出

**关键：三关要同时满足**，所以一次性发完：

```bash
curl -i -X POST "http://127.0.0.1:8080/?admin=1" \
     -H "Cookie: cookie=78" \
     -d "priestess=prophet"

curl -i -X POST "http://127.0.0.1:8080/?admin=1" -H "Cookie: cookie=78" -d "priestess=prophet"     
```

响应里会看到：

```
不错呢预言家,这个就是你想要的了,但是别忘了编码问题哦。flag= ZmxhZ3s...
```

### Step 5｜Base64 解码

```bash
echo 'ZmxhZ3s...' | base64 -d
```

得到 `flag{...}`，提交即可。

## 四、为什么能成（原理）

一个 HTTP 请求由四部分组成，**这四部分都是客户端说了算**：

```
POST /?admin=1 HTTP/1.1        ← 请求行：方法 POST + 路径 ?admin=1
Host: 127.0.0.1:8080           ← 请求头
Cookie: cookie=78              ← 请求头（Cookie 也是头）
Content-Type: ...              ← 请求头
                               
priestess=prophet              ← 请求体（空行之后）
```

服务端代码大致是：

```php
if (isset($_COOKIE['cookie']) && $_COOKIE['cookie'] == 78) { $step1 = true; }
if ($_SERVER['REQUEST_METHOD'] === 'POST' && $post_data['priestess'] === 'prophet') { $step2 = true; }
if (isset($_GET['admin']) && $_GET['admin'] == 1) { $step3 = true; }
if ($step1 && $step2 && $step3) { echo base64_encode($flag); }
```

所以「三关」其实只是同一个请求的三种输入位置，会看报文结构就一次过。

## 五、怎么修（防御）

- **Cookie 不能当身份凭据**：后端应查会话表，而不是信客户端给的键值
- **不要用「请求方法」当业务判断**：POST 不代表安全，路由该显式声明
- **`$_GET` / `$_POST` / `$_COOKIE` 一律当不可信输入**，校验后再用
- Flag 不该能被「一关一关解锁」，应当是「验证通过才生成」的一次性凭据

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`（填**明文**），未注入则首次请求自动生成随机 flag
并缓存到容器 `/tmp/.web_ctf_flag`。页面输出的是它的 Base64，所以最后一步要解码。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `http-message-ctf` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/http-message-ctf` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
| 镜像版本 | 上传后生成的 Unix 时间戳 | 下拉选最新那条，不要手填 |
| 镜像拉取策略 | `Always` | 与平台默认一致 |
| 服务描述 | HTTP 站点（浏览器访问） | 只是备注，不影响运行 |
| 传输协议 | `tcp` | 全库只有 TCP 服务 |
| 应用协议 | `http` | HTTP 站点选 http |
| **服务端口** | **`8080`** | 容器内实际监听端口，必须与 Dockerfile 的 `EXPOSE` 一致 |
| CPU 限制 | `0.1, 0.5` | 最小, 最大（核） |
| 内存限制（MB） | `32, 128` | 最小, 最大 |
| 存储限制（MB） | `64, 1024` | 最小, 最大 |
| restricted | **勾选** | 平台会 drop `NET_BIND_SERVICE`，所以容器监听的是 `8080` 而不是 80 |
| 出网 | **关闭** | 本题不需要外网 |

> 端口必须填 **8080**。历史上按 80 填过的记录要改过来：容器里 Apache 已改成 `Listen 8080`，填 80 会连不上。
