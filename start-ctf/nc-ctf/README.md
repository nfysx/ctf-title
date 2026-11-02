# nc-ctf · nc 使用

> 考点：裸 TCP 服务的连接（wsrx 映射 → `nc`）
> 在线环境端口：**1337**（容器内）／本地 **8096**
> 镜像包：`connect-nc.tar`（`docker save` 自 `nc-ctf:latest`）

---

## 题面

刚才我们学习使用了 wsrx，接下来我们尝试使用 **netcat**（又称 ncat、nc）软件来连接一些特殊的题目环境。

`nc` 是一个很常用的工具，我们同样会在很多方向的题目中碰到它。当遇到一些题目的环境用浏览器打不开的时候，我们就需要尝试用 `nc` 来打开它。

请你仔细阅读 **netcat 引入** 这篇维基，并使用 `nc` 来连接本题环境。

本题环境给的是 **wsrx 链接**怎么办？

1. 先用 wsrx 将链接**映射到本地**，它会给出一个本地地址，形如 `127.0.0.1:12345`；
2. 再 `nc 127.0.0.1 12345`（Windows 上可能是 `ncat 127.0.0.1 12345`）。

连上后会先看到一段说明，**按一下回车**，flag 就会出来。

> 如果看到的是「这不是 Web 服务」的提示，说明你用的是浏览器或 curl —— 换 `nc` 就对了。

---

## 题解

本题环境不是 Web 服务，是**裸 TCP 服务**，所以浏览器打不开。

```bash
wsrx 把平台链接映射到本地          # 得到 127.0.0.1:12345
nc 127.0.0.1 12345                 # 看到 banner 后按一下回车
```

- 连上先出 banner（nc 用法速查），**这时没有 flag**；
- 发任意一行（回车即可）→ 打印 `Your Flag: flag{...}`；
- 再敲几行会原样回显（证明双向），输入 `exit` 或 Ctrl+C 断开。

浏览器/curl 打过去会收到「这不是 Web 服务」并断开，**不给 flag** —— 这正是本题要讲的点。

---

## 本地跑

```bash
cd nc-ctf
docker compose up -d --build
nc 127.0.0.1 8096          # 看到 banner 后按一次回车
```

### flag 从哪来（`src/flag.inc.php`）

逻辑沿用 `web-ctf`，flag 值按 `misc-ctf` 的写法直接写在文件里，优先级：

1. 环境变量 `FLAG` —— 平台 checker 的 `environ()` 注入，比赛时走这条；
2. `src/flag.inc.php` 里的常量 `CTF_FLAG = 'flag{connect_with_netcat_not_a_browser}'`；
3. 都没有才随机 UUIDv4，缓存到容器 `/tmp/.nc_ctf_flag`（同一实例内一致）。

所以本地不设 `FLAG` 也看到和线上一样的 flag；要覆盖就：

```powershell
$env:FLAG="flag{xxx}"; docker compose up -d --force-recreate
```

可调环境变量：`BIND_ADDR` / `BIND_PORT`(1337) / `BANNER_TIMEOUT`(90) / `ECHO_TIMEOUT`(180) / `MAX_CLIENTS`(128)。

## 自测

```powershell
pwsh D:\ctf\start-ctf\_platform\selftest-nc.ps1 -Port 8096 -ExpectFlag 'flag{connect_with_netcat_not_a_browser}'
```

覆盖：banner 无 flag / 回车出 flag / HTTP 被拒 / 回显 / exit / 并发不互相阻塞 / 20 次秒断后仍存活。

### 为什么线上输出全是英文

Windows 默认控制台是 GBK（代码页 936），服务端发 UTF-8 中文会变成 `浣犺繛涓婃潵浜` 这种乱码；
更糟的是双字节对齐会把 `\n` 吃掉，几行并成一行。所以：

- 服务端所有出站文本是 **纯 ASCII**（banner / flag 块 / 回显 / 拒绝文案 / 超时），
- 行尾统一 **CRLF**（`crlf()` 统一处理），Windows 老控制台也不会黏行。

题面、题解、平台后台这些走网页（UTF-8）的地方照旧用中文，不受影响。

---

## 上传平台

```powershell
cd D:\ctf\start-ctf

# 只打包（不需要 token）
pwsh .\_platform\upload-image.ps1 -Challenge .\nc-ctf -Image connect-nc -SkipUpload

# 打包 + 上传到平台 registry
pwsh .\_platform\upload-image.ps1 -Challenge .\nc-ctf -Image connect-nc -Token <token>
```

**tar 必须叫 `connect-nc.tar`**（仓库名 = 文件名去掉扩展名）。

后台「添加镜像」只有一处和 Web 题不同：

| 字段 | 值 |
|---|---|
| 容器名称 | `connect-nc` |
| 镜像标签 / 版本 | `connect-nc` / 上传后最新的时间戳 |
| CAP | 开（restricted） |
| 传输协议 | `TCP` |
| 应用协议 | **`raw`** ← 选成 http 就收不到数据 |
| 服务端口 | `1337` |
| CPU / 内存 / 存储 | 0.01–0.1 / 32–64Mi / 64–1024MB |

题目定义（后台「创建题目」）：名称 `nc 使用`、标签 `Misc`(主)+`Network`、分数 `50/50/1`、隐藏否。

免费提示一条：

> wsrx 映射出来的是 TCP 地址（形如 `127.0.0.1:12345`），不是 `http://` 链接；拿到端口后 `nc 127.0.0.1 12345`。

本题 flag 是**静态可读**的，所有队伍相同：

```
flag{connect_with_netcat_not_a_browser}
```

引导题不需要按队伍动态生成，选手从页面抄下来交即可。

### Checker（平台预设 `simple`，静态 flag）

````rust
//! * simple.rx
//! Static flag check.

use ret2api::utils;
use ret2api::regex;

/// check the flag in regex format, only apply to content without prefix
const ENABLE_REGEX = false;
/// case sensitive
const CASE_SENSITIVE = true;
/// the flag prefix, the game name for example, `flag` means the flag will be `flag{...}`
const PREFIX = "flag";
/// the flag template (readable recommended), used to generate the correct flag content
/// if regex enabled, the template is like `^you_.+_impact$`
const TEMPLATE = "connect_with_netcat_not_a_browser";

/// Check flag submitted by user.
///
/// * bucket: the challenge `ret2api::bucket::Bucket` object
/// * user: { id: number, account: string, institute_id: number }
/// * team: { id: Option<number>, name: Option<string>, institute_id: Option<number> }
/// * submission: { id: number, user_id: number, team_id: number, challenge_id: number, content: string }
///
/// Returns: Result<(bool, string, Option<{peer_team: i64, reason: string}>), any>
/// means (correct, msg, audit: { peer_team, reason }), when audit is not None, the team will be treated as cheated,
/// and the platform will publish a event to administrators.
///
/// The audit message will be validate again in the platform, so don't worry about false positives.
pub async fn check(bucket, user, team, submission) {
  let flag = utils::Flag::parse(submission.content)?;
  if flag.prefix() != PREFIX {
    return Ok((false, `Wrong format! flag should be ${PREFIX}{...}`, None));
  }

  if ENABLE_REGEX {
    if regex::test(TEMPLATE, flag.content())? {
      Ok((true, "Correct!", None))
    } else {
      Ok((false, "Incorrect!", None))
    }
  } else if CASE_SENSITIVE {
    if flag.content() == TEMPLATE {
      Ok((true, "Correct!", None))
    } else {
      Ok((false, "Incorrect!", None))
    }
  } else {
    if utils::lower(flag.content()) == utils::lower(TEMPLATE) {
      Ok((true, "Correct!", None))
    } else {
      Ok((false, "Incorrect!", None))
    }
  }
}

/// Provides the environment variables when user starts the challenge container.
///
/// NOTE: 平台预设 simple.rx 的 environ 返回空表，而本题容器要从 FLAG 读，
/// 所以这里只加了这一段 —— 静态 flag 就是 `flag{TEMPLATE}`。
pub async fn environ(bucket, user, team) {
  Ok(#{
    FLAG: utils::Flag::new().with_prefix(PREFIX).with_content(TEMPLATE).to_string(),
  })
}
````