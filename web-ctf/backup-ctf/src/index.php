<?php
require __DIR__ . '/flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

$TOKEN = 'debug-2026';          // 调试接口口令，正式环境应关闭
$out = '';

if (isset($_GET['debug']) && $_GET['debug'] === $TOKEN) {
    $out = ctf_flag();
}
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>内部通知系统</title>
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#f5f1e8;color:#2a2a35}
.card{background:#fff;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.12);max-width:560px}
h1{margin-top:0;font-size:22px}code{background:#eee8dc;padding:2px 6px;border-radius:4px;color:#b4472a}
pre{background:#2a2a35;color:#7de3a1;padding:14px;border-radius:8px;overflow-x:auto}
.tip{color:#8a8676;font-size:14px}</style></head>
<body><div class="card">
<h1>内部通知系统 v1.0</h1>
<p>系统正在升级，功能暂不可用。</p>
<p class="tip">运维备注：记得把调试入口关掉。</p>
<?php if ($out !== ''): ?>
  <h2>调试输出</h2>
  <pre><?php echo htmlspecialchars($out, ENT_QUOTES, 'UTF-8'); ?></pre>
<?php endif; ?>
</div></body></html>
