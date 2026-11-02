<?php
require __DIR__ . '/flag.inc.php';
header('Content-Type: text/html; charset=utf-8');

$msg = '';
$link = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_FILES['f'])) {
    $f = $_FILES['f'];
    $name = basename($f['name']);
    // 只信客户端声明的 Content-Type —— 这是本关唯一的检查
    $type = isset($f['type']) ? (string)$f['type'] : '';

    if ($f['error'] !== UPLOAD_ERR_OK) {
        $msg = '上传失败：' . $f['error'];
    } elseif (strpos($type, 'image/') !== 0) {
        $msg = '只允许上传图片（Content-Type 必须是 image/*），你上传的是：' . htmlspecialchars($type, ENT_QUOTES, 'UTF-8');
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
<style>body{font-family:system-ui,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#141018;color:#efe6f5}
.card{background:#221a2b;padding:36px 44px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,.5);max-width:580px;width:100%}
h1{margin-top:0;font-size:22px;color:#d8b4fe}label{display:block;margin:14px 0 6px;font-size:14px;color:#b9a8c8}
button{margin-top:18px;padding:10px 22px;border:0;border-radius:6px;background:#d8b4fe;color:#141018;font-weight:600;cursor:pointer}
.msg{margin-top:18px;padding:12px;border-radius:6px;background:#141018;border-left:3px solid #d8b4fe;word-break:break-all}
.tip{color:#9b8aa9;font-size:13px;line-height:1.7}code{background:#141018;padding:2px 6px;border-radius:4px;color:#fde68a}
</style></head>
<body><div class="card">
<h1>上传你的文件</h1>
<form method="post" enctype="multipart/form-data">
  <input type="file" name="f">
  <button type="submit">上传</button>
</form>
<?php if ($msg !== ''): ?>
  <div class="msg"><?php echo $msg; ?>
  <?php if ($link !== ''): ?><br>访问：<a href="<?php echo $link; ?>" style="color:#d8b4fe"><?php echo $link; ?></a><?php endif; ?>
  </div>
<?php endif; ?>
<p class="tip">tips:文件上传嘛...传点最好的语言的文件吧！<br>
    tips:注意请求头哦</p>
</div></body></html>
