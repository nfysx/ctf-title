<?php
require __DIR__ . '/flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

// 身份完全由客户端 Cookie 决定
// 而且服务端只是把它 base64 解码一下，就直接信了 —— 编码不是保护
if (!isset($_COOKIE['user'])) {
    // 第一次来：种一个初始身份（guest 的 base64 编码）
    setcookie('user', base64_encode('guest'), [
        'path'     => '/',
        'httponly' => false,   // 故意不设 HttpOnly，好让学生能在控制台里看到/改
        'samesite' => 'Lax',
    ]);
    $raw_cookie = base64_encode('guest');
} else {
    $raw_cookie = (string)$_COOKIE['user'];
}

$decoded  = base64_decode($raw_cookie, false);   // 宽容模式：少个 = 也能解
$identity = ($decoded === false || $decoded === '') ? 'guest' : $decoded;
$is_admin = ($identity === 'admin');
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>个人中心</title>
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#0b0f14;color:#d7e0ea}
.card{background:#151c26;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.5);max-width:580px;width:100%}
h1{margin-top:0;font-size:22px;color:#8ee6c1}code{background:#0b0f14;padding:2px 6px;border-radius:4px;color:#ffd479}
pre{background:#0b0f14;padding:14px;border-radius:8px;color:#8ee6c1;overflow-x:auto}
.badge{display:inline-block;padding:3px 10px;border-radius:999px;background:#26303c;font-size:13px}
.warn{color:#8a97a6;font-size:14px;line-height:1.8}</style></head>
<body><div class="card">
<h1>个人中心</h1>
<p>当前身份：<b><?php echo htmlspecialchars($identity, ENT_QUOTES, 'UTF-8'); ?></b>
   <span class="badge"><?php echo $is_admin ? 'admin' : 'user'; ?></span></p>
<hr>
<?php if ($is_admin): ?>
  <h2>欢迎您，管理员</h2>
  <pre><?php echo htmlspecialchars(ctf_flag(), ENT_QUOTES, 'UTF-8'); ?></pre>
<?php else: ?>
  <p class="warn"> 您当前是访客，服务器无访问权限</p>
  <p class="warn">饼干不对</p>
<?php endif; ?>
</div></body></html>
