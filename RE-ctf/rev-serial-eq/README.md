# 四元方程组

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 四元方程组 |
| 考点 | 模 256 线性方程组；矩阵求逆（伴随矩阵 / 行列式）；常量矩阵定位 |
| 难度 | 易（4） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,536 B） |
| Flag | `flag{the_serial_is_a_system_of_equations}` |

## 题面（直接复制到平台「简介」）

程序把输入按每 4 个字符一组做一次变换，再和文件里的一段目标数组比较，输入长度必须是 41。附件 `challenge` 是 Linux x86-64 的 ELF，读一行输入即可。目标是求出能通过校验的那串 flag，提交格式 `flag{...}`。

**提示**：每一组 4 个字符和目标数组之间存在一组固定的线性方程，系数矩阵就写在程序的只读数据区里；想想这组方程怎样反过来解。

## 二、解题步骤

### Step 1　跑一下

```bash
cd files
chmod +x challenge
echo 'aaaa' | ./challenge
```

```
input your flag: Wrong!
```

### Step 2　定位矩阵与目标数组

矩阵是随机的 16 个字节，没有可读特征；但"第一组明文是 `flag`"可以当判别式：
任取 16 字节当矩阵 M，用它把 `"flag"` 变换一次得到 4 字节 y0，再到文件里搜 y0，命中处就是目标数组 T 的起点。

```python
data = open("challenge", "rb").read()
PREFIX = b"flag"
for off in range(len(data) - 16):
    M = [list(data[off + 4 * r: off + 4 * r + 4]) for r in range(4)]
    y0 = bytes(sum(M[r][c] * PREFIX[c] for c in range(4)) & 0xFF for r in range(4))
    pos = data.find(y0)
    if pos >= 0:
        print("M @", hex(off), " T @", hex(pos))
```

```
M @ 0x2030  T @ 0x3020
```

### Step 3　解方程组

每个块满足 `y = M·x (mod 256)`，求 `x = M⁻¹·y`。手算是 `x = adj(M)·y / det(M)`，
模 256 下"除法"是乘以行列式的模逆（行列式必须是奇数才有逆）。

```python
BLOCKS = 11

def det3(a):
    return (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1])
            - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
            + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))

def det4(m):
    return sum((-1) ** j * m[0][j] *
               det3([[m[r][c] for c in range(4) if c != j] for r in range(1, 4)])
               for j in range(4))

def solve(M, y):
    inv = pow(det4(M) % 256, -1, 256)
    cof = [[(-1) ** (i + j) *
            det3([[M[r][c] for c in range(4) if c != j] for r in range(4) if r != i])
            for j in range(4)] for i in range(4)]
    adj = [[cof[j][i] % 256 for j in range(4)] for i in range(4)]
    return [sum(adj[i][j] * y[j] for j in range(4)) * inv % 256 for i in range(4)]

T = data[0x3020:0x3020 + 4 * BLOCKS]
out = []
for b in range(BLOCKS):
    out += solve(M, list(T[4 * b:4 * b + 4]))
print(bytes(out).rstrip(b"\x00").decode())
```

```
flag{the_serial_is_a_system_of_equations}
```

41 字节明文补 3 个 0 到 44 字节，正好 11 个块。

### Step 4　提交验证

```bash
echo 'flag{the_serial_is_a_system_of_equations}' | ./challenge
```

```
input your flag: Good!
```

## 三、原理

### 1. 为什么可逆

模 256 下矩阵可逆 ⟺ 行列式与 256 互素 ⟺ 行列式是奇数。本题矩阵行列式为奇数，所以每个块都能唯一解出。

### 2. 为什么单独成组

变换只作用在一组 4 字节内部，块与块之间没有耦合。因此这是 11 个独立的 4 元一次方程组，不是 44 元的大方程组，解起来只是重复 11 次矩阵乘法。

### 3. 为什么"搜 y0"能定位

矩阵和目标是两块裸常量，中间没有分隔标记。用 `M·"flag"` 去文件里反查，等价于要求"这块矩阵下，第一组明文必须是 flag"；只有真正的常量区满足，误报可以忽略。

### 4. 反汇编画像

```
movzbl (%rsi,%rax,1),%edx     ; 取一个输入字节
imul   %ecx,%edx              ; 乘矩阵元素
add    %edx,%r8d              ; 累加 4 项
and    $0xff,%r8d             ; 模 256
cmp    %dl,(%rdi,...)         ; 与目标比较
```

嵌套 4 层循环 + `and 0xff` + 常数比较，就是模 256 线性校验的典型形状。

## 四、防御建议

1. **线性校验可代数还原。** 只要变换是线性的、常量在客户端，就能直接反解。校验逻辑应放服务端。
2. **要构造不可逆的校验，用密码学原语。** HMAC-SHA256、Argon2 等；不要自研"可逆变换 + 常量比较"。
3. **常量别裸放。** 矩阵和目标数组放在只读数据节，是最容易被脚本扫出来的形态；即便加密，密钥在同一个文件里也等于没加密。
4. **检测侧**：4 层嵌套 + `and 0xff` + `memcmp` 的循环结构，配合数据里的 4×4 常量，可以直接识别。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,536 B | `303f1cf88aa5c06a5d6df7eeead4fa54bcddb8ae97d77a7e25e8fb2442347c0a` |

- 矩阵 `M[4][4]` 位于文件偏移 `0x2030`（16 B）；目标数组 `T[44]` 位于 `0x3020`。
- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`；附件中无明文 flag。

---


