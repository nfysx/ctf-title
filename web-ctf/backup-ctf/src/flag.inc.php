<?php
/**
 * 动态 flag 读取器
 *
 *  1) 优先读环境变量 FLAG（平台/运维注入，推荐做法）
 *  2) 没有注入时，生成一个随机 flag 并缓存到文件，
 *     保证同一实例内多次请求返回同一个值
 *
 * 用法： require __DIR__ . '/flag.inc.php';  echo ctf_flag();
 */

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

    $env = getenv('FLAG');
    if (is_string($env) && trim($env) !== '') {
        return $cached = trim($env);
    }

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
