# ssh-ctf · ssh 引入

> 考点：ssh 的连接方式（wsrx 映射 → `ssh 用户名@IP -p 端口`）
> 在线环境端口：**2222**（容器内）／本地 **8098**
> 登录凭据：`xaiu` / `sec`，flag 在 `/tmp/flag`

---

## 题面

你已经学会了 wsrx 和 nc 的使用。最后，我们来了解一下 ssh 的连接方法，这个在部分题目会使用到。

命令格式是 `ssh 用户名@IP地址 -p 端口号`。若一个题目给了 ssh 环境，你使用 wsrx 将环境映射到 `127.0.0.1:1145` 端口，用户名是 `xaiu`，那么可以敲 `ssh xaiu@127.0.0.1 -p 1145` 登录。

这里给了一个 ssh 环境，试着登录一下吧。Flag 放置在 `/tmp/flag` 内，你在登录后的界面敲 `cat /tmp/flag` 就可以找到。（用户名 `xaiu`，密码 `sec`）

那使用 Linux 的同学呢？别问了，我相信你肯定知道了。

---

## 题解

```bash
# 1) wsrx 把平台给的链接映射到本地，得到 127.0.0.1:<端口>
# 2) 登录（第一次会问 host key，敲 yes）
ssh xaiu@127.0.0.1 -p <映射端口>
# 3) 密码 sec，进去以后：
cat /tmp/flag
```

Windows 自带 `ssh.exe`（Win10 1803+ 起默认预装），PowerShell 里直接敲就行；没有的话装 OpenSSH Client 或直接用 Git Bash。

两个常见卡点：

| 现象 | 说明 |
|---|---|
| `Permission denied (publickey,password)` | 密码打错，或者用户名不是 `xaiu` |
| `Connection refused` | wsrx 没映射 / 端口写成了 2222（那是容器内部的端口，不是本地映射端口） |

---

## 本地跑

```bash
cd ssh-ctf
docker compose up -d --build
ssh xaiu@127.0.0.1 -p 8098     # 密码 sec，进去 cat /tmp/flag
```

### flag 从哪来（`src/entrypoint.sh` + `src/flag.txt`）

优先级（逻辑沿用 web-ctf，值按 misc-ctf 的写法放在文件里）：

1. 环境变量 `FLAG` —— 平台 checker 的 `environ()` 注入，比赛时走这条；
2. `src/flag.txt` 里的静态值 `flag{ssh_is_a_remote_shell}`；
3. 都没有就随机，写进 `/tmp/flag`。

启动时把结果写进 `/tmp/flag`（权限 644，登录后直接 `cat`）：

```sh
printf "%s\n" "$FLAG" > /tmp/flag
```

覆盖：`$env:FLAG="flag{xxx}"; docker compose up -d --force-recreate`。

## 自测

需要一个带 ssh 客户端的测试镜像（只建一次）：

```powershell
docker build -f D:\ctf\start-ctf\_platform\ssh-client.Dockerfile -t ssh-client:latest D:\ctf\start-ctf\_platform
pwsh D:\ctf\start-ctf\_platform\selftest-ssh.ps1 -Port 8098 -ExpectFlag 'flag{ssh_is_a_remote_shell}'
```

覆盖：正确密码登录 / 错误密码被拒 / motd 显示 / 连续 5 次登录 / 服务持续可用。

### 服务端几个刻意的选择

| 选择 | 原因 |
|---|---|
| sshd 监听 **2222** | 平台 restricted 模式丢 `CAP_NET_BIND_SERVICE`，绑不了 22 |
| host key 每次启动现生成 | 不同实例各一份，选手收到 host key 变更提示属正常 |
| `PermitRootLogin no` / `AllowUsers xaiu` | 只留题目账号，root 进不来 |
| motd 全 ASCII | Windows 终端 GBK 下中文会乱码（同 nc 那道题的教训） |
| `UsePAM` 不写 | Alpine 的 openssh 不带 PAM，写了直接 `Unsupported option UsePAM` |

---

## 上传平台

```powershell
cd D:\ctf\start-ctf
pwsh .\_platform\upload-image.ps1 -Challenge .\ssh-ctf -Image connect-ssh -SkipUpload   # 只出包
pwsh .\_platform\upload-image.ps1 -Challenge .\ssh-ctf -Image connect-ssh -Token <token>  # 出包 + 上传
```

后台「添加镜像」：

| 字段 | 值 |
|---|---|
| 容器名称 | `connect-ssh`（≤15 字符） |
| 镜像标签 / 版本 | `connect-ssh` / 上传后最新的时间戳 |
| CAP | 开（restricted） |
| 传输协议 | `TCP` |
| 应用协议 | **`原始`**（raw） |
| 服务端口 | **`2222`** |
| CPU / 内存 / 存储 | 0.01–0.1 / 64–128Mi / 64–1024MB |

题目定义：名称 `ssh 引入`、标签 `Misc`(主)+`Network`、分数 `60/60/1`、隐藏否。

免费提示一条：

> wsrx 映射出来的是 `127.0.0.1:<端口>`，登录命令是 `ssh xaiu@127.0.0.1 -p <端口>`，密码 `sec`；进去以后 `cat /tmp/flag`。

### Checker（平台预设 `simple`，静态 flag）

本题 flag 静态可读、所有队伍相同：`flag{ssh_is_a_remote_shell}`。

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
const TEMPLATE = "ssh_is_a_remote_shell";

/// Check flag submitted by user.
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
/// NOTE: 平台预设 simple.rx 的 environ 返回空表；本题容器要把 flag 写进 /tmp/flag，
/// 所以这里补一段，静态 flag 就是 `flag{TEMPLATE}`。
pub async fn environ(bucket, user, team) {
  Ok(#{
    FLAG: utils::Flag::new().with_prefix(PREFIX).with_content(TEMPLATE).to_string(),
  })
}
````
