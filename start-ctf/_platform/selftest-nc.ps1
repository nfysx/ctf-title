# nc-ctf 自测脚本（对着已启动的容器跑）
#   用法： pwsh ./selftest-nc.ps1 -Port 8096 [-ExpectFlag "flag{...}"]
param(
  [int]$Port = 8096,
  [string]$HostAddr = "127.0.0.1",
  [string]$ExpectFlag = ""
)

$ErrorActionPreference = "Stop"
$script:fail = 0

function Read-Available($stream, [int]$settleMs = 250, [int]$rounds = 8) {
  $buf = New-Object byte[] 16384
  $sb = New-Object System.Text.StringBuilder
  for ($i = 0; $i -lt $rounds; $i++) {
    if ($stream.DataAvailable) {
      $n = $stream.Read($buf, 0, $buf.Length)
      if ($n -gt 0) { [void]$sb.Append([Text.Encoding]::UTF8.GetString($buf, 0, $n)) }
    } else {
      Start-Sleep -Milliseconds $settleMs
      if (-not $stream.DataAvailable) { break }
    }
  }
  return $sb.ToString()
}

function New-Conn() {
  $c = New-Object System.Net.Sockets.TcpClient
  $c.Connect($HostAddr, $Port)
  $c.ReceiveTimeout = 4000
  return $c
}

function Send-Text($stream, [string]$text) {
  $bytes = [Text.Encoding]::UTF8.GetBytes($text)
  $stream.Write($bytes, 0, $bytes.Length)
  $stream.Flush()
}

function Check([string]$name, [bool]$ok, [string]$detail = "") {
  if (-not $ok) { $script:fail++ }
  $tag = if ($ok) { "PASS" } else { "FAIL" }
  Write-Host ("[{0}] {1}  {2}" -f $tag, $name, $detail)
}

Write-Host "== nc-ctf selftest @ ${HostAddr}:${Port} =="

# T1: banner（不含 flag）
$c = New-Conn; $s = $c.GetStream()
$banner = Read-Available $s
Check "T1 连上即出 banner" ($banner -match "nc usage") ("len=" + $banner.Length)
Check "T1 banner 里没有 flag" (-not ($banner -match "flag\{")) ""
$c.Close()

# T2: 回车换 flag
$c = New-Conn; $s = $c.GetStream()
$null = Read-Available $s
Send-Text $s "`n"
$resp = Read-Available $s
$hasFlag = $resp -match "flag\{"
Check "T2 回车后拿到 flag" $hasFlag ""
if ($ExpectFlag) { Check "T2 flag 与注入值一致" ($resp -match [regex]::Escape($ExpectFlag)) $ExpectFlag }
Check "T2 回显提示存在" ($resp -match "echoes them back") ""
$c.Close()

# T3: HTTP 请求被拒（不给 flag）
$c = New-Conn; $s = $c.GetStream()
$null = Read-Available $s
Send-Text $s "GET / HTTP/1.1`r`nHost: probe`r`nConnection: close`r`n`r`n"
$resp = Read-Available $s
Check "T3 HTTP 被识别" ($resp -match "not a web service") ""
Check "T3 HTTP 不给 flag" (-not ($resp -match "flag\{")) ""
$c.Close()

# T4: 一条连接内的完整对话（回车 -> flag -> 回显 -> exit）
$c = New-Conn; $s = $c.GetStream()
$null = Read-Available $s
Send-Text $s "`n"
$null = Read-Available $s
Send-Text $s "hello nc`n"
$echo = Read-Available $s
Check "T4 回显生效" ($echo -match "hello nc") ""
Send-Text $s "exit`n"
$bye = Read-Available $s
Check "T4 exit 可断开" ($bye -match "bye") ""
$c.Close()

# T5: 并发不互相阻塞（A 挂在 banner 态，B 仍能立刻拿到 banner）
$a = New-Conn; $sa = $a.GetStream()
$null = Read-Available $sa
$sw = [Diagnostics.Stopwatch]::StartNew()
$b = New-Conn; $sb2 = $b.GetStream()
$bannerB = Read-Available $sb2
$sw.Stop()
Check "T5 第二条连接立刻拿到 banner" (($bannerB -match "nc usage") -and ($sw.Elapsed.TotalSeconds -lt 2)) ("elapsed=" + [math]::Round($sw.Elapsed.TotalSeconds,2) + "s")
$b.Close(); $a.Close()

# T6: 连上即断 x20，服务端仍然正常
for ($i = 0; $i -lt 20; $i++) { $x = New-Conn; $x.Close() }
Start-Sleep -Milliseconds 400
$c = New-Conn; $s = $c.GetStream()
$banner2 = Read-Available $s
Check "T6 20 次秒断后仍可服务" ($banner2 -match "nc usage") ""
$c.Close()

Write-Host ""
if ($script:fail -eq 0) { Write-Host "ALL PASS" -ForegroundColor Green } else { Write-Host ("FAILED: " + $script:fail) -ForegroundColor Red; exit 1 }
