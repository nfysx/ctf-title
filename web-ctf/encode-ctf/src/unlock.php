<?php
require __DIR__ . '/flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

$code = isset($_GET['code']) ? (string)$_GET['code'] : '';
$ok = ($code === 'bluebird');
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>门后</title>
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#0e1512;color:#d6e8dd}
.card{background:#16211b;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.5);max-width:600px}
h1{margin-top:0;font-size:22px;color:#6ee7a8}pre{background:#0e1512;padding:14px;border-radius:8px;color:#6ee7a8;overflow-x:auto}
p{line-height:1.8}.tip{color:#8aa595;font-size:13px}
</style></head>
<body><div class="card">
<?php if ($ok): ?>
  <h1>编码开门！</h1>
  <pre><?php echo htmlspecialchars(ctf_flag(), ENT_QUOTES, 'UTF-8'); ?></pre>
<?php else: ?>
  <h1>无效编码！</h1>
  <p> 再试试吧 </p>
<?php endif; ?>
</div></body></html>
