<?php
/**
 * ============================================================================
 *  股海惊涛 · 模拟炒股（目标一亿）
 *  CTF Web 题（抓包改包）· 服务端自动计时 · 破产自动重开
 * ----------------------------------------------------------------------------
 *  玩法：
 *      本金 MONEY（默认 5 万），终点 TARGET（默认 1 亿）。
 *      页面每 TICK_INTERVAL 秒自动催服务端过一天，资金乘一个随机系数
 *      （TICK_MIN ~ TICK_MAX，默认 0.40 ~ 1.70 —— 几何均值约 0.83，
 *        钱会持续缩水，所以"破产"是常态，"到一亿"纯看运气）。
 *      资金不足 1 元 => 破产 => 自动重开一局，一直循环，玩家一句话都不用点。
 *
 *  两个"条件"（参数名 = 前缀 + 20 位十六进制，即 10 个随机字节；
 *  每个会话都不同，源码里看不到，只能从抓到的包里抄）：
 *
 *      条件A  破产条件  bankrupt_has_xxxxxxxxxxxxxxxxxxxx
 *              破产那一刻起为真，之后每个请求都带着，直到「重新开始」把它清掉。
 *
 *      条件B  中途条件  on_the_way_xxxxxxxxxxxxxxxxxxxx
 *              资金落到终点的 45%~55%（4500万 ~ 5500万）时留下；
 *              资金超过 5500万 之后就没了。
 *
 *  终局判定：
 *      到一亿之后，用同一会话把请求重发一次（action=noop = 只查状态），
 *      这一步请求里必须同时带上两个条件，才会下发 flag；
 *      缺任何一个 => 「错过了路上的风景」。
 *      "到一亿"存在服务端会话里，所以光看源码构造请求没用，得先把游戏玩到一亿。
 *
 *  出题备注：flag 写在 DEFAULT_FLAG，容器里可用环境变量 FLAG 覆盖。
 * ============================================================================
 */

header('Content-Type: text/html; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

/* --------------------------- 配置（只改这里） ---------------------------- */

const MONEY         = 50000;        // 起始资金 —— 只有一个钱数配置
const TARGET        = 100000000;    // 终点（一亿），固定
const TICK_MIN      = 0.40;         // 每日系数下限
const TICK_MAX      = 1.70;         // 每日系数上限
/* 多少秒自动过一天（浏览器端定时器 + 服务端节流）。
   自测时可传 TICK_INTERVAL=0 关掉节流，让请求打多快就走多快。 */
$TICK_INTERVAL_ENV = getenv('TICK_INTERVAL');
define('TICK_INTERVAL', ($TICK_INTERVAL_ENV === false || $TICK_INTERVAL_ENV === '')
    ? 1 : max(0, (float)$TICK_INTERVAL_ENV));

const MID_RATIO_LO  = 0.45;         // 条件B 区间下界 = 终点的 45%（4500万）
const MID_RATIO_HI  = 0.55;         // 条件B 区间上界 = 终点的 55%（5500万）
const AUTO_RESTART  = 1;            // 破产后自动重开（1=自动，0=停在弹窗等玩家点）
const LOG_KEEP      = 400;          // 最多保留多少条流水
const HISTORY_KEEP  = 2400;         // 资金曲线最多保留多少个点
const DAYS_MAX_ONE_GAME = 4000;     // 单局最多多少天（防止极端情况卡住）

define('MID_LO', (int)round(TARGET * MID_RATIO_LO));   // 条件B 下沿（4500万）
define('MID_HI', (int)round(TARGET * MID_RATIO_HI));   // 条件B 上沿（5500万）

/* 测试模式：设环境变量 DEBUG=1 时多出一个 action=debug（把状态置成到站 / 指定余额）。线上不要开。 */
$DEBUG_ENV = getenv('DEBUG');
define('DEBUG_MODE', ($DEBUG_ENV === '1' || $DEBUG_ENV === 'true'));
/* 测试注入后，保持多少秒不自动过天（方便测试观察注入的这个值） */
define('DEBUG_HOLD', 0.2);

/* flag：统一走 flag.inc.php（环境变量 FLAG 注入，未注入则用内置默认值） */

/* 两个条件参数名的固定前缀（后半段每个会话随机） */
const PREFIX_BANKRUPT = 'bankrupt_has_';
const PREFIX_MIDWAY   = 'on_the_way_';

/* 顶部警示标语（每 3 天换一条；会话开始时随机挑个起点，保证每条都有机会上首屏） */
$SLOGANS = array(
    '失败乃成功之母',
    '泰山在半山坡才开票',
    '股市有风险，入市需谨慎',
    '梭哈一时爽，天台排队长',
    '别人恐惧我贪婪，别人贪婪我贪婪',
    '涨停是运气，跌停是实力',
    '本页行情全是随机数，亏了别怪市场',
    '会买的是徒弟，会卖的是师傅，会空仓的是祖师爷',
    '你不赌，不就是在赌自己会输？',
    '账户归零不可怕，跟着老师傅再来五万！',
    '老实赚钱，很枯燥吧',
);

/* 右下角「换一个」可切换的主题（纯 CSS，不依赖外部图片） */
$THEMES = array(
    'midnight' => array('name' => '午夜盘口', 'bg' => 'linear-gradient(160deg,#05070f 0%,#0b1226 45%,#131c38 100%)', 'accent' => '#5aa9ff'),
    'ember'    => array('name' => '熔断之夜', 'bg' => 'linear-gradient(160deg,#150608 0%,#2a0b12 45%,#3d1219 100%)', 'accent' => '#ff6b6b'),
    'jade'     => array('name' => '绿肥红瘦', 'bg' => 'linear-gradient(160deg,#04120c 0%,#0a2418 45%,#123526 100%)', 'accent' => '#25d07a'),
    'violet'   => array('name' => '杠杆深渊', 'bg' => 'linear-gradient(160deg,#0d0518 0%,#1c0d33 45%,#2a1350 100%)', 'accent' => '#b98bff'),
    'sand'     => array('name' => '韭菜田野', 'bg' => 'linear-gradient(160deg,#141005 0%,#2a2109 45%,#3d3011 100%)', 'accent' => '#ffcf5c'),
    'steel'    => array('name' => '冷清交割', 'bg' => 'linear-gradient(160deg,#0a0c0f 0%,#171c22 45%,#232b33 100%)', 'accent' => '#9fb4c7'),
);
$THEME_KEYS = array_keys($THEMES);

/* ---------------------------- 兼容老版本 PHP ----------------------------- */

if (!function_exists('random_bytes')) {
    function random_bytes($length) {
        if (function_exists('openssl_random_pseudo_bytes')) {
            $strong = false;
            $bytes = openssl_random_pseudo_bytes($length, $strong);
            if ($bytes !== false && strlen($bytes) === $length) { return $bytes; }
        }
        if (function_exists('mcrypt_create_iv') && defined('MCRYPT_DEV_URANDOM')) {
            $bytes = @mcrypt_create_iv($length, MCRYPT_DEV_URANDOM);
            if ($bytes !== false && strlen($bytes) === $length) { return $bytes; }
        }
        $bytes = '';
        for ($i = 0; $i < $length; $i++) { $bytes .= chr(mt_rand(0, 255)); }
        return $bytes;
    }
}

/* --------------------------- 会话与请求处理 ------------------------------ */

/**
 * 会话文件目录。
 * 容器里用系统 temp 就行；有些环境（权限受限、中文路径下的老 PHP 等）写不了 temp，
 * 就依次退到源码目录下的 .sessions。判定方式是"真的建个文件试一下"，
 * 因为 is_writable() 有时并不可靠。
 */
function dir_really_writable($d) {
    if (!$d || !is_dir($d)) { return false; }
    $probe = rtrim($d, "/\\") . DIRECTORY_SEPARATOR . '.probe_' . getmypid() . '_' . mt_rand(1000, 9999);
    $ok = @file_put_contents($probe, 'x');
    if ($ok !== false) { @unlink($probe); return true; }
    return false;
}
function pick_session_dir() {
    $cands = array(
        sys_get_temp_dir(),          // 容器 / 常规环境
        __DIR__ . '/.sessions',      // 退路：源码目录下自建
        ini_get('session.save_path'),// 最后看 php.ini 配的
    );
    foreach ($cands as $d) {
        if (!$d) { continue; }
        $d = rtrim($d, "/\\");
        if (!is_dir($d)) { @mkdir($d, 0777, true); }
        if (dir_really_writable($d)) { return $d; }
    }
    return null;
}
$sessDir = pick_session_dir();
if ($sessDir !== null) { session_save_path($sessDir); }
$SESS_OK = ($sessDir !== null);

$secure = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off');
if (session_status() === PHP_SESSION_NONE) {
    /* 加 @ 抑制：实在没有可写目录时也不要把 Warning 打到页面上 */
    if (PHP_VERSION_ID >= 70300) {
        @session_start(array('cookie_httponly' => true, 'cookie_samesite' => 'Lax', 'cookie_secure' => $secure));
    } else {
        @session_start();
    }
}

require __DIR__ . '/flag.inc.php';
$flag = ctf_flag();

/* 每个会话随机生成两个 20 位十六进制（10 随机字节）条件名 */
if (empty($_SESSION['cond_bankrupt'])) {
    $_SESSION['cond_bankrupt'] = PREFIX_BANKRUPT . bin2hex(random_bytes(10));
}
if (empty($_SESSION['cond_midway'])) {
    $_SESSION['cond_midway'] = PREFIX_MIDWAY . bin2hex(random_bytes(10));
}
$COND_BANKRUPT = $_SESSION['cond_bankrupt'];
$COND_MIDWAY   = $_SESSION['cond_midway'];

/** 判断某个条件是否出现在本次请求里（只认"看起来为真"的值） */
function cond_flag_true($key) {
    if (!isset($_REQUEST[$key])) { return false; }
    $raw = $_REQUEST[$key];
    if (is_array($raw)) { return false; }
    $norm = strtolower(trim((string)$raw));
    return !in_array($norm, array('', '0', 'false', 'off', 'no', 'null', 'none'), true);
}

/**
 * 解析客户端上报的余额。
 * 页面自己发的包永远是纯数字，但手改包时很容易照抄界面上的 ¥100,000,000 这类写法，
 * 裸的 (float) 会得到 100 / 0 —— 这就是「金额改不了」。
 * 返回 null 表示这一串不是数字 => 忽略本次上报（不要当成 0 把账户打穿）。
 */
function parse_money_report($raw) {
    if (is_array($raw)) { return null; }
    $v = (string)$raw;
    $strip = array('¥', '￥', ',', '，', ' ', "\t", "\r", "\n", "\xc2\xa0", "\xe3\x80\x80");
    $v = str_replace($strip, '', $v);
    if ($v === '' || !is_numeric($v)) { return null; }
    return (float)$v;
}

$has_cond_bankrupt = cond_flag_true($COND_BANKRUPT);
$has_cond_midway   = cond_flag_true($COND_MIDWAY);

/* ------------------------------- 游戏状态 -------------------------------- */

/** 下一次可以过天的时间点（留 5ms 余量，保证"同一拍不会既重开又过天"） */
function next_tick_at() {
    return microtime(true) + TICK_INTERVAL + 0.005;
}

/** 全新一局：什么都不带（"不保留痕迹"） */
function fresh_state() {
    return array(
        'money'      => (float)MONEY,
        'days'       => 0,          // 本局天数
        'total_days' => 0,          // 累计天数
        'best'       => (float)MONEY,
        'history'    => array((float)MONEY),
        'log'        => array(),
        'bankrupt'   => false,
        'target'     => false,
        'midway_seen'=> false,      // 当前是否正站在 45%~55% 这一档
        'hit_midway' => false,      // 本局是否走到过这一档（条件B 的痕迹）
        'forced'     => false,      // 余额是被测试接口直接指定的
        'force_factor' => 0.0,      // 测试接口指定的当日系数（0 = 正常随机）
        'bankrupts'  => 0,          // 累计破产次数（只用于页面提示）
        'next_tick'  => next_tick_at(),
    );
}

if (!isset($_SESSION['game']) || !is_array($_SESSION['game']) || !isset($_SESSION['game']['money'])) {
    $_SESSION['game'] = fresh_state();
}
$state = &$_SESSION['game'];
foreach (fresh_state() as $k => $v) {
    if (!array_key_exists($k, $state)) { $state[$k] = $v; }
}

function roll_factor() {
    return round(TICK_MIN + (TICK_MAX - TICK_MIN) * (mt_rand() / mt_getrandmax()), 2);
}

function trim_state(&$state) {
    if (count($state['log']) > LOG_KEEP) { $state['log'] = array_slice($state['log'], -LOG_KEEP); }
    if (count($state['history']) > HISTORY_KEEP) { $state['history'] = array_slice($state['history'], -HISTORY_KEEP); }
}

/**
 * 过一天。
 * 页面的定时器每 TICK_INTERVAL 秒催一次；服务端再兜一层节流，
 * 免得有人狂刷请求把一天过成一百天。
 */
function do_day(&$state) {
    if ($state['target'] || $state['bankrupt']) { return false; }

    $now = microtime(true);
    if ($state['next_tick'] > 0 && $now < $state['next_tick']) { return false; }

    $old    = (float)$state['money'];
    /* 测试接口可以指定当日系数（DEBUG=1 时才可能非 0），正常玩法永远是 0 */
    $factor = ($state['force_factor'] > 0) ? (float)$state['force_factor'] : roll_factor();
    $state['force_factor'] = 0.0;
    $after  = round($old * $factor, 2);
    if ($after >= TARGET) { $after = (float)TARGET; }

    $state['money'] = $after;
    $state['days']++;
    $state['total_days']++;
    $state['history'][] = $after;
    if ($after > $state['best']) { $state['best'] = $after; }

    $note = ($factor >= 1) ? '盈利' : '亏损';
    if ($after >= TARGET) { $note = '到达终点'; }
    elseif ($after < 1)   { $note = '破产'; }
    $state['log'][] = array('day' => $state['days'], 'factor' => $factor, 'money' => $after, 'note' => $note);

    /* 单局天数上限，兜底用 */
    if ($state['days'] >= DAYS_MAX_ONE_GAME) {
        $state['money'] = 0.0;
        $after = 0.0;
        $state['log'][count($state['log']) - 1]['note'] = '清盘';
    }

    /* 站上 45%~55% 这一档：留下条件B 的痕迹 */
    mark_midway($state, $after);
    $state['forced'] = false;

    if ($after >= TARGET) {
        $state['target'] = true;
        $state['money']  = (float)TARGET;
    } elseif ($after < 1) {
        $state['money']    = 0.0;
        $state['bankrupt'] = true;
        $state['bankrupts']++;
    }

    trim_state($state);
    $state['next_tick'] = next_tick_at();
    return true;
}

/** 重新开始：一切归零，不留任何痕迹（破产痕迹随之从请求里消失） */
function do_restart(&$state) {
    $kept_total = (int)$state['total_days'];
    $kept_bank  = (int)$state['bankrupts'];
    $state = fresh_state();
    $state['total_days'] = $kept_total;      // 累计天数只是计数，不算"痕迹"
    $state['bankrupts']  = $kept_bank;
    $state['next_tick']  = next_tick_at();
    return '已重新开始：资金回到 ¥' . number_format(MONEY) . '，痕迹不留。';
}

/* ------------------------------ 请求处理 --------------------------------- */

$action  = isset($_REQUEST['action']) ? (string)$_REQUEST['action'] : '';
$ajax    = (isset($_REQUEST['ajax']) && (string)$_REQUEST['ajax'] === '1');
$is_tick = (isset($_REQUEST['tick']) && (string)$_REQUEST['tick'] === '1');
$since   = isset($_REQUEST['since']) ? (int)$_REQUEST['since'] : 0;

$flash = '';
$auto_restarted = false;

if ($action === 'restart') {
    $flash = do_restart($state);
} elseif ($action === 'noop') {
    $flash = '';          // 只查状态，用来"领取"到站结果
} elseif ($action === 'sync') {
    /* ------------------------------------------------------------------
       客户端余额上报：页面每拍把当前余额同步给服务端，服务端采纳。
       注意：上报值会在"过完这一天"之后再应用（见下面 $synced_money 那段），
       否则刚采纳就被当日行情覆盖了。
       ------------------------------------------------------------------ */
    $flash = '';
} elseif ($action === 'debug' && DEBUG_MODE) {
    /* 测试专用：把状态直接设成想要的样子。
         action=debug                    => 置成"到站一亿"
         action=debug&money=43e6         => 置成指定余额
         action=debug&money=46e6&factor=1 => 同时指定"下一拍的系数"（确定性验证用） */
    $dbg = isset($_REQUEST['money']) ? (float)$_REQUEST['money'] : (float)TARGET;
    if ($dbg <= 0) { $dbg = (float)TARGET; }
    $state['money']  = $dbg;
    $state['target'] = ($dbg >= TARGET);
    $state['bankrupt'] = false;
    $state['midway_seen'] = false;
    $state['forced'] = true;
    $state['force_factor'] = isset($_REQUEST['factor']) ? (float)$_REQUEST['factor'] : 0.0;
    /* 保证注入的值能稳定留一会儿（否则同一个请求里、或紧接着的请求里就被过天改掉了） */
    $state['next_tick'] = microtime(true) + DEBUG_HOLD;
    $state['history'][] = $dbg;
    $flash = '';
}

/* --------------------------------------------------------------------------
   客户端余额上报：请求里带了 money 参数就采纳。
   （页面每拍那个包形如 ajax=1&since=4&tick=1&money=96850 —— 抓包改这里的 money
     就能改金额。这里不要求 action=sync。）

   顺序很关键：必须在 do_day() 之前采纳。
   原版是先过天、再采纳，而页面报的是上一拍显示的旧值，等于每拍都用旧值把当天行情
   冲掉 —— 总资产会永远卡在起始的 50000（日志和曲线却在动），也就永远到不了一亿。
   放到前面之后，当日行情才是乘在上报值上的，余额会正常涨跌。
   -------------------------------------------------------------------------- */
$synced = false;
if (isset($_REQUEST['money'])) {
    $up = parse_money_report($_REQUEST['money']);
    if ($up === null) {
        /* 不是数字（照抄了界面上的 ¥100,000,000 / 写成 100万 等）=> 忽略本次上报 */
    } else {
        if ($up < 0)      { $up = 0.0; }
        if ($up > 1.0e12) { $up = 1.0e12; }        // 防离谱数值撑爆界面

        $state['money']    = $up;
        $state['target']   = ($up >= TARGET);
        $state['bankrupt'] = ($up < 1);
        if ($up > (float)$state['best']) { $state['best'] = $up; }
        $synced = true;
    }
}

/* 自动过一天（带节流） */
$now = microtime(true);
if ($state['next_tick'] <= 0 || $now >= $state['next_tick']) {
    do_day($state);
}

$just_bankrupt = (bool)$state['bankrupt'];   // 本次请求里刚刚破产（自动重开前记下来）

if ($synced) {
    /* 采纳过上报值：把过天推迟到下一拍之前一点，让上传的金额能被看见、也来得及抓包改 */
    $state['next_tick'] = microtime(true) + max(0.2, TICK_INTERVAL * 0.9);
    $just_bankrupt = (bool)$state['bankrupt'];
}


/* 破产后自动重开（同步上报的破产交给页面决定，不自动重开） */
if (AUTO_RESTART && $state['bankrupt'] && !$synced) {
    do_restart($state);
    $auto_restarted = true;
}

/* 余额变了就重新判定"这一档"（放在上报之后，条件B 的痕迹才准） */
mark_midway($state, (float)$state['money']);

/** 当前资金是否落在 45%~55% 这一档上 */
function in_midway($money) {
    return ($money >= MID_LO && $money <= MID_HI);
}

/**
 * 按当前余额刷新"这一档"的痕迹（条件B）。
 * 过完一天要调一次；客户端上报余额之后也要再调一次。
 */
function mark_midway(&$state, $money) {
    if (!$state['target'] && in_midway($money)) {
        $state['midway_seen'] = true;
        $state['hit_midway']  = true;
    }
    if ($state['midway_seen'] && $money > MID_HI) {
        $state['midway_seen'] = false;
    }
}

/* --------------------------- 标语 / 主题轮换 ----------------------------- */
/* 会话开始时随机挑一个起点（否则每次都从第一条开始，后面几条永远轮不到首屏） */
if (!isset($_SESSION['slogan_off'])) { $_SESSION['slogan_off'] = mt_rand(0, count($SLOGANS) - 1); }
$slogan_i = $_SESSION['slogan_off'] + (int)floor($state['total_days'] / 3);
$slogan   = $SLOGANS[$slogan_i % count($SLOGANS)];
$theme_k  = $THEME_KEYS[$slogan_i % count($THEME_KEYS)];
$theme    = $THEMES[$theme_k];

/* -------------------- 终局判定：到站 + 两个条件 = flag ------------------- */

$ending = '';        // '' | 'flag' | 'miss'
if ($state['target']) {
    /* 只要到站那一刻请求里同时带着两个条件，就给 flag。
       不再限定 action=noop —— 页面每拍只有 tick 这一个包，
       玩家抓包改的也是它，卡住 action 会让"改完却领不到奖"。 */
    if ($has_cond_bankrupt && $has_cond_midway) {
        $ending = 'flag';
    } else {
        $ending = 'miss';
    }
}

$miss_list = array();
if ($ending === 'miss') {
    if (!$has_cond_bankrupt) { $miss_list[] = '破产那一刻留下的条件'; }
    if (!$has_cond_midway)   { $miss_list[] = '45%~55% 那一档的条件'; }
}

/* ---------------------------- 视图辅助数据 -------------------------------- */

$money_now = (float)$state['money'];
/* "站在这一档上"只用于页面提示；条件B 的痕迹仍由过天后的落点决定 */
$midway_active = (!$state['target'] && in_midway($money_now));

$hist = $state['history'];
if (count($hist) > 400) {
    $hist = array_merge(array($hist[0]), array_slice($hist, -399));
}
$hist_f = array();
foreach ($hist as $v) { $hist_f[] = (float)$v; }

$log_all = $state['log'];
$log_out = $log_all;
if ($since > 0 && $since <= count($log_all)) { $log_out = array_slice($log_all, $since); }
$log_total = count($log_all);

$payload = array(
    'money'        => $money_now,
    'days'         => (int)$state['days'],
    'total_days'   => (int)$state['total_days'],
    'best'         => (float)$state['best'],
    'bankrupt'     => (bool)$state['bankrupt'],
    'just_bankrupt'=> $just_bankrupt,      // 前端据此记下"破产过"这个痕迹
    'target'       => (bool)$state['target'],
    'midway'       => (bool)$midway_active,     // 当前资金是否正落在 45%~55% 这一档
    'hit_midway'   => (bool)$state['hit_midway'],
    'bankrupts'    => (int)$state['bankrupts'],
    'auto_restart' => (bool)$auto_restarted,
    'log'          => $log_out,
    'log_total'    => $log_total,
    'history'      => $hist_f,
    'slogan'       => $slogan,
    'theme'        => array('key' => $theme_k, 'name' => $theme['name'], 'accent' => $theme['accent']),
    'ending'       => $ending,
    'miss_list'    => $miss_list,
    'message'      => $flash,
    'flag'         => ($ending === 'flag') ? base64_encode($flag) : '',
    'cond_bankrupt'=> $COND_BANKRUPT,
    'cond_midway'  => $COND_MIDWAY,
    /* 标语池：只用于自检/展示，不影响判定 */
    'slogans'      => $SLOGANS,
);

$json_flags = JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES;

/* 自动交易的轮询 / 任何带 action 的请求 => 返回 JSON */
if ($ajax || $is_tick || $action !== '') {
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode($payload, $json_flags);
    exit;
}
?>
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>股海惊涛</title>
<style>
  :root{
    --accent:<?php echo $theme['accent']; ?>;
    --panel:rgba(14,18,32,.78); --line:rgba(140,165,255,.16);
    --txt:#eef2ff; --dim:#94a1c8; --up:#ff4d5e; --down:#25d07a; --gold:#ffcf5c;
  }
  *{box-sizing:border-box;margin:0;padding:0}
  body{
    min-height:100vh;font-family:"Segoe UI","Microsoft YaHei",system-ui,sans-serif;color:var(--txt);
    background:<?php echo $theme['bg']; ?>;background-attachment:fixed;padding:14px 14px 70px;
  }
  /* 顶部正中：警示标语 + 旁边的「换一个」 */
  .slogan{text-align:center;margin:2px auto 16px;max-width:920px;
    font-size:20px;font-weight:700;letter-spacing:3px;color:var(--accent);
    text-shadow:0 0 22px rgba(255,255,255,.14);border-bottom:1px dashed var(--line);padding-bottom:11px}
  .slogan .stext{vertical-align:middle}
  .sloganbtn{display:inline-block;margin-left:12px;vertical-align:middle;
    font-size:12.5px;font-weight:600;letter-spacing:.5px;padding:4px 12px;border-radius:99px;
    color:var(--txt);background:transparent;border:1px solid var(--line);cursor:pointer;
    transition:.18s;line-height:1.5}
  .sloganbtn:hover{color:var(--accent);border-color:var(--accent);transform:translateY(-1px)}
  .slogan small{display:block;font-size:12px;font-weight:400;letter-spacing:1px;color:var(--dim);margin-top:6px}
  .wrap{max-width:1080px;margin:0 auto}
  .grid{display:grid;grid-template-columns:1.05fr .95fr;gap:16px}
  @media(max-width:900px){.grid{grid-template-columns:1fr}}
  .card{background:var(--panel);border:1px solid var(--line);border-radius:16px;padding:18px;
        box-shadow:0 18px 44px rgba(0,0,0,.5)}
  .card h2{font-size:13px;color:var(--dim);font-weight:600;letter-spacing:1px;margin-bottom:12px}
  .money{font-size:46px;font-weight:800;font-variant-numeric:tabular-nums;line-height:1.1;
         background:linear-gradient(92deg,#fff,var(--accent) 70%);-webkit-background-clip:text;background-clip:text;color:transparent}
  .row{display:flex;gap:18px;flex-wrap:wrap;margin-top:12px;color:var(--dim);font-size:13px}
  .row b{color:var(--txt);font-weight:600;font-variant-numeric:tabular-nums}
  /* 目标提示：一行文字（不放进度条 —— 5 万到 1 亿跨度太大，进度条基本不动） */
  .goalline{margin-top:12px;font-size:12.5px;color:var(--dim)}
  .goalline b{color:var(--txt);font-size:14px;font-variant-numeric:tabular-nums}
  .goalline.done b{color:var(--gold)}
  .goalline.done #goalnote{color:var(--gold)}
  .btns{display:flex;gap:10px;flex-wrap:wrap;margin-top:18px}
  button{font:inherit;font-weight:600;color:#08101f;border:0;border-radius:11px;padding:10px 18px;cursor:pointer;
         background:linear-gradient(180deg,#fff,var(--accent));transition:.18s;letter-spacing:.5px}
  button:hover{transform:translateY(-1px);filter:brightness(1.08)}
  button.ghost{background:transparent;color:var(--txt);border:1px solid var(--line)}
  .banner{margin-top:14px;border-left:3px solid var(--accent);background:rgba(255,255,255,.05);
          padding:11px 14px;border-radius:0 10px 10px 0;font-size:14px;line-height:1.7;min-height:44px}
  .banner.ok{border-color:var(--gold);background:rgba(255,207,92,.12)}
  .banner.bad{border-color:var(--up);background:rgba(255,77,94,.12)}
  table{width:100%;border-collapse:collapse;font-size:13px;font-variant-numeric:tabular-nums}
  th,td{padding:6px;text-align:right;border-bottom:1px solid rgba(255,255,255,.06)}
  th{color:var(--dim);font-weight:600;font-size:12px}
  th:first-child,td:first-child{text-align:left}
  .up{color:var(--up)} .down{color:var(--down)} .dim{color:var(--dim)}
  .scroll{max-height:360px;overflow:auto}
  .scroll::-webkit-scrollbar{width:8px}.scroll::-webkit-scrollbar-thumb{background:rgba(255,255,255,.14);border-radius:9px}
  .flagbox{margin-top:14px;border:1px dashed rgba(255,207,92,.5);background:rgba(255,207,92,.08);
           border-radius:12px;padding:14px;word-break:break-all;font-family:Consolas,monospace;font-size:13px;line-height:1.7}
  .themebtn{position:fixed;right:18px;bottom:18px;z-index:80;display:flex;align-items:center;gap:8px;
            background:rgba(10,14,26,.88);border:1px solid var(--line);color:var(--txt);
            border-radius:99px;padding:9px 15px;font-size:13px;cursor:pointer;backdrop-filter:blur(6px)}
  .themebtn:hover{border-color:var(--accent);color:var(--accent)}
  .themebtn i{width:12px;height:12px;border-radius:50%;background:var(--accent);display:inline-block}
  /* 破产弹窗：默认隐藏，破产时才由脚本加 .on 打开 */
  .mask{position:fixed;inset:0;background:rgba(3,6,14,.84);display:none;
        align-items:center;justify-content:center;padding:20px;z-index:99}
  .mask.on{display:flex}
  .dlg{max-width:420px;width:100%;background:#101728;border:1px solid var(--line);border-radius:18px;
       padding:26px;text-align:center;box-shadow:0 30px 70px rgba(0,0,0,.6)}
  .dlg h3{font-size:22px;margin-bottom:10px}
  .dlg p{color:var(--dim);font-size:14px;line-height:1.8;margin-bottom:18px}
  .dlg .btns{justify-content:center}
</style>
</head>
<body>
<div class="slogan" id="slogan">
  <span class="stext" id="slogantext"><?php echo htmlspecialchars($slogan, ENT_QUOTES, 'UTF-8'); ?></span>
  <button class="sloganbtn" id="btnSlogan" type="button" title="随机换一条警示标语">换一个</button>
  <small>本页行情由随机数生成，与真实市场无关 · 每 <?php echo TICK_INTERVAL; ?> 秒自动过一个交易日 · 破产将自动重开</small>
</div>

<div class="wrap">
  <div class="grid">
    <section class="card">
      <h2>账户总资产</h2>
      <div class="money" id="money"><?php echo '¥' . number_format($money_now); ?></div>
      <div class="row">
        <span>本局第 <b id="days"><?php echo (int)$state['days']; ?></b> 天</span>
        <span>累计 <b id="totaldays"><?php echo (int)$state['total_days']; ?></b> 天</span>
        <span>历史最高 <b id="best"><?php echo '¥' . number_format((float)$state['best']); ?></b></span>
      </div>

      <div class="goalline<?php echo $state['target'] ? ' done' : ''; ?>" id="goalline">
        目标 <b><?php echo '¥' . number_format(TARGET); ?></b>
        <span class="dim" id="goalnote"><?php echo $state['target'] ? '—— 已经到站' : ('—— 从 ' . '¥' . number_format(MONEY) . ' 出发'); ?></span>
      </div>

      <div class="btns">
        <button id="btnRestart" class="ghost">重新开始</button>
      </div>

      <div class="banner" id="banner">行情已开盘，本页会自动替你交易。</div>
      <?php if (!$SESS_OK): ?>
      <div class="banner bad" style="margin-top:8px">
        注意：当前环境没有可写的会话目录，成绩无法保存（仅本地调试会出现，容器里正常）。
      </div>
      <?php endif; ?>
      <div id="flagwrap" style="display:none"><div class="flagbox" id="flagbox"></div></div>
    </section>

    <section class="card">
      <h2>资金曲线</h2>
      <svg id="chart" viewBox="0 0 400 150" preserveAspectRatio="none" style="width:100%;height:150px;display:block">
        <defs>
          <linearGradient id="fill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="rgba(120,170,255,.5)"/>
            <stop offset="100%" stop-color="rgba(120,170,255,0)"/>
          </linearGradient>
        </defs>
        <polygon id="area" fill="url(#fill)" points=""/>
        <polyline id="line" fill="none" stroke="var(--accent)" stroke-width="2" points=""/>
      </svg>

      <h2 style="margin-top:16px">交易流水</h2>
      <div class="scroll">
        <table>
          <thead><tr><th>日</th><th>系数</th><th>余额</th><th>备注</th></tr></thead>
          <tbody id="logbody"></tbody>
        </table>
      </div>
    </section>
  </div>
</div>

<!-- 破产弹窗：只有一个「重新开始」 -->
<div class="mask" id="mask">
  <div class="dlg">
    <h3>你破产了</h3>
    <p>账户余额归零，这一局结束了。<br>要不要重来，你自己决定。</p>
    <div class="btns">
      <button id="btnDlgRestart">重新开始</button>
    </div>
  </div>
</div>

<button class="themebtn" id="btnTheme"><i></i><span id="themename"><?php echo htmlspecialchars($theme['name'], ENT_QUOTES, 'UTF-8'); ?></span> · 换一个</button>

<script>
(function () {
  var $ = function (id) { return document.getElementById(id); };
  /* 金额一律显示整数（不带小数点后的分位） */
  function fmt(n) {
    var v = Number(n);
    if (!isFinite(v)) { v = 0; }
    return '¥' + Math.round(v).toLocaleString('zh-CN', {maximumFractionDigits: 0});
  }

  var THEME_KEYS = <?php echo json_encode($THEME_KEYS, JSON_UNESCAPED_UNICODE); ?>;
  var THEMES = <?php echo json_encode($THEMES, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
  var TICK_MS = <?php echo (int)(TICK_INTERVAL * 1000); ?>;
  var COND_BANKRUPT = <?php echo json_encode($COND_BANKRUPT); ?>;
  var COND_MIDWAY   = <?php echo json_encode($COND_MIDWAY); ?>;

  var logShown = 0;          // 已经渲染了多少条流水
  var emptyShown = false;
  var busy = false;
  var currentThemeKey = <?php echo json_encode($theme_k); ?>;

  /* 顶部的「换一个」：从标语池里随机换一条（本地生效，不动服务端状态） */
  var SLOGANS = <?php echo json_encode($SLOGANS, JSON_UNESCAPED_UNICODE); ?>;
  var sloganForced = false;          // 玩家自己换过之后，就别被自动轮换覆盖
  function pickSlogan() {
    var cur = $('slogantext').textContent;
    if (SLOGANS.length < 2) { return; }
    for (var i = 0; i < 20; i++) {
      var cand = SLOGANS[Math.floor(Math.random() * SLOGANS.length)];
      if (cand !== cur) { $('slogantext').textContent = cand; return; }
    }
  }
  if ($('btnSlogan')) {
    $('btnSlogan').onclick = function () { sloganForced = true; pickSlogan(); };
  }

  /* 两个条件的生命周期：只在「当前真的满足」时才带在请求里，不留痕迹、不留存。
       条件A bankrupt_has_xxx  —— 只在破产那一刻（含刚破产的那一拍）出现
       条件B on_the_way_xxx    —— 只在资金正落在 45%~55% 那一档时出现
     其他时候请求里都没有它们；错过就没了，所以看到就得赶紧把参数名记下来。 */
  var curBankrupt = false;   // 当前是否处于破产状态
  var curMidway   = false;   // 当前资金是否正落在 45%~55% 档
  function clearConds() { curBankrupt = false; curMidway = false; }

  /* extra 是"key=value"形式的字符串（如 'action=restart'），直接拼进去。
     注意别写成 for..in —— 传字符串进去会把每个字符当成一个键（曾被这个坑过）。 */
  function buildBody(extra) {
    var parts = ['ajax=1', 'since=' + logShown];
    if (curBankrupt) { parts.push(COND_BANKRUPT + '=true'); }
    if (curMidway)   { parts.push(COND_MIDWAY + '=true'); }
    if (extra) { parts.push(extra); }
    return parts.join('&');
  }

  /* ---------- 自动交易 + 余额上报：合成一个请求 ----------
     每拍的请求体形如：
         ajax=1&since=4&tick=1&action=sync&money=96850
     服务端先采纳上报的 money，再过一天。所以抓包只要盯这一个包，
     把里面的 money 改成想要的值就能改金额。 */
  function tick() {
    if (busy) { return; }
    busy = true;
    var nowMoney = $('money').textContent.replace(/[^0-9.]/g, '');
    var extra = 'tick=1';
    if (nowMoney !== '') {
      /* 只带 money，不带 action=sync —— 这样 action 保持为空，
         到站领 flag 时不会被 action 值挡住（服务端要求 action=noop 才给 flag）。 */
      extra += '&money=' + encodeURIComponent(nowMoney);
    }
    fetch('index.php', {
      method: 'POST',
      headers: {'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', 'X-Requested-With': 'fetch'},
      body: buildBody(extra),
      credentials: 'same-origin'
    })
      .then(function (r) { return r.json(); })
      .then(function (st) { render(st); })
      .catch(function () { /* 网络抖一下就算了，下一轮接着来 */ })
      .then(function () { busy = false; setTimeout(tick, TICK_MS); });
  }

  function sendAction(action) {
    if (action === 'restart') { clearConds(); }   // 重新开始：两个条件一并清掉
    return fetch('index.php', {
      method: 'POST',
      headers: {'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', 'X-Requested-With': 'fetch'},
      body: buildBody('action=' + action),
      credentials: 'same-origin'
    }).then(function (r) { return r.json(); }).then(function (st) {
      $('mask').classList.remove('on');
      render(st);
    });
  }

  /* ---------- 渲染 ---------- */
  function render(st) {
    $('money').textContent = fmt(st.money);
    $('days').textContent = st.days;
    $('totaldays').textContent = st.total_days;
    $('best').textContent = fmt(st.best);
    /* 标语：玩家自己点过「换一个」就不再被自动轮换覆盖 */
    if (!sloganForced) { $('slogantext').textContent = st.slogan; }

    /* 流水：只把新增的插到最上面 */
    if (st.log_total < logShown) {                 // 重开过了，全部重画
      logShown = 0;
      $('logbody').innerHTML = '';
      emptyShown = false;
    }
    if (st.log && st.log.length) {
      if (emptyShown) { $('logbody').innerHTML = ''; emptyShown = false; }
      var html = st.log.map(function (it) {
        var f = (it.factor === null) ? '<span class="dim">—</span>'
              : '<span class="' + (it.factor >= 1 ? 'up' : 'down') + '">' + Number(it.factor).toFixed(2) + 'x</span>';
        var cls = it.note === '盈利' ? 'up' : (it.note === '亏损' ? 'down' : 'dim');
        return '<tr><td>' + it.day + '</td><td>' + f + '</td><td>' + fmt(it.money)
             + '</td><td class="' + cls + '">' + it.note + '</td></tr>';
      }).join('');
      $('logbody').insertAdjacentHTML('afterbegin', html);
      logShown = st.log_total;
      var all = $('logbody').querySelectorAll('tr');
      for (var i = 120; i < all.length; i++) { all[i].remove(); }
    }
    if (!logShown && !$('logbody').querySelector('tr')) {
      $('logbody').innerHTML = '<tr data-empty="1"><td colspan="4" class="dim">还没有交易记录</td></tr>';
      emptyShown = true;
    }

    drawChart(st.history);

    var msg = st.message || '';
    var cls = 'banner';
    if (st.ending === 'flag') {
      cls = 'banner ok';
      $('flagwrap').style.display = '';
      $('flagbox').innerHTML = '<span style="color:var(--gold);font-weight:700">✓ 你集齐了路上的风景，这就是你要的东西：</span><br>'
        + st.flag + '<br><span class="dim">（这是一串 Base64，解码后才是真正的 flag）</span>';
    } else if (st.ending === 'miss') {
      cls = 'banner bad';
      $('flagwrap').style.display = '';
      $('flagbox').innerHTML = '<span style="color:#ff9aa6;font-weight:700">✗ 错过了路上的风景</span><br>'
        + '<span class="dim">缺少：' + st.miss_list.join('、') + '</span>';
      if (!msg) { msg = '你到了一亿，可惜路上的痕迹没有带全。'; }
    } else {
      $('flagwrap').style.display = 'none';
      if (st.auto_restart && !msg) {
        msg = '这一局亏光了，已自动重开。累计破产 ' + st.bankrupts + ' 次。';
      }
      if (!msg) {
        msg = st.target ? '一亿，到了 —— 现在把两个条件带进请求里。'
            : '行情已开盘，本页会自动替你交易。';
      }
    }
    $('banner').className = cls;
    $('banner').innerHTML = msg;

    updateGoal(st.money, !!st.target);

    /* 两个条件只在「当前满足」时带在下一拍请求里：破产 / 正站在 45%~55% 档 */
    curBankrupt = !!(st.bankrupt || st.just_bankrupt);
    curMidway   = !!st.midway;
    /* 弹窗判定：以"这一拍刚破产"为准。
       注意不能只看 st.bankrupt —— 自动重开会把 bankrupt 立刻重置成 false，
       那样弹窗永远不会出现（之前就是这个 bug）。 */
    if ($('mask')) {
      var dlgTitle = document.querySelector('#mask h3');
      var dlgText = document.querySelector('#mask p');
      if (st.bankrupt) {
        if (dlgTitle) { dlgTitle.textContent = '你破产了'; }
        if (dlgText) { dlgText.textContent = '账户余额归零，这一局结束了。要不要再来一把？'; }
        $('mask').classList.add('on');
      } else if (st.just_bankrupt) {
        if (dlgTitle) { dlgTitle.textContent = '你破产了'; }
        if (dlgText) { dlgText.textContent = '账户余额归零，这一局已经自动重开了（累计天数保留）。'; }
        $('mask').classList.add('on');
      }
    }

    applyTheme(st.theme.key);
  }

  /* 目标提示：只更新那一行文字的状态 */
  var GOAL_LO = <?php echo (float)MONEY; ?>;
  function updateGoal(money, reached) {
    if (!$('goalline')) { return; }
    $('goalline').className = 'goalline' + (reached ? ' done' : '');
    if ($('goalnote')) {
      $('goalnote').textContent = reached
        ? '—— 已经到站'
        : ('—— 从 ' + fmt(GOAL_LO) + ' 出发');
    }
  }

  function drawChart(hist) {
    /* 对数刻度：余额在 5 万~10 万之间波动时，线性刻度会画成一条平线，
       看不出涨跌；按对数归一才能显出每一拍的起伏（尖峰）。 */
    var pts = (hist || []).map(Number).filter(function (n) { return n > 0; });
    var line = $('line'), area = $('area');
    if (pts.length === 0) { line.setAttribute('points', ''); area.setAttribute('points', ''); return; }
    var W = 400, H = 150, pad = 8;
    var lo = Math.log10(Math.max(1, Math.min.apply(null, pts)));
    var hi = Math.log10(Math.max(10, Math.max.apply(null, pts)));
    var span = Math.max(0.35, hi - lo);
    var n = pts.length;
    var xy = pts.map(function (v, i) {
      var x = (n === 1) ? pad : (i / (n - 1)) * (W - 2 * pad) + pad;
      var y = H - pad - ((Math.log10(v) - lo) / span) * (H - 2 * pad);
      return [x, Math.max(pad, Math.min(H - pad, y))];
    });
    if (n === 1) { xy.push([W - pad, xy[0][1]]); }
    var ptsStr = xy.map(function (p) { return p[0].toFixed(1) + ',' + p[1].toFixed(1); }).join(' ');
    line.setAttribute('points', ptsStr);
    area.setAttribute('points', xy[0][0].toFixed(1) + ',' + (H - pad) + ' ' + ptsStr + ' '
      + xy[xy.length - 1][0].toFixed(1) + ',' + (H - pad));
  }

  /* ---------- 右下角：换一个主题 ---------- */
  function applyTheme(key) {
    var t = THEMES[key];
    if (!t || key === currentThemeKey) { return; }
    currentThemeKey = key;
    document.documentElement.style.setProperty('--accent', t.accent);
    document.body.style.background = t.bg;
    $('themename').textContent = t.name;
  }
  $('btnTheme').onclick = function () {
    var i = THEME_KEYS.indexOf(currentThemeKey);
    applyTheme(THEME_KEYS[(i + 1) % THEME_KEYS.length]);
  };

  $('btnRestart').onclick = function () { sendAction('restart'); };
  $('btnDlgRestart').onclick = function () { sendAction('restart'); };

  /* 首次渲染用服务端算好的状态，然后开始自动交易 */
  var first = <?php echo json_encode($payload, $json_flags); ?>;
  render(first);
  logShown = first.log_total;
  if (!first.log_total) {
    $('logbody').innerHTML = '<tr data-empty="1"><td colspan="4" class="dim">还没有交易记录</td></tr>';
    emptyShown = true;
  }
  setTimeout(tick, TICK_MS);   // 每拍一个请求（里面同时带上余额上报）
})();
</script>
</body>
</html>
