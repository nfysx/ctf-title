<?php
/**
 * 反射型 XSS
 * 会话标识（sid）放在 Cookie 里 —— 但它只是标识，本身换不到任何东西
 * 想拿 flag，得让页面里的 claim() 跑起来
 */
require __DIR__ . '/sid.inc.php';
header('Content-Type: text/html; charset=utf-8');

// 每次访问发一个服务端签发的会话标识（与 flag 无关，不含任何信息）
if (!isset($_COOKIE['sid']) || !ctf_sid_valid((string)$_COOKIE['sid'])) {
    $sid = ctf_make_sid();
    setcookie('sid', $sid, time() + 3600, "/");
} else {
    $sid = (string)$_COOKIE['sid'];
}

// 禁用浏览器XSS防护
header("X-XSS-Protection: 0");

$greeting = '';
if (array_key_exists("name", $_GET) && $_GET['name'] !== '') {
    // 漏洞：直接拼接用户输入，无过滤
    $greeting = '<pre>' . $_GET['name'] . ' 不是你的名字</pre>';
}
?>
<!DOCTYPE html>
<html lang="zh">
<head>
    <meta charset="UTF-8">
    <title>你好啊，预言家</title>  
    <style>
        body {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
            font-family: sans-serif;
            background-image: url('1.jpg'); 
            background-size: cover;          
            background-position: center;    
            background-repeat: no-repeat;   
            background-attachment: fixed;   
            background-blend-mode: overlay; 
        }
        .container {
            background: rgba(255, 255, 255, 0.35);  
            backdrop-filter: blur(4px);  
            padding: 2rem 3rem;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
            text-align: center;
            max-width: 500px;
            width: 100%;
        }

        h2 {
            margin-top: 0;
        }

        form {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 10px;
        }

        input[type="text"] {
            width: 80%;
            padding: 8px 12px;
            border: 1px solid #ccc;
            border-radius: 4px;
            font-size: 16px;
        }

        input[type="submit"] {
            padding: 8px 20px;
            background: #007bff;
            color: white;
            border: none;
            border-radius: 4px;
            font-size: 16px;
            cursor: pointer;
        }

        input[type="submit"]:hover {
            background: #0056b3;
        }

        #result {
            margin-top: 20px;
            padding: 10px;
            background: rgba(255,255,255,0.7);
            border-radius: 4px;
            min-height: 40px;
        }

        .hint {
            margin-top: 20px;
            color: #333;
            font-size: 14px;
            line-height: 1.7;
        }
    </style>
    <script>
    function claim() {
        var m = document.cookie.match(/(?:^|;\s*)sid=([^;]+)/);
        var sid = m ? m[1] : '';
        var box = document.getElementById('result');
        if (!sid) { box.textContent = '没有 sid，先刷新一下页面'; return; }
        fetch('/claim.php?sid=' + encodeURIComponent(sid))
            .then(function (r) { return r.text(); })
            .then(function (t) { box.innerHTML = '<pre>' + t + '</pre>'; })
            .catch(function (e) { box.textContent = '领取失败：' + e; });
    }
    </script>
</head>
<body>
    <div class="container">
        <h2>输入你的名字</h2>
        <form method="GET" action="">
            <input type="text" name="name" value="<?php echo isset($_GET['name']) ? htmlspecialchars($_GET['name']) : ''; ?>">
            <input type="submit" value="完成">
        </form>
        <div id="result">
            <?php echo $greeting; ?>
        </div>
        <p class="hint">tips: 想吃饼干了呢，预言家。<br>
         tips: 源码...源码...</p>
    </div>
</body>
</html>
