# connect-ctf · 连接平台

> 考点：在线环境的连接方式（浏览器 / curl / nc 都行）
> 在线环境端口：**8080**（容器内）／本地 **8092**
> 镜像包：`connect-ctf.tar`（`docker save` 自 `connect-ctf-web:latest`）

---

## 题面

很高兴看到你来参加比赛。

我是本次比赛的运维同学，接下来我们用几道题来熟悉比赛平台的一些操作和常用工具的使用。这些题目不是正式题目，仅供你熟悉在线环境的连接方法。

请仔细阅读平台文档里「如何连接题目」一节，用合适的方法连接本题的环境，**使用浏览器打开就可以看到本题的 flag**。

后续很多方向的题目都要用到这种连接方法。若你通过此题，从此比赛尽是坦途。

> 提示：环境启动后，平台会给出一份访问地址（通常需要 WebSocketReflectorX / wsrx 隧道）。
> 先用平台提供的方式把地址打到本地，再访问即可。

---

## 题解

页面没有任何机关：拿到 wsrx 映射出的地址，浏览器打开就看到 flag。

```bash
curl -s http://<平台给的地址>/ | grep -oE 'flag\{[^}]+\}'
```

```powershell
# PowerShell 5.1 里 curl 是 Invoke-WebRequest 的别名，用 curl.exe
curl.exe -s http://<平台给的地址>/
```

页面还回显请求时间 / 来源 IP / User-Agent，响应头带 `X-Challenge: connect-ctf`；
这一步的重点是让选手理解「在线环境 = 一个服务端，浏览器只是客户端」。

---

## 本地跑

```bash
cd connect-ctf
docker compose up -d --build
# 打开 http://127.0.0.1:8092/
```

容器内 Apache 监听 **8080**（不是 80）：平台 restricted 模式会丢 `CAP_NET_BIND_SERVICE`，连 root 也绑不了 80。

### flag 从哪来（`src/flag.inc.php`）

优先级（逻辑沿用 web-ctf，值按 misc-ctf 的写法写死在文件里）：

1. 环境变量 `FLAG` —— 平台 checker 注入；
2. `src/flag.inc.php` 里的 `CTF_FLAG = 'flag{connect_the_platform_with_a_browser}'`（**这串是我拟的，要改就改这一行**）；
3. 都没有才随机 UUIDv4，缓存到 `/tmp/.web_ctf_flag`。

注入覆盖：`FLAG=flag{xxx} docker compose up -d --force-recreate`。

---

## 上传平台

```powershell
cd D:\ctf\start-ctf
pwsh .\_platform\upload-image.ps1 -Challenge .\connect-ctf -Image connect-ctf -BuildTag connect-ctf-web:latest -Token <token>
```

**tar 必须叫 `connect-ctf.tar`**（仓库名 = 文件名去掉扩展名）。

后台「添加镜像」：容器名 `connect-ctf`、传输协议 `TCP`、应用协议 `http`、端口 `8080`、CAP 开、出网关。
题目定义：名称 `连接平台`、标签 `Misc`(主)+`Web`、分数 `50/50/1`。

免费提示一条：不需要任何技巧，把地址粘进浏览器，页面中间就是 flag。

### Checker（`dynamic-uuid`，贴进后台 Checker tab）

````rust
//! * dynamic-uuid.rx — 每队动态 flag（UUID 形式 + 隐写队伍信息）
//!
//! 本题只做「引导」用途：容器里把 FLAG 环境变量直接渲染到页面上，
//! 选手照抄提交即可；check 仍然保留完整的隐写校验，方便以后复用模板。

use ret2api::audit;
use ret2api::utils;

/// whether the flag uuid contains hyphen (`-`)
const WITH_HYPHEN = true;
/// whether the flag uuid is case sensitive
const CASE_SENSITIVE  = true;
/// the key to initialize the flag encryptor
const ENCRYPT_KEY = "BgK6YjPlLHkmxhLjEWF1fUn3";
/// the flag prefix, `flag` means the flag will be `flag{...}`
const PREFIX = "flag";
/// the flag hash key, used to generate the uuid content
const HASH_KEY = "SCvPNcvEpAg5DxYJWavNcd9fKbJYs4x1";

/// Check flag submitted by user.
pub async fn check(bucket, user, team, submission) {
  let flag = utils::Flag::parse(submission.content)?;
  if flag.prefix() != PREFIX {
    return Ok((false, `Wrong format! flag should be ${PREFIX}{...}`, None))
  }

  if CASE_SENSITIVE && flag.content().chars().any(|c| c.is_uppercase()) {
    return Ok((false, "Incorrect!", None))
  }

  let enc_id = if let Some(id) = team.id {
    id
  } else {
    user.id
  };

  // extract peer_team id
  let res = audit::decode_uuid(HASH_KEY, ENCRYPT_KEY, flag.content(), WITH_HYPHEN);
  let dec_id = match res {
    Ok(id) => id,
    // error handling
    Err(_) => return Ok((false, "Incorrect!", None))
  };
  // the flag is correct, with steganographic data matched
  if dec_id == enc_id {
    return Ok((true, "Correct!", None));
  }
  // the flag is wrong, let's see whether the team has cheated
  if let Some(id) = team.id {
    if dec_id != id {
      return Ok((false, "Incorrect!", Some(#{
        peer_team: dec_id,
        reason: `team ${id}:'${team.name.unwrap()}' may cheated with team ${dec_id}`
      })));
    }
  }

  Ok((false, "Incorrect!", None))
}

/// Provides the environment variables when user starts the challenge container.
pub async fn environ(bucket, user, team) {
  let enc_id = if let Some(id) = team.id {
    id
  } else {
    user.id
  };
  let content = audit::encode_uuid(HASH_KEY, ENCRYPT_KEY, enc_id, WITH_HYPHEN);
  Ok(#{
    FLAG: utils::Flag::new().with_prefix(PREFIX).with_content(content).to_string(),
  })
}
````
