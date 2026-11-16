# 运行时才存在的代码

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 运行时才存在的代码 |
| 考点 | 自修改代码（SMC）；ELF 自定义节；`mprotect` + 就地解密；静态复现解密器 |
| 难度 | 中（7） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,616 B） |
| Flag | `flag{self_modifying_code_at_runtime}` |

## 题面（直接复制到平台「简介」）

这个 Linux 程序在磁盘上和运行时并不是同一副样子：有一节内容要等程序启动后才被就地改写，然后才被调用。附件 `challenge` 是 x86-64 的 ELF，读一行输入并判断对错。要拿到的是它认可的 flag，提交格式 `flag{...}`。

**提示**：静态反汇编看到的字节未必是会执行的那一份；留意程序启动后对哪一段内存改了页属性、又是拿什么密钥去改写的。

## 二、解题步骤

### Step 1　看节表

```bash
cd files
chmod +x challenge
readelf -S challenge | grep -E 'smcx|smcd'
```

```
  [17] smcx              PROGBITS         0000000000001380  00001380
  [20] smcd              PROGBITS         0000000000002070  00002070
```

`smcx`（142 字节）与 `smcd`（8 字节）是作者加的自定义节，正常编译不会出现这种名字。
`readelf -SW challenge | grep -E 'smcx|smcd'` 的输出里，`smcx` 带 `AX` 标志（可执行），`smcd` 只标 `A`（只读数据），运行时要把它改成可写才能就地解密。

### Step 2　确认它们是密文

```bash
objdump -d --start-address=0x1380 --stop-address=0x1390 challenge
```

反汇编在 `0x1380` 附近全是无法识别的字节；而 `main` 里有一段循环：
`mprotect(page, len, PROT_READ|PROT_WRITE|PROT_EXEC)` + 逐字节 `xor SMK[i % 8]` + 随后调用目标函数。
ELF 里这两个节的起止地址由链接器生成的 `__start_smcx` / `__stop_smcx` 符号给出，运行时不存在"解析节表"这一步，但密钥仍然必须出现在程序里。

### Step 3　静态复现解密器

解密器必须留在程序里，所以密钥 `SMK` 一定可读（本题是 8 字节常量，文件偏移 `0x2068`，
`ee 39 ea 31 6a 36 cd bb`）。拿它把两个节异或回去：

```python
import struct

data = open("challenge", "rb").read()
e_shoff = struct.unpack_from("<Q", data, 0x28)[0]
es = struct.unpack_from("<H", data, 0x3A)[0]
n = struct.unpack_from("<H", data, 0x3C)[0]
si = struct.unpack_from("<H", data, 0x3E)[0]
stroff = struct.unpack_from("<Q", data, e_shoff + si * es + 0x18)[0]
secs = {}
for i in range(n):
    o = e_shoff + i * es
    noff = struct.unpack_from("<I", data, o)[0]
    name = data[stroff + noff: data.index(b"\x00", stroff + noff)].decode()
    addr, off, size, flags = struct.unpack_from("<QQQQ", data, o + 8)
    secs[name] = (off, size, addr, flags)
print(secs["smcx"], secs["smcd"])
```

```
(4992, 142, 4992, 6) (8304, 8, 8304, 2)
```

即 `smcx` 在文件偏移 `0x1380`、142 字节，`smcd` 在 `0x2070`、8 字节。

不知道密钥也没关系：把所有 8 字节窗口都当候选 SMK，用它解 `smcd` 得到 `TK`；明文固定以 `flag{` 开头，
于是 `ENC[0..4] = "flag{" ^ TK[0..4]`，到文件里搜这 5 个字节就能定位目标串。实测只有 `0x2068` 一个候选成立：

```python
SMK = bytes([0xee,0x39,0xea,0x31,0x6a,0x36,0xcd,0xbb])
smcd_off, smcd_size = 0x2070, 8
blob = data[smcd_off:smcd_off + smcd_size]
tk = bytes(b ^ SMK[i & 7] for i, b in enumerate(blob))
print("TK =", tk.hex())
```

```
TK = 9deb407142dbd582
```

### Step 4　用 TK 还原 flag

`ENC` 在文件偏移 `0x2040`（36 字节，`.rodata`），运行时按 `real[i] = ENC[i] ^ TK[i % 8]` 还原成明文再 `strcmp`。

```python
enc = data[0x2040:0x2040 + 36]
print(bytes(b ^ tk[i & 7] for i, b in enumerate(enc)).decode())
```

```
flag{self_modifying_code_at_runtime}
```

### Step 5　动态路线（可选）

用 gdb 让程序跑过两次 `mprotect`，再 `x/142bx 0x...` dump `smcx`：此刻它已经是明文代码，
栈上还有 `smc_check` 拼好的 36 字节明文。注意 PIE 下地址每次不同，先用 `info proc mappings` 取基址。

### Step 6　提交

```
flag{self_modifying_code_at_runtime}
```

## 三、原理

### 1. SMC 让"磁盘字节 ≠ 执行字节"

正常运行的程序，代码节在磁盘和内存里是一样的。SMC 打破这个前提：磁盘上是密文，进程里是明文。
于是静态反汇编看到的都是无效指令，必须**复现解密过程**或**在正确时机 dump 内存**。

### 2. 解密器一定可分析

解密器自己不能是密文（否则自举矛盾），所以密钥必须在程序里、算法必须是普通指令。
这两条决定了 SMC 只能提高成本，不能阻止分析。本题的密钥就是 8 字节明文常量，躺在只读数据节里。

### 3. 为什么用自定义节

把代码/数据放进 `smcx` / `smcd` 有两个好处：一是不与正常节混在一起，方便整体异或；
二是节表里一眼就能看出"这里被动过手脚"，对出题人是标记，对分析者反而是路标。
ELF 的 `__start_xxx` / `__stop_xxx` 让程序不用解析自身节表就能拿到节的起止地址。

### 4. 密钥藏两份的意义

- `SMK` 解开代码与 `TK`；
- `TK` 再解开目标串。

两层使得"只看 `.rodata` 里的 ENC"得不到答案，必须至少走到 `smcd` 这一层。但两层都是异或，拿到 `SMK` 之后是几行脚本的事。

## 四、防御建议

1. **SMC 不是保护。** 解密器与密钥都在进程内，静态可复现、动态可 dump。它只对抗"打开 IDA 就看完"这一种场景。
2. **要真正提高成本，得靠组合**：控制流平坦化 + 字符串加密 + 反调试 + 完整性自校验，且密钥来自外部（服务端下发、硬件密钥），而不是常量。
3. **机密别放客户端。** 校验逻辑搬到服务端，客户端只发请求、收结果。
4. **检测侧**：`mprotect(PROT_EXEC)` 紧跟循环写代码节、非常规节名、节熵异常，都是 SMC/加壳的典型信号。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,616 B | `40fd1e6f65594ba32f3947ad6fc11869b53b49cc02bf51a4eda51ed66e21b017` |

- `smcx`：文件偏移 `0x1380`，142 字节（加密的校验函数，`AX`）；`smcd`：文件偏移 `0x2070`，8 字节（加密的 TK）。
- 解密密钥 `SMK` 位于文件偏移 `0x2068`；目标串 `ENC` 位于 `0x2040`（36 字节）。
- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`；附件中无明文 flag。

---


