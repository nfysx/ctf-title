<?php
header('Content-Type: text/html; charset=utf-8');

// 静态谜面：编码链是固定的，flag 依然是动态的
$PUZZLE = 'NzU2ZTZjNmY2MzZiMmU3MDY4NzAzZjYzNmY2NDY1M2Q2MjZjNzU2NTYyNjk3MjY0';
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>双码临门</title>
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#0e1512;color:#d6e8dd}
.card{background:#16211b;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.5);max-width:680px;width:100%}
h1{margin-top:0;font-size:22px;color:#6ee7a8}code{background:#0e1512;padding:2px 6px;border-radius:4px;color:#fde68a}
pre{background:#0e1512;padding:14px;border-radius:8px;color:#6ee7a8;overflow-x:auto;word-break:break-all;white-space:pre-wrap}
p{line-height:1.8}.tip{color:#8aa595;font-size:13px;line-height:1.8}
</style></head>
<body><div class="card">
<h1>双码临门</h1>
<p> 想想常见编码吧 </p>
<pre><?php echo htmlspecialchars($PUZZLE, ENT_QUOTES, 'UTF-8'); ?></pre>
<p class="tip">
    编码编码，真是编码啊（
</p>
</div></body></html>
