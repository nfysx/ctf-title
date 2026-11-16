# 题解：web-hello-headers（读 HTTP 响应头）

> web-ctf 方向第一课。形态：**纯附件题**，不起容器。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | HTTP 报文结构（起始行 / 头 / 空行 / 正文）、请求头与响应头的分工、状态码；flag 藏在响应头 `X-Flag` 里 |
| 难度 | 1 |
| 端口 | 无（纯附件，无容器） |
| 附件 | `files/exchange.txt`（1.0 KB，一次完整的请求 + 响应原始报文） |
| Flag | `flag{read_the_response_headers}` |
| 判题 | `checker/main.rx` 静态模板 A：`check` 直接比对提交内容，`environ` 返回空对象 |

一句话：一个 HTTP 交换是「请求头 + 空行 + 正文」和「响应头 + 空行 + 正文」两段文本拼起来的；flag 在**响应的头**里，不在正文里。

## 题面（直接复制到平台「简介」）

一次完整的 HTTP 交换被原样抓了下来，请求和响应都在同一个文件里。
附件只有一个文件：files/exchange.txt，本题不起容器。
目标是在这份报文里找出带 flag 的那个字段。

**提示**：响应头里未必只有你熟悉的字段；名字里带 Flag 的那条不一定就是答案，判断依据是值的形状。

## 二、题目描述

> 下面整段就是选手看到的题面，可直接搬到平台。

### 1. 一次 HTTP 交换长什么样

浏览器访问一个地址，服务端和它之间来回的是**纯文本**。抓下来去掉 TCP/TLS 那些底层的东西，就剩两段：

```text
GET /dashboard HTTP/1.1        <- 请求行：方法 + 路径 + 协议版本
Host: challenge.local:8080     <- 请求头：一行一个「名字: 值」
...
                               <- 空行：头的结束标记
[请求正文，GET 一般没有]

HTTP/1.1 200 OK                <- 状态行：协议版本 + 状态码 + 原因短语
Server: nginx/1.24.0           <- 响应头
...
                               <- 空行：头的结束标记
<!DOCTYPE html>...             <- 响应正文
```

三条硬规则：

1. **先有一行起始行**（请求叫请求行，响应叫状态行），然后是若干 `名字: 值` 的头。
2. **空行是分界线**：第一个空行之前全是头，之后全是正文。头里可以有冒号，正文里可以有换行，靠空行才分得开。
3. **头是给对端看的元数据，正文才是内容**。浏览器按 `Content-Type` 决定怎么解释正文，按 `Content-Length` 决定读多少字节。

### 2. 请求头 ≠ 响应头

同一个字段名在两边的含义可以完全不同：请求里的 `Accept` 是「我能接受什么」，响应里的 `Content-Type` 是「我实际给了什么」。

| 只在请求里 | 只在响应里 | 两边都能出现 |
|---|---|---|
| `Host` `Cookie` `Referer` `User-Agent` `Accept-Language` `Upgrade-Insecure-Requests` `X-Forwarded-For` | `Server` `Set-Cookie` `Location` `X-Powered-By` `X-Content-Type-Options` | `Content-Type` `Content-Length` `Cache-Control` `Date` `X-Request-Id` |

`X-` 开头的是**自定义头**，没有标准规定含义，谁都能加、两边都能有。所以看见 `X-Flag` 不代表它是标准字段，只代表服务端自己塞了一个。

### 3. 状态码在说什么

状态码是响应行里的三位数字，第一位决定大类：

| 码 | 含义 | 典型场景 |
|---|---|---|
| 200 | OK | 正常返回内容 |
| 301 / 302 | 永久 / 临时重定向 | `Location` 指向新地址，浏览器自己再发一次 |
| 401 | 未认证 | 没登录，或凭据无效 |
| 403 | 已认证但禁止 | 登录了，没权限 |
| 404 | 资源不存在 | 路径写错，或被刻意隐藏 |
| 500 | 服务端错误 | 后端崩了 |

本题附件里的状态码是 `200`：请求成功，头 + 正文都给全了。

### 4. 你的任务

`files/exchange.txt` 是一次完整的交换。**flag 在响应头里，不在响应正文里**，而且不止一个 `X-` 头在抢注意力。

先别猜，按顺序做三件事：

1. 找到**第一个空行**，确认上半是请求、下半是响应；
2. 在响应半段里列出所有以 `X-` 开头的头，逐个看值；
3. 认出哪一个的值是真的 `flag{...}`。

命令行版本（Windows）：

```powershell
cd D:\ctf\web-ctf\web-hello-headers
Select-String -Path files\exchange.txt -Pattern '^X-'
```

Linux / macOS / Git Bash：

```bash
cd web-hello-headers
grep -n '^X-' files/exchange.txt
```

## 三、解题步骤

### Step 1｜确认报文分界

文件里第一个空行之前是请求（9 行），之后是响应。请求是 `GET /dashboard HTTP/1.1`，请求头 8 条。

### Step 2｜只列响应里以 X- 开头的头

```text
$ Select-String -Path files\exchange.txt -Pattern '^X-'

files\exchange.txt:17:X-Request-Id: 6f1c9a3e-2b47-4d18-9a55-0d3c7e8a1b02
files\exchange.txt:18:X-Powered-By: PHP/7.4.33
files\exchange.txt:19:X-Content-Type-Options: nosniff
files\exchange.txt:20:X-Trace-Id: trace-5b2e91c4
files\exchange.txt:21:X-Flag-Hint: headers-are-part-of-the-response
files\exchange.txt:22:X-Flag: flag{read_the_response_headers}
```

6 条 `X-` 头，其余五条都不是答案：`X-Request-Id` 与 `X-Trace-Id` 是链路追踪 id、`X-Powered-By` 泄露了后端语言、`X-Content-Type-Options` 是安全头、`X-Flag-Hint` 的值是一句英文提示（`headers-are-part-of-the-response`），**名字里有 Flag 不等于值是 flag**。

### Step 3｜取值

```text
$ Select-String -Path files\exchange.txt -Pattern '^X-Flag:'

files\exchange.txt:22:X-Flag: flag{read_the_response_headers}
```

### Step 4｜顺手校验报文自洽

响应头 `Content-Length: 177`，正文实际字节数 177、字符数 147，中文一个字占 3 个字节，所以「字节数 ≠ 字符数」。这也是为什么 `Content-Length` 只能按字节算：

```text
Content-Length 声明 = 177
正文实际字节数     = 177
正文字符数         = 147
Content-Length 校验 : PASS
```

### Step 5｜提交

`flag{read_the_response_headers}`，全小写，花括号照抄。

## 四、原理

- **头是元数据通道**：正文放内容，头放「怎么处理这份内容」。所以鉴权、缓存、跳转、内容协商全在头里。
- **头也是数据面**：既然头是服务端可控的输出，那么「把敏感信息写进自定义头」就等于把信息发给客户端。本题就是这个错误。现实里对应的是：把内部 id、后端版本、调试信息、甚至接口 token 塞进 `X-` 头。
- **`X-Flag-Hint` 为什么不算答案**：名字里有 `Flag` 不代表值是 flag。要按**值的形状**判断（`flag{...}`），不是按名字猜。
- **抓包视角**：浏览器 F12 的 `Network` 面板把同一个报文拆成标签页（Headers / Payload / Response）。命令行等价物是 `curl -I URL`（只要头）与 `curl -i URL`（头 + 正文），Windows 上写成 `curl.exe`：

```text
$ curl.exe -s -I http://127.0.0.1:8123/index.html
HTTP/1.0 200 OK
Server: SimpleHTTP/0.6 Python/3.14.7
Date: Mon, 02 Nov 2026 16:44:13 GMT
Content-type: text/html
Content-Length: 2086
Last-Modified: Mon, 02 Nov 2026 16:43:53 GMT
```

两点说明：`Date` 是**那一次请求**的时间戳，每次跑都不一样；以上其余字段是固定的。上例指向任意一台本地 HTTP 服务器即可复现，例如 web-hello-source 那台的 `python -m http.server 8123`。

## 五、防御建议

- **别把敏感信息放头里**：自定义头对客户端完全可见，也常被日志、CDN、代理原样记录。flag、token、内部 id 都不该出现在响应头。
- **默认可信边界**：服务端下发什么，客户端就拥有什么。真正的「只给有权限的人」要靠鉴权 + 服务端判定，不能靠「名字起得隐蔽」。
- **关掉版本泄露**：`Server`、`X-Powered-By` 会告诉攻击者你在跑什么版本，Nginx 用 `server_tokens off`，PHP 用 `expose_php = Off`。
- **安全头要加对**：`X-Content-Type-Options: nosniff`、`Content-Security-Policy`、`Strict-Transport-Security` 是防御用的；与「往头里塞业务数据」是两回事。

## 六、附件清单

| 文件 | 大小 | 作用 |
|---|---|---|
| `files/exchange.txt` | 1037 字节 | 一次完整 HTTP 交换的原始报文；flag 在响应头 `X-Flag`，另有 5 个干扰 `X-` 头 |
| `checker/main.rx` | 457 字节 | 静态 flag checker，模板 A |
| `README.md` | 本文件 | 题解 |

附件总计 1.0 KB，纯文本，记事本 / VS Code / `type` 均可直接查看。
