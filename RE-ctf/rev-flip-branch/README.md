# 写反的分支

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 写反的分支 |
| 考点 | 条件跳转与条件码；ELF 下的 patch；文件偏移与虚拟地址的关系；常量还原 |
| 难度 | 易（3） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,504 B） |
| Flag | `flag{flip_the_branch_and_win}` |

## 题面（直接复制到平台「简介」）

这个 Linux 程序会读入你的输入并做一次比较，但结果用反了：输入正确时打印 `Wrong!`，输入随意时反而打印 `Good!`。附件是 `challenge`（ELF，x86-64）。需要提交的是它本来想校验的那串 flag，格式 `flag{...}`。

**提示**：比较之后的那条条件跳转决定了走哪条提示；先用反汇编确认它有没有被写反，再决定是改程序里的字节，还是从常量把答案推出来。

## 二、解题步骤

### Step 1　观察反常行为

```bash
cd files
chmod +x challenge
echo 'flag{flip_the_branch_and_win}' | ./challenge
```

```
input your flag: Wrong!
```

```bash
echo 'abc' | ./challenge
```

```
input your flag: Good!
```

正确输入返回 1，随意输入返回 0，条件分支被判反了。

### Step 2　从常量拿 flag（不改二进制）

比较用的真值来自 `ENC` 常量，运行时逐字节异或 `0x5a` 还原：

```python
d = open("challenge", "rb").read()
for off in range(len(d) - 29):
    dec = bytes(b ^ 0x5A for b in d[off:off + 29])
    if dec.startswith(b"flag{") and dec.endswith(b"}"):
        print(hex(off), dec.decode())
```

```
0x3010 flag{flip_the_branch_and_win}
```

### Step 3　定位那条分支

```bash
objdump -d challenge | grep -B4 -A6 'call.*<strcmp@plt>'
```

```
118c:  e8 2f ff ff ff        call   10c0 <strcmp@plt>
1191:  85 c0                 test   %eax,%eax
1193:  74 30                 je     11c5
1195:  48 8d 3d 87 0e 00 00  lea    0xe87(%rip),%rdi        # 2023
...
11c5:  48 8d 3d 50 0e 00 00  lea    0xe50(%rip),%rdi        # 201c
11cc:  e8 cf fe ff ff        call   10a0 <puts@plt>
```

`0x2023` 是 `"Good!"`、`0x201c` 是 `"Wrong!"`。`strcmp` 返回 0（相等）时 `je` 成立，跳到 `0x11c5` 打印 `Wrong!`，这就是写反的那一条。

### Step 4　patch：把 je 改成 jne

位置：虚拟地址 `0x1193`，机器码 `74 30`（`je rel8`）。ELF 里 `.text` 的虚拟地址与文件偏移都是 `0x1100` 起，所以文件偏移同样是 `0x1193`：

```bash
readelf -S challenge | grep -A1 ' .text'
```

```bash
python3 - <<'PY'
d = bytearray(open("challenge", "rb").read())
assert d[0x1193] == 0x74          # je
d[0x1193] = 0x75                  # jne
open("patched", "wb").write(d)
PY
chmod +x patched
echo 'flag{flip_the_branch_and_win}' | ./patched
```

```
input your flag: Good!
```

### Step 5　提交

```
flag{flip_the_branch_and_win}
```

## 三、原理

### 1. 分支就是一次条件跳转

`if (a == b) ... else ...` 编译成 `test/cmp` + 一条 `jcc`。`je`（`0f 84` 或短跳 `74`）与 `jne`（`0f 85` / `75`）只差一位，语义正好相反。改这一位等于把 `if` 与 `else` 对调。

### 2. patch 之后为什么还要 flag

patch 完程序对任何输入都打印 `Good!`，但题目要提交的是字符串，不是提示语。真正的答案是 `ENC ^ 0x5a`；patch 只用于验证"分支改对了"。实战里这两步经常成对出现：先 patch 让程序走通，再从常量还原输入。

### 3. 常量为什么在文件里

`ENC` 是 `static const` 数组，运行时要用它做比较，除非自解密，它的字节必须原样留在只读数据节。本题在文件偏移 `0x3010`（`.rodata`），29 字节。

### 4. ELF 下的偏移换算

PIE 可执行文件里，代码段 `.text` 的 `p_vaddr` 与 `p_offset` 通常相等（本题都是 `0x1100`），所以"虚拟地址 = 文件偏移"，patch 时不需要再做基址换算。换成非 PIE 或段布局不同时，用 `readelf -S` 把两列对上即可。运行时基址由内核随机化，但**文件偏移不变**，所以 patch 位置是稳定的。

## 四、防御建议

1. **本地校验天然可 patch。** 客户端里的 `if (input == secret)` 无论写多绕，最终都是一条条件跳转；把判断搬到服务端才是结构性解法。
2. **别把"分支反转"当混淆。** 它挡不住 5 分钟，还让程序行为变得可疑（正确输入报错），等于给逆向者发提示。
3. **需要完整性就用签名。** 用代码签名 + 服务端验签；反调试/反篡改只提高成本，不能替代服务端校验。
4. **检测侧**：`je`/`jne` 单 bit 差异，以及"比较结果与提示语方向相反"的逻辑，是自动化 patch 检测最容易发现的模式。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,504 B | `66c7a06aaa2c621b5558c57e7fa690d9fe78c91db57acd2cd3a7654e0198493c` |

- `ENC`（29 B，`flag ^ 0x5a`）位于文件偏移 `0x3010`；写反的 `je` 位于文件偏移 `0x1193`（VA 同为 `0x1193`）。
- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`；附件中无明文 flag。

---


