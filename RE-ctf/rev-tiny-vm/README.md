# 字节码虚拟机

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 字节码虚拟机 |
| 考点 | 自定义 ISA / 内嵌解释器；字节码定位与模拟执行；trace 还原期望输入 |
| 难度 | 难（10） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,472 B） |
| Flag | `flag{tiny_vm_trace_it_out}` |

## 题面（直接复制到平台「简介」）

程序里没有直接的比较代码：它加载一段三百多字节的字节码，交给一个内嵌的小解释器执行，校验逻辑全在那段字节码里。附件 `challenge` 是 Linux x86-64 的 ELF，输入长度的要求也写在字节码的第一条指令里。要拿到的是能通过校验的 flag，提交格式 `flag{...}`。

**提示**：先从解释器的分派结构把每个操作码的语义整理出来，再按同样的语义把那段字节码模拟执行一遍。

## 二、解题步骤

### Step 1　跑一下，确认长度

```bash
cd files
chmod +x challenge
echo 'abc' | ./challenge
```

```
input your flag: Wrong!
```

### Step 2　找到解释器与字节码

`objdump -d` 的 `main` 里没有比较逻辑，只有一个"取字节码 → 查跳转表 → 更新累加器"的循环：

```
cmpb   $0xd,(%r?,%r?,1)      ; op > 0x0d -> 非法
ja     ...
movzbl (%r9,%rax,1),%eax     ; op = code[pc]
...
jmp    *%rcx                 ; 分派到 14 个分支之一
```

字节码在只读数据区：长度 314 字节，文件偏移 `0x2080`。开头三个字节是 `0c 1a 02`
（`CMP_LEN 26` → 长度锁死 26，然后 `LD` 开始算每个字符）。

```python
d = open("challenge", "rb").read()
for off in range(len(d) - 4):
    if d[off] == 0x0c and d[off + 1] == 26 and d[off + 2] == 0x02:
        print("bytecode @", hex(off))
```

```
bytecode @ 0x2080
```

### Step 3　逆出操作码表

把 switch 的 14 个分支逐个看过去，得到下表（单累加器 + 单输入指针，无栈）：

| 操作码 | 名称 | 语义 |
| --- | --- | --- |
| `0x00` | HALT_FAIL | 返回 0 |
| `0x01` | HALT_OK | 返回 1 |
| `0x02` | LD imm8 | `r = imm` |
| `0x03` | XOR imm8 | `r ^= imm` |
| `0x04` | ADD imm8 | `r = (r + imm) & 0xff` |
| `0x05` | SUB imm8 | `r = (r - imm) & 0xff` |
| `0x06` | MUL imm8 | `r = (r * imm) & 0xff` |
| `0x07` | ROL imm8 | `r = rol8(r, imm & 7)` |
| `0x08` | ROR imm8 | `r = ror8(r, imm & 7)` |
| `0x09` | CMP_BYTE | `in[pos]` 必须等于 `r`，否则返回 0；`pos++` |
| `0x0a` | JMP rel8 | 相对跳转 |
| `0x0b` | JZ rel8 | `r == 0` 时相对跳转 |
| `0x0c` | CMP_LEN imm8 | 输入长度必须等于 imm |
| `0x0d` | NOT | `r = ~r & 0xff` |

每个字符对应一小段：`LD s; MUL a; ADD b; XOR k; ROL t; CMP_BYTE`，中间还夹着"跳过垃圾字节"的 `JMP`
（永远不会执行，属于干扰）。

### Step 4　模拟执行，trace 出每个字符

不需要真的跑程序：按上表写一个小解释器，把每个 `CMP_BYTE` 处的累加器值记下来，就是期望输入。

```python
HALT_FAIL, HALT_OK, LD, XOR, ADD, SUB, MUL, ROL, ROR, CMP_BYTE, JMP, JZ, CMP_LEN, NOT = range(14)

def rol8(v, s): return ((v << s) | (v >> (8 - s))) & 0xFF
def ror8(v, s): return ((v >> s) | (v << (8 - s))) & 0xFF

def emulate(code):
    r, pc, out = 0, 0, bytearray()
    while pc < len(code):
        op = code[pc]; pc += 1
        if op == HALT_FAIL: return None
        if op == HALT_OK:   return bytes(out)
        if op == LD:    r = code[pc]; pc += 1
        elif op == XOR: r ^= code[pc]; pc += 1
        elif op == ADD: r = (r + code[pc]) & 0xFF; pc += 1
        elif op == SUB: r = (r - code[pc]) & 0xFF; pc += 1
        elif op == MUL: r = (r * code[pc]) & 0xFF; pc += 1
        elif op == ROL: r = rol8(r, code[pc] & 7); pc += 1
        elif op == ROR: r = ror8(r, code[pc] & 7); pc += 1
        elif op == CMP_BYTE: out.append(r)
        elif op == JMP:
            d8 = code[pc]; pc += 1; pc += d8 if d8 < 0x80 else d8 - 256
        elif op == JZ:
            d8 = code[pc]; pc += 1
            if r == 0: pc += d8 if d8 < 0x80 else d8 - 256
        elif op == CMP_LEN: pc += 1
        elif op == NOT: r = (~r) & 0xFF
        else: return None
    return None

d = open("challenge", "rb").read()
print(emulate(d[0x2080:0x2080 + 314]).decode())
```

```
flag{tiny_vm_trace_it_out}
```

### Step 5　提交验证

```bash
echo 'flag{tiny_vm_trace_it_out}' | ./challenge
```

```
input your flag: Good!
```

## 三、原理

### 1. 虚拟机保护做了什么

把"校验逻辑"从本地指令改写成自定义 ISA 的字节码，解释器负责执行。静态看，本地代码里只有分派循环，真正的算法藏在数据里。

### 2. 为什么可解

字节码要被执行，就必须被解释器完整理解；而解释器是普通机器码，可以读。于是：

- 操作码语义可从 switch/跳转表还原；
- 字节码是数据，可直接取出；
- 模拟执行是确定性的，不需要跑原程序。

**结论**：VM 保护把"读代码"变成"读解释器 + 模拟字节码"，成本从 1 升到 2～3 倍，但性质仍是可分析。

### 3. 干扰项

字节码里夹了 `JMP rel8` 跳过随机字节的片段。它们永远不会执行，但会让"直接按操作码密度扫"的脚本错位，正确做法是按控制流走，而不是线性扫描。

### 4. 为什么要 trace 而不是穷举

每个字符由 `LD/MUL/ADD/XOR/ROL` 五条指令的合成结果决定，26 个字符共 130 条指令。逐条记录 `CMP_BYTE` 处的值，等价于直接读出答案；而按字节穷举需要 26 × 256 次尝试。

## 四、防御建议

1. **VM 保护不是加密。** 解释器可读、字节码可取、执行可模拟。要真正提高成本，必须叠加：解释器混淆、字节码加密 + 运行时解密、多套 ISA、控制流平坦化。
2. **机密不要进字节码。** 本题期望输入完全由字节码常量决定，因此天然可还原。校验应放服务端。
3. **需要离线方案**：用 HMAC + 外部密钥（服务端下发/硬件密钥），而不是"自定义 ISA 里的常量比较"。
4. **检测侧**：出现"取字节 + 跳转表分派 + 单累加器"的循环、以及只读数据里一段高熵且与代码无交叉引用的字节串，基本就是 VM 保护；可以先定位分派表再反推 ISA。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,472 B | `f49c540b3b6ba9ce6ae5731adf6f014eebb5d13f7a8ee65ede4b074d803bad5d` |

- 字节码位于文件偏移 `0x2080`，长度 314 字节，以 `0c 1a 02`（`CMP_LEN 26` / `LD`）开头。
- 解释器位于 `.text`，操作码 0x00～0x0d 共 14 个，无栈、单 8 位累加器。
- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`；附件中无明文 flag（字节码是二进制，`strings` 无命中）。

---


