# 题解：单字节异或的已知明文攻击（crypto-xor-known）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 易 |
| 考点 | 异或的可逆性；已知明文反推密钥 |
| 附件 | `files/gen.py`、`files/output.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

`output.txt` 只有一行十六进制密文，题面说明它是 flag 与**一个字节**的密钥逐字节异或的结果。

## 题面（直接复制到平台「简介」）

某服务给 flag 套了一层最简单的混淆：逐字节异或同一个单字节密钥，结果按 hex 存进配置备份。备份里只剩 `ciphertext` 一行，密钥没留下。还原原文。

**提示**：flag 的格式是固定的，异或又是自逆的；取一个你本来就知道的明文字节代回去。

## 三、解题步骤

flag 的格式是固定的，第一个字节必然是 `f`（`0x66`）。
异或满足 `c = m ⊕ k` ⟺ `k = c ⊕ m`，所以第一个字节就能反推出密钥：

```python
ct = bytes.fromhex("<output.txt 里的 hex>")
key = ct[0] ^ ord('f')          # 0x66
print(key)                       # 0x6b
print(bytes(b ^ key for b in ct).decode())
```

```bash
cd crypto-xor-known
python exp/solve.py
```

```
flag{known_plaintext_xor}
```

不依赖明文前缀也能做：密钥空间只有 256，全试一遍挑可打印的那条。

## 四、原理

异或是自逆运算：`(m ⊕ k) ⊕ k = m`。它没有扩散、没有混淆，密文的每个字节只由同一位置的明文字节和密钥决定。
因此只要知道任意一个位置的明文字节，同一个位置的密钥字节立刻确定；单字节密钥意味着知道一个字节就等于知道整把密钥。

这类「密钥空间小于明文可猜测性」的加密，安全性上等价于没有加密。CTF 里 flag 格式本身（`flag{`）就是免费提供的已知明文。

## 五、防御建议

- 不要用单字节/固定短密钥异或当加密。它连混淆都没有，`flag{` 前缀就能一击命中。
- 需要流式加密时用标准构造：AES-CTR、ChaCha20，密钥由 CSPRNG 或 KDF 产生，nonce 每次唯一。
- 如果只是想做简单的可逆混淆，明确它不提供机密性，不要用它保护 flag、口令或令牌。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/gen.py` | 出题脚本，密钥 `0x6b` 写死为常量；`FLAG=... python gen.py` 可复现 output.txt |
| `files/output.txt` | 单字节异或密文的 hex |
