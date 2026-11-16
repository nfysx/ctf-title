# 加了壳的二进制

## 一、题目与考点

| 项 | 内容 |
| --- | --- |
| 题目 | 加了壳的二进制 |
| 考点 | ELF 加壳（UPX）；自解压 stub；脱壳与格式还原 |
| 难度 | 易（5） |
| 附件 | `challenge`（UPX 压缩的 Linux x86-64 ELF，367,984 B） |
| Flag | `flag{upx_packed_not_protected}` |

## 题面（直接复制到平台「简介」）

附件 `challenge` 能正常运行，但用 `strings` 扫它只会得到几千行乱码，一个像样的字符串都没有。它是一个 Linux x86-64 程序，读一行输入并判断对错。要拿到的是它认可的 flag，格式 `flag{...}`。

**提示**：先确认这个文件是不是「原始」形态，节名、节大小和文件熵都能说明问题；把它恢复成正常形态之后，再用平时的工具看。

## 二、解题步骤

### Step 1　看文件特征

```bash
cd files
chmod +x challenge
file challenge
readelf -S challenge | head -20
```

```
challenge: ELF 64-bit LSB executable, x86-64, version 1 (SYSV), statically linked, no section header
```

"no section header" 说明节表被壳抹掉了；文件开头就是 `UPX!` 标记：

```bash
grep -abo 'UPX!' challenge
```

```
236:UPX!
364867:UPX!
367939:UPX!
367948:UPX!
```

### Step 2　脱壳

```bash
upx -d challenge
ls -l challenge
```

```
                       File size         Ratio      Format      Name
   --------------------   ------   ------   -----------   -----------
    950736 <-    367984   38.71%   linux/amd64   challenge

Unpacked 1 file.
-rwxr-xr-x 1 root root 950736 ... challenge
```

（UPX 打印的 950736 就是还原出来的**文件**大小，和 `ls -l` 一致。）

版本要求：本附件用 **UPX 3.96** 打包，**3.96 与 4.x 都能解开**，装个系统自带的 `upx-ucl` 即可：

```bash
sudo apt install upx-ucl      # Debian/Ubuntu 自带的就是 3.96
upx -d challenge
```

### Step 3　脱壳后直接读

```bash
strings -n 6 challenge | grep 'flag{'
```

```
flag{upx_packed_not_protected}
```

脱壳后的文件 950,736 B（静态链接、已 strip），能正常执行：

```bash
chmod +x challenge
echo 'flag{upx_packed_not_protected}' | ./challenge
```

```
input your flag: Good!
```

### Step 4　提交

```
flag{upx_packed_not_protected}
```

## 三、原理

### 1. UPX 做了什么

UPX 把原程序的各段压成一份数据，塞进新加的壳段，把入口点改到自解压 stub；运行时 stub 先把原映像解压回内存，再跳回原入口。原始的程序头、段表、重定位信息都记录在 UPX 头里，所以 `upx -d` 能无损还原。

### 2. 为什么"壳 ≠ 保护"

- 文件在磁盘上是压缩的，但**进程内存里一定是解压后的**。任何时刻 dump 内存都能拿到明文。
- `UPX!` 头、入口 stub 的固定形态都是公开特征；`upx -d` 一条命令就能还原。
- 压缩不改变语义：字符串常量还原后照样在 `.rodata` 里。

### 3. 版本兼容（实测）

题目打包时特意选了**兼容面最大**的组合：

| 组合 | 结果 |
| --- | --- |
| 动态 PIE + UPX 4.2.2 | 包只有 5.8 KB，但 **UPX 3.96 解不开**（`need a newer version of UPX`），Ubuntu 22.04 / Debian 12 的选手会被卡住 |
| 动态 PIE + UPX 3.96 | 3.96 直接拒绝打包（`NotCompressibleException`） |
| **静态非 PIE + UPX 3.96**（本题采用） | 3.96 与 4.2.2 都能 `upx -d` 还原，任何发行版装个 `upx-ucl` 就能做 |

代价是包大了一点（950,736 → 367,984，38.71%）。对一道「易」题来说，让选手不必去 GitHub 下新版 UPX 更重要。

### 4. 手工脱壳的思路

不想用 `upx -d` 时：在调试器里让 stub 跑完，对可执行段下内存断点或直接 dump 整个映像，
再用工具重建动态链接信息。CTF 里没必要走这么远，但这是真实恶意样本分析的常规动作。

## 四、防御建议

1. **加壳不是保护。** 它只对抗"静态字符串扫描"，对动态分析和 `upx -d` 完全无效。
2. **不要靠壳隐藏秘密。** 机密应放服务端；客户端的授权判断终究会被 patch 或 dump。
3. **发布策略**：用 `-s` 去符号、去掉调试信息、开 ASLR/PIE，比加壳更有意义；需要防篡改就做数字签名 + 服务端验签。
4. **检测侧**：文件熵接近 8、`UPX!` 标记、节表缺失（"no section header"），都是"被 UPX 压过"的判据。

## 五、附件清单

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| `files/challenge` | 367,984 B | `6b125daa715e39ac54b3f25d803defc6ac343a02118703021a0227bccd72a48e` |

- 打包使用 UPX 3.96，格式 `linux/amd64`，压缩比 38.71%（950,736 → 367,984）；`UPX!` 标记位于文件偏移 `236`。
- 压缩状态下 `strings` 扫出约 5,400 行乱码、**零**条 flag 命中；`upx -d` 还原出的 950,736 B 原始文件里可直接读到 flag，且可正常执行。

---


