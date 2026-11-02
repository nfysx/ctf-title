<?php
require __DIR__ . '/../flag.inc.php';
header('Content-Type: text/html; charset=utf-8');
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>管理员后台</title>
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#1e1e2e;color:#cdd6f4}
.card{background:#313244;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.4);max-width:560px}
h1{margin-top:0;color:#a6e3a1;font-size:22px}pre{background:#1e1e2e;padding:14px;border-radius:8px;color:#f9e2af;overflow-x:auto}</style></head>
<body><div class="card">
<h1>欢迎您，管理员</h1>
<p>这是你要的东西：</p>
<pre><?php echo htmlspecialchars(ctf_flag(), ENT_QUOTES, 'UTF-8'); ?></pre>
</div></body></html>
