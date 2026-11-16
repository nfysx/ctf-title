# 异或比较

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 异或比较 |
| 考点 | 逐字节异或（滚动密钥）；已知明文反推；常量数组定位 |
| 难度 | 易（2） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,504 B） |
| Flag | `flag{xor_with_a_rolling_key}` |

## 题面（直接复制到平台「简介」）

附件是一个 Linux 控制台程序：把输入逐字节做一次简单运算，再和文件里的一段字节数组逐字节比较，全对才打印 `Good!`。你手上只有 `challenge`（ELF，x86-64），没有源码。目标是找出能通过比较的那串输入，提交格式 `flag{...}`。

**提示**：把「输入经过的那步运算」和「比较用的那段常量」放在一起看：先想清楚这一步运算能不能反向做，以及运算用的密钥被放在了哪里。

## 二、解题步骤

### Step 1　跑一下

```bash
cd files
chmod +x challenge
echo 'aaa' | ./challenge
```

```
input your flag: Wrong!
```

### Step 2　找密钥

密钥是 6 字节可打印常量，`strings` 直接命中：

```bash
strings -n 6 challenge | grep 'K3y'
```

```
K3y!26
```

它在文件偏移 `0x2029`（`.rodata`）。

### Step 3　还原密文

密文是 28 字节 `unsigned char` 数组，位于 `0x3010`。不知道确切偏移也没关系：把每个偏移都按算法异或回去，用 `flag{...}` 的形状做判别即可。

```python
d = open("challenge", "rb").read()
KEY = b"K3y!26"
for off in range(len(d) - 28):
    cand = bytes(d[off + i] ^ KEY[i % 6] ^ i for i in range(28))
    if cand.startswith(b"flag{") and cand.endswith(b"}"):
        print(hex(off), cand.decode())
```

```
0x3010 flag{xor_with_a_rolling_key}
```

### Step 4　提交验证

```bash
echo 'flag{xor_with_a_rolling_key}' | ./challenge
```

```
input your flag: Good!
```

## 三、原理

### 1. 异或自反

`a ^ k ^ k = a`，所以"加密"与"解密"是同一段代码。本题多异或了一个下标 `i`，下标本身是公开信息，不破坏可逆性：

```
out[i] = in[i] ^ KEY[i % 6] ^ (i & 0xff)
```

### 2. 反汇编对照（实测）

```bash
objdump -d challenge | sed -n '/<main>:/,/ret/p' | grep -E 'xor|movzbl|cmp' | head -12
```

循环里能看到"取密钥字节 → 异或下标 → 异或输入字节 → 与密文比较"这条链，和上面的公式一一对应。

### 3. 已知明文攻击

即使密钥不在文件里，flag 的前缀固定是 `flag{`：
`k[i] = c[i] ^ p[i]` 一次就还原出密钥前 5 字节；密钥只有 6 字节循环，一轮推完。所有"短密钥异或"都倒在这一步。

### 4. 为什么按偏移穷举能定位密文

判别式是"解出来必须形如 `flag{...}`"。28 字节同时满足首 5 字节与前缀一致、末字节为 `}` 的概率可以忽略，所以扫一遍文件就够，不需要先逆出结构。

## 四、防御建议

1. **异或不是加密。** 无扩散、无密钥调度，短密钥 + 已知明文格式秒破。要保密用 AES-GCM / ChaCha20-Poly1305。
2. **密钥不要进二进制。** 本题密钥是 6 个可打印字节，`strings` 一次命中。密钥应放服务端、平台密钥库，或用 KDF + 设备绑定派生。
3. **离线校验用 MAC。** 需要离线判断时用 HMAC-SHA256 + 常数时间比较，别自己拼校验式。
4. **检测侧**：二进制里出现"逐字节 xor 两次 + memcmp"且比较目标指向只读数据节，是硬编码校验的典型指纹。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,504 B | `dc6a31f2bd46486b2cf6a497d48764c0d9ce30d3aedc02f44d090b78dfb081c6` |

- 密文 28 字节位于文件偏移 `0x3010`；密钥 `K3y!26` 位于 `0x2029`。
- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`；附件中无明文 flag。

---


