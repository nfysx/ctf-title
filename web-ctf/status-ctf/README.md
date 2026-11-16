# 题解：跳转测试台（状态码 · 302 响应头）

> 对应课件 p24「状态码」、p48「Burp」。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | HTTP 状态码、3xx 重定向、响应头、`curl -i` / 抓包 |
| 难度 | 1 |
| 端口 | 8088 |
| Flag 格式 | `flag{...}` |

一句话：flag 藏在**那次 302 的响应头**里，而浏览器会自动跟随跳转，所以你「看不见」它。

## 题面（直接复制到平台「简介」）

一个跳转测试台：点一下按钮就从一页跳到另一页，浏览器会自动跟随。
flag 就发生在这一跳里，而跳过去之后的页面什么都不显示。
目标是拿到跳转过程中给出的 flag：浏览器打开 http://<地址>:8088/ 。

**提示**：跟走之后你看到的是终点，要看的是跳走之前那份响应里写了什么。

## 二、启动环境

> **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd status-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8088/
```

## 三、手把手解题

### Step 1｜正常点开，什么也没有

点页面上的「开始跳转」，浏览器直接落到 `/landing.php`：

> 这里是 `/landing.php`，页面本身什么都没有。

因为 **302 被浏览器自动跟随了**，你只看到终点，看不到中间那一步。

### Step 2｜不跟随，看原始响应（curl）

```bash
curl -i http://127.0.0.1:8088/jump.php
```

输出：

```
HTTP/1.1 302 Found
Date: ...
Server: Apache/2.4.x
X-Powered-By: PHP/7.4.x
X-CTF-Flag: flag{...}          ← 就是它
Location: /landing.php
Content-Length: 0
```

### Step 3｜只要头也行

```bash
curl -s -o /dev/null -D - http://127.0.0.1:8088/jump.php
```

`-o /dev/null` 丢掉 body，`-D -` 把响应头打到屏幕。

### Step 4｜Burp 方式

1. 开拦截，访问 `/jump.php`
2. Proxy → HTTP history 找到那条 **302**
3. 展开 response headers，能看到 `X-CTF-Flag`

> 也可以把 Burp 的「Follow redirection」关掉，直接看 302 的原始响应。

## 四、为什么能成（原理）

一次重定向其实是**两个请求**：

```
请求 1  GET /jump.php  →  响应 302 + Location: /landing.php  （这一步藏着 flag）
请求 2  GET /landing.php → 响应 200 + 页面                    （浏览器自动发的）
```

浏览器为了「体验好」自动帮你发了第二个请求，**第一个响应就被略过了**。
抓包工具和 `curl` 默认不跟跳转（`curl -L` 才跟），所以能看到它。

顺带记住几个 3xx：

| 码 | 含义 | 方法是否保持 |
|---|---|---|
| 301 | 永久重定向 | 可能变成 GET |
| 302 | 临时重定向 | 可能变成 GET |
| 303 | 见其它 | **强制** GET |
| 307 | 临时重定向 | **保持**原方法 |

## 五、怎么修（防御）

- 敏感信息**不要放在 3xx 的响应头里**（日志、代理、浏览器扩展都可能留下它）
- 跳转目标要白名单校验，否则就是**开放重定向**（钓鱼常用）
- 用 301/302 时注意「POST 被降级成 GET」的业务后果，该用 307 就用 307

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`；未注入则自动随机生成并缓存到 `/tmp/.web_ctf_flag`。
Flag **只出现在 `/jump.php` 的响应头**，`/landing.php` 不含任何 flag。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `status-ctf` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/status-ctf` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
