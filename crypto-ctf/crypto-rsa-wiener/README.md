# 题解：RSA Wiener 攻击（crypto-rsa-wiener）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 难 |
| 考点 | 小私钥指数；`e/n` 的连分数展开与收敛子；Legendre 定理 |
| 附件 | `files/gen.py`、`files/output.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

`output.txt` 只有 `n`（1024 bit）、`e`、`c`。私钥指数 `d` 只有 240 bit，满足 `d < n^0.25 / 3`。

## 题面（直接复制到平台「简介」）

某个 RSA 实现为了省解密时间，把私钥指数取得比常规小得多，其余流程看上去一切正常。附件给了公钥 `(n, e)` 和一段密文 `c`，没有任何私钥材料。还原明文。

**提示**：`e` 相对 `n` 偏大，是因为和它配对的那个数太小了。盯着 `e/n` 这个分数，看它能被什么样的有理数逼近。

## 三、解题步骤

需要实现连分数展开 + 收敛子枚举：

```python
def continued_fraction(num, den):
    while den:
        a = num // den
        yield a
        num, den = den, num - a * den

def convergents(num, den):
    h0, h1 = 0, 1        # 分子
    k0, k1 = 1, 0        # 分母
    for a in continued_fraction(num, den):
        h0, h1 = h1, a * h1 + h0
        k0, k1 = k1, a * k1 + k0
        yield h1, k1
```

对每个收敛子 `(k_num, d)`，用 `e·d ≡ 1 (mod φ)` 反推 `φ`，再由 `φ = n - p - q + 1` 解二次方程拿 `p, q`：

```python
for k_num, d in convergents(e, n):
    if k_num == 0 or (e * d - 1) % k_num:
        continue
    phi = (e * d - 1) // k_num
    s = n - phi + 1                 # p + q
    disc = s * s - 4 * n            # (p-q)^2
    r = isqrt(disc)
    if r * r == disc:               # 完全平方，说明这组 (k, d) 是对的
        p, q = (s + r) // 2, (s - r) // 2
        m = pow(c, d, n)
        break
```

```bash
cd crypto-rsa-wiener
python exp/solve.py
```

```
flag{rsa_wiener_small_private_exponent}
```

判定成功的依据不是「解出可读文本」，而是 `disc` 是完全平方且 `p*q == n`，这是数学上的硬校验。

## 四、原理

由 `e·d ≡ 1 (mod φ(n))` 得 `e·d - k·φ(n) = 1`，其中 `k` 是某个正整数。两边除以 `d·φ(n)`：

```
e/φ(n) - k/d = 1/(d·φ(n))
```

而 `φ(n) = n - p - q + 1`，与 `n` 的差只有 `O(√n)`，所以用 `n` 近似 `φ(n)` 引入的误差也很小，于是

```
|e/n - k/d| = |e·d - k·n| / (n·d) ≈ k/(n·d) < 1/(2d^2)     （当 d < n^0.25/3）
```

由 Legendre 定理，如果 `|α - a/b| < 1/(2b^2)`，那么 `a/b` 必然是 `α` 的连分数收敛子之一。
因此 `k/d` 一定出现在 `e/n` 的连分数收敛子序列里，逐个试即可，复杂度是 `O(log n)` 个候选。

这就是为什么「`d` 小」是致命的：私钥空间从 `φ(n)` 缩到 `n^0.25/3`，
而连分数恰好能在多项式时间内枚举完这个缩小后的空间。Boneh-Durfee 把这个界推到了 `n^0.292`，
本质上是同一件事的格形式推广。

## 五、防御建议

- `d` 必须和 `n` 同量级：用标准密钥生成流程，`d ≡ e^{-1} (mod φ)` 且不要人为截短。
- `e` 固定取 65537 时，`d` 由 `φ` 唯一决定、不会小；问题只出现在「自己选小 d 再反算 e」的实现里。
- 加密用 OAEP、签名用 PSS，避免确定性映射带来的额外攻击面。
- 密钥生成交给成熟库（OpenSSL、`cryptography`），不要手写扩展欧几里得去凑参数。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/gen.py` | 出题脚本；`p, q, d` 属于出题机私钥参数，通过环境变量传入（`RSA_P/RSA_Q/RSA_D`），不随附件发布 |
| `files/output.txt` | 公钥 `n, e` 与密文 `c` |
