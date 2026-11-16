# 走地图

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | ELF 逆向、C++ STL 容器内存布局（`std::map` 红黑树 / `std::unordered_map` 哈希桶）、gdb 内存 dump、图的最短路、MD5 |
| 类型 | Reverse · 逆向 |
| 难度 | 5 / 5 |
| 附件 | `mapmap2`（2,360,568 B，Linux x86-64 ELF） |
| Flag | `flag{8b587367b99e5e2fcbdb6598da14b9bc}` |

## 题面（直接复制到平台「简介」）

附件 `mapmap2` 是一个 Linux x86-64 的 ELF 程序（约 2.3 MB，C++ 编写），读入一串由 `s`/`d`/`w`/`a` 组成的移动指令，在一张「地图」上走。它不会直接告诉你答案，只会用你给的指令串做校验。需要还原出正确的那串指令；最终提交的是这串指令的 MD5，格式 `flag{<32 位小写十六进制>}`。

**提示**：地图是运行时用 STL 容器现搭的，地址每次都不一样；与其硬读反汇编，不如让它把图建完，再把堆内存 dump 下来按容器布局自己解析。

## 二、解题步骤

### Step 1｜先看程序给了什么线索

```bash
file mapmap2
strings mapmap2 | grep -i -E 'flag|md5'
# flag{md5(your input)}
```

提示很明确：**最终答案是你还原出的那串输入的 MD5**，格式 `flag{md5(your input)}`。

### Step 2｜认出核心数据结构

IDA 打开，主函数里能看出地图的类型是：

```cpp
map<int, unordered_map<int, void*>>*   // 外层红黑树按"行"索引，内层哈希桶按"列"索引
```

外层 `std::map` 的 key 是行号，每个节点挂一个 `std::unordered_map`（列号 → 指向下一个地图节点的指针）。
也就是说：**整张图就是一张用 STL 容器拼出来的邻接表**，边上携带的信息被压进 `(行号 << 4) | 列号` 的低位。

### Step 3｜把堆 dump 下来

容器节点都分配在堆上，所以只要在"地图构建完成、准备读输入"的位置断下来，把整块 heap 导出即可：

```gdb
(gdb) b *0x462D8C
(gdb) r
(gdb) info proc mappings
     0x640000   0x730000   0xf0000   0x0  rw-p   [heap]
(gdb) x/2gx 0x63F530
0x63f530:  0x000000000065b3d0   0x0000000000667b50     # 入口节点 / 出口节点
(gdb) dump memory heap 0x640000 0x730000
```

得到三个关键量：入口 `0x65b3d0`、出口 `0x667b50`、堆基址 `0x640000`。

> 每台机器上堆地址都不同，入口/出口要自己重新断点取，别照抄。

### Step 4｜按 libstdc++ 布局离线解析

**std::map 节点（红黑树）**

| 偏移 | 含义 |
|---|---|
| `+0x08` | parent |
| `+0x10` | left |
| `+0x18` | right |
| `+0x20` | key（int，4 字节） |
| `+0x28` | value 起始（这里是一个 unordered_map 对象） |

header 节点自身：`map+0x10` 是 root，`map+0x18` 是最小节点，`map+0x20` 是最大节点；
中序遍历用"后继"规则（右子树最左下，或向上找第一个作为左孩子的祖先）。

**std::unordered_map 桶节点（单链表）**

| 偏移 | 含义 |
|---|---|
| `+0x08` | key（int，4 字节） |
| `+0x10` | value（这里是指向下一个地图节点的指针的低 32 位） |

遍历：从桶头顺着链表走。

两层展开后就拿到了全部有向边，边长 = `(map_key << 4) | bucket_key`。

### Step 5｜求最短路 → 还原输入 → 取 MD5

```python
import networkx as nx
G = nx.DiGraph()
# ... 按上面布局把 edges 填进 G（节点是地图节点地址，边权 = (h << 4) | l）...

path = nx.shortest_path(G, 0x65b3d0, 0x667b50)
buf  = bytes(G[a][b]['w'] for a, b in zip(path, path[1:]))
assert len(buf) == 268
print(buf.decode())
```

得到的 268 字节指令串：

```text
ddssaassddddssddssssaassaawwwwwwaassssssssssddssssssddwwddddssssddssssddwwwwddssddwwddssddwwwwwwwwwwaawwaawwddwwaaaawwwwddwwddssddddddssaassaassddssddwwddwwwwwwwwwwwwddssssssssssddddssssssddwwwwwwddssddssaassssddssssaaaawwwwaassssaawwaassssssddddssssddssssssaassdddddd
```

```python
from hashlib import md5
print('flag{%s}' % md5(buf).hexdigest())
```

```text
flag{8b587367b99e5e2fcbdb6598da14b9bc}
```

## 三、原理

- **STL 容器的内存布局是稳定的 ABI**：`std::map` 就是红黑树，`std::unordered_map` 就是"桶数组 + 单链表"。
  只要知道 libstdc++ 的节点偏移，dump 下来的裸内存可以被完整还原成逻辑结构，**不需要在调试器里单步走完整个程序**。
- **"图"不一定要画出来**：这里的地图本质是邻接表，求最短路直接交给 `networkx` 即可。
- **答案是输入的哈希而不是输入本身**：题目用 MD5 把 268 字节的长输入压成定长 flag，
  同时防止"暴力试输入"（每次试都要真正跑一遍程序）。

## 四、防御建议

- 这类"把判断逻辑藏在复杂数据结构里"的做法只能提高逆向成本，**不是安全边界**。真正的授权校验应在服务端、用签名/证书。
- 如果业务里确实需要把中间结果放内存并暴露给不可信进程，考虑加内存加密或做完整性校验（如 PAC / CFI）。
- 对客户端二进制而言，任何"本地算出来的通过标志"都可以被 patch，所以不要把安全判断放在前端。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
|---|---|---|
| `mapmap2` | 2,360,568 B | `ba7726ed37f66f8913dbd02be7290006237000278d3a5aa8c031524f9ba9cf94` |
