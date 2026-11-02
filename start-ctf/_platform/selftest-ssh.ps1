# ssh-ctf 自测脚本（对着已启动的容器跑）
#   用法： pwsh ./selftest-ssh.ps1 -Port 8098 [-ExpectFlag "flag{...}"]
#   依赖： docker 镜像 ssh-client:latest（alpine + openssh-client + sshpass）
param(
  [int]$Port = 8098,
  [string]$ExpectFlag = "",
  [string]$ClientImage = "ssh-client:latest"
)

$ErrorActionPreference = "Stop"
$script:fail = 0

function Check([string]$name, [bool]$ok, [string]$detail = "") {
  if (-not $ok) { $script:fail++ }
  $tag = if ($ok) { "PASS" } else { "FAIL" }
  Write-Host ("[{0}] {1}  {2}" -f $tag, $name, $detail)
}

function Invoke-Ssh {
  param(
    [string]$Pass,
    [string]$Remote,
    [bool]$Tty = $false,
    [string]$StdinText = ""
  )
  $ttyArg = ""
  if ($Tty) { $ttyArg = "-tt " }
  $tmpl = "sshpass -p {0} ssh {1}-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR -o NumberOfPasswordPrompts=1 -p {2} xaiu@host.docker.internal {3}"
  $remoteArg = ""
  if ($Remote) { $remoteArg = "'" + $Remote + "'" }   # 必须加引号，否则 ; 会在本地被执行
  $inner = $tmpl -f ("`"" + $Pass + "`""), $ttyArg, $Port, $remoteArg
  if ($StdinText) { $inner = "printf {0} | {1}" -f ("`"" + $StdinText + "`""), $inner }
  $out = docker run --rm --add-host host.docker.internal:host-gateway $ClientImage -c $inner 2>&1
  return ($out -join "`n")
}

Write-Host ("== ssh-ctf selftest @ 127.0.0.1:" + $Port + " ==")

$r1 = Invoke-Ssh -Pass "sec" -Remote "whoami; cat /tmp/flag"
Check "T1 密码登录成功" ($r1 -match "xaiu") ($r1 -split "`n" | Select-Object -First 1)
Check "T1 /tmp/flag 可读" ($r1 -match "flag\{") ""
if ($ExpectFlag) { Check "T1 flag 与预期一致" ($r1 -match [regex]::Escape($ExpectFlag)) $ExpectFlag }

$r2 = Invoke-Ssh -Pass "wrong" -Remote "id"
Check "T2 错误密码被拒" ($r2 -match "Permission denied") ""

$r3 = Invoke-Ssh -Pass "sec" -Remote "" -Tty $true -StdinText "exit\n"
Check "T3 motd 显示" ($r3 -match "XIAU-NCTF 2026 - ssh") ""

$ok = 0
for ($i = 0; $i -lt 5; $i++) { if ((Invoke-Ssh -Pass "sec" -Remote "echo LOGIN_OK") -match "LOGIN_OK") { $ok++ } }
Check "T4 连续 5 次登录" ($ok -eq 5) ("ok=" + $ok + "/5")

$r5 = Invoke-Ssh -Pass "sec" -Remote "echo ALIVE"
Check "T5 服务持续可用" ($r5 -match "ALIVE") ""

Write-Host ""
if ($script:fail -eq 0) { Write-Host "ALL PASS" -ForegroundColor Green } else { Write-Host ("FAILED: " + $script:fail) -ForegroundColor Red; exit 1 }