# 换过表的 Base64

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 换过表的 Base64 |
| 考点 | 自定义 Base64 字母表识别；编码表置换 → 下标反查；ELF 静态定位常量（`.rodata` + 交叉引用） |
| 难度 | 2 |
| 附件 | `challenge`（Linux x86-64 ELF，14,560 B） |
| Flag | `flag{custom_b64_alphabet}` |

## 题面（直接复制到平台「简介」）

一个 Linux 命令行程序：读一行输入，编码后和程序里保存的一段密文比较，一致才打印 `Good!`。附件 `challenge`（14,560 B，x86-64 ELF），没有源码。那段密文看起来很像 Base64，直接解码却是乱码；要提交的是它真正认可的那串 flag，格式 `flag{...}`。

**提示**：标准 Base64 解不出东西时，先怀疑编码用的字符表和标准表不一样；程序里同时保存着密文和那张表，注意它们各自的长度特征。

## 二、解题步骤

### Step 1　先跑一遍，确认行为

```bash
chmod +x challenge
echo 'flag{test}' | ./challenge
```

```text
input your flag: Wrong!
```

退出码 1。输对了则打印 `Good!` 并返回 0。

### Step 2　把两段常量捞出来

答案是「一段 36 字符、以 `==` 结尾的串」+「一段正好 64 字符的串」。用 `strings` 按字符集特征扫：

```bash
strings -a -t x challenge | grep -E '[A-Za-z0-9+/]{16,}'
```

```text
   2040 rXbwrPNnG+65gvTyMnM5+vhCRjwwMXY5yl==
   2080 2KuEphj84USZF67iloxzfYd+MrDgRG9yLwBnHAXcJq3eCN/s1bOQ5TvPa0tVkWmI
```

一眼能分：**36 字符、带 `==` 的那条是密文**，**正好 64 字符、`+` 与 `/` 都在的那条是字母表**。

### Step 3　确定它们在文件里的位置

`strings -t x` 给的是文件偏移。本题是 `-no-pie` 编译的，虚拟地址 = 文件偏移 + 固定基址，看节表就知道怎么换算：

```bash
objdump -h challenge | grep -E ' .text| .rodata'
```

```text
  16 .rodata       000000c1  0000000000402000  0000000000402000  00002000
```

`.rodata` 的文件偏移是 `0x2000`、虚拟地址是 `0x402000`，所以：

- 密文：文件偏移 `0x2040` → 虚拟地址 **`0x402040`**
- 字母表：文件偏移 `0x2080` → 虚拟地址 **`0x402080`**

### Step 4　确认程序真的在比较这一个

只看字符串还不能下结论，要找交叉引用：

```bash
objdump -d challenge | grep -B3 -A6 '402040'
```

```text
  40125d: lea    0xddc(%rip),%rsi        # 402040
  401264: mov    %r11,%rdi
  40126f: call   4010d0 <strcmp@plt>
  401274: mov    %eax,%r12d
  401277: test   %eax,%eax
  401279: je     401367
```

`strcmp` 里 `rsi` 就是目标串（第二参数），`rdi` 是你输入编码后的缓冲区。相等（`eax==0`）就跳到 `0x401367`，那里 `puts("Good!")`。

字母表在 `0x4011c3` 被 `lea ... %rdi` 引用（编码函数拿它当查表基址）。

### Step 5　反解

知道了机制，剩下的就是逐字符反查下标：**密文里的每个字符，先在自定义表里找到它的下标，再用这个下标去标准表取字符**，得到的才是合法 Base64。

```bash
python3 exp/solve.py
```

```text
字母表: 2KuEphj84USZF67iloxzfYd+MrDgRG9yLwBnHAXcJq3eCN/s1bOQ5TvPa0tVkWmI
密文  : rXbwrPNnG+65gvTyMnM5+vhCRjwwMXY5yl==
标准表: ZmxhZ3tjdXN0b21fYjY0X2FscGhhYmV0fQ==
flag  : flag{custom_b64_alphabet}
```

也可以手搓一行：

```python3
STD = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
TBL = "2KuEphj84USZF67iloxzfYd+MrDgRG9yLwBnHAXcJq3eCN/s1bOQ5TvPa0tVkWmI"
CT  = "rXbwrPNnG+65gvTyMnM5+vhCRjwwMXY5yl=="
import base64
print(base64.b64decode("".join("=" if c=="=" else STD[TBL.index(c)] for c in CT)).decode())
```

### Step 6　验证

```bash
echo 'flag{custom_b64_alphabet}' | ./challenge
# input your flag: Good!
echo $?      # 0
```

## 三、原理

标准 Base64 做两件事：把字节流按 6 bit 切成 64 种取值，再用一张固定的 64 字符表把这些值映射成可打印字符。

**换表只改了第二件事。** 分组方式、6 bit 切法、`=` 填充规则全部不变。所以：

- 用标准解码器去解换过表的密文，**不会报错**，只会吐出垃圾字节（本题实测得到 25 个非打印/错误字符）；
- 反过来，只要知道表，把每个字符的**下标**换回标准表，得到的就是一条完全合法的标准 Base64。

这就是为什么这题不需要「实现一个解码器」，只需要一次映射。表的置换不是加密，它连密钥都没有。

## 四、防御建议

自定义编码表只能拖慢逆向，**挡不住任何人**：表就在二进制里，`strings` 一次就能拿到。

- 不要把「换表 / 自研编码」当作保护手段。它和异或、压缩、加壳一样，只提高一点阅读成本。
- 真要让客户端无法伪造，靠的是**服务端校验**与**签名**，而不是把算法藏起来。
- 反过来说：本题这种「客户端算出来的结论」在真实场景里一定可以被 patch。上一版 PE 里那个「分支写反」的 bug 就是活例子。

## 五、动态 Flag

不适用。本题为附件题，flag 静态判定，比对逻辑在 `checker/main.rx`：

```rhai
const FLAG = "flag{custom_b64_alphabet}";
pub fn check(bucket, user, team, submission) {
    if submission.content == FLAG { return Ok((true, "Correct!", None)); }
    Ok((false, "Incorrect!", None))
}
pub async fn environ(bucket, user, team) { Ok(#{}) }
```

## 六、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 14,560 B | `4722a4bae93570ea8f695bc18f3da3c9c6718a3c51a09058c6c220d8351dbf0c` |

- ELF64 x86-64、`-no-pie`、动态链接、已 strip（`objdump -t` 无符号）；
- 自定义字母表位于文件偏移 `0x2080`（`.rodata`，VA `0x402080`），64 字符，`=` 填充逻辑与标准 Base64 一致；
- 目标密文位于文件偏移 `0x2040`（`.rodata`，VA `0x402040`），36 字符 + `\0`；
- 运行环境：任意 Linux x86-64，`chmod +x` 后直接跑；也可用 `gdb` 在 `0x40126f`（`call strcmp`）下断，看 `rsi`。

---


