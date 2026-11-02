<?php
require __DIR__ . '/../flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

$t = isset($_GET['token']) ? (string)$_GET['token'] : '';
$ok = ($t === 'sk-9f2c1a7b');
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>后台 · 星记</title>
<style>
body{font-family:"M PLUS Rounded 1c","Noto Sans SC",system-ui,sans-serif;
     background:#f7f3ee;color:#4a4453;display:flex;justify-content:center;
     align-items:center;min-height:100vh;margin:0}
.card{background:#fff;border-radius:22px;padding:34px 44px;box-shadow:0 10px 30px rgba(160,120,160,.14);max-width:600px;width:100%}
h1{font-size:20px;margin:0 0 10px;color:#e07fa8}
input{width:100%;box-sizing:border-box;padding:10px 14px;border-radius:12px;border:1px solid #e6dfe9;font-size:14px}
button{margin-top:14px;padding:10px 22px;border:0;border-radius:12px;background:#e07fa8;color:#fff;font-weight:600;cursor:pointer}
pre{background:#2b2735;color:#b9f5d0;border-radius:14px;padding:16px;overflow-x:auto}
p{font-size:14px;color:#8d8497;line-height:1.8}
</style></head>
<body><div class="card">
<?php if ($ok): ?>
  <h1>后台 · 已登录</h1>
  <p>欢迎回来，站长。</p>
  <pre><?php echo htmlspecialchars(ctf_flag(), ENT_QUOTES, 'UTF-8'); ?></pre>
<?php else: ?>
  <h1>后台登录</h1>
  <form method="get">
    <input name="token" placeholder="输入后台口令" autocomplete="off">
    <button type="submit">进入</button>
  </form>
  <p>口令没有写在页面上。想想站长平时会把东西<b>另存一份</b>到哪儿。</p>
<?php endif; ?>
</div></body></html>
