# 题解：AES-ECB 块重排（crypto-aes-ecb-reorder）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 中 |
| 考点 | ECB 模式的块独立性；PKCS7 填充剥除；密文块拼接冒充服务端从未产生过的明文 |
| 形态 | 容器题，容器内 `1337/TCP`，宿主机映射 `9201` |
| 依赖 | 镜像内 `pycryptodome`（`pip3 install pycryptodome` 已在 Dockerfile 里） |
| 下发文件 | 无。密钥在构建时随机生成进镜像的 `/app/key`，flag 由 `FLAG` 环境变量注入 `/app/flag` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

服务端把用户输入拼进模板 `b"name=" + name + b"&admin=0"`，PKCS7 填充后整块 AES-128-ECB 加密；
提交的密文解密后明文以 `admin=1` 结尾才返回 flag。

## 题面（直接复制到平台「简介」）

一个内部资料系统把你填的昵称拼进模板后整块加密，权限字段就写在同一段明文里：`name=<昵称>&admin=0`。它只在解密后的明文以 `admin=1` 结尾时给出 flag，你能用的操作只有「加密一个昵称」和「提交一段密文」。入口：`nc <题目地址> 9201`，两个操作都以 hex 传输。

**提示**：你控制的那段和你要改的那段在同一份明文里。留意加密模式对每一个块分别做了什么，以及填充是在哪一端被剥掉的。

## 三、解题步骤

ECB 每 16 字节独立加密，密文块可以像积木一样搬。两步都要对齐到块边界：

1. **让第 0 块以 `&` 结束**。`name=` 占 5 字节，填 10 个 `A` 正好 15 字节，第 16 字节是模板自带的 `&`：

   ```
   pt = name=AAAAAAAAAA&admin=0        + pad
        └──── 16B ────┘└── 16B ───...
   ```

2. **造一块内容恰为 `admin=1` + `0x09`×9**。填 11 个 `C` 让 `name=`+`C`*11 占满 16 字节，第 1 块就是我们想要的：

   ```
   pt = name=CCCCCCCCCCC | admin=1\x09\x09\x09\x09\x09\x09\x09\x09\x09 | &admin=0 + pad
        └── 16B ──┘        └───────────── 16B ─────────────┘
   ```

3. **拼接两块提交**。服务端解出 `name=AAAAAAAAAA&admin=1\x09…`，`unpad` 按最后一字节 `0x09` 剥掉 9 字节，明文变成 `name=AAAAAAAAAA&admin=1`，条件成立。

```bash
cd crypto-aes-ecb-reorder
python exp/exp.py 127.0.0.1 9201
```

`exp/exp.py` 走 TCP，只用服务端当加密 oracle，不碰任何本地密钥：

```python
ct_a = c.encrypt(b"A" * 10)                      # 第 0 块 = name=AAAAAAAAAA&
ct_b = c.encrypt(b"C" * 11 + b"admin=1" + b"\x09" * 9)   # 第 1 块 = admin=1 + 0x09*9
c.submit(ct_a[0:16] + ct_b[16:32])
```

实跑输出（一次完整运行；密钥构建时随机生成，所以密文每次 build 都不同）：

```
name=AAAAAAAAAA      -> 72eae01f4d24493019a35471e1887153d5533e727b96ec30a2712c81858733ba
  block0 (块内明文 name=AAAAAAAAAA&): 72eae01f4d24493019a35471e1887153
name=CCCCCCCCCCC+admin=1+0x09*9 -> e4e01a7fd5d88a61814a73912a484b4cc8b92f9d2e89a621542848210a907d9c4021b6f127603124817e10519adaab79
  block1 (块内明文 admin=1+0x09*9): c8b92f9d2e89a621542848210a907d9c
crafted token        -> 72eae01f4d24493019a35471e1887153c8b92f9d2e89a621542848210a907d9c
server: result = flag{aes_ecb_block_reorder}
flag: flag{aes_ecb_block_reorder}
```

朴素提交过不了（`predictable` 模板尾巴永远跟着 `&admin=0`）：

```
name = hex(b"admin=1")                -> pt = name=admin=1&admin=0        result = denied
name = hex(b"A"*9)                    -> pt = name=AAAAAAAAA&admin=0      result = denied
name = hex(b"admin=1" + b"\x09"*9)    -> pt = name=admin=1\x09…&admin=0    result = denied
```

最后一条最容易误判：`unpad` 只剥**尾部**填充，而尾部仍然是 `&admin=0` + 真填充，
所以自己塞的 `0x09` 不生效，必须靠块拼接让 `admin=1` 落到明文最末尾。

## 四、原理

ECB 对每个 16 字节块单独做 `AES_K(block)`，块与块之间没有链式关系，也没有随机 IV。
数学上 `C = E(P)` 是逐块置换：密文块的集合与明文块的集合一一对应，所以

- 换位置 → 明文块跟着换位置；
- 拼接两次不同查询得到的块 → 得到的是两段明文的拼接。

服务端的约束检查只发生在自己的模板上（`&admin=0` 由服务端加），
而攻击者提交的是**密文**，解密时服务端既不验证块的来源，也不重新检查明文是否由自己产生过。
`unpad` 只认最后一字节给出的填充长度，攻击者塞的 `0x09`×9 被当成合法填充剥掉，
模板自带的 `&admin=0` 随之落进填充区消失。

使攻击成立的两个必要条件正好被这题的布置满足:

1. **用户输入与判定字段在同一个 ECB 明文里**（拼接后的块仍然能被正确解密）；
2. **填充是 PKCS7 且末尾可控**（`0x09`×9 恰好覆盖 9 字节的 `&admin=0`）。

如果换成 CBC（块间链式、需要前一块密文做 XOR）或 AEAD（整段认证），
拼出来的密文要么解出乱码，要么直接认证失败。

## 五、防御建议

- 不要用 ECB。它既泄露明文块的重复模式（ECB 企鹅图），也允许块重排。
  用 AES-GCM 或 AES-CBC + 独立 HMAC，任何一块被改动都会认证失败。
- 认证要覆盖**完整明文**（含模板拼接后的结果），而不是只校验用户输入字段。
- 结构化数据别用可拼接的字符串模板，改用服务端签名、不可篡改的 token（如 AEAD 加密的 JSON）。
- 密钥只放服务端/镜像内，构建时随机生成（本题镜像的 `/app/key` 就是 `os.urandom(16)`），不进代码仓库、不下发。

## 六、文件清单

| 文件 | 说明 |
|---|---|
| `src/server.py` | TCP 服务本体：`encrypt` oracle 与 `submit` 判定，密钥读 `/app/key`，flag 读 `/app/flag` |
| `src/entrypoint.sh` | 写 `/app/flag`（来自 `FLAG` 环境变量）并起 `socat TCP-LISTEN:1337,fork` |
| `Dockerfile` | `ubuntu:22.04` + `python3`/`python3-pip`/`socat` + `pycryptodome`；构建时生成随机 `/app/key` |
| `docker-compose.yml` | `9201:1337`，`FLAG` 从环境变量透传 |
| `checker/main.rx` | 静态 flag 判定（模板 A） |
| `exp/exp.py` | 走 TCP 的块重排 exp |

本地跑：

```bash
docker build -t crypto-aes-ecb-reorder:latest .
docker run --rm -d --cap-drop NET_BIND_SERVICE \
  -e "FLAG=flag{aes_ecb_block_reorder}" -p 9201:1337 crypto-aes-ecb-reorder:latest
python exp/exp.py 127.0.0.1 9201
```

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `crypto-aes-ecb-reorder` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/crypto-aes-ecb-reorder` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
| 镜像版本 | 上传后生成的 Unix 时间戳 | 下拉选最新那条，不要手填 |
| 镜像拉取策略 | `Always` | 与平台默认一致 |
| 服务描述 | Pwn/交互题（nc 连上去打） | 只是备注，不影响运行 |
| 传输协议 | `tcp` | 全库只有 TCP 服务 |
| 应用协议 | `raw` | 裸 TCP / SSH / socat 选 raw |
| **服务端口** | **`1337`** | 容器内实际监听端口，必须与 Dockerfile 的 `EXPOSE` 一致 |
| CPU 限制 | `0.1, 0.3` | 最小, 最大（核） |
| 内存限制（MB） | `64, 128` | 最小, 最大 |
| 存储限制（MB） | `64, 1024` | 最小, 最大 |
| restricted | **勾选** | 平台会 drop `NET_BIND_SERVICE`，所以容器监听的是 `1337` 而不是 80 |
| 出网 | **关闭** | 本题不需要外网 |

> 端口必须填 **1337**。这是 socat / 裸 TCP 服务的监听端口，选手用 `nc <地址> <映射端口>` 连。
