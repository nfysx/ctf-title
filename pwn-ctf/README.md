# pwn-ctf · Pwn 入门

> 12 道题，坡度 5 易 : 3 中 : 2 难，知识点对齐 `Pwn入门指北`。
> 前两道是**工具课**（怎么用工具认识一个二进制），后面才是各种漏洞。
> 除这两道，其余 10 道都是**可交互容器**（`nc` 连上去打）。

---

## 题目一览（12 题）

| 目录 | 题名 | 考点 | 难度 | 本地端口 | 形态 |
|---|---|---|---|---|---|
| `pwn-hello-tools/` ☆ | 工具链第一课 | `file` → `checksec` → `strings` → `objdump` 各在看什么 | 1 | — | 附件 |
| `pwn-hello-gdb/` ☆ | 调试第一课 | gdb 断点 + `call win()`；给一条不用 gdb 的 ret2win 路 | 1 | — | 附件 |
| `pwn-stack-check/` | 覆盖栈上的判断变量 | `gets` 溢出改相邻局部变量 | 易 | 9101 | 容器 |
| `pwn-ret2text/` | 跳回程序里的函数 | 溢出改返回地址到 `backdoor()` | 易 | 9102 | 容器 |
| `pwn-fmt-leak/` | 格式化字符串泄露 | `printf(buf)` 用 `%n$s` 读栈上 flag | 易 | 9103 | 容器 |
| `pwn-one-byte/` | 一个字节改分支 | 只许写 1 字节到相邻全局变量 | 易 | 9104 | 容器 |
| `pwn-int-underflow/` | 负数长度绕过 | `unsigned` 比较 + 负数 → 超大读入 | 易 | 9105 | 容器 |
| `pwn-ret2shellcode/` | 栈上执行代码 | NX 关，跳到栈上 shellcode | 中 | 9106 | 容器 |
| `pwn-fmt-write/` | 格式化字符串任意写 | `%n` 改 GOT，把 `exit` 指到 `backdoor` | 中 | 9107 | 容器 |
| `pwn-canary-leak/` | 先泄露再打 | 开 canary，先泄露再溢出 | 中 | 9108 | 容器 |
| `pwn-ret2libc/` | 泄露 libc 再 system | `puts@got` 泄露 + ret2libc，`files/` 里带 `libc.so.6` | 难 | 9109 | 容器 |
| `pwn-uaf/` | 释放后使用 | note 结构体 UAF，改函数指针 | 难 | 9110 | 容器 |

> ☆ = 引入题，先做这两道。它们不考漏洞，只考「你知道工具在干什么」。
> 之后 5 道易题按上表顺序做，坡度是顺着来的。

## 跑起来

```bash
cd pwn-ctf/pwn-ret2text
docker compose up -d --build
nc 127.0.0.1 9102

# 每题都有 exp（不对外发布）
python exp/exp.py
```

exp 用 pwntools，装法 `pip install pwntools`。

## 平台上传要点（10 道容器题）

镜像包在 `D:\pwn-ctf-images\`，每个 88.9 MB。**这 10 题的填写值完全一样，只有容器名称不同。**

### 三个关键值

| 字段 | 值 | 为什么 |
|---|---|---|
| **服务端口** | **`1337`** | 容器内 `socat TCP-LISTEN:1337`。不能填 80：平台 restricted 会 drop `NET_BIND_SERVICE`，1024 以下连 root 都绑不了 |
| **传输协议** | **`tcp`** | 本库没有 UDP 服务 |
| **应用协议** | **`raw`** | 裸 TCP 游戏协议。选 `http` 会让平台健康检查发 `GET / HTTP/1.1`，程序收到就退出 |

其余：`镜像拉取策略 = Always`、`CPU = 0.1, 0.3`、`内存 = 64, 128`、`存储 = 64, 1024`、
**restricted 勾选**、**出网关闭**。

### 逐题对照

| 镜像包 | 容器名称 / 仓库名 | 本地端口 | 容器内端口 | 传输 | 应用 | 连接后第一屏 |
|---|---|---|---|---|---|---|
| `pwn-stack-check.tar.gz` | `pwn-stack-check` | 9101 | **1337** | `tcp` | `raw` | `Tell me the password:` |
| `pwn-ret2text.tar.gz` | `pwn-ret2text` | 9102 | **1337** | `tcp` | `raw` | `input:` |
| `pwn-fmt-leak.tar.gz` | `pwn-fmt-leak` | 9103 | **1337** | `tcp` | `raw` | `Say something:` |
| `pwn-one-byte.tar.gz` | `pwn-one-byte` | 9104 | **1337** | `tcp` | `raw` | `name:` |
| `pwn-int-underflow.tar.gz` | `pwn-int-underflow` | 9105 | **1337** | `tcp` | `raw` | `send length (4 bytes little endian):` |
| `pwn-ret2shellcode.tar.gz` | `pwn-ret2shellcode` | 9106 | **1337** | `tcp` | `raw` | `stack: 0x7fff...` |
| `pwn-fmt-write.tar.gz` | `pwn-fmt-write` | 9107 | **1337** | `tcp` | `raw` | `name:` |
| `pwn-canary-leak.tar.gz` | `pwn-canary-leak` | 9108 | **1337** | `tcp` | `raw` | （无 banner，直接等输入） |
| `pwn-ret2libc.tar.gz` | `pwn-ret2libc` | 9109 | **1337** | `tcp` | `raw` | `input:` |
| `pwn-uaf.tar.gz` | `pwn-uaf` | 9110 | **1337** | `tcp` | `raw` | `1. new  2. edit  3. free  4. run  5. exit` |

### 环境变量

`checker/main.rx` 的 `environ()` 返回的 map 会原样注入容器，这 10 题只要一个：

```toml
FLAG = "flag{...每队唯一...}"
```

`entrypoint.sh` 写进 `/app/flag`，二进制读它。未注入时用诱饵值 `flag{local_default}`，
镜像里不带真答案。

### 上传前自检

```bash
docker load -i D:\pwn-ctf-images\pwn-ret2text.tar.gz
docker run --rm -d --name t --cap-drop NET_BIND_SERVICE -e "FLAG=flag{x}" -p 19102:1337 pwn-ret2text:latest
python -c "import socket;s=socket.create_connection(('127.0.0.1',19102),timeout=5);print(s.recv(64))"
# 期望 b'input:\n'
docker rm -f t
```

## 这个方向的常见坑

| 现象 | 原因 |
|---|---|
| exp 本地能打、平台上打不通 | 本地没带 `--cap-drop NET_BIND_SERVICE`，容器能起但平台下起不来 |
| ret 进 backdoor 后 `movaps` 崩 | glibc 2.35 要求 16 字节栈对齐，backdoor 要加 `force_align_arg_pointer` |
| 找不到 `pop rdi; ret` | glibc 2.34+ 不再生成 `__libc_csu_init`，小二进制里没有现成 gadget |
| ret2libc 偏移对不上 | `files/libc.so.6` 必须和容器内 libc 同版本（`ldd --version` 对一下） |
| 容器起来了但 `nc` 立刻断开 | 二进制读到 EOF 就退出；用 `nc` 时要保持连接，别用 `echo x \| nc` 跑交互题 |

## 目录结构

```text
pwn-ctf/
├── pwn-hello-tools/   files/hello_pwn + src/ + checker/ + README.md   （附件题）
├── pwn-hello-gdb/     files/hello_gdb + src/ + checker/ + README.md   （附件题）
└── pwn-stack-check/   src/pwn.c + files/pwn + exp/exp.py + Dockerfile
                       docker-compose.yml + checker/main.rx + README.md  （容器题）
```

约定：容器题的 `checker/main.rx` 用 `dynamic-uuid` 模板（按队伍 id 生成不同 flag）；
附件题用静态模板。两处模板见 `_PLATFORM_RULES.md`。
