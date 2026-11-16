# pwn-hello-tools

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 1（该方向第一课） |
| 形态 | 附件题，无容器 |
| 考点 | `file` / `checksec` / `strings` / `objdump -d` 四件套；ELF 头、保护位、只读数据段、反汇编 |
| 附件 | `files/hello_pwn`（ELF 64-bit x86-64，16 KB） |
| Flag | 提交格式 `flag{...}`，内容由程序在 key 正确时打印 |

Pwn 的第一步不是写利用，是**把二进制定性**：它是什么格式、开了哪些保护、里面有哪些常量、
主逻辑长什么样。这四件事对应四个命令，本题把四个命令各跑一遍，每一条都说清在看什么。

## 题面（直接复制到平台「简介」）

靶机上留了一个命令行小程序，跑起来会要一个 key，key 被写死在程序里。附件 `hello_pwn` 就是它，一个 Linux 可执行文件。目标：找出这个 key，让程序把 flag 打印出来。

**提示**：`strings` 只看连续的可打印字符；不以这种形式存放的内容，得靠 `objdump -d` 去读。

## 二、题目描述

附件是一个 Linux 可执行文件。直接运行：

```bash
chmod +x hello_pwn
./hello_pwn
```

输出：

```
Enter key: 
```

随便输点东西回车，得到 `Access denied.`。
程序内部拿输入和一个常量做比较，比较通过才打印 flag。这个常量**没有写在题面里**，它在二进制里。

目标：不用猜、不用爆破，用工具把这个常量找出来，再把它喂回程序。

## 三、解题步骤

### 第 0 步：先跑一次

```bash
./hello_pwn
# Enter key: wrong
# Access denied.
```

先确认程序的行为，再动手分析。行为永远优先于猜测。

### 第 1 步：`file`：这是什么文件

```bash
file hello_pwn
```

```
hello_pwn: ELF 64-bit LSB executable, x86-64, version 1 (SYSV), dynamically linked,
interpreter /lib64/ld-linux-x86-64.so.2, BuildID[sha1]=..., for GNU/Linux 3.2.0, not stripped
```

逐段读这句话：

| 字段 | 含义 | 对后续的影响 |
|---|---|---|
| `ELF 64-bit LSB` | Linux 的可执行格式，64 位，小端 | 地址 8 字节、按小端序写进 payload |
| `x86-64` | 指令集 | 反汇编要用 x86-64 的反汇编器 |
| `executable` | 不是 `shared object`（PIE 可执行文件会显示 shared object） | 代码段地址固定，`0x400000` 起 |
| `dynamically linked` | 依赖 libc.so.6 | 后面会遇到 `printf@plt` 这类跳转 |
| `not stripped` | 符号表还在 | `nm`、`objdump` 能直接看到函数名 |

`not stripped` 是最省事的一条：函数名还在，`objdump` 会把 `<main>`、`<strcmp@plt>` 一起标出来。

### 第 2 步：`checksec`：开了哪些保护

```bash
checksec --file=hello_pwn
```

```
    Arch:       amd64-64-little
    RELRO:      Partial RELRO
    Stack:      No canary found
    NX:         NX enabled
    PIE:        No PIE (0x400000)
    Stripped:   No
```

每一条的含义：

| 保护 | 本题值 | 含义 |
|---|---|---|
| RELRO | Partial | `.got.plt` 可写，GOT 劫持类手法可用；Full RELRO 会把整张 GOT 变只读 |
| Canary | 无 | 栈上没插哨兵，覆盖返回地址不会被检测 |
| NX | enabled | 栈不可执行，放 shellcode 到栈上跑不通 |
| PIE | 关 | 代码段固定加载在 `0x400000`，地址可以硬编码 |
| Stripped | No | 符号未删 |

没装 `checksec` 时，两条替代写法等价：

```bash
# pwntools
python3 -c "from pwn import *; print(ELF('./hello_pwn').checksec())"

# 纯 binutils
readelf -h hello_pwn | grep Type        # EXEC = 非 PIE，DYN = PIE
readelf -lW hello_pwn | grep GNU_STACK  # 带 RWE 表示栈可执行
readelf -sW hello_pwn | grep -c FUNC    # 有符号表 = 未 strip
```

本题的保护状态说明：这道题不走栈溢出路线，保护信息只用来说明"四件套第 2 条在看什么"。

### 第 3 步：`strings`：有哪些硬编码常量

`strings` 把文件里连续 4 个以上的可打印字符抠出来。C 语言的字符串常量在编译后会原样躺在
`.rodata` 段里，所以提示语、密钥、路径这类东西都能被它扫到。

```bash
strings -a hello_pwn | less
```

输出不到 60 行，从头翻到尾。里面会出现：

```
Enter key: 
Access denied.
Access granted.
```

外加一行**不属于任何提示语**的英文短串。那一行就是 key：它出现在程序里，但没有被 `printf` 打印过。

`strings` 只能看见"连续的可打印字符"。如果作者把常量打散、加密、或者用 `\xNN` 拼出来，
`strings` 就什么都看不到，本题没有做这层处理，key 是明文。

### 第 4 步：`objdump -d`：主逻辑长什么样

`strings` 告诉你"有什么"，`objdump` 告诉你"怎么用"。反汇编 `main`：

```bash
objdump -d hello_pwn | sed -n '/<main>:/,/^$/p'
```

关键片段（地址与本题一致）：

```asm
401214: mov    0x2e55(%rip),%rdx        # 404070 <stdin@GLIBC_2.2.5>
40121b: lea    -0x50(%rbp),%rax         # 输入缓冲区
40121f: mov    $0x40,%esi               # 最多读 0x40 字节
401227: call   4010c0 <fgets@plt>
...
40123b: lea    -0x50(%rbp),%rax
40123f: lea    0xe26(%rip),%rdx         # 40206c  -> "\n"
40124c: call   4010b0 <strcspn@plt>     # 去掉行尾换行
...
40125a: lea    0xdef(%rip),%rdx         # 402050 <secret_key>   <-- 被比较的常量
401267: call   4010d0 <strcmp@plt>
40126c: test   %eax,%eax
40126e: je     401286 <main+0xb0>       # strcmp 返回 0 才跳到打印分支
401270: lea    0xdf7(%rip),%rax         # 40206e -> "Access denied."
40127a: call   401090 <puts@plt>
```

读法：

1. `fgets` 把输入读进栈上 `rbp-0x50` 的缓冲区；
2. `strcspn` 把行尾的 `\n` 换成 `\0`；
3. `strcmp(输入, 0x402050)`；
4. `test eax,eax` + `je`：结果为 0（相等）就跳到打印分支，否则 `Access denied.`。

注释里的 `402050 <secret_key>` 已经把常量所在的符号名写出来了。用 `nm` 也能直接看到它的地址：

```bash
nm hello_pwn | grep -i key
```

### 第 5 步：把 key 喂回去

把第 3 步找到的那一行原样送进程序：

```bash
echo '<你找到的 key>' | ./hello_pwn
```

或者交互式跑，手动输入：

```bash
./hello_pwn
# Enter key: <你找到的 key>
# Access granted.
# flag{...}
```

拿到的 `flag{...}` 就是答案。

### 第 6 步：留给你自己动手

上面的路线走通了以后，试两件事：

1. **不用 key 直接拿 flag**。第 4 步的片段里，`strcmp` 成功之后有一个循环：

   ```asm
   401290: lea    0xd89(%rip),%rdx        # 402020 <enc_flag>
   40129e: movzbl (%rax),%eax
   4012a1: xor    $0x37,%eax
   ```

   flag 是以"逐字节异或 0x37"的形式存放在 `0x402020` 的。用 `objdump -s` 或 `xxd` 把这段字节
   读出来，自己做一次异或，看能不能直接拼出 flag。

2. **为什么 `strings` 抓不到 flag**。用第 3 步的 `strings` 输出对一遍，确认 flag 的明文确实不在里面。
   想清楚"异或之后为什么就不可打印了"，这是后面所有编码/加密类题目的基础。

## 四、原理

**ELF 里字符串住在哪**。C 的字符串字面量编译后进 `.rodata`（只读数据段），运行时不改、不加密，
所以在文件里能按原样读到。`strings` 只是扫描文件的字节流，把连续可打印字符切出来，不做任何解析。

**为什么 `file` 一眼能看出这么多东西**。ELF 头里有 `e_type`（EXEC/DYN）、`e_machine`（x86-64）、
`e_ident[EI_CLASS]`（64 位）等字段，`file` 读的就是这些。

**为什么要看保护**。攻击面由保护决定：NX 决定能不能跳栈上执行，PIE 决定地址能不能硬编码，
RELRO 决定 GOT 能不能写，canary 决定溢出会不会被发现。四件套里的 `checksec` 就是给这几项定性。

**`objdump -d` 在做什么**。它把 `.text` 段的机器码按指令边界逐条翻译成汇编，再把符号表里的名字
贴回去（`call 4010c0 <fgets@plt>`）。有符号表时，反汇编可读性会高一个档次，这也是为什么
"not stripped" 值得单独记一笔。

**这类题的通用套路**：`file` → `checksec` → `strings` → `objdump`。四步之内拿不到线索，
再上 `gdb` 动态调试；再不行才动手写利用。本题就是这条流水线的最短版本。

## 五、防御建议

- **不要把密钥/flag 编进二进制**。二进制的字符串常量对 `strings` 完全透明。
  密钥从环境变量或配置文件读，且不要跟着程序一起发出去。
- 即使把常量异或/分片，静态分析仍然能还原（第 6 步就是演示）。真要保密，密钥不能出现在客户端。
- 发布二进制时 `strip` 掉符号表：`strip hello_pwn`。符号名会泄露函数用途和结构。
- 打开 PIE（`-fPIE -pie`）和 canary（`-fstack-protector-all`），让后续利用的成本上升。
- 校验逻辑不要用 `strcmp`，它会在第一个不同字节就返回，可被逐字节侧信道试探。
  需要比较密钥时用常量时间比较函数。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/hello_pwn` | 题目附件，ELF 64-bit x86-64，16304 字节 |

```
sha256: 9c09aa2ebb057af9935a08c63e5fef8411242c841dfa01affe19d8b210bfddae
```

`file` 输出：

```
hello_pwn: ELF 64-bit LSB executable, x86-64, version 1 (SYSV), dynamically linked,
interpreter /lib64/ld-linux-x86-64.so.2, for GNU/Linux 3.2.0, not stripped
```

`checksec`：

```
RELRO:    Partial RELRO
Stack:    No canary found
NX:       NX enabled
PIE:      No PIE (0x400000)
Stripped: No
```

出题人自用（不发给选手）：`src/hello_pwn.c` 为源码，编译命令
`gcc -o files/hello_pwn src/hello_pwn.c -fno-stack-protector -no-pie`，
编译环境 `ubuntu:22.04` + `gcc 11.4.0`。
