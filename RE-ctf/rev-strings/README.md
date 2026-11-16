# 明文字符串

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 明文字符串 |
| 考点 | ELF 结构；`.rodata` 里的字符串常量；`strings` 与十六进制定位 |
| 难度 | 易（1） |
| 附件 | `challenge`（Linux x86-64 动态链接 ELF，14,472 B） |
| Flag | `flag{strings_is_enough}` |

## 题面（直接复制到平台「简介」）

这是一个很小的 Linux 控制台程序：启动后读一行输入，和内部保存的一串字符比较，相同打印 `Good!`，不同打印 `Wrong!`。附件只有 `challenge` 一个文件（ELF，x86-64），`chmod +x` 之后直接运行。要拿到的是它认可的那串 flag，提交格式 `flag{...}`。

**提示**：程序用来比较的字符串必须原样留在可执行文件里才能参与运行；先让 `strings` 把文件里的可打印字节扫出来，再判断哪一段像答案。

## 二、解题步骤

### Step 1　跑一下，确认输入输出

```bash
cd files
chmod +x challenge
echo 'flag{test}' | ./challenge
```

```
input your flag: Wrong!
```

### Step 2　搜可打印字符串

```bash
strings -n 6 challenge | grep 'flag{'
```

```
flag{strings_is_enough}
```

没有 `strings` 时，用 Python 直接搜：

```python
import re
d = open("challenge", "rb").read()
m = re.search(rb"flag\{[a-z0-9_]+\}", d)
print(hex(m.start()), m.group().decode())
```

```
0x2030 flag{strings_is_enough}
```

`0x2030` 落在 `.rodata` 里（这一节在文件里从 `0x2000` 开始），是编译进二进制的只读字符串常量。

### Step 3　提交验证

```bash
echo 'flag{strings_is_enough}' | ./challenge
```

```
input your flag: Good!
```

退出码 0。

## 三、原理

### 1. 字符串常量存在哪里

C 里的 `static const char TARGET[] = "..."` 在编译后落在 ELF 的只读数据节 `.rodata`。本题因为长度短、只读，被放在 `0x2030`；有些编译器会放进 `.data`。两种都能被 `strings` 扫到，`strings` 不解析 ELF，只找连续的可打印字节（阈值由 `-n` 指定）。

### 2. 为什么 `strings` 一定命中

`strcmp(buf, TARGET)` 需要 `TARGET` 的地址，编译器必须把这段字节原样留在文件里，运行时才能比较。除非作者显式做了加密或拆字（例如把字符串拆成多个字面量拼接、或在启动时才解密），否则明文必然在文件里。

### 3. `-s` 去符号不影响

附件用 `gcc -O2 -s` 编译，`.symtab` 被删掉了，函数名、全局变量名都看不到；但字符串常量属于**数据**，在 `.rodata` 里，`-s` 不会删它。

## 四、防御建议

1. **不要把机密编译进客户端。** API key、内部地址、授权串、答案字符串一旦进入二进制，就等于公开，`strings` 和熵扫描是流水线作业，不需要逆向功底。
2. **发布前做一次常量体检。** `strings -n 4` 加关键字列表（`key`、`token`、`password`、`flag`、内网域名）跑一遍；再对每个节算熵，熵突然接近 8 的节要人工看。
3. **确实需要客户端的秘密，就换协议。** 敏感判断放服务端，客户端只发请求；离线场景用平台密钥库（DPAPI / Keychain / TPM）封装，别写死在代码里。
4. **检测侧**：CTF 之外的恶意样本也常用明文 C2 地址与硬编码密钥，`strings` + YARA 依然是第一道筛子。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,472 B | `3e2a8712bf163281a1cac5179620f99c8a0ea373ab650fa16aa0218ca9be2bdf` |

- ELF 64-bit LSB pie executable（x86-64，动态链接），`gcc -O2 -s` 编译；入口 `0x11c0`，`objdump -f` 识别为 `elf64-x86-64`。
- flag 明文位于文件偏移 `0x2030`（`.rodata`）。本题是唯一允许明文出现的一题，其余各题的附件里都搜不到明文 flag。

---


