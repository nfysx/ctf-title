# 题解：RSA 低指数开方（crypto-rsa-small-e）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 易 |
| 考点 | RSA 加密公式；`m^e < n` 时模运算没有发生，直接开 e 次方 |
| 附件 | `files/gen.py`、`files/output.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

`output.txt` 给出 `n`（1024 bit）、`e = 3` 和密文 `c`。

## 题面（直接复制到平台「简介」）

一份旧工具用硬编码的 `e = 3` 加密通知，没有做任何填充。附件给出 1024 bit 的模数 `n`、指数 `e` 和密文 `c`。把那条通知恢复出来。

**提示**：先估一下明文的位长，再看 `m^e` 会不会压根没被模数截断过。

## 三、解题步骤

```python
c = <output.txt 里的 c>
e = 3

def iroot(x, k):                 # 整数 k 次根，向下取整
    lo, hi = 0, 1 << ((x.bit_length() + k - 1) // k + 1)
    while lo < hi:
        mid = (lo + hi + 1) // 2
        if mid ** k <= x: lo = mid
        else: hi = mid - 1
    return lo

m = iroot(c, e)
print(m.to_bytes((m.bit_length() + 7) // 8, "big").decode())
```

```bash
cd crypto-rsa-small-e
python exp/solve.py
```

```
flag{rsa_small_exponent_root}
```

验证：`iroot(c,3)**3 == c` 成立，说明 `c` 是完全立方数，中间没有取模。

## 四、原理

RSA 的密文是 `c = m^e mod n`。如果 `m^e < n`，那么 `mod n` 这一步什么都没做，
密文就是 **整数意义下的完全 e 次方**：`c = m^3`。

flag 长度约 28 字节 = 224 bit，`m^3` 约 672 bit，而 `n` 是 1024 bit，所以 `m^3 < n` 成立，攻击条件满足。
此时私钥没有任何作用，求整数立方根即可，代价是几次大整数除法。

反过来，只要 `m` 足够长（`m^3 > n`）普通开方就失效，攻击者得换成「低指数 + 多组密文 CRT 组合」或 Coppersmith 这类方法。
标准防御是加密前做随机化填充：OAEP 让 `m` 变成接近 `n` 的随机数，并且每次加密结果不同，
既堵住开方，也堵住重放和确定性比较。

## 五、防御建议

- 加密必须带随机填充：RSA-OAEP（SHA-256）。PKCS#1 v1.5 加密已被 Bleichenbacher 类攻击反复打穿。
- 公钥指数用 `e = 65537`，不要用 3；即便用 3，只要填充正确也安全，但没必要留隐患。
- 密钥长度至少 2048 bit；本文的 1024 bit 只用于题目。
- 不要把「教科书 RSA」（无填充）用在任何真实协议里。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/gen.py` | 出题脚本，n/e 为常量，加密前断言 `m^3 < n`；`FLAG=... python gen.py` 可复现 output.txt |
| `files/output.txt` | 公钥 `n, e` 与密文 `c` |
