# 题解：秘密档案室（robots.txt 信息泄露）

> 对应课件 p31「信息泄露：先捡最容易的」第一条。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | `robots.txt` 泄露、目录探测思路 |
| 难度 | 1 |
| 端口 | 8082 |
| Flag 格式 | `flag{...}` |

一句话：站长在 `robots.txt` 里写了「哪些目录别被爬虫收录」，等于把**藏东西的位置**告诉了所有人。

> 找到它那一刻的心情：**我去，不早说**。

## 二、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd robots-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8082/
```

## 三、手把手解题

### Step 1｜首页什么也没有

打开首页只有一句话：「这里的资料不对外公开。但管理员太懒了，总有些东西忘了藏好。」

没有任何链接、没有表单 —— **这时候该去翻「约定俗成的位置」，而不是点页面**。

### Step 2｜第一件事：看 robots.txt

任何网站都先试这个：

```bash
curl http://127.0.0.1:8082/robots.txt
```

返回：

```
User-agent: *
Disallow: /admin_8f3a/
Disallow: /backup/
```

`Disallow` 的本意是「**请求爬虫不要访问**」，但它同时等价于「**这里有好东西**」。

### Step 3｜访问被禁的目录

```bash
curl http://127.0.0.1:8082/admin_8f3a/
```

页面直接给出 Flag。

另一个 `/backup/` 是干扰项（不存在 / 404）。

### Step 4｜顺手把「约定俗成的位置」养成习惯

真实环境里开局通常按这个顺序扫：

```
/robots.txt        /sitemap.xml      /.git/         /.svn/
/www.zip           /backup.zip       /.DS_Store     /phpinfo.php
/index.php.bak     /index.php~       /.index.php.swp
```

工具化：`dirsearch -u http://127.0.0.1:8082 -e php,txt,zip,bak` 或 `feroxbuster`。

## 四、为什么能成（原理）

`robots.txt` 是爬虫的**君子协定**，不是访问控制：

- 它只是纯文本，**任何浏览器都能读**
- 站长的「隐藏」动作，本身就是在公开一份目录清单
- 真实泄露案例里，`Disallow` 里经常出现 `/admin`、`/backup`、`/api/internal`

## 五、怎么修（防御）

- `robots.txt` 里**只写真的公开路径**，敏感目录根本不写（靠权限保护，不靠「不告诉别人」）
- 敏感目录要有认证：`.htaccess` / Nginx `auth_basic` / 应用层登录
- 目录不放在 Web 根下（备份文件、源码仓库请放在 Web 根之外）

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`（**明文**），未注入则首次请求自动生成随机 flag
并缓存到 `/tmp/.web_ctf_flag`。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```
