<?php
require __DIR__ . '/flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

$t = isset($_GET['token']) ? (string)$_GET['token'] : '';
$ok = ($t === 'sk-9f2c1a7b');
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>调试入口 · 星记</title>
<style>
body{font-family:"M PLUS Rounded 1c","Noto Sans SC",system-ui,sans-serif;
     background:#f7f3ee;color:#4a4453;display:flex;justify-content:center;
     align-items:center;min-height:100vh;margin:0}
.card{background:#fff;border-radius:22px;padding:34px 44px;box-shadow:0 10px 30px rgba(160,120,160,.14);max-width:600px}
h1{font-size:20px;margin:0 0 10px;color:#e07fa8}
pre{background:#2b2735;color:#b9f5d0;border-radius:14px;padding:16px;overflow-x:auto}
p{font-size:14px;color:#8d8497;line-height:1.8}
</style></head>
<body><div class="card">
<?php if ($ok): ?>
  <h1>调试入口已解锁</h1>
  <p>你找到了运维忘删的东西。</p>
  <pre><?php echo htmlspecialchars(ctf_flag(), ENT_QUOTES, 'UTF-8'); ?></pre>
<?php else: ?>
  <h1>调试入口</h1>
  <p>需要 <code>token</code> 参数。</p>
  <p>提示：口令被运维写进了某个<b>备份文件</b>里 —— 备份通常不会出现在导航上。</p>
<?php endif; ?>
</div></body></html>
