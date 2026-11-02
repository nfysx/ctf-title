<?php
require __DIR__ . '/flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

$host = getenv('DB_HOST') ?: 'db';
$name = getenv('DB_NAME') ?: 'ctf';
$user = getenv('DB_USER') ?: 'ctf';
$pass = getenv('DB_PASS') ?: 'ctf123';

// 数据库容器可能还没起来，重试一会儿
$pdo = null;
$connErr = '';
for ($i = 0; $i < 40; $i++) {
    try {
        $pdo = new PDO("mysql:host={$host};port=3306;dbname={$name};charset=utf8mb4",
                       $user, $pass, [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]);
        break;
    } catch (Exception $e) {
        $connErr = $e->getMessage();
        usleep(500000);
    }
}

$msg = '';
$level = '';

if ($pdo && isset($_POST['username'], $_POST['password'])) {
    $u = $_POST['username'];
    $p = $_POST['password'];

    // 故意拼接 —— 真实项目里必须用预处理
    $sql = "SELECT * FROM users WHERE username = '$u' AND password = '$p'";
    try {
        $rows = $pdo->query($sql)->fetchAll(PDO::FETCH_ASSOC);
        if (!$rows) {
            $msg = '用户名或密码错误';
        } else {
            $row = $rows[0];
            if ($row['role'] === 'admin') {
                $level = 'admin';
                $msg = '登录成功：' . $row['username'];
            } else {
                $msg = '登录成功，但你是「' . htmlspecialchars($row['username'], ENT_QUOTES, 'UTF-8') . '」，不是管理员。';
            }
        }
    } catch (Exception $e) {
        $msg = 'SQL 执行出错：' . $e->getMessage();
    }
}
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>登录 · 青鸟管理系统</title>
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#0f172a;color:#e2e8f0}
.card{background:#1e293b;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.5);max-width:560px;width:100%}
h1{margin-top:0;font-size:22px;color:#7dd3fc}label{display:block;margin:14px 0 6px;font-size:14px;color:#94a3b8}
input{width:100%;padding:9px 12px;border-radius:6px;border:1px solid #334155;background:#0f172a;color:#e2e8f0;box-sizing:border-box}
button{margin-top:18px;padding:10px 22px;border:0;border-radius:6px;background:#7dd3fc;color:#0f172a;font-weight:600;cursor:pointer}
.msg{margin-top:18px;padding:12px;border-radius:6px;background:#0f172a;border-left:3px solid #7dd3fc;word-break:break-all}
pre{background:#0f172a;padding:14px;border-radius:8px;color:#86efac;overflow-x:auto}
.err{margin-top:18px;padding:12px;border-radius:6px;background:#3b1d1d;border-left:3px solid #f87171;color:#fca5a5;word-break:break-all}
</style></head>
<body><div class="card">
<h1>青鸟管理系统 · 登录</h1>
<?php if (!$pdo): ?>
  <div class="err">数据库连接失败：<?php echo htmlspecialchars($connErr, ENT_QUOTES, 'UTF-8'); ?></div>
<?php endif; ?>
<form method="post">
  <label>用户名</label><input name="username" autocomplete="off">
  <label>密码</label><input name="password" autocomplete="off">
  <button type="submit">登录</button>
</form>
<?php if ($msg !== ''): ?><div class="msg"><?php echo htmlspecialchars($msg, ENT_QUOTES, 'UTF-8'); ?></div><?php endif; ?>
<?php if ($level === 'admin'): ?>
  <h2>正在疯狂注入ing</h2>
  <pre><?php echo htmlspecialchars(ctf_flag(), ENT_QUOTES, 'UTF-8'); ?></pre>
<?php endif; ?>
</div></body></html>
