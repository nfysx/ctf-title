<?php
/**
 * XAIU-NCTF 2026 · nc 使用 —— 裸 TCP 服务
 *
 * 设计要点：
 *   1) 连上先给 banner（不含 flag）；
 *   2) 收到第一行后再判：开头像 HTTP 请求 → 说明"这不是 Web 服务"然后断开；
 *      否则 → 发 flag，并进入回显模式（证明 nc 是双向的）。
 *   3) 全程非阻塞 + stream_select，一个客户端挂着不影响别人。
 *
 * 只用 PHP 内置的 stream_* 函数，不需要任何扩展。
 */
declare(strict_types=1);

require __DIR__ . '/flag.inc.php';

date_default_timezone_set(getenv('TZ') ?: 'Asia/Shanghai');

const HTTP_RX = '/^(GET|POST|HEAD|PUT|DELETE|OPTIONS|PATCH|TRACE|CONNECT)\s/i';

function log_line(string $msg): void {
  @fwrite(STDOUT, "[" . date("Y-m-d H:i:s") . "] " . $msg . "\n");
}


const BANNER = <<<'TXT'
============================================================
  XAIU-NCTF 2026 - nc usage
============================================================


  nc (netcat; on Windows usually ncat) is a raw TCP client/server
  tool, widely used to debug network services.
    nc <ip> <port>            connect to a TCP port
    nc -lvnp 4444             listen on 4444 locally
    nc -zv <ip> 1-1000        scan ports
    nc <ip> <port> < req.txt  send a file as the request
    nc -lvnp 4444 > out.bin   save whatever comes back
  add -w 5 for a timeout, Ctrl+C to quit.

  Note: this connection is bidirectional.
------------------------------------------------------------
 Press ENTER to get the flag.
============================================================
TXT;

const FLAG_BLOCK = <<<'TXT'

------------------------------------------------------------
[ok] You connected with nc.
     A browser cannot reach this: it is not a web service.

Your Flag:

TXT;

const ECHO_TIP = <<<'TXT'
------------------------------------------------------------
tips: type a few more lines, the server echoes them back.
------------------------------------------------------------

TXT;

const HTTP_REJECT = <<<'TXT'

[!] This looks like an HTTP request (GET/POST/HEAD ...).

    This environment is not a web service, so browsers and curl cannot talk to it.
    Use instead:  nc <address> <port>   then press ENTER.

    Hint: wsrx maps the link to a TCP address (e.g. 127.0.0.1:12345),
    not to an http:// URL.
TXT;

function send_all($sock, string $data): bool {
  $len = strlen($data);
  $off = 0;
  while ($off < $len) {
    $n = @fwrite($sock, substr($data, $off));
    if ($n === false || $n === 0) {
      return false;
    }
    $off += $n;
  }
  return true;
}

// Windows 终端（conhost / GBK 代码页）对裸 LF 兼容差，出站统一 CRLF。
function crlf(string $s): string {
  return str_replace("\n", "\r\n", $s);
}

// 回显一批行；遇到 exit/quit 或写失败返回 false（表示该断开）。
function echo_lines($sock, string $payload): bool {
  foreach (explode("\n", $payload) as $line) {
    $line = rtrim($line, "\r");
    if (trim($line) === "") {
      continue;
    }
    $t = trim($line);
    if ($t === "exit" || $t === "quit") {
      @send_all($sock, "bye\r\n");
      return false;
    }
    if (!send_all($sock, "  | " . $line . "\r\n")) {
      return false;
    }
  }
  return true;
}

$bind          = getenv("BIND_ADDR") ?: "0.0.0.0";
$port          = (int) (getenv("BIND_PORT") ?: 1337);
$bannerTimeout = (int) (getenv("BANNER_TIMEOUT") ?: 90);
$echoTimeout   = (int) (getenv("ECHO_TIMEOUT") ?: 180);
$maxClients    = (int) (getenv("MAX_CLIENTS") ?: 128);

$server = @stream_socket_server("tcp://" . $bind . ":" . $port, $errno, $errstr);
if (!$server) {
  fwrite(STDERR, "listen failed on " . $bind . ":" . $port . " -> " . $errstr . " (" . $errno . ")\n");
  exit(1);
}
stream_set_blocking($server, false);
log_line("listening on " . $bind . ":" . $port);

$flag    = ctf_flag();
$clients = [];
$nextId  = 0;

while (true) {
  $read = [$server];
  foreach ($clients as $c) {
    $read[] = $c["sock"];
  }
  $write = null;
  $except = null;
  $n = @stream_select($read, $write, $except, 1);
  if ($n === false) {
    usleep(200000);
    continue;
  }

  foreach ($read as $sock) {
    if ($sock === $server) {
      $conn = @stream_socket_accept($server, 0);
      if (!$conn) {
        continue;
      }
      if (count($clients) >= $maxClients) {
        @fwrite($conn, "server is busy, please retry later.\r\n");
        @fclose($conn);
        continue;
      }
      stream_set_blocking($conn, false);
      $rid = (int) $conn;
      $cid = ++$nextId;
      $clients[$rid] = [
        "id"     => $cid,
        "sock"   => $conn,
        "state"  => "banner",
        "since"  => microtime(true),
        "peer"   => (string) @stream_socket_get_name($conn, true),
        "lines"  => 0,
      ];
      log_line("connect #" . $cid . " (rid " . $rid . ") from " . $clients[$rid]["peer"]);
      if (!send_all($conn, crlf(BANNER))) {
        @fclose($conn);
        unset($clients[$rid]);
      }
      continue;
    }

    $rid = (int) $sock;
    if (!isset($clients[$rid])) {
      continue;
    }
    $data = @fread($sock, 8192);
    if ($data === false || $data === "") {
      log_line("disconnect #" . $clients[$rid]["id"] . " (eof)");
      @fclose($sock);
      unset($clients[$rid]);
      continue;
    }

    if ($clients[$rid]["state"] === "banner") {
      if (preg_match(HTTP_RX, $data)) {
        log_line("http-like request rejected from " . $clients[$rid]["peer"]);
        @send_all($sock, crlf(HTTP_REJECT));
        @fclose($sock);
        unset($clients[$rid]);
        continue;
      }
      if (trim($data) === "") {
        // 只是一个回车，也算"敲门"
        $data = "\n";
      }
      log_line("handshake ok #" . $clients[$rid]["id"] . " (banner -> echo)");
      $ok = send_all($sock, crlf(FLAG_BLOCK) . $flag . "\r\n\r\n" . crlf(ECHO_TIP));
      if (!$ok) {
        @fclose($sock);
        unset($clients[$rid]);
        continue;
      }
      $clients[$rid]["state"] = "echo";
      $clients[$rid]["since"] = microtime(true);
      $clients[$rid]["lines"] = 0;
      // 同一批数据里剩下的行（piped 输入常见）也要回显，别丢
      $nl = strpos($data, "\n");
      $rest = $nl === false ? "" : substr($data, $nl + 1);
      if ($rest !== "" && !echo_lines($sock, $rest)) {
        @fclose($sock);
        unset($clients[$rid]);
      }
      continue;
    }

    // state === echo
    $clients[$rid]["lines"]++;
    $clients[$rid]["since"] = microtime(true);
    if (!echo_lines($sock, $data)) {
      @fclose($sock);
      unset($clients[$rid]);
    }
  }

  // 空闲回收
  $now = microtime(true);
  foreach ($clients as $rid => $c) {
    $limit = $c["state"] === "banner" ? $bannerTimeout : $echoTimeout;
    if (($now - $c["since"]) < $limit) {
      continue;
    }
    if ($c["state"] === "banner") {
      @send_all($c["sock"], "\r\nTimeout: press ENTER after connecting, then try again.\r\n");
    } else {
      @send_all($c["sock"], "\r\nIdle timeout, connection closed.\r\n");
    }
    @fclose($c["sock"]);
    unset($clients[$rid]);
    log_line("timeout #" . $c["id"] . " (" . $c["state"] . ")");
  }
}