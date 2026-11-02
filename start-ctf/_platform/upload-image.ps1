<#
.SYNOPSIS
  构建题目镜像 → docker save 成 <Image>.tar → 上传到 ret2shell 平台自带 registry。

.DESCRIPTION
  平台接口： POST {api}/game/{gameId}/registry（multipart，字段名 file）
  服务端用 skopeo 把 tar 复制进内部仓库；仓库名 = 文件名去掉扩展名（connect-nc.tar -> connect-nc），
  标签 = 上传时刻的 Unix 时间戳。
  tar 里的镜像必须是 linux/amd64（服务器 x86_64）。

.EXAMPLE
  pwsh ./upload-image.ps1 -Challenge ..\nc-ctf -Image connect-nc -SkipUpload
  pwsh ./upload-image.ps1 -Challenge ..\nc-ctf -Image connect-nc -Token eyJ...
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$Challenge,   # 题目目录（含 Dockerfile）
  [Parameter(Mandatory = $true)][string]$Image,       # registry 仓库名 = tar 文件名
  [string]$BuildTag = "",
  [string]$BaseUrl  = 'http://10.24.10.232',
  [string]$Token    = $env:R2S_TOKEN,
  [int]   $GameId   = 4,
  [switch]$SkipBuild,
  [switch]$SkipUpload
)

$ErrorActionPreference = 'Stop'
if (-not $BuildTag) { $BuildTag = $Image + ":latest" }
$dir = (Resolve-Path $Challenge).Path
$tar = Join-Path $dir ($Image + ".tar")
$api = $BaseUrl.TrimEnd('/') + '/api'

function Get-Api([string]$path) {
  $c = [System.Net.Http.HttpClient]::new()
  $c.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
  $r = $c.GetAsync($api + $path).GetAwaiter().GetResult()
  $body = $r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
  $c.Dispose()
  if (-not $r.IsSuccessStatusCode) { throw "GET $path -> $([int]$r.StatusCode) $body" }
  if ([string]::IsNullOrWhiteSpace($body)) { return $null }
  return $body | ConvertFrom-Json
}

$needApi = -not $SkipUpload
if ($needApi -and -not $Token) { throw 'need -Token or env R2S_TOKEN, or pass -SkipUpload' }

if ($needApi) {
  Write-Host '== 1/4 平台信息' -ForegroundColor Cyan
  $game = Get-Api ("/game/" + $GameId)
  $bucket = $game.bucket
  if (-not $bucket) { Write-Warning '   游戏 bucket 未初始化，镜像会落到 org "_"'; $bucket = '_' }
  $registry = Get-Api ("/game/" + $GameId + "/registry/config")
  if (-not $registry -or -not $registry.enabled) { throw 'platform registry is disabled' }
  Write-Host ("   game={0}  bucket={1}  external={2}" -f $game.name, $bucket, $registry.external)
}

if (-not $SkipBuild) {
  Write-Host '== 2/4 构建镜像' -ForegroundColor Cyan
  & docker build -t $BuildTag $dir | Out-Host
  if ($LASTEXITCODE -ne 0) { throw 'docker build failed' }
}

Write-Host '== 3/4 导出 docker-archive' -ForegroundColor Cyan
if (Test-Path $tar) { Remove-Item $tar -Force }
& docker save $BuildTag -o $tar | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'docker save failed' }
Write-Host ("   {0} ({1} MB)" -f $tar, [math]::Round((Get-Item $tar).Length / 1MB, 1))

if ($SkipUpload) {
  Write-Host ''
  Write-Host ("已生成镜像包：" + $tar) -ForegroundColor Green
  return
}

Write-Host '== 4/4 上传' -ForegroundColor Cyan
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromMinutes(30)
$client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
$form = [System.Net.Http.MultipartFormDataContent]::new()
$fs = [System.IO.File]::OpenRead($tar)
$part = [System.Net.Http.StreamContent]::new($fs)
$part.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
$form.Add($part, 'file', (Split-Path $tar -Leaf))
$sw = [Diagnostics.Stopwatch]::StartNew()
$resp = $client.PostAsync(($api + "/game/" + $GameId + "/registry"), $form).GetAwaiter().GetResult()
$body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
$sw.Stop(); $fs.Dispose(); $form.Dispose(); $client.Dispose()
if (-not $resp.IsSuccessStatusCode) { throw "upload -> $([int]$resp.StatusCode) $body" }
Write-Host ("   uploaded in {0:n1}s" -f $sw.Elapsed.TotalSeconds) -ForegroundColor Green

$tags = Get-Api ("/game/" + $GameId + "/registry/" + $Image)
$tag = $tags | Where-Object { $_ -match '^[0-9]+$' } | Sort-Object { [int64]$_ } -Descending | Select-Object -First 1
Write-Host ""
Write-Host ("镜像地址（填进后台「镜像版本」选它，或 env.toml 的 tag）：" ) -ForegroundColor Yellow
Write-Host ("  " + $registry.external + "/" + $bucket + "/" + $Image + ":" + $tag) -ForegroundColor Green
