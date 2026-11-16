# 反编译第一课：读 main

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 反编译第一课：读 main |
| 考点 | 反编译器的基本用法（Ghidra 导入 → 找 main → 读伪代码）；符号表的意义；为什么 `strings` 拿不到"顺序" |
| 难度 | 1 |
| 附件 | `hello_decomp`（Linux x86-64 动态链接 ELF，16,440 B，`-O2`，保留符号表） |
| Flag | `flag{decompiler_reads_the_logic}` |

## 题面（直接复制到平台「简介」）

这是反编译的第一课：flag 被拆成 4 段短常量，每段都不长。附件 `hello_decomp` 是 Linux x86-64 的 ELF（保留符号表）。只靠 `strings` 能看到碎片，但看不到拼接顺序；把顺序读出来才能拼出 flag，提交格式 `flag{...}`。

**提示**：顺序信息只存在于代码里。用反编译器打开，从入口找到主函数，看它是按什么次序把这些常量接起来的。

## 二、解题步骤

### Step 1　先确认 `strings` 能给到什么

```bash
cd files
chmod +x hello_decomp
strings -t x -n 4 hello_decomp | grep -E '2140|2148|2153|215a'
```

```
   2140 _reads
   2148 decompiler
   2153 _logic
   215a _the
```

四段碎片都在，但注意两件事：

1. `strings` 按**文件偏移**排序，这是数据在文件里的摆放顺序，和源码里的拼接顺序无关；
2. 最短的一段是 4 个字符（`_the`），所以要用 `-n 4`；GNU `strings` 默认最小长度就是 4，但如果有人写成 3 字符的片段就会被漏掉。

按上面这个顺序（文件偏移）硬拼，得到 `_reads` + `decompiler` + `_logic` + `_the` = `_readsdecompiler_logic_the`，明显不通。正确顺序在代码里。

### Step 2　Ghidra：打开、导入、分析

1. 启动 Ghidra → `File → New Project...` → 选 `Non-Shared Project` → 指定一个空目录 → 起名 → `Finish`。
2. `File → Import File...` → 选中 `hello_decomp`。弹窗里 Format 会自动识别成 `Executable and Linking Format (ELF)`，直接 `OK`。
3. 在项目树里双击刚导入的文件。提示 `Analyze?` 时点 `Yes`（默认分析器即可），等左下角进度条走完。
4. 左侧 `Symbol Tree` → `Functions` → 找到 `main` 双击。本题**保留了符号表**，所以 `main`、`P1..P4`、`append` 都是真名；如果是 `-s` 去符号的样本，这里会显示成 `FUN_00101120`，那时从 `entry` 传给 `__libc_start_main` 的第一个参数去找 main。
5. 右侧 `Decompiler` 面板就是伪代码；没显示就点 `Window → Decompiler`。

### Step 3　在伪代码里读拼接顺序

`main` 里是连续四次对 `append` 的调用，参数为同一个栈缓冲区 + 一段常量。语义等价的 C 形式如下（Ghidra 会按自己的命名风格显示，比如 `(*DAT_...)(...)` 或 `append(...)`，调用顺序不变）：

```c
strcpy(expect, "flag{");
append(expect, P3);       /* decompiler */
append(expect, P4);       /* _reads     */
append(expect, P1);       /* _the       */
append(expect, P2);       /* _logic     */
append(expect, "}");
```

顺序即 `P3 → P4 → P1 → P2`。在 `Listing` 里双击 `P3`（或按 `g` 跳到 `0x2148`）就能看到它的内容。四段分别是：

| 符号 | 地址 | 内容 |
| --- | --- | --- |
| `P3` | `0x2148` | `decompiler` |
| `P4` | `0x2140` | `_reads` |
| `P1` | `0x215a` | `_the` |
| `P2` | `0x2153` | `_logic` |

### Step 4　没有 Ghidra 时的等价路线

`objdump` 也能给出调用顺序，反编译器做的第一件事就是把同样的信息整理成人能读的形式：

```bash
objdump -d hello_decomp | grep -E 'lea.*<P[1-4]>|call.*<append>'
```

```
    11ac:  48 8d 35 95 0f 00 00  lea    0xf95(%rip),%rsi        # 2148 <P3>
    11c9:  e8 c2 01 00 00        call   1390 <append>
    11ce:  48 8d 35 6b 0f 00 00  lea    0xf6b(%rip),%rsi        # 2140 <P4>
    11d8:  e8 b3 01 00 00        call   1390 <append>
    11dd:  48 8d 35 76 0f 00 00  lea    0xf76(%rip),%rsi        # 215a <P1>
    11e7:  e8 a4 01 00 00        call   1390 <append>
    11ec:  48 8d 35 60 0f 00 00  lea    0xf60(%rip),%rsi        # 2153 <P2>
    11f6:  e8 95 01 00 00        call   1390 <append>
    1205:  e8 86 01 00 00        call   1390 <append>
```

每次调用前先把某个片段装进 `rsi`（第二参数），再 `call append`，顺序一眼可见：P3、P4、P1、P2。
最后那条没有 `lea` 的调用接的是字面量 `"}"`，即收尾的花括号。
符号表保留了 `P1..P4` 与 `append`，所以 `objdump` 直接把名字标了出来，这正是符号表的价值。

### Step 5　拼起来并验证

按 Step 3 的顺序把四段接起来，套上 `flag{}`：

```bash
echo 'flag{decompiler_reads_the_logic}' | ./hello_decomp
```

```
input the flag: Good!
```

退出码 0。

### Step 6　自己动手（可选）

- 把 `P1..P4` 的声明顺序改一下再编译：`strings` 的输出顺序会变，但程序接受的 flag 不变，证明"数据摆放顺序 ≠ 使用顺序"。
- 用 `nm hello_decomp` 看看符号表里的 `main`、`P1..P4`、`append` 分别在哪；再对 `-s` 版本跑一次，观察这些名字全部消失。
- 把 `append` 的 `__attribute__((noinline))` 去掉重编译，再用 Ghidra 看：四个调用会被内联成一串 `mov`，伪代码可读性明显下降。

## 三、原理

### 1. 反编译器做了什么

它把机器码还原成"基本块 + 数据流"：栈上缓冲区、常量地址、调用参数位置都能恢复，于是 `call append` 的前一步 `lea rsi, [P3]` 就变成伪代码里的 `append(expect, P3)`。它不会恢复原始源码的变量名（除符号表里有），但**顺序、参数、控制流**这三样是最可靠的。

### 2. 为什么 `strings` 拿不到顺序

`strings` 的输出顺序等于"常量在文件里的偏移顺序"，由链接器决定，和源码顺序无关。本题故意让声明顺序（P1、P2、P3、P4）和拼接顺序（P3、P4、P1、P2）错开，文件里的排列又是 `_reads / decompiler / _logic / _the`，三种顺序互不相同。**"顺序"是逻辑信息，只存在于代码里**，这是本课最核心的一句。

### 3. 为什么用 `noinline` 的 `append`

如果直接写 `strcat(expect, P3);`，`-O2` 会把整条拼接内联成几条 `mov`，伪代码退化成字节搬运，反而不适合第一课。加 `noinline` 后每次拼接都是真实调用，伪代码与反汇编都保持"一行一次拼接"的形状，顺序一目了然。

### 4. 符号表差异

| 编译方式 | Ghidra 函数树 | `objdump` 显示 |
| --- | --- | --- |
| `-O2`（本题） | `main` / `append` / `P1..P4` | `call 1390 <append>` |
| `-O2 -s` | `FUN_00101120` 之类 | `call 1390` |

去符号只删名字，不删结构；分析成本从"读名字"变成"跟调用链"，但结论一样。

## 四、防御建议

1. **拆常量不构成保护。** 它把"读一行"变成"读四行"，反编译五分钟就能还原顺序。
2. **去符号只提高一点成本。** 入口点、启动流程、调用链都在，函数边界可以被工具自动识别。
3. **机密不要放客户端。** 无论拆成几段、是否加密，只要"答案"能被本地算出来，就一定可以被还原；正确的做法是把判断放到服务端，客户端只发请求。
4. **检测侧**：一串短常量 + 连续 `strcat`/`memcpy` 调用，是"运行时拼字符串"的典型形态；把它和 `strcmp` 的位置关联起来，可以直接定位校验点。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/hello_decomp` | 16,440 B | `394945228bac218818e02561518a8123d154bbeb9a06577566f374021b59607b` |

- ELF 64-bit LSB pie executable（x86-64，动态链接），`-O2`（保留符号表），入口 `0x12a0`；`main` 在 `0x1120`，拼接函数 `append` 在 `0x1390`。
- 四段常量位于 `.rodata`：`_reads` @ `0x2140`、`decompiler` @ `0x2148`、`_logic` @ `0x2153`、`_the` @ `0x215a`（文件偏移）。
- 附件中不含完整 flag：`b"decompiler_reads_the_logic"` 全文搜索无命中（实测）。

---


