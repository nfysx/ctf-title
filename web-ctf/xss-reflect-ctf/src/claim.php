<?php
/**
 * 凭会话标识领 flag
 *
 * 三道门槛（都过了才给）：
 *   1. sid 必须是本服务端签发的（HMAC 签名对得上 —— 自己编的 sid 过不了）
 *   2. 请求带的 sid 必须和当前浏览器 Cookie 里的 sid 完全一致
 *   3. 请求必须是从页面发起的（带 Referer）—— 直接在地址栏敲会被拒
 *
 * 第 3 条就是为什么「F12 里抄一下 Cookie 再手动打开这个地址」不成立。
 */
require __DIR__ . '/sid.inc.php';
header('Content-Type: text/plain; charset=utf-8');

$cookie_sid = isset($_COOKIE['sid']) ? (string)$_COOKIE['sid'] : '';
$given_sid  = isset($_GET['sid']) ? (string)$_GET['sid'] : $cookie_sid;

// 1. 签名
if (!ctf_sid_valid($cookie_sid)) {
    http_response_code(403);
    echo "这个 sid 不是服务端签发的（签名不对），先刷新一下首页拿个新的\n";
    exit;
}

// 2. 与当前会话一致
if (!hash_equals($cookie_sid, $given_sid)) {
    http_response_code(403);
    echo "sid 和当前会话对不上\n";
    exit;
}

// 3. 必须从页面发起
$ref = isset($_SERVER['HTTP_REFERER']) ? (string)$_SERVER['HTTP_REFERER'] : '';
if ($ref === '' || strpos($ref, '/claim.php') !== false) {
    http_response_code(403);
    echo "这个地址不能直接打开 —— 得让页面里的 claim() 帮你请求\n";
    exit;
}

echo ctf_flag();
