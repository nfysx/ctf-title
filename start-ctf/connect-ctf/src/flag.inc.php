<?php
/**
 * flag 读取器（连接平台，题目自带一份，不共享）
 *
 * 取值顺序（逻辑沿用 web-ctf，flag 值按 misc-ctf 的写法直接写在文件里）：
 *   1) 环境变量 FLAG —— 平台 checker 的 environ() 注入，比赛时走这条；
 *   2) 本文件的常量 CTF_FLAG —— 静态可读 flag，本地/离线跑也能拿到同一串；
 *   3) 都没有就随机 UUIDv4，缓存到容器 /tmp，保证同一实例内一致。
 *
 * 用法： require __DIR__ . '/flag.inc.php';  echo ctf_flag();
 */
const CTF_FLAG = 'flag{connect_the_platform_with_a_browser}';

function ctf_random_flag(): string
{
    $b = random_bytes(16);
    $b[6] = chr((ord($b[6]) & 0x0f) | 0x40);   // version 4
    $b[8] = chr((ord($b[8]) & 0x3f) | 0x80);   // variant
    $h = bin2hex($b);
    return sprintf('flag{%s-%s-%s-%s-%s}',
        substr($h, 0, 8), substr($h, 8, 4), substr($h, 12, 4),
        substr($h, 16, 4), substr($h, 20, 12));
}

function ctf_flag(): string
{
    static $cached = null;
    if ($cached !== null) {
        return $cached;
    }

    // 1) 平台注入
    $env = getenv('FLAG');
    if (is_string($env) && trim($env) !== '') {
        return $cached = trim($env);
    }

    // 2) 写死在文件里的静态 flag
    if (defined('CTF_FLAG') && trim((string) CTF_FLAG) !== '') {
        return $cached = trim((string) CTF_FLAG);
    }

    // 3) 随机兜底：同实例内保持一致
    $file = sys_get_temp_dir() . '/.web_ctf_flag';
    if (is_readable($file)) {
        $v = trim((string) @file_get_contents($file));
        if ($v !== '') {
            return $cached = $v;
        }
    }

    $v = ctf_random_flag();
    @file_put_contents($file, $v);
    return $cached = $v;
}