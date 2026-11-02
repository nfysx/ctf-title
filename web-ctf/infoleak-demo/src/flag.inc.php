<?php
/**
 * 动态 flag 读取器（与其他题目一致）
 *   1) 优先环境变量 FLAG
 *   2) 未注入则首次请求生成随机 flag，缓存在 /tmp/.web_ctf_flag
 */
function ctf_random_flag(): string
{
    $b = random_bytes(16);
    $b[6] = chr((ord($b[6]) & 0x0f) | 0x40);
    $b[8] = chr((ord($b[8]) & 0x3f) | 0x80);
    $h = bin2hex($b);
    return sprintf('flag{%s-%s-%s-%s-%s}',
        substr($h,0,8), substr($h,8,4), substr($h,12,4), substr($h,16,4), substr($h,20,12));
}
function ctf_flag(): string
{
    static $c = null;
    if ($c !== null) return $c;
    $env = getenv('FLAG');
    if (is_string($env) && trim($env) !== '') return $c = trim($env);
    $f = sys_get_temp_dir() . '/.web_ctf_flag';
    if (is_readable($f)) {
        $v = trim((string)@file_get_contents($f));
        if ($v !== '') return $c = $v;
    }
    $v = ctf_random_flag();
    @file_put_contents($f, $v);
    return $c = $v;
}
