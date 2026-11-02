<?php
/**
 * 访问统计（站长自己加的小脚本）
 */
$id   = isset($_GET['id']) ? $_GET['id'] : '1';
$file = __DIR__ . '/data/visit-' . $id . '.json';

// 没做存在性判断 —— 报错会把绝对路径吐出来
$raw  = file_get_contents($file);
$data = json_decode($raw, true);
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8">
<title>访问统计 · 星记</title>
<style>
body{font-family:"M PLUS Rounded 1c","Noto Sans SC",system-ui,sans-serif;
     background:#f7f3ee;color:#4a4453;display:flex;justify-content:center;
     align-items:center;min-height:100vh;margin:0}
.card{background:#fff;border-radius:22px;padding:34px 44px;box-shadow:0 10px 30px rgba(160,120,160,.14);max-width:560px}
h1{font-size:20px;margin:0 0 6px;color:#e07fa8}
p{margin:6px 0;font-size:14px;color:#8d8497}
b{color:#4a4453}
</style></head>
<body><div class="card">
<h1>访问统计</h1>
<p>页面：<b><?php echo htmlspecialchars($id, ENT_QUOTES, 'UTF-8'); ?></b></p>
<p>累计访问：<b><?php echo isset($data['hits']) ? (int)$data['hits'] : 0; ?></b> 次</p>
<p>最后来源：<b><?php echo isset($data['last']) ? htmlspecialchars($data['last'], ENT_QUOTES, 'UTF-8') : '-'; ?></b></p>
</div></body></html>
