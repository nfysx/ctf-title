# 工具链第一课：三个命令

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 工具链第一课：三个命令 |
| 考点 | `file` / `strings` / `objdump -d` 各自在看什么；字符串常量与代码立即数的区别；ELF 节布局 |
| 难度 | 1 |
| 附件 | `hello_re`（Linux x86-64 动态链接 ELF，14,472 B，`-O2 -s`） |
| Flag | `flag{toolchain_in_three_commands}` |

## 题面（直接复制到平台「简介」）

这是一道教学关卡：程序运行后会打印欢迎语，并告诉你 flag 由两段密钥拼成。附件 `hello_re` 是 Linux x86-64 的 ELF，16 KB 不到。两段密钥各只在一个工具的输出里出现，接起来就是 flag 花括号里的内容，提交格式 `flag{...}`。

**提示**：把「看格式」「找明文」「看代码」这三件事分给对应的命令；一段找不到时，换另一个工具的输出再试。

## 二、解题步骤

下面的命令都在 `files` 目录下执行。

### Step 1　先问格式：`file`（或 `objdump -f`）

```bash
cd files
chmod +x hello_re
file hello_re
```

```
hello_re: ELF 64-bit LSB pie executable, x86-64, version 1 (SYSV), dynamically linked, ..., stripped
```

`pie executable` 说明按位置无关方式链接、动态加载；`stripped` 说明符号表被删过。`objdump -f` 给出同一结论和入口地址：

```bash
objdump -f hello_re
```

```
hello_re:     file format elf64-x86-64
architecture: i386:x86-64, flags 0x00000150:
HAS_SYMS, DYNAMIC, D_PAGED
start address 0x0000000000001300
```

想知道"代码在哪、数据在哪"，看节表：

```bash
readelf -S hello_re | grep -E ' .text| .rodata'
```

```
  [14] .text             PROGBITS        0000000000001100 00001100 000003c9 00  AX  0   0 16
  [15] .rodata           PROGBITS        0000000000002000 00002000 00000270 00   A  0   0 32
```

`.text` 从 `0x1100` 起、带 `AX` 标志，这就是 `objdump -d` 要看的地方；`.rodata` 放只读常量，`-d` 不会碰它们。

### Step 2　问明文：`strings`

```bash
strings -n 4 hello_re | grep -i toolchain
```

```
toolchain_in_
```

`strings` 的判据很简单：连续 ≥ N 个可打印字节（GNU 实现默认 N=4，`-n` 可改），遇到 `\0` 或不可打印字节就截断。所以 C 里的 `static const char KEY1[] = "...";` 必然出现在输出里，那段字节必须原样留在文件里，程序运行时才能用它比较。

注意 `-s`（去符号）删的是 `.symtab`，不是常量：附件是 `-O2 -s` 编译的，函数名看不到，`toolchain_in_` 照样在。

### Step 3　问代码：`objdump -d`

KEY2 不在 `strings` 的输出里，因为它根本不是一段连续的数据，它被逐字节写进栈上缓冲区：

```c
static void build_key2(volatile char *dst) { dst[0] = 't'; dst[1] = 'h'; ... }
```

`volatile` 让编译器不能把这 14 个字节折成 `.rodata` 里的一段常量，只能编成一串"把立即数写进栈"的指令。用 `objdump -d` 反汇编，再过滤 `movb`：

```bash
objdump -d hello_re | grep 'movb'
```

```
    11dd:  c6 04 24 74           movb   $0x74,(%rsp)
    11e8:  c6 44 24 09 68        movb   $0x68,0x9(%rsp)
    11fe:  c6 44 24 0a 72        movb   $0x72,0xa(%rsp)
    120f:  c6 44 24 0b 65        movb   $0x65,0xb(%rsp)
    1214:  c6 44 24 0c 65        movb   $0x65,0xc(%rsp)
    1219:  c6 44 24 0d 5f        movb   $0x5f,0xd(%rsp)
    121e:  c6 44 24 0e 63        movb   $0x63,0xe(%rsp)
    1223:  c6 44 24 0f 6f        movb   $0x6f,0xf(%rsp)
    1228:  c6 44 24 10 6d        movb   $0x6d,0x10(%rsp)
    122d:  c6 44 24 11 6d        movb   $0x6d,0x11(%rsp)
    1232:  c6 44 24 12 61        movb   $0x61,0x12(%rsp)
    1237:  c6 44 24 13 6e        movb   $0x6e,0x13(%rsp)
    123c:  c6 44 24 14 64        movb   $0x64,0x14(%rsp)
    1241:  c6 44 24 15 73        movb   $0x73,0x15(%rsp)
    1246:  c6 44 24 16 00        movb   $0x0,0x16(%rsp)
```

读法：目标地址 `(%rsp)`、`0x9(%rsp)`、`0xa(%rsp)` …… 逐个加一，就是缓冲区里连续的字节；立即数就是字符。`$0x74` 是 ASCII 的 `t`，`$0x00` 是结尾的 `NUL`。完整转换见 Step 4。

### Step 4　自己动手：把 15 个立即数变成字符串

这一步留给你。要求：只用上面 `objdump` 的输出，把 `$0x??` 按栈偏移顺序（`(%rsp) → 0x9 → 0xa → … → 0x15`）转成字符，遇到 `0x00` 停止，得到 KEY2。

一个可用的写法（先自己想，再对照）：

```python
import re, subprocess
out = subprocess.run(["objdump", "-d", "hello_re"], capture_output=True, text=True).stdout
pairs = []
for line in out.splitlines():
    m = re.search(r"movb\s+\$0x([0-9a-f]{2}),(?:0x([0-9a-f]+))?\(%rsp\)", line)
    if m:
        pairs.append((int(m.group(2), 16) if m.group(2) else 0, int(m.group(1), 16)))
pairs.sort()                       # 按栈偏移排，就是写入顺序
print(bytes(v for _, v in pairs if v).decode())
```

```
three_commands
```

### Step 5　拼接并验证

```
KEY1 = toolchain_in_
KEY2 = three_commands
flag = flag{KEY1KEY2}
```

```bash
echo 'flag{toolchain_in_three_commands}' | ./hello_re
```

```
input the flag: Good!
```

退出码 0。

### Step 6　再走一步（可选）

- 把 `build_key2` 里的 `volatile` 去掉、用 `-O2` 重新编译，再跑一次 `strings`：KEY2 会跑到 `.rodata` 里，`objdump -d` 里那条 `movb` 链就消失了。这一步能看清"编译器优化"与"工具能看到什么"的关系。
- 用 `objdump -s -j .rodata hello_re` 看看只读数据节长什么样，注意 `-s` 才会把数据按十六进制 + ASCII 打出来，`-d` 不会。

## 三、原理

### 1. 三个命令看的是文件的不同部分

| 命令 | 输入 | 输出 | 看不见什么 |
| --- | --- | --- | --- |
| `file` | 文件头魔法字节 | 格式/架构 | 内容 |
| `strings` | 全文件的字节流 | 可打印串 | 代码逻辑、非连续数据 |
| `objdump -d` | 代码节（`.text`） | 指令 | 只读数据节内容、运行期算出来的值 |

KEY1 是数据（连续、可打印、以 NUL 结尾）→ 只有 `strings` 看得到。KEY2 是代码里的立即数（每个字节分别嵌在一条指令里）→ 只有 `objdump -d` 看得到。

### 2. 为什么立即数在 `strings` 里拼不起来

`strings` 要求字节连续。`movb $0x74,(%rsp)` 的机器码是 `c6 04 24 74`：目标地址字节把 `74` 和上一条隔开了，两条指令之间还夹着别的指令。在字节流里，`t h r e e ...` 从来不是连续的 14 个可打印字节，所以 `strings` 抓不到。

### 3. `volatile` 在这里的作用

不加 `volatile` 时，`-O2` 会发现 `dst` 的内容是编译期已知的常量，于是把它折成 `.rodata` 里的一段 14 字节常量 + 一次 `memcpy`。那样 KEY2 就会出现在 `strings` 里，题目想教的两条路就并成了一条。（Step 6 的动手实验就是验证这一点。）

### 4. 栈偏移不是随机数

`(%rsp) → 0x9 → 0xa → … → 0x16` 是同一个局部数组的连续元素；编译器把 `dst[i]` 摊平成逐字节写。看到"一串目标地址连续、立即数各不相同的 `movb`"，就是"在拼一个字符串"，这是逆向里最常见的常量重建模式。

## 四、防御建议

1. **密钥不要编译进客户端。** `strings` 一秒出结果，`movb` 链也是五分钟的事。授权、密钥、内部地址一律放服务端。
2. **把 `strings`/熵扫描放进发布流程。** 每次构建后跑一遍关键字列表（`key`、`token`、`secret`、`flag`、内网域名），命中了就人工看。
3. **不要用"拆成立即数"当保护。** 它只让明文不可读，不改变"信息在客户端"这一事实。
4. **检测侧**：`movb $imm,disp(%rsp)` 连续出现、目标位移递增，是"运行时拼字符串"的稳定特征，可以直接用 YARA / 反汇编特征命中。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/hello_re` | 14,472 B | `25a329166725b9f288e21f5ffab572647ad2d4e781683f4b894f99e842491bf9` |

- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`（无符号表），入口 `0x1300`；`.text` 文件偏移 `0x1100`。
- KEY1 `toolchain_in_` 位于文件偏移 `0x2258`（`.rodata`）；KEY2 只以 15 条 `movb` 立即数（含结尾 `0x00`）的形式存在于 `.text`。
- 附件中不含完整 flag：`b"toolchain_in_three_commands"` 与 `b"three_commands"` 全文搜索均无命中（实测）。

---


