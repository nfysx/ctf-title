# 题解：Diffie-Hellman 私钥泄露（crypto-dh-leak）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 易 |
| 考点 | Diffie-Hellman 密钥交换；私钥泄露后共享秘密可直接重算 |
| 附件 | `files/gen.py`、`files/output.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

`output.txt` 给出一组 DH 参数：`p`、`g`、双方公钥 `alice_public`/`bob_public`，以及 **alice 的私钥** `alice_private`；
密文是 flag 每个字节与 `shared_secret % 256` 异或后的整数数组。

## 题面（直接复制到平台「简介」）

一套自建的握手服务在调试时把内部状态一起写进了日志，运维把这份日志和抓到的密文打包成了附件。`output.txt` 里有模数 `p`、生成元 `g`、双方公钥、一个私钥，以及一段按字节数组给出的密文。把被异或掉的明文还原出来，就是 flag。

**提示**：`g`、双方公钥和那个私钥都在同一张参数表里；Diffie-Hellman 的两个私钥本来只该待在各自的进程内存里。

## 三、解题步骤

附件里的参数照抄成脚本：

```python
p             = <output.txt 里的 p>
bob_public    = <output.txt 里的 bob_public>
alice_private = <output.txt 里的 alice_private>
ciphertext    = [<output.txt 里的数组>]

shared = pow(bob_public, alice_private, p)   # 有 alice 私钥就能算共享秘密
key = shared % 256
print(bytes(c ^ key for c in ciphertext).decode())
```

直接跑现成的解答：

```bash
cd crypto-dh-leak
python exp/solve.py
```

输出：

```
flag{diffie_hellman_leaked_private_key}
```

## 四、原理

DH 的安全性建立在离散对数上：给定 `p`、`g`、`g^a mod p`，求 `a` 在计算上不可行。
共享秘密的定义是

```
shared = (g^b)^a mod p = (g^a)^b mod p
```

两边都必须有对方公钥和自己私钥。私钥一旦泄露，`pow(bob_public, alice_private, p)` 一步就得到共享秘密，
离散对数问题被完全绕过，协议本身没破，破的是密钥保管。

第二层问题在 `key = shared % 256`：这把 256 位的共享秘密压成了 8 bit。
就算私钥没泄露，攻击者遍历 `0..255` 逐个试解也能得到可读明文，256 次尝试的成本可以忽略。
把共享秘密直接当对称密钥（或用 HKDF 派生）才不会丢掉熵。

## 五、防御建议

- 私钥只存在于进程内存，不写日志、不进调试输出、不随崩溃转储落盘。
- 共享秘密不要截断成 `% 256` 这类单字节 key；用 HKDF-SHA256 派生出定长密钥，并且加 KDF 上下文。
- 用成熟库（`cryptography`、`PyCryptodome`）的 ECDH/X25519，不要手写大数运算。
- 生产环境加临时密钥（ephemeral）与密钥轮换，避免一份私钥泄露导致历史流量全部可解。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/gen.py` | 出题脚本，固定参数，写死 p/g/双方私钥；`FLAG=... python gen.py` 可复现 output.txt |
| `files/output.txt` | 泄露数据：DH 参数 + 私钥 + 密文数组 |
