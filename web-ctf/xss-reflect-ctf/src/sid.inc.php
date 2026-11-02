<?php
/**
 * 会话标识 sid 的签发与校验
 *
 * sid = 32 位随机 hex + 32 位 HMAC 签名
 * 签名密钥只存在服务端（/tmp/.web_ctf_sid_secret），客户端伪造不出来。
 */
require_once __DIR__ . '/flag.inc.php';

function ctf_sid_secret(): string
{
    static $s = null;
    if ($s !== null) return $s;

    $f = sys_get_temp_dir() . '/.web_ctf_sid_secret';
    if (is_readable($f)) {
        $v = trim((string)@file_get_contents($f));
        if (strlen($v) >= 32) return $s = $v;
    }
    $v = bin2hex(random_bytes(32));
    @file_put_contents($f, $v);
    return $s = $v;
}

/** 签发一个新 sid：32 hex 随机值 + 32 hex 签名 = 64 hex */
function ctf_make_sid(): string
{
    $raw = bin2hex(random_bytes(16));
    $sig = substr(hash_hmac('sha256', $raw, ctf_sid_secret()), 0, 32);
    return $raw . $sig;
}

/** 校验 sid 是否是本服务端签发的（签名对得上才算） */
function ctf_sid_valid(string $sid): bool
{
    if (!preg_match('/^[0-9a-f]{64}$/', $sid)) return false;
    $raw = substr($sid, 0, 32);
    $sig = substr($sid, 32);
    $expect = substr(hash_hmac('sha256', $raw, ctf_sid_secret()), 0, 32);
    return hash_equals($expect, $sig);
}
