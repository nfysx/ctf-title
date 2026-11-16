# 题解：LCG 参数与状态恢复（crypto-lcg-recover）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 难 |
| 考点 | 线性同余发生器；未知模数的恢复；差分消元；状态预测 |
| 附件 | `files/gen.py`、`files/output.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

`output.txt` 给 12 个连续状态和一段密文。递推式 `x[n+1] = (a·x[n] + c) mod m` 中，`m`、`a`、`c` 全部未知。

## 题面（直接复制到平台「简介」）

一个玩具随机数服务用线性同余发生器产出密钥流，模数、乘数、增量从未公开。附件给了 12 个连续状态，以及一段用后续状态最低字节异或出来的密文。把后面的输出预测出来，恢复 flag。

**提示**：先想办法把增量从式子里拿掉；模数是可以从这组数据里算出来的量，不是要猜的量。

## 三、解题步骤

第 1 步：**差分消掉 `c`**。令 `d[i] = x[i+1] - x[i]`，则 `d[i+1] ≡ a·d[i] (mod m)`。

第 2 步：**用二次关系逼出 `m`**。由 `d[i+1] ≡ a·d[i]` 得

```
d[i+1]·d[i-1] - d[i]^2 ≡ a·d[i]·d[i-1] - d[i]·a·d[i-1] ≡ 0  (mod m)
```

所以每个 `t[i] = d[i+1]·d[i-1] - d[i]^2` 都是 `m` 的倍数，取它们的 **gcd** 就还原出 `m`，不需要事先知道模数。

第 3 步：解出 `a` 和 `c`，然后顺推：

```python
diff = [states[i+1] - states[i] for i in range(len(states) - 1)]
m = 0
for i in range(1, len(diff) - 1):
    m = gcd(m, abs(diff[i+1] * diff[i-1] - diff[i] * diff[i]))

a = (diff[1] * pow(diff[0], -1, m)) % m
c = (states[1] - a * states[0]) % m
assert all((a * states[i] + c) % m == states[i+1] for i in range(len(states) - 1))

x = states[-1]
ks = b""
while len(ks) < len(ct):
    x = (a * x + c) % m
    ks += bytes([x & 0xFF])
print(bytes(p ^ q for p, q in zip(ct, ks)).decode())
```

```bash
cd crypto-lcg-recover
python exp/solve.py
```

```
m = 223102906991622036588066425153623486487
a = 47410186567162206455253335760507505337
c = 84169797112620466167566791627728286303
flag = flag{lcg_state_recovery}
```

解出的 `m, a, c` 必须让全部 11 条递推关系都成立，这个断言保证恢复正确，不是靠肉眼判断明文像不像。

## 四、原理

LCG 是**线性**发生器：状态序列完全由 `(m, a, c)` 决定，`m` 位状态最多 `m` 个值，一经确定就完全可预测。
它的「难」只在于参数保密，而不是计算困难，所以少量连续输出就足以还原全部参数。

关键一步是消除 `c`：差分 `d[i]` 满足齐次关系 `d[i+1] = a·d[i] mod m`。
齐次关系可以再乘一次构造出**与 `a` 无关**的二次式 `d[i+1]d[i-1] - d[i]^2`，它是 `m` 的倍数。
于是 `m` 变成一堆整数的公因子，gcd 直接算出，即使 `m` 是 128 bit 素数也不影响。
有了 `m`，`a` 用模逆求得，`c` 用一次代入求得，之后所有状态可无限外推。

如果只泄漏**高位截断**的输出（`x >> 32`），gcd 技巧失效，需要格约简（HNP/LLL），那是这个攻击的加强版。

## 五、防御建议

- 不要用 LCG 做任何安全用途：线性结构让参数和状态都能从少量输出还原。
- 需要伪随机数用于密码学时用 CSPRNG：`os.urandom`、`secrets`、`/dev/urandom`、哈希 DRBG。
- 即使是非密码用途（模拟、抽样），也不要让输出直接参与密钥、token、nonce 的生成。
- 需要可预测性（如确定性回放）时用带密钥的 PRF（HMAC-DRBG），并把密钥与输出隔离。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/gen.py` | 出题脚本；`m, a, c, s0` 通过环境变量传入（`LCG_M/LCG_A/LCG_C/LCG_S0`），不随附件发布 |
| `files/output.txt` | 12 个连续状态 + 密文 hex |
