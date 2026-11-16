# start-ctf · 赛前热身

> 这一组不是正题。它的唯一目的是把「**连上环境 → 拿到东西 → 提交 flag**」这条链路走通，
> 顺带把比赛要用的工具装好。做完这 5 道再进正题，后面每个方向都会顺很多。

5 道题分两类：

| 类型 | 题 | 你能学到 |
|---|---|---|
| 有容器 | `connect-ctf` `nc-ctf` `ssh-ctf` | 浏览器 / netcat / ssh 三种连接方式 |
| 纯附件 | `start-hello-flag` ☆ `start-hello-toolbox` ☆ | flag 的书写规则、自己的环境是否就绪 |

---

## 题目一览（5 题）

| 目录 | 题名 | 考点 | 难度 | 端口 | 形态 |
|---|---|---|---|---|---|
| `connect-ctf/` | 连接平台 | 浏览器打开就看见 flag，认识「启动环境 → 拿到地址 → 访问」 | 0 | 本地 **8092** → 容器 **8080** | 容器 |
| `nc-ctf/` | nc 使用 | `nc`（netcat）连裸 TCP 服务，回车后打印 flag | 0 | 本地 **8096** → 容器 **1337** | 容器 |
| `ssh-ctf/` | ssh 引入 | `ssh` 登录题目容器（`xaiu` / `sec`），`cat /tmp/flag` | 1 | 本地 **8098** → 容器 **2222** | 容器 |
| `start-hello-flag/` ☆ | 先读懂规则 | flag 格式：小写前缀、半角花括号、括号内只允许小写字母数字下划线 | 1 | — | 附件 |
| `start-hello-toolbox/` ☆ | 先把环境检查一遍 | 跑环境自检脚本，按提示设环境变量换 flag | 1 | — | 附件 |

> ☆ = 引入题。`start-hello-flag` 教「什么算一个合法的 flag」（平台是逐字节比较，多一个空格就判错），
> `start-hello-toolbox` 教「赛前怎么确认自己的机器能干活」（python / curl / nc / git / pwntools）。

## 三题容器各自的连接方式

```bash
# connect-ctf：浏览器打开即可，页面中间就是 flag
#   本地 http://127.0.0.1:8092/      平台 http://<隧道地址>/

# nc-ctf：连上去按一次回车，才打印 flag（GET 会被判成 HTTP 并断开）
nc 127.0.0.1 8096

# ssh-ctf：密码 sec
ssh -p 8098 xaiu@127.0.0.1
cat /tmp/flag
```

> Windows 没有自带 `nc`。装法：设置 → 应用 → 可选功能 → 添加「Telnet 客户端」不行，
> 要用 **ncat**（`winget install nmap.nmap`）或直接用 Python：
> `python -c "import socket;s=socket.create_connection(('127.0.0.1',8096));print(s.recv(4096).decode())"`

## 跑起来

```bash
cd start-ctf/connect-ctf && docker compose up -d --build    # 8092
cd start-ctf/nc-ctf      && docker compose up -d --build    # 8096
cd start-ctf/ssh-ctf     && docker compose up -d --build    # 8098

# 附件题不用起服务
cd start-ctf/start-hello-toolbox/files && python check_env.py
```

## 平台上传要点

| 题 | 容器名称 | 服务端口 | 传输协议 | 应用协议 |
|---|---|---|---|---|
| `connect-ctf` | `connect-ctf` | **8080** | `tcp` | `http` |
| `nc-ctf` | `nc-ctf` | **1337** | `tcp` | `raw` |
| `ssh-ctf` | `ssh-ctf` | **2222** | `tcp` | `raw` |

`restricted` 勾选、出网关闭。容器内监听的都是高位端口，平台以 restricted 起容器会 drop
`CAP_NET_BIND_SERVICE`，连 root 都绑不了 80 和 22。

## 常见坑

| 现象 | 原因 |
|---|---|
| 浏览器打开是空白 / `ERR_EMPTY_RESPONSE` | 端口填成了 80，容器实际听 8080（见总 README 第八节自检） |
| `nc` 连上没反应 | `nc-ctf` 要先回车才给 flag，直接发 `GET /` 会被判成 HTTP 断开 |
| `ssh` 报 `Connection refused` | 填的是 22；容器里 sshd 监听 **2222** |
| `python check_env.py` 不打印 flag | 缺 `CTF_READY=1`，脚本会把三种设法的命令打给你 |

## 目录结构

```text
start-ctf/
├── connect-ctf/   src/ flag.inc.php index.php + Dockerfile + docker-compose.yml + checker/ + README.md
├── nc-ctf/        src/ server.php + ...            （裸 TCP 1337）
├── ssh-ctf/       src/ sshd_config motd entrypoint.sh + ...（sshd 2222）
├── start-hello-flag/    files/hello.txt + checker/ + README.md
├── start-hello-toolbox/ files/check_env.py + checker/ + README.md
└── _platform/     上传脚本与平台参考（不是题）
```

约定：容器题的 flag 一律来自环境变量 `FLAG`（平台 `environ()` 注入），不硬编码；附件题的 flag 写在 `checker/main.rx`。
