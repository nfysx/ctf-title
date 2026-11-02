<?php
header('Content-Type: text/html; charset=utf-8');

require __DIR__ . '/flag.inc.php';
$flag = ctf_flag();   // 动态 flag：优先环境变量 FLAG，未注入则自动随机生成
$message = '';

function parse_raw_body() {
    $raw = file_get_contents('php://input');
    $params = [];
    parse_str($raw, $params);
    return $params;
}

$post_data = $_POST;
if (empty($post_data)) {
    $post_data = parse_raw_body();
}

// ---------- 第一关 ----------
if (isset($_COOKIE['cookie']) && $_COOKIE['cookie'] == 78) {
    $step1 = true;
} else {
    $step1 = false;
    $message = "预言家,没有饼干了,我需要 <img src='data:image/png;base64,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' width='500' height='200'/>个饼干。";
}

// ---------- 第二关 ----------
$priestess = isset($post_data['priestess']) ? trim($post_data['priestess']) : '';
if ($_SERVER['REQUEST_METHOD'] === 'POST' && $priestess === 'prophet') {
    $step2 = true;
} else {
    $step2 = false;
    if ($step1) $message = "预言家,现在的交流方法不太安全,改成一个安全的方式吧。而且你是知道女祭司和预言家是一对的,别忘用英语写在数据里哦。";
}

// ---------- 第三关 ----------
if (isset($_GET['admin']) && $_GET['admin'] == 1) {
    $step3 = true;
} else {
    $step3 = false;
    if ($step1 && $step2) $message = "预言家,你的链接里缺少了一个参数,管理员的值不为一哦。";
}

if ($step1 && $step2 && $step3) {
    $message = "不错呢预言家,这个就是你想要的了,但是别忘了编码问题哦。flag= " . base64_encode($flag);
}
?>
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>HTTP协议</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        body {
            font-family: Arial, sans-serif;
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
            background: url('1.jpg') no-repeat center center fixed;
            background-size: cover;
        }
        .card {
            background: rgba(255, 255, 255, 0.4);
            backdrop-filter: blur(8px);
            -webkit-backdrop-filter: blur(8px);
            padding: 40px 35px;
            border-radius: 16px;
            box-shadow: 0 8px 24px rgba(0, 0, 0, 0.3);
            max-width: 600px;
            width: 100%;
            margin: 20px;
            border: 1px solid rgba(255, 255, 255, 0.3);
        }
        h1 {
            text-align: center;
            color: #1a1a2e;
            font-weight: 600;
            border-bottom: 2px solid rgba(0, 0, 0, 0.1);
            padding-bottom: 15px;
            margin-bottom: 20px;
        }
        p {
            text-align: center;
            color: #333;
            margin: 10px 0;
        }
        .status {
            font-size: 18px;
            margin: 20px 0;
            padding: 15px;
            border-radius: 8px;
            background: rgba(255, 255, 255, 0.6);
            color: #1a1a2e;
            border-left: 4px solid #4a6fa5;
            word-break: break-word;
        }
        code {
            background: rgba(0, 0, 0, 0.08);
            padding: 2px 8px;
            border-radius: 4px;
            font-family: 'Courier New', monospace;
            color: #c7254e;
        }
        @media (max-width: 480px) {
            .card { padding: 25px 20px;}
        } 
    </style>
</head>
<body>
    <div class="card">
        <h1>预言家，我知道你为何而来</h1>
        <p>如果你想要，就自己来拿，这规矩你懂的</p>
        <div class="status">
            <?php echo $message; ?>
        </div>
    </div>
</body>
</html>