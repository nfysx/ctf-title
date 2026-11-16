# 题解：一次性密码本复用（crypto-otp-reuse）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 中 |
| 考点 | OTP 的「一次性」；同一密钥流复用导致 `c1 ⊕ c2 = m1 ⊕ m2`；已知明文恢复密钥流 + crib 对齐偏移 |
| 附件 | `files/gen.py`、`files/output.txt`、`files/known_message.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

`output.txt` 给两条密文：`c_public` 对应一份**原文已知**的公开通知（`known_message.txt`），
`c_secret` 对应含 flag 的秘密文件。两者用同一个密钥流，但秘密文件的密文**不是从密钥流偏移 0 开始**。

## 题面（直接复制到平台「简介」）

运维用同一个一次性密码本加密了两份文件：一份是已经归档的公开通知，一份是含 flag 的秘密文件。附件给了两条密文和那份通知的原文，秘密文件的密文不是从密码本开头起的。还原 flag。

**提示**：密钥只有一份，却被用了两次，两份密文放在一起之后，式子里还会剩下什么？偏移对不上的地方，用秘密文件开头那几个已知字节去对齐。

## 三、解题步骤

第 1 步：用已知明文恢复密钥流。异或自逆，`k = m_public ⊕ c_public` 就得到偏移 0 起的那一段密钥流。

第 2 步：偏移未知，用 crib 对齐。秘密文件的明文以 `flag{` 开头（5 字节），
把候选密钥流窗口逐字节滑过去，能对上 `flag{` 的偏移只有一个：

```python
ks = bytes(a ^ b for a, b in zip(public, c_public))       # 已知明文 -> 密钥流
crib = b"flag{"
hits = [t for t in range(len(ks) - len(c_secret) + 1)
        if all(c_secret[i] ^ ks[t + i] == crib[i] for i in range(len(crib)))]
assert len(hits) == 1                 # 唯一
t = hits[0]
flag = bytes(c_secret[i] ^ ks[t + i] for i in range(len(c_secret)))
```

```bash
cd crypto-otp-reuse
python exp/solve.py
```

```
offset = 61
flag{otp_key_reuse}
```

直接拿 `ks[0:len]` 去异或 `c_secret` 只会得到乱码，这就是偏移那一步的意义。

## 四、原理

OTP 的机密性完全依赖密钥流「只用一次」。两条密文用同一段密钥流时：

```
c1 ⊕ c2 = (m1 ⊕ k) ⊕ (m2 ⊕ k) = m1 ⊕ m2
```

密钥流被消掉了，问题退化成「两条明文异或」，这是一个不需要密钥的经典问题：
只要有一边可猜（这里是公开通知的原文），另一边就被唯一确定。
偏移未知也不构成额外强度：`flag{` 这 5 个已知字节提供 40 bit 的筛选强度，
在不到 80 个候选偏移里必然唯一命中。

换句话说，复用把「需要猜 137×8 bit 密钥」降级成「需要猜 0 bit 密钥 + 一点对齐工作」。
任何伪随机流（CTR、ChaCha、这里 sha256 链）只要 nonce 复用，都会退化成同一类问题。

## 五、防御建议

- 一次一密必须真的只用一次：每条消息独立随机密钥，或 AEAD 每条消息唯一 nonce。
- 流式加密用 AES-CTR / ChaCha20-Poly1305，nonce 用计数器并持久化，防止重启后重复。
- 用带认证的模式：密文被拼接或截断时直接报错，而不是解出乱码。
- 不要把同一密钥流用于「公开文件 + 秘密文件」这种组合，公开文件本身就是已知明文攻击的原料。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/gen.py` | 出题脚本，密钥流种子与偏移写死为常量；`FLAG=... python gen.py` 可复现两个数据文件 |
| `files/output.txt` | `c_public`、`c_secret` 两条密文的 hex |
| `files/known_message.txt` | 公开通知原文（与 `c_public` 对应的已知明文） |
