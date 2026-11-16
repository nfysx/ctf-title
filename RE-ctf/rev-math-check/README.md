# 倒着走完变换

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 倒着走完变换 |
| 考点 | 多轮可逆变换（xor / 置换 / 线性 / 循环移位）；逆序求逆；置换表定位 |
| 难度 | 中（8） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,592 B） |
| Flag | `flag{follow_the_transform_backwards}` |

## 题面（直接复制到平台「简介」）

输入要连续经过几次变换才和文件里的目标数组比较，其中既有逐字节运算，也有依赖位置的重排和线性运算。附件 `challenge` 是 Linux x86-64 的 ELF，flag 长度 36。目标是倒推出能通过校验的那串 flag，提交格式 `flag{...}`。

**提示**：这几步都能反向做，参数（常量、置换表、乘数与加数、移位量）都能从数据区读出来；注意必须按相反的顺序逐步还原。

## 二、解题步骤

### Step 1　跑一下

```bash
cd files
chmod +x challenge
echo 'AAAABBBBCCCCDDDDEEEEFFFFGGGGHHHHIIII' | ./challenge
```

```
input your flag: Wrong!
```

### Step 2　读四段变换

`objdump -d` 里三段都能直接对上：

```
imul   $0x6b,%eax,%eax          ; 乘 0x6b
lea    0x11(%rdx,%rax,1),%eax   ; + i*7 + 0x11
rol    $0x3,%al                 ; 循环左移 3
```

再配合数据区里那张 36 字节置换表，整条链可还原成：

```
1) a[i] = in[i] ^ 0xa7
2) b[PERM[i]] = a[i]
3) c[i] = (b[i] * 0x6b + i * 7 + 0x11) & 0xff
4) T[i] = rol8(c[i], 3)
```

### Step 3　定位常量

`PERM` 是 0..35 的一个排列，这个性质本身就是强特征：扫文件，找 36 字节且排序后正好是 `0..35` 的窗口。

```python
d = open("challenge", "rb").read()
for off in range(len(d) - 36):
    if sorted(d[off:off + 36]) == list(range(36)):
        print("PERM @", hex(off))
```

```
PERM @ 0x3060
```

目标数组 `T` 是另一段 36 字节，位于 `0x3020`。

### Step 4　倒着做一遍

```python
LEN, MUL, ADD, ROL = 36, 0x6B, 0x11, 3

def ror8(v, s):
    return ((v >> s) | (v << (8 - s))) & 0xFF

perm = list(d[0x3060:0x3060 + LEN])
T = d[0x3020:0x3020 + LEN]

inv = pow(MUL, -1, 256)                       # 0x6b 在模 256 下的乘法逆元
c = [ror8(t, ROL) for t in T]                 # 反第 4 步
b = [((c[i] - i * 7 - ADD) * inv) & 0xFF for i in range(LEN)]   # 反第 3 步
a = [b[perm[i]] for i in range(LEN)]          # 反第 2 步
print(bytes(v ^ 0xA7 for v in a).decode())    # 反第 1 步
```

```
flag{follow_the_transform_backwards}
```

### Step 5　提交验证

```bash
echo 'flag{follow_the_transform_backwards}' | ./challenge
```

```
input your flag: Good!
```

## 三、原理

### 1. 每一步都是双射

| 步骤 | 逆运算 | 可逆条件 |
| --- | --- | --- |
| `a = x ^ 0xa7` | `x = a ^ 0xa7` | 异或自反 |
| `b[PERM[i]] = a[i]` | `a[i] = b[PERM[i]]` | `PERM` 是排列 |
| `c = b*0x6b + 7i + 0x11` | `b = (c - 7i - 0x11) * 0x6b⁻¹` | 0x6b 为奇数，模 256 有逆 |
| `T = rol8(c, 3)` | `c = ror8(T, 3)` | 移位可逆 |

四步都不可交换，所以必须**严格逆序**：先反 rol，再反线性，再反置换，最后反异或。

### 2. 乘法逆元怎么来

模 2⁸ 下只有奇数可逆（偶数与 256 不互素）。`0x6b = 107` 是奇数，`pow(0x6b, -1, 256) = 0x43`；
验证：`0x6b * 0x43 = 0x1c31`，`0x1c31 % 256 == 1`。脚本里一行 `pow(MUL, -1, 256)` 即可，不需要手推。

### 3. 置换表为什么能被扫出来

`PERM` 必须是 `0..35` 的一个排列才能保证"打乱顺序"不丢信息。这个约束把候选窗口压缩到极少数：
随机 36 字节恰好是排列的概率约 `36!/36³⁶ ≈ 10⁻¹⁵`，所以扫一遍文件就能唯一锁定。

### 4. 为什么这类题必须写脚本

四步嵌套，手算等于解 36 个独立方程；而脚本里每步只有一行。识别"变换链"之后，
工作量集中在确认每一步的参数（异或值、置换表、乘数、加数、移位量），这些参数都在反汇编里紧邻着常量表。

## 四、防御建议

1. **可逆变换不是加密。** 只要每一步都是双射且参数在客户端，整条链就能被逆序剥掉。校验应放服务端。
2. **需要离线校验就用 MAC。** HMAC-SHA256 的输出不能"倒推"，与本题的构造有本质区别。
3. **常量别裸放**：置换表、乘数、目标串集中出现在只读数据节，是最容易被脚本扫描的形态。
4. **检测侧**：出现"查表写回 + 常数乘法 + 循环移位"的连续块，且伴随 36/64 字节排列常量，基本可判定为自研可逆校验。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,592 B | `c3b0add83105a60839674d2e9de93c522d49c15940273fe87c0e421bd74c8067` |

- 置换表 `PERM[36]` 位于文件偏移 `0x3060`；目标数组 `T[36]` 位于 `0x3020`。
- 参数：`xor 0xa7`、`MUL 0x6b`、`ADD 0x11`、`ROL 3`。
- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`；附件中无明文 flag。

---


