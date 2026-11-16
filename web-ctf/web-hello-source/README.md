# 题解：web-hello-source（查看网页源码）

> web-ctf 方向第一课。形态：**纯附件题**，不起容器。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 「查看源码」的三条路径（`Ctrl+U` / F12 Elements / `curl`）；HTML 注释对渲染不可见、对源码可见 |
| 难度 | 1 |
| 端口 | 无（纯附件，无容器） |
| 附件 | `files/index.html`（2.0 KB 单文件静态页） |
| Flag | `flag{view_source_is_the_first_move}` |
| 判题 | `checker/main.rx` 静态模板 A：`check` 直接比对提交内容，`environ` 返回空对象 |

一句话：服务器发出去的是一份文本，浏览器只负责把它渲染成画面；注释不参与渲染，但一直躺在文本里。

## 题面（直接复制到平台「简介」）

一份从站点上保存下来的静态页面，内容是一则赛事公告。
附件只有一个文件：files/index.html，本题不起容器。
目标是拿到页面里那串 flag。

**提示**：渲染出来的画面只是文件的一部分，注释也属于文件内容；跳过渲染直接读原文。

## 二、题目描述

> 下面整段就是选手看到的题面，可直接搬到平台。

### 1. 浏览器拿到的到底是什么

你在地址栏敲一个地址，服务器回给你的是一份**文本**。浏览器读这份文本，按 HTML 规则把标签翻译成画面。

`<!-- 内容 -->` 是 HTML 注释。渲染时整段跳过：页面上看不到它，框选复制也复制不到。但它仍然是文件的一部分，和正文一起从服务器发出来。**能拿到文件本身，就能拿到注释。**

拿到「文件本身」这件事，行话叫**查看源码**。

### 2. 三条路

**路一 · `Ctrl+U`**

在页面上按 `Ctrl+U`（macOS 是 `Command+Option+U`），浏览器新开一个标签，显示服务器发来的原始文本。
地址栏也可以直接写：`view-source:http://127.0.0.1:8123/index.html`。

**路二 · F12 → Elements**

按 `F12` 打开开发者工具，切到 `Elements`（元素）。这里显示的是**解析后的 DOM 树**，不是原始文件：浏览器会补全缺失的闭合标签、规范化属性引号、把「浏览器理解的结构」摆给你看；注释以 `<!-- -->` 的形式保留在树里。

两条路看到的不是同一个东西，别混：

| 面板 | 看到的内容 | 适合找什么 |
|---|---|---|
| View Source（`Ctrl+U`） | 服务器发来的**原文** | 注释、隐藏字段、被 JS 改写前的原始结构 |
| Elements（F12） | 浏览器**解析后**的 DOM | 运行时被 JS 插入/修改的节点、当前生效的样式 |

**路三 · `curl`**

命令行直接取文件，不渲染、不执行 JS、不带 Cookie：

```bash
curl -s http://127.0.0.1:8123/index.html
```

Windows 的 PowerShell 5.1 里 `curl` 是 `Invoke-WebRequest` 的别名，敲 `curl -s URL` 会报「请为以下参数提供值: Uri」。用 `curl.exe`（Windows 10 1803+ 自带）：

```powershell
curl.exe -s http://127.0.0.1:8123/index.html
```

只挑出注释行：

```powershell
curl.exe -s http://127.0.0.1:8123/index.html | Select-String '<!--'
```

```bash
curl -s http://127.0.0.1:8123/index.html | grep -n '<!--'
```

### 3. 自己起一个页面（离线也能练）

平台上的形态是**下载附件**，不带容器。附件就是一个 2 KB 的 HTML 文件，双击能看；但要练「从 HTTP 取源码」这条路，用 Python 自带的静态服务器把它挂起来更接近真实场景：

```powershell
cd D:\ctf\web-ctf\web-hello-source
python -m http.server 8123 --bind 127.0.0.1 --directory files
```

浏览器打开 `http://127.0.0.1:8123/index.html`，练完 `Ctrl+C` 停服。

### 4. 你要做的事

这份页面里留了三行注释，其中一行是 `debug-marker: flag{...}`。用上面任意一条路把源码取出来，找到那一行。

一个提醒：正文公告里也出现了一次 `flag{...}` 字样，那是写给选手看的**格式示例**，不是答案。真 flag 的花括号之间有实际内容。

## 三、解题步骤

### Step 1｜把页面挂起来

见上文「二、3. 自己起一个页面」。服务器日志会打印每条请求，后面每一步都能对上：

```text
Serving HTTP on 127.0.0.1 port 8123 (http://127.0.0.1:8123/) ...
```

### Step 2｜路一：Ctrl+U

浏览器打开页面后按 `Ctrl+U`，源码末尾三行：

```text
  <!-- build 2026-10-28T09:14+08:00 · static · rev 7 -->
  <!-- TODO(ops): 赛前把下面这行演练标记删掉 -->
  <!-- debug-marker: flag{view_source_is_the_first_move} -->
```

### Step 3｜路二：F12 → Elements

打开开发者工具 → `Elements`，注释节点同样在树里（等价于解析后的 DOM 里剩下的注释节点）：

```text
#1  <!-- build 2026-10-28T09:14+08:00 · static · rev 7 -->
#2  <!-- TODO(ops): 赛前把下面这行演练标记删掉 -->
#3  <!-- debug-marker: flag{view_source_is_the_first_move} -->
```

### Step 4｜路三：curl

```text
$ curl.exe -s http://127.0.0.1:8123/index.html | Select-String '<!--'

  <!-- build 2026-10-28T09:14+08:00 · static · rev 7 -->
  <!-- TODO(ops): 赛前把下面这行演练标记删掉 -->
  <!-- debug-marker: flag{view_source_is_the_first_move} -->
```

只筛 flag 行会命中两条，一条是公告正文里的格式示例、一条是注释里的真 flag：

```text
$ curl.exe -s http://127.0.0.1:8123/index.html | Select-String 'flag\{'

    <p>flag 一律通过平台提交框提交，格式固定为 <code>flag{...}</code>，区分大小写，多一个空格也算错。</p>
  <!-- debug-marker: flag{view_source_is_the_first_move} -->
```

### Step 5｜提交

取注释里那一串（从 `flag{` 到 `}`，共 35 个字符，全小写），提交框粘贴。

## 四、原理

- **Web 的本质是文本**：HTTP 响应体就是一串字节，浏览器解析它、渲染它，但解析不等于删除。注释是「给读源码的人看的」，不是「给浏览器看的」。
- **注释会原样出站**：服务器不会在发送前删注释（那是构建期该干的事），所以注释里写什么，谁都能读到。
- **渲染视图 ≠ 源码视图**：Elements 是解析结果，View Source 是原始字节。前端框架渲染出来的内容在 View Source 里可能根本不存在（只有一段 `<div id="app"></div>`），这时候要找的是接口和 JS，而不是源码注释，这是本题之后的路。
- **`curl` 是另一个客户端**：它不执行 JS、不带浏览器状态，所以它看到的等于服务器「真正发出来的字节」。做接口题时，这一步能排掉一半「浏览器缓存 / 前端拦截」造成的假象。

## 五、防御建议

- **注释里不放敏感信息**：口令、内网地址、测试账号、TODO 里的业务逻辑，都不要写。本题的 `debug-marker` 就是典型事故，运维用注释给自己留记号，忘了删。
- **构建期剥离**：上线产物用 HTML 压缩/构建插件去掉注释（`html-minifier-terser` 的 `removeComments: true`），别依赖「记得删」。
- **区分「注释」与「数据」**：需要传给前端又要藏起来的数据，正确做法是根本不下发，而不是下发后放进注释。
- **上线检查表**：源码里 grep 一遍 `TODO|FIXME|password|secret|flag`，再对外发布。

## 六、附件清单

| 文件 | 大小 | 作用 |
|---|---|---|
| `files/index.html` | 2086 字节 | 单文件静态页，flag 在末尾 HTML 注释里 |
| `checker/main.rx` | 460 字节 | 静态 flag checker，模板 A |
| `README.md` | 本文件 | 题解 |

附件总计 2.0 KB，无外部资源、无网络依赖，双击即可打开（也可用任意静态服务器托管）。
