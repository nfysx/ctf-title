<?php
/**
 * 动态 flag 读取器
 *
 *  1) 优先读环境变量 FLAG（平台注入，推荐）
 *  2) 没有注入时用内置默认值，保证本地/自测时和题面写的 flag 一致
 */
function ctf_flag(): string
{
    static $cached = null;
    if ($cached !== null) { return $cached; }

    $env = getenv('FLAG');
    if (is_string($env) && trim($env) !== '') { return $cached = trim($env); }

    return $cached = 'flag{change_money_and_carry_both_conditions}';
}
