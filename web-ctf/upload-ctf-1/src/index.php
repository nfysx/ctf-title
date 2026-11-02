<?php
require __DIR__ . '/flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

$msg = '';
$link = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_FILES['f'])) {
    $f = $_FILES['f'];
    $name = basename($f['name']);

    if ($f['error'] !== UPLOAD_ERR_OK) {
        $msg = '上传失败：' . $f['error'];
    } elseif (move_uploaded_file($f['tmp_name'], __DIR__ . '/uploads/' . $name)) {
        $msg = '上传成功：uploads/' . htmlspecialchars($name, ENT_QUOTES, 'UTF-8');
        $link = 'uploads/' . rawurlencode($name);
    } else {
        $msg = '保存失败，检查 uploads 目录权限';
    }
}
?>
<!DOCTYPE html>
<html lang="zh"><head><meta charset="UTF-8"><title>头像上传</title>
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#101a18;color:#e6f5f0}
.card{background:#1a2724;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.5);max-width:580px;width:100%}
h1{margin-top:0;font-size:22px;color:#7ee0c0}label{display:block;margin:14px 0 6px;font-size:14px;color:#a8c8bd}
button{margin-top:18px;padding:10px 22px;border:0;border-radius:6px;background:#7ee0c0;color:#101a18;font-weight:600;cursor:pointer}
.msg{margin-top:18px;padding:12px;border-radius:6px;background:#101a18;border-left:3px solid #7ee0c0;word-break:break-all}
.tip{color:#8aa9a0;font-size:13px;line-height:1.7}code{background:#101a18;padding:2px 6px;border-radius:4px;color:#fde68a}
</style></head>
<body><div class="card">
<h1>上传你的文件</h1>
<form method="post" enctype="multipart/form-data">
  <input type="file" name="f">
  <button type="submit">上传</button>
</form>
<?php if ($msg !== ''): ?>
  <div class="msg"><?php echo $msg; ?>
  <?php if ($link !== ''): ?><br>访问：<a href="<?php echo $link; ?>" style="color:#7ee0c0"><?php echo $link; ?></a><?php endif; ?>
  </div>
<?php endif; ?>
<p class="tip">tips:文件上传嘛...传点最好的语言文件吧！</p>
</div></body></html>
