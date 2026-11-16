# RE-ctf · 逆向入门

> 15 道题：2 道工具课 + 10 道主干题（5 易 : 3 中 : 2 难）+ 3 道早期题。
> 知识点对齐 `Reverse入门指北 2026`：先会用工具，再谈对抗。
> **全部是附件题**，给二进制自己在本地逆，不起容器。

---

## 题目一览（15 题）

| 目录 | 题名 | 考点 | 难度 | 形态 |
|---|---|---|---|---|
| `rev-hello-toolchain/` ☆ | 工具链第一课 | `file` 看格式、`strings` 捞 KEY1、`objdump` 找 KEY2 | 1 | 附件 |
| `rev-hello-decompile/` ☆ | 反编译第一课 | `strings` 只看得到碎片，拼接顺序在伪代码里 | 1 | 附件 |
| `rev-strings/` | 明文字符串 | flag 明文躺在 `.rdata` | 易 | 附件 |
| `rev-xor-loop/` | 异或比较 | `c[i] = p[i] ^ KEY[i%6] ^ i` | 易 | 附件 |
| `rev-flip-branch/` | 写反的分支 | `strcmp` 结果被反用，`74 20` → `75 20` | 易 | 附件 |
| `rev-serial-eq/` | 四元方程组 | 每 4 字符一组做 `y = M·x mod 256` | 易 | 附件 |
| `rev-upx/` | 加了壳的二进制 | UPX 脱壳后 `strings` | 易 | 附件 |
| `rev-rc4/` | RC4 比对 | 密钥藏在程序里的字节数组 | 中 | 附件 |
| `rev-smc/` | 运行时才存在的代码 | `VirtualProtect` 就地解密后调用 | 中 | 附件 |
| `rev-math-check/` | 倒着走完变换 | xor → 置换 → 线性 → rol，四项依次求逆 | 中 | 附件 |
| `rev-anti-debug/` | 反调试下面的真路径 | `IsDebuggerPresent` + `CheckRemoteDebuggerPresent` 双检 | 难 | 附件 |
| `rev-tiny-vm/` | 字节码虚拟机 | 314 字节字节码 + 14 个操作码，要 dump 出来 trace | 难 | 附件 |
| `custom-b64-ctf/` | 换过表的 Base64 | 自定义 64 字符表，反查下标映射回标准表 | 2 | 附件 |
| `stl-map-path-ctf/` | 走地图 | `map<int, unordered_map>` 堆内存还原成图 + 最短路 | 5 | 附件 |
| `teach-demo/` | 逆向教学工具 | MoeCTF 的 `RE.Tutorial.exe` 与仿制工程（工作区，非单题） | — | 附件 |

> ☆ = 引入题。这两道是**工具课**：只教你怎么用工具看一个二进制，不考漏洞也不考算法。
> 后面 12 道才是「逆出算法」。

## 依赖

| 工具 | 用途 | 备注 |
|---|---|---|
| `strings` `objdump` `nm` `readelf` | 静态看字符串 / 反汇编 / 符号表 / 节表 | binutils 自带，Linux 装个 `binutils` 就有 |
| Ghidra 或 IDA | 反编译看伪代码 | `rev-hello-decompile` 与多道中难题要用 |
| `upx`（任意版本） | 脱壳 | `apt install upx-ucl` 即可；附件按 3.96 打包，3.96 与 4.x 都能解 |
| `gdb`（可选） | 动态调试 | `rev-anti-debug` 给了不改二进制也能过的路 |

> 二进制全是 **Linux x86-64 ELF**（`gcc` 在 `ubuntu:22.04` 里编的），Linux 上直接 `chmod +x` 就能跑，
> 也能用 `gdb` 直接调，不需要 wine。macOS 上跑不了，但静态分析（`strings` / `objdump` / Ghidra）不受影响。

## 跑起来

```bash
cd RE-ctf/rev-strings/files
chmod +x challenge && strings challenge | grep -i flag   # 第一课：strings 一把梭
objdump -f challenge                                     # 看它是什么

cd ../rev-hello-toolchain/files
strings hello_re | grep -E '^[a-z_]{4,}

# 每题都有解答脚本（不对外发布）
python ../exp/solve.py
```

## 平台上传要点

15 道全是附件题，**不占端口、不需要镜像**。

- 附件放在各自的 `files/` 下，原样下发
- `checker/main.rx` 是静态模板，`FLAG` 与二进制实际接受的串一致（每题都实测过）
- `exp/` 与 `src/` **不要下发**

## 这个方向的常见坑

| 现象 | 原因 |
|---|---|
| `strings` 什么都看不到 | flag 逐字节异或过，或被拆成多段常量（`rev-hello-toolchain` 就是） |
| 反编译出来找不到 `main` | 二进制带 `-s` 去了符号；从 `entry` 往下找（`rev-hello-decompile` 特意保留符号表就是为了能看到 `main`） |
| `upx -d` 报 `CantUnpackException` | 附件用 UPX 3.96 打的包，但本机 upx 太旧；`apt install upx-ucl` 后即可，3.96 与 4.x 都能解 |
| patch 后仍然 `Wrong!` | 有两处检查（`rev-anti-debug` 就是双检），只改一处没用 |
| 反调试题一调试就退出 | 不用调试器也能过：真校验只依赖两个常量表，静态逆出来即可 |

## 目录结构

```text
RE-ctf/
├── rev-hello-toolchain/  files/hello_re + src/main.c + checker/ + README.md
├── rev-strings/          files/challenge + src/ + checker/ + exp/solve.py + README.md
└── ...（其余同 rev-strings 的排布）
```

约定：`files/` 只放选手拿到的二进制；源码与 exp 留在 `src/` `exp/`，不随附件下发。
   # KEY1
objdump -d hello_re | grep movb             # KEY2（逐字节 movb 立即数）

# 每题都有解答脚本（不对外发布）
python ../exp/solve.py
```

## 平台上传要点

15 道全是附件题，**不占端口、不需要镜像**。

- 附件放在各自的 `files/` 下，原样下发
- `checker/main.rx` 是静态模板，`FLAG` 与二进制实际接受的串一致（每题都实测过）
- `exp/` 与 `src/` **不要下发**

## 这个方向的常见坑

| 现象 | 原因 |
|---|---|
| `strings` 什么都看不到 | flag 逐字节异或过，或被拆成多段常量（`rev-hello-toolchain` 就是） |
| 反编译出来找不到 `main` | 二进制带 `-s` 去了符号；从 `entry` → `__tmainCRTStartup` 往下找 |
| `upx -d` 报 `CantUnpackException` | 本机 upx 太旧或包不是它打的；附件按 3.96 打包，`apt install upx-ucl` 即可 |
| patch 后仍然 `Wrong!` | 有两处检查（`rev-anti-debug` 就是双检），只改一处没用 |
| 反调试题一调试就退出 | 不用调试器也能过：真校验只依赖两个常量表，静态逆出来即可 |

## 目录结构

```text
RE-ctf/
├── rev-hello-toolchain/  files/hello_re + src/main.c + checker/ + README.md
├── rev-strings/          files/challenge + src/ + checker/ + exp/solve.py + README.md
└── ...（其余同 rev-strings 的排布）
```

约定：`files/` 只放选手拿到的二进制；源码与 exp 留在 `src/` `exp/`，不随附件下发。
