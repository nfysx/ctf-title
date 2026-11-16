# RC4 比对

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | RC4 比对 |
| 考点 | RC4（KSA/PRGA）实现识别；硬编码密钥提取；密文定位 |
| 难度 | 中（6） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,512 B） |
| Flag | `flag{rc4_with_a_known_key}` |

## 题面（直接复制到平台「简介」）

程序对输入做一层流加密，再和一段 26 字节的密文逐字节比较，长度不对直接判错。附件 `challenge` 是 Linux x86-64 的 ELF，密钥以字节数组的形式写在程序里。目标是还原出那段密文对应的明文 flag，提交格式 `flag{...}`。

**提示**：先认出程序里那段「建表 + 逐字节异或」的循环用的是什么加密；对称流密码只要拿到密钥，加密与解密就是同一段代码。

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

### Step 2　认出 RC4

```bash
objdump -d challenge | grep -c 'movzbl'
```

256 字节 S 盒建表 + 逐字节异或的结构，配合反汇编里连续两次交换（`mov/mov/mov` 三连）与 `xor` 输出，就是 RC4 的 KSA + PRGA。

### Step 3　取出密钥与密文

两块常量都在只读数据区：密钥 8 字节位于文件偏移 `0x3030`，密文 26 字节位于 `0x3010`。

密钥没有可读形态（是 `0x52 0xc4 0x1f ...` 这种字节数组），所以用"能解出 `flag{...}`"当判别式自动找：

```python
def rc4(key, data):
    s = list(range(256))
    j = 0
    for i in range(256):
        j = (j + s[i] + key[i % len(key)]) & 0xFF
        s[i], s[j] = s[j], s[i]
    out, i, j = bytearray(), 0, 0
    for b in data:
        i = (i + 1) & 0xFF
        j = (j + s[i]) & 0xFF
        s[i], s[j] = s[j], s[i]
        out.append(b ^ s[(s[i] + s[j]) & 0xFF])
    return bytes(out)

def ks0(key):                      # 只算第一个密钥流字节，用来做廉价过滤
    s = list(range(256))
    j = 0
    for i in range(256):
        j = (j + s[i] + key[i % len(key)]) & 0xFF
        s[i], s[j] = s[j], s[i]
    j = s[1]
    s[1], s[j] = s[j], s[1]
    return s[(s[1] + s[j]) & 0xFF]

d = open("challenge", "rb").read()
table = {}
for k in range(len(d) - 8):
    table.setdefault(ks0(d[k:k + 8]), []).append(k)   # 明文首字节已知为 'f'

for c in range(len(d) - 26):
    for k in table.get(d[c] ^ ord("f"), []):
        pt = rc4(d[k:k + 8], d[c:c + 26])
        if pt.startswith(b"flag{") and pt.endswith(b"}"):
            print(hex(k), hex(c), pt.decode()); raise SystemExit
```

结果：

```
0x3030 0x3010 flag{rc4_with_a_known_key}
```

密钥实际是 `52 c4 1f 9b 6e 33 a8 07`。

### Step 4　提交验证

```bash
echo 'flag{rc4_with_a_known_key}' | ./challenge
```

```
input your flag: Good!
```

## 三、原理

### 1. RC4 是对称流密码

`ciphertext = plaintext ^ keystream`，解密与加密同一段代码。keystream 由密钥经 KSA 建表、PRGA 逐字节产生。只要密钥在手里，密文就能还原，本题密钥就写在二进制里。

### 2. 为什么"密钥不是字符串"也挡不住

密钥是 8 字节随机数组，`strings` 看不到，但它仍然是 8 字节常量：调试器里看一眼调用点，或者像上面那样用"解出来必须是 `flag{...}`"反向搜索，都能拿到。混淆密钥形态只增加几行脚本。

### 3. RC4 本身的问题

- 密钥流前若干字节存在统计偏差（WEP 攻击就利用这点），RC4 已被 TLS 弃用。
- 无认证：改动密文不会被发现。
- 单密钥复用两条明文异或即可消去密钥流。

正确做法是 AES-GCM / ChaCha20-Poly1305 这类带认证的 AEAD。

### 4. 定位密文的小技巧

密文长度 = 明文长度（流密码不膨胀）。本题明文 26 字节，密文也是 26 字节，所以看到"26 字节常量 + memcmp 长度 26"就基本锁定比较点了。

## 四、防御建议

1. **硬编码密钥 = 没有密钥。** 二进制里的任何字节都可以被读出来；密钥应在服务端或平台密钥库。
2. **不要用 RC4。** 已过时且有已知偏差；用 AES-GCM / ChaCha20-Poly1305，配合随机 nonce。
3. **校验要有认证性。** 本地比较应改为 HMAC/签名验证，避免"改密文就改结果"。
4. **检测侧**：256 项 S 盒初始化 + 交换 + 异或输出的三段循环是 RC4 的稳定指纹，YARA 与 IDA 的 crypto 签名都能识别。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,512 B | `4afd4db30654f5bf5da51310f39758b1f77c087356a5b624a43599869a3da00d` |

- 密钥 8 字节位于文件偏移 `0x3030`（`52 c4 1f 9b 6e 33 a8 07`）；密文 26 字节位于 `0x3010`。
- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s`；附件中无明文 flag。

---


