<?php
require __DIR__ . '/flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

// 两个条件都靠请求头判断 —— 而请求头是客户端说了算
$xff = isset($_SERVER['HTTP_X_FORWARDED_FOR']) ? trim((string)$_SERVER['HTTP_X_FORWARDED_FOR']) : '';
$ua  = isset($_SERVER['HTTP_USER_AGENT']) ? (string)$_SERVER['HTTP_USER_AGENT'] : '';

$cond1 = (strpos($xff, '127.0.0.1') !== false);          // 来源 IP
$cond2 = (stripos($ua, 'AdminBot') !== false);            // 采集机器人

$ok = $cond1 && $cond2;
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>内网管理入口</title>
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#101820;color:#d9e6ef}
.card{background:#1a2733;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.5);max-width:600px;width:100%}
h1{margin-top:0;font-size:22px;color:#7fd1ff}code{background:#101820;padding:2px 6px;border-radius:4px;color:#ffd479}
pre{background:#101820;padding:14px;border-radius:8px;color:#7fd1ff;overflow-x:auto}
ul{line-height:2}li.no{color:#f38ba8}li.yes{color:#a6e3a1}.tip{color:#8aa0b2;font-size:13px;line-height:1.7}
</style></head>
<body><div class="card">
<h1>内网管理入口</h1>
<p class="tip">本入口仅允许来自127.0.0.1的AdminBot访问</p>
<ul>
  <li class="<?php echo $cond1 ? 'yes' : 'no'; ?>">
      当前来源 IP：<?php echo $cond1 ? '通过' : '不通过'; ?>
      　当前值：<code><?php echo $xff === '' ? '(未提供)' : htmlspecialchars($xff, ENT_QUOTES, 'UTF-8'); ?></code>
  </li>
  <li class="<?php echo $cond2 ? 'yes' : 'no'; ?>">
      客户端标识判定：<?php echo $cond2 ? '通过' : '不通过'; ?>
      　当前值：<code><?php echo htmlspecialchars(substr($ua, 0, 60), ENT_QUOTES, 'UTF-8'); ?></code>
  </li>
</ul>
<?php if ($ok): ?>
  <h2>校验通过</h2>
  <pre><?php echo htmlspecialchars(ctf_flag(), ENT_QUOTES, 'UTF-8'); ?></pre>
<?php else: ?>
  <p class="tip"> tips：两个条件都满足才给哦<br>
   tips: 了解一下XFF吧！
  </p>
<?php endif; ?>
</div></body></html>
