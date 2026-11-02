<?php
require __DIR__ . '/flag.inc.php';

date_default_timezone_set(getenv('TZ') ?: 'Asia/Shanghai');

$flag = ctf_flag();
$ua   = $_SERVER['HTTP_USER_AGENT'] ?? '未知';
$ip   = $_SERVER['REMOTE_ADDR'] ?? '未知';
$time = date('Y-m-d H:i:s');

header('Content-Type: text/html; charset=utf-8');
header('X-Challenge: connect-ctf');
header('X-Flag-Hint: you-already-have-it');
?><!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>连接成功 · XAIU-NCTF 2026</title>
<style>
  :root{color-scheme:dark}
  *{box-sizing:border-box}
  body{
    margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
    font-family:system-ui,-apple-system,"Segoe UI","Noto Sans SC",sans-serif;
    background:radial-gradient(1200px 600px at 50% -10%,#16324f 0%,#0b1017 55%,#070a0f 100%);
    color:#c9d1d9;padding:32px 20px;
  }
  .card{
    width:100%;max-width:720px;background:#11161dcc;border:1px solid #22303d;
    border-radius:16px;padding:40px 44px;box-shadow:0 24px 70px rgba(0,0,0,.65);
    backdrop-filter:blur(6px);
  }
  .badge{
    display:inline-flex;align-items:center;gap:8px;font-size:13px;letter-spacing:.4px;
    color:#3fb950;background:#0f2a1a;border:1px solid #1f6f39;border-radius:999px;
    padding:5px 12px;margin-bottom:22px;
  }
  .dot{width:8px;height:8px;border-radius:50%;background:#3fb950;box-shadow:0 0 10px #3fb950}
  h1{margin:0 0 10px;font-size:26px;color:#e6edf3;font-weight:650}
  .sub{margin:0 0 28px;color:#8b949e;line-height:1.8;font-size:15px}
  .label{font-size:12px;letter-spacing:1.6px;text-transform:uppercase;color:#6e7681;margin-bottom:10px}
  .flagval{
    display:block;width:100%;background:#0b0f14;border:1px dashed #2f81f7;border-radius:10px;
    padding:18px 20px;font-family:ui-monospace,SFMono-Regular,Consolas,"Liberation Mono",monospace;
    font-size:17px;color:#79c0ff;word-break:break-all;line-height:1.6;
  }
  .flagval::selection{background:#2f81f7;color:#fff}
  hr{border:0;border-top:1px solid #21262d;margin:28px 0 20px}
  .meta{display:grid;grid-template-columns:88px 1fr;gap:8px 14px;font-size:13.5px;color:#8b949e}
  .meta b{color:#c9d1d9;font-weight:500}
  .tips{margin-top:22px;font-size:13px;color:#6e7681;line-height:1.9}
  .tips code{color:#f0b429;background:#0b0f14;padding:1px 6px;border-radius:4px}
  a{color:#58a6ff;text-decoration:none}
</style>
</head>
<body>
  <main class="card">
    <div class="badge"><span class="dot"></span>ENVIRONMENT CONNECTED</div>
    <h1>连接成功，欢迎来到 XAIU-NCTF 2026</h1>
   

    <div class="label">Your Flag</div>
    <code class="flagval"><?= htmlspecialchars($flag, ENT_QUOTES, 'UTF-8') ?></code>

    <hr>
    <div class="meta">
      <b>请求时间</b><span><?= htmlspecialchars($time) ?></span>
      <b>来源地址</b><span><?= htmlspecialchars($ip) ?></span>
      <b>User-Agent</b><span><?= htmlspecialchars($ua) ?></span>
    </div>
  </main>
</body>
</html>
