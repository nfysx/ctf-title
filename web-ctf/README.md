# Web-CTF 题目集

> 一个帮朋友出的 CTF Web 入门题集。
> 每道题的题解写在各自文件夹的 `README.md` 里。

题目都围绕一套「Web 安全入门」课程的知识点设计：**从信息泄露开始，到协议、注入、越权、上传**。
难度控制在 1–2，适合刚接触 CTF 的新手。

---

## 题目一览（12 题）

| 目录 | 题名 | 考点 | 难度 | 端口 | 对应课件 |
|---|---|---|---|---|---|
| `http-message-ctf/` | 预言家，我知道你为何而来 | HTTP 协议、Cookie、请求方法、URL 参数、Base64 | 2 | **8080** | p17 请求详解 · p48 Burp |
| `xss-reflect-ctf/` | 你好啊，预言家 | 反射型 XSS、读 Cookie、凭 sid 向服务端换 flag | 1 | **8081** | p33 XSS |
| `robots-ctf/` | 秘密档案室 | `robots.txt` 信息泄露 | 1 | **8082** | p31 信息泄露 |
| `backup-ctf/` | 内部通知系统 | 备份文件泄露、源码审计 | 1 | **8083** | p31 信息泄露 |
| `sqli-ctf/` | 青鸟管理系统 | SQL 注入、注释符、登录绕过 | 2 | **8084** | p32 SQL 注入 |
| `cookie-ctf/` | 个人中心 | Cookie 伪造、**Base64 编码**、水平越权 | 1 | **8085** | p20 Cookie · p48 编码 · p35 越权 |
| `upload-ctf-1/` | 头像上传（基础） | 文件上传、上传目录可执行（**不用伪造请求头**） | 1 | **8086** | p36 文件上传 |
| `header-ctf/` | 内网管理入口 | 请求头伪造、`X-Forwarded-For` | 1 | **8087** | p17 请求详解 |
| `status-ctf/` | 跳转测试台 | 状态码、302 重定向、响应头 | 1 | **8088** | p24 状态码 |
| `encode-ctf/` | 两层门 | Base64、十六进制、编码≠加密 | 1 | **8089** | p48 编码 |
| `upload-ctf-2/` | 头像上传（进阶） | 文件上传、`Content-Type` 可伪造 | 2 | **8091** | p36 文件上传 |
| `connect-ctf/` | 连接平台 | 在线环境的连接方式（赛前引导题，页面直接给 flag） | 0 | **8092** | 赛前环境熟悉 |

> **`upload-ctf-1` 与 `upload-ctf-2` 是同一道题的两个难度**：
> 前者直接传 `.php` 就能成（只留上传漏洞）；后者加了一道 `Content-Type` 检查，需要伪造请求头绕过。
> 建议课堂顺序：先 `-1` 建立「传上去 → 访问它」的直觉，再上 `-2` 讲绕过。

端口 8080–8089 + 8091 + 8092，**可以同时启动互不冲突**（8090 留给示例站）。

> `connect-ctf` 是**赛前引导题**（对应 NCTF 那类「如何连接平台」的热身题）：
> 没有任何漏洞，浏览器一打开页面就显示 flag，用来让选手先跑通「启动环境 → 连上 → 拿到东西」这条链路。
> 建议放在比赛开场，或者当天的第一张 PPT 上现场演示。
>
> 它同时带了 **ret2shell 平台题目包**（`connect-ctf/ret2shell/`：`config.toml` / `env.toml` /
> `checker/main.rx` / `hints.toml` / `answer.md` + 一键上传脚本），可直接传到线上平台；
> 容器内监听 **8080**（平台 restricted 模式会丢 `CAP_NET_BIND_SERVICE`，绑不了 80），宿主映射 8092。

---

## 教学示例站（不参与题目编号）

| 目录 | 站名 | 内容 | 难度 | 端口 |
|---|---|---|---|---|
| `infoleak-demo/` | 星记博客（二次元个人博客） | **信息泄露总示范** —— 把 PPT p31 清单上的 10 种泄露点全摆在一个站里 | 1 | **8090** |

它不算一道单点题，是**课堂演示用的靶子**：站点本体是真实博客的构建产物（视觉、交互都保持原样，
所以看着就是个正常的二次元个人博客），在此基础上故意加了一批运维常犯的错。

用法是「照着清单让学生一条条找」：`robots.txt` / `.git` / 备份 `.bak` / 打包 `www.zip` /
`.DS_Store` / `phpinfo.php` / 响应头 / HTML 注释 / 报错页 / 目录扫描，十条全在。
找齐之后再讲怎么把几条串成一条攻击链。

题解与演示脚本见 `infoleak-demo/README.md`。

---

## 动态 Flag 机制

**仓库里没有任何可用的固定 flag。**

每道题的 `flag.inc.php` 都实现了同一套逻辑：

```
1) 优先读环境变量 FLAG（平台 / 运维注入，填【明文】flag）
2) 没注入时，首次请求自动生成一个随机 flag，
   并缓存到容器 /tmp/.web_ctf_flag，保证同一实例内多次请求一致
```

生成的随机 flag 形如 `flag{40fd67b7-344e-4edd-9afd-707be0ffc3a7}`（UUID v4）。

**注入方式**——`docker-compose.yml` 里已经准备好这一行：

```yaml
    environment:
      - FLAG=${FLAG:-}
      - TZ=Asia/Shanghai
```

所以平台只要在启动时带上环境变量即可，**不用改任何文件**：

```bash
FLAG=flag{your-dynamic-flag-here} docker compose up -d
```

（Windows PowerShell：`$env:FLAG="flag{...}"; docker compose up -d`）

不设 `FLAG` 就自动随机，两者都已实测。

> 注意：`FLAG` 里填**明文**。至于页面输出的是明文还是编码后的形式，由各题决定
> （`http-message-ctf` 和 `xss-reflect-ctf` 会额外做一次 Base64，题解里有写）。

---

## 部署

每道题的目录里都有 `Dockerfile` + `docker-compose.yml`，彼此独立：

```bash
cd robots-ctf
docker compose up -d --build
# 打开 http://127.0.0.1:8082/
docker compose down -v          # 用完清理
```

依赖：Docker + Docker Compose v2（`docker compose`，不是老的 `docker-compose`）。

| 题 | 基础镜像 | 说明 |
|---|---|---|
| 9 题 | `php:7.4-apache` | 单容器 |
| `sqli-ctf` | `php:7.4-apache` + `pdo_mysql`，另有 `mariadb:10.11` | 双容器，首次启动自动执行 `init.sql` 建表 |

**为什么 sqli 用 MariaDB 而不是 SQLite**：PHP 7.4 起不再内置 SQLite，`pdo_sqlite` 需要系统
`libsqlite3-dev`，构建时必须联网 `apt-get`。改用 `pdo_mysql`（走内置 mysqlnd）可以**完全离线构建**。

没有 Docker 也可以直接用 PHP 内置服务器跑（仅限单容器的那 9 题）：

```bash
cd robots-ctf
php -S 127.0.0.1:8082 -t src
```

---

## 在 Windows 上怎么敲这些命令

题解里用的都是 Linux / macOS 写法。在 Windows 上直接抄会踩几个坑，这里给等价写法。

### 最常见的坑：`curl` 在 PowerShell 5.1 里是别名

Windows PowerShell 5.1（`powershell.exe`）内置了 `curl → Invoke-WebRequest` 的别名，
所以你敲的 `curl` 根本不是 curl。两种典型报错：

```powershell
# 带 -H 时
curl -s http://127.0.0.1:8087/ -H "X-Forwarded-For: 127.0.0.1"
# → Invoke-WebRequest : 无法绑定参数"Headers"。无法将"System.String"类型的值转换为
#   "System.Collections.IDictionary"类型。

# 带 -s 时（URL 被 -s 吃掉，于是它反过来问你要 Uri）
curl -s http://127.0.0.1:8087/
# → 位于命令管道位置 1 的 cmdlet Invoke-WebRequest
#   请为以下参数提供值:
#   Uri:
```

**认症状**：只要报错里出现 `Invoke-WebRequest`、`请为以下参数提供值`、`无法绑定参数 Headers`，
就一定是被别名劫持了。

**四种解决办法**（任选其一）：

| 办法 | 写法 | 适用范围 |
|---|---|---|
| ① 用 `curl.exe`（**最直接**，Windows 10 1803+ 自带） | `curl.exe -s URL -H "X-Forwarded-For: 127.0.0.1"` | 任何 PowerShell |
| ② **换 Git Bash**（装 Git 时自带，**Linux 写法直接可用**） | 开 Git Bash 窗口，`curl -s URL -H "..."` | 一劳永逸 |
| ③ 临时删掉别名 | `Remove-Item Alias:curl -ErrorAction SilentlyContinue`，然后照常敲 `curl` | 只影响当前窗口 |
| ④ **永久删掉别名**（推荐） | 见下面「一劳永逸」 | 所有新窗口 |

> ⚠️ **`Set-Alias curl curl.exe` 是无效的** —— 内置别名带 `AllScope` 属性，会报
> `The AllScope option cannot be removed from the alias 'curl'`。只能 `Remove-Item` 把它删掉。

### 一劳永逸：把别名写死删掉

在 **Windows PowerShell** 里执行一次：

```powershell
# 1) 确保 profile 文件存在（已存在就跳过这步）
if (-not (Test-Path $PROFILE)) { New-Item -Path $PROFILE -ItemType File -Force | Out-Null }

# 2) 追加一行（不会覆盖你已有的配置，比如 oh-my-posh）
Add-Content -Path $PROFILE -Encoding UTF8 -Value 'Remove-Item Alias:curl -ErrorAction SilentlyContinue'

# 3) 让当前窗口立刻生效
. $PROFILE
```

之后**任何新开的 PowerShell 窗口**里，`curl` 都是真的 curl 了。

查一下你的 profile 在哪：`$PROFILE`。

### 也可以直接换 pwsh 7

`pwsh`（PowerShell 7+）**已经移除了** `curl` 别名，所以同样的命令能直接跑。

```powershell
# 看自己现在是哪个版本
$PSVersionTable.PSVersion
# 5.x  → 有别名，需要上面那些处理
# 7.x+ → 没别名，直接跑

# 已经装了 pwsh 的话，从开始菜单开 "PowerShell 7" 即可
pwsh
```

> 判断自己在哪个 shell：`$PSVersionTable.PSVersion` + `Get-Alias curl`（有输出就是被劫持）。

### 其它命令对照表

| 题解里的写法（Linux） | Windows PowerShell 等价写法 |
|---|---|
| `curl -s URL -H "A: B"` | `curl.exe -s URL -H "A: B"` |
| `echo 'Z3Vlc3Q=' \| base64 -d` | `[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('Z3Vlc3Q='))` |
| `echo -n 'admin' \| base64` | `[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('admin'))` |
| `echo '756e…' \| xxd -r -p` | `$b = for ($i=0; $i -lt $hex.Length; $i+=2) { [Convert]::ToByte($hex.Substring($i,2),16) }; [Text.Encoding]::UTF8.GetString($b)` |
| `unzip www.zip -d out` | `Expand-Archive www.zip -DestinationPath out` |
| `grep 关键词 文件` | `Select-String -LiteralPath 文件 -Pattern 关键词` |
| `strings -e l .DS_Store` | Windows 没有 `strings`，用 Python（见示例站题解第 8 条） |
| `dirsearch -u URL` | `pip install dirsearch`，然后 `dirsearch -u URL`（它本身就是 Python 脚本） |

`base64` / `xxd` / `strings` / `unzip` / `grep` 都是 Linux 自带、Windows 默认没有的命令。

### 实测过的一条（header-ctf 第 8087 端口）

```powershell
# ❌ PowerShell 5.1 里这样是错的（被别名劫持）
curl -s http://127.0.0.1:8087/ -H "X-Forwarded-For: 127.0.0.1" -H "User-Agent: AdminBot/1.0"

# ✅ 这样是对的
curl.exe -s http://127.0.0.1:8087/ -H "X-Forwarded-For: 127.0.0.1" -H "User-Agent: AdminBot/1.0"

# ✅ 或者纯 PowerShell
Invoke-WebRequest http://127.0.0.1:8087/ -UseBasicParsing `
  -Headers @{"X-Forwarded-For"="127.0.0.1"; "User-Agent"="AdminBot/1.0"}
```

---

## 项目结构

```
web-ctf/
├── http-message-ctf/
│   ├── src/                  # 站点根目录
│   │   ├── index.php
│   │   ├── flag.inc.php      # 动态 flag 读取器
│   │   └── 1.jpg
│   ├── Dockerfile
│   ├── docker-compose.yml
│   └── README.md             # 题解
├── sqli-ctf/                 # 唯一带数据库的一题
│   ├── init.sql              # 建表 + 种子数据
│   ├── src/
│   ├── Dockerfile
│   ├── docker-compose.yml    # web + db 两个服务
│   └── README.md
├── robots-ctf/
│   └── ...                   # 同 http-message-ctf 结构
├── infoleak-demo/            # 教学示例站（不参与题目编号）
│   ├── src/                  # 真实博客构建产物 + 后加的漏洞点
│   ├── dotgit/               # 构建时 COPY 成 /var/www/html/.git，用于演示 .git 泄露
│   ├── Dockerfile
│   ├── docker-compose.yml
│   └── README.md
├── connect-ctf/               # 赛前引导题：进去就看见 flag
├── ...（共 12 道题 + 1 个示例站）
├── .gitignore
└── README.md
```

约定：

- 站点源码一律放 `src/`（全部题目统一为 `src/` 结构）
- 每道题自带一份 `flag.inc.php`，**不共享**，方便单独打包部署
- 每题一个独立端口

---

## 验证情况

已在真实 Docker 环境（Docker Desktop 29.6.1）逐题跑过：

- **12 个镜像**（11 题 + 1 示例站）全部构建成功
- 全部实例同时启动，8080–8089 + 8091 端口就绪，示例站占 8090
- **12 个实例全量校验通过**：正例拿到 flag、反例不给 flag、
  12 个动态 flag 互不相同且与各自容器内 `/tmp/.web_ctf_flag` 一致
- `FLAG=flag{...} docker compose up -d` 注入实测生效
- 所有容器日志无 PHP Fatal / Parse / Warning

---

## 新增一题的步骤

1. 建目录 `xxx-ctf/`，源码放 `src/`
2. 复制一份 `flag.inc.php` 到 `src/`
3. 在需要的页面 `require __DIR__ . '/flag.inc.php';` 然后 `echo ctf_flag();`
4. `Dockerfile`：`FROM php:7.4-apache` + `COPY src/ /var/www/html/`
5. `docker-compose.yml`：分配一个没被占用的端口，`environment` 里留 `- FLAG=${FLAG:-}` 和 `TZ`
6. 写 `README.md` 题解，跑一遍确认能打通

---

## ⚠️ 安全提醒

这些应用**故意包含漏洞**：注入、上传木马、伪造身份等等。

- 只在**本机或受控内网**运行
- 默认 `ports` 映射是 `0.0.0.0`，暴露到公网前请改成 `"127.0.0.1:8082:80"`
- `upload-ctf` 传上去的木马能执行任意 PHP，别放在有真实数据的机器上

---


