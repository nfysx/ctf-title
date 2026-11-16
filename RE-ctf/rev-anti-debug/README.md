# 反调试下面的真路径

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 反调试下面的真路径 |
| 考点 | `ptrace(PTRACE_TRACEME)` 自检；Linux 反调试原理；静态还原绕过反调试 |
| 难度 | 难（9） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,536 B） |
| Flag | `flag{anti_debug_hides_the_real_path}` |

## 题面（直接复制到平台「简介」）

程序启动后先读输入，然后检查自己是否被调试：一旦发现被调试就直接退出，什么都不打印。附件 `challenge` 是 Linux x86-64 的 ELF。要拿到的是它真正校验的那串 flag，提交格式 `flag{...}`。

**提示**：反调试只挡住「边跑边看」这一条路；真正参与校验的常量都在文件里，静态读出来自己算一遍同样能拿到答案。

## 二、解题步骤

### Step 1　正常跑

```bash
cd files
chmod +x challenge
echo 'flag{anti_debug_hides_the_real_path}' | ./challenge
```

```
input your flag: Good!
```

退出码 0。没有被调试时程序行为完全正常。

### Step 2　挂调试器，看它怎么跑掉

```bash
printf 'flag{anti_debug_hides_the_real_path}\n' > /tmp/in.txt
gdb -q -batch -ex 'run < /tmp/in.txt' ./challenge
```

```
input your flag: [Inferior 1 (process 94) exited with code 01]
```

输入是对的，但程序在看到调试器之后直接 `exit(1)`，连 `Wrong!` 都不打印。反汇编里能看到这一步：

```
11ad:  xor    %eax,%eax
11af:  call   10f0 <ptrace@plt>
11b4:  add    $0x1,%rax
11b8:  je     1177                 ; rax == -1 -> 直接退出
11ba:  mov    %rbx,%rdi
11bd:  call   10c0 <strlen@plt>
11c2:  cmp    $0x24,%rax            ; 长度必须 36
```

`ptrace(PTRACE_TRACEME)` 在"已经被别人跟踪"时返回 -1，于是 `rax+1 == 0`，跳进退出分支。

### Step 3　不装调试器也能拿 flag：静态还原

真校验是逐字节可逆运算，只依赖两块常量：目标数组 `T`（文件偏移 `0x3020`，36 字节）与密钥 `K`（`0x3048`，8 字节）。

```
v = in[i] ^ K[i % 8]
v = rol4(v)                     ; (v << 4) | (v >> 4)
v = (v + i * 0x13) & 0xff
v 与 T[i] 比较
```

逆过来就是答案：

```python
d = open("challenge", "rb").read()
T = d[0x3020:0x3020 + 36]
K = d[0x3048:0x3048 + 8]

def ror4(v):
    return ((v >> 4) | (v << 4)) & 0xFF

out = bytes(ror4((T[i] - i * 0x13) & 0xFF) ^ K[i % 8] for i in range(36))
print(out.decode())
```

```
flag{anti_debug_hides_the_real_path}
```

这条路线不依赖调试器、也不改二进制：反调试只挡住"动态调试"，挡不住"读常量 + 反算"。

### Step 4　另一条路：patch 掉自检

把 `0x11af` 处那条 `call ptrace@plt`（5 字节）换成 `xor eax,eax; nop*3`，让自检恒返回 0：

```bash
python3 - <<'PY'
d = bytearray(open("challenge", "rb").read())
assert d[0x11af] == 0xe8           # call rel32
d[0x11af:0x11b4] = bytes([0x31, 0xC0, 0x90, 0x90, 0x90])
open("patched", "wb").write(d)
PY
chmod +x patched
gdb -q -batch -ex 'run < /tmp/in.txt' ./patched
```

```
input your flag: Good!
[Inferior 1 (process 36) exited normally]
```

改完之后在调试器里也能正常走完校验，说明这道题拦住的是"运行观察"，不是"理解逻辑"。

### Step 5　提交

```
flag{anti_debug_hides_the_real_path}
```

## 三、原理

### 1. `PTRACE_TRACEME` 在读什么

一个进程在同一时刻只能有一个跟踪者。调用 `ptrace(PTRACE_TRACEME)` 相当于说"让我的父进程跟踪我"：
已经有人跟踪时内核直接返回 -1（`EPERM`），没人跟踪时返回 0 并把自己登记为被跟踪者。
gdb 附加的进程已经处于被跟踪状态，所以这一步必然失败。

等价的检测还有读 `/proc/self/status` 的 `TracerPid:` 字段，非 0 就说明有人在跟踪。
两种做法都很便宜，也都只挡住"动态"这一面。

### 2. 反调试挡的是"动态"，不是"静态"

- 动态分析：调试器里跑，代码路径被改，看不到真校验。
- 静态分析：反汇编 + 读常量 + 反算，完全不受影响。

本题的校验是纯可逆运算，所以静态路线的成本低于"绕过反调试"的成本。这也是真实样本里常见的失误：
反调试做得很勤，校验却仍把密钥/目标串平铺在数据节。

### 3. 为什么还能 patch

`PTRACE_TRACEME` 是一次普通函数调用，返回值决定分支走向。把调用替换成"返回 0"就等于告诉程序"没人跟踪我"。
再进一步，Linux 上还有 `LD_PRELOAD` 覆盖 `ptrace`、或者直接改 `Being traced` 的判断分支等做法，成本都在分钟级。

## 四、防御建议

1. **反调试不是保护。** 它只提高动态分析成本；静态分析只要常量可读就能还原。
2. **机密不要以"可逆变换 + 常量"的形式放客户端。** 应该把判断交给服务端，客户端只发请求。
3. **要用反调试就组合使用**：`ptrace` 自检、`/proc/self/status` 交叉校验、时序检测、完整性自校验，
   并且让校验本身依赖外部密钥（服务端下发 / 硬件密钥），而不是数据节常量。
4. **检测侧**：二进制里出现 `ptrace@plt` 且紧接着"返回值比较 + 直接退出"的模式，是反调试的标准指纹。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,536 B | `1cb211c90d331cb74b53a025fb47d9c268fd81876dcbff9a2ebfecb5387fe171` |

- 目标数组 `T[36]` 位于文件偏移 `0x3020`；密钥 `K[8]` 位于 `0x3048`。
- 自检调用 `call ptrace@plt` 位于文件偏移 `0x11af`（VA 同为 `0x11af`）。
- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`；附件中无明文 flag。

---


