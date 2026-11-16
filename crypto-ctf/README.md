# crypto-ctf · 密码学入门

> 12 道题，坡度 5 易 : 3 中 : 2 难，知识点对齐 `Crypto入门指北`。
> 前两道是**概念课**（编码不是加密、模运算求逆），后面才是真正的攻击。
> 只有 1 道是容器题（需要一个加密 oracle），其余全是附件题：给脚本 + 泄露数据，自己算。

---

## 题目一览（12 题）

| 目录 | 题名 | 考点 | 难度 | 形态 | 附件 |
|---|---|---|---|---|---|
| `crypto-lesson-encoding/` ☆ | 编码不是加密 | base64 / hex / URL 三段拼接 | 易 | 附件 | `task.txt` |
| `crypto-lesson-inverse/` ☆ | 模逆元 | 扩展欧几里得求 `a·x ≡ 1 mod m` | 易 | 附件 | `task.txt` |
| `crypto-dh-leak/` | DH 私钥泄露 | 会话私钥被打印，共享密钥可直接重算 | 易 | 附件 | `gen.py` `output.txt` |
| `crypto-xor-known/` | 已知明文异或 | 用 `flag{` 反推单字节密钥 | 易 | 附件 | 同左 |
| `crypto-encode-stack/` | 多层编码 | 反序 → rot13 → hex → base64 | 易 | 附件 | 同左 |
| `crypto-rsa-small-e/` | 低指数开方 | `e=3` 且明文小，直接开立方 | 易 | 附件 | 同左 |
| `crypto-classic/` | 古典密码 | 凯撒 + 莫尔斯 + 培根 | 易 | 附件 | 同左 |
| `crypto-rsa-common-modulus/` | 共模攻击 | 同一 `n` 两组 `(e, c)`，扩展欧几里得合出明文 | 中 | 附件 | 同左 |
| `crypto-aes-ecb-reorder/` | ECB 块重排 | 用加密 oracle 切块拼接，让 `admin=0` 变 `admin=1` | 中 | **容器** | — |
| `crypto-otp-reuse/` | 一次性密码本复用 | 两条密文异或消掉密钥流 | 中 | 附件 | `gen.py` `output.txt` `known_message.txt` |
| `crypto-rsa-wiener/` | Wiener 攻击 | `d` 很小时连分数展开还原私钥 | 难 | 附件 | `gen.py` `output.txt` |
| `crypto-lcg-recover/` | LCG 状态恢复 | `gcd` 还原模数，验证递推后预测下一个 | 难 | 附件 | 同左 |

> ☆ = 引入题。编码、模运算这两个概念后面每道题都会用到，先做掉。

## 依赖

```bash
pip install pycryptodome     # 只有 crypto-aes-ecb-reorder 的 exp 需要
```

其余全部用标准库（`hashlib` / `base64` / `urllib`），Wiener 的连分数、LCG 的模数恢复都是手写实现。

## 跑起来

```bash
# 附件题：看题、算、提交
cd crypto-ctf/crypto-dh-leak && cat files/output.txt

# 每题都有解答脚本（不对外发布）
python exp/solve.py

# 唯一的容器题
cd crypto-ctf/crypto-aes-ecb-reorder && docker compose up -d --build
nc 127.0.0.1 9201
```

## 平台上传要点

| 题 | 形态 | 服务端口 | 传输协议 | 应用协议 |
|---|---|---|---|---|
| `crypto-aes-ecb-reorder` | 容器 | **1337**（本地映射 9201） | `tcp` | `raw` |
| 其余 11 道 | 附件 | — | — | — |

附件题的 `checker/main.rx` 是静态模板，`FLAG` 写死在脚本里；
容器题的密钥在**构建时随机生成**，只存在于镜像层，所以重新 build 后密文会变（README 已注明）。

## 这个方向的常见坑

| 现象 | 原因 |
|---|---|
| 解出来是乱码 | 编码链顺序反了；`base64` 缺 `=` 补齐、`hex` 奇数长度都会解出垃圾 |
| `pow(a, -1, m)` 报错 | Python < 3.8 不支持负指数；手写扩展欧几里得 |
| RSA 开方开出小数 | 明文不满足 `m³ < n`，低指数攻击不成立，换思路，不要硬开 |
| ECB 那题提交被 denied | 块边界没对齐：要取「第 0 块」和「第 1 块」，不是整段拼 |
| Wiener 解出的 `d` 验不过 | `e·d ≡ 1 mod φ(n)` 要验；连分数只取收敛子，要筛 |

## 目录结构

```text
crypto-ctf/
├── crypto-dh-leak/       files/gen.py files/output.txt + checker/ + exp/solve.py + README.md
├── crypto-aes-ecb-reorder/  src/server.py src/entrypoint.sh + Dockerfile
│                            docker-compose.yml + checker/ + exp/exp.py + README.md
└── ...（其余同 crypto-dh-leak 的排布）
```

约定：附件题不下发解答脚本；`gen.py` 的随机参数在出题时固定成常量写进 `files/`，
否则 README 和 checker 会对不上。
