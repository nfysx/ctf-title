# 题解：凯撒 + 培根 + 莫尔斯（crypto-classic）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 易 |
| 考点 | 古典密码的串联还原：凯撒位移、培根编码、莫尔斯电码 |
| 附件 | `files/gen.py`、`files/output.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

`output.txt` 里只有 `.`、`-`、`/` 三种字符。flag 依次经过 **凯撒 → 培根 → 莫尔斯** 三层。

## 题面（直接复制到平台「简介」）

一封老式密电被存成纯文本，内容只有点、横和斜杠。留下它的人只记得它被三种古典手段依次处理过，顺序也忘了。还原 flag。

**提示**：斜杠是分隔符。把点横翻译成两种状态，再按每五个一组去读。

## 三、解题步骤

三层都是双射，倒着解：

1. **莫尔斯**：按 `/` 切开，`.- ` 记作 `A`、`-...` 记作 `B`，得到一串 A/B。
2. **培根**：每 5 个 A/B 一组，`A=0`、`B=1` 读成 5 bit 整数，就是符号在 `abcdefghijklmnopqrstuvwxyz{}_` 里的下标。
3. **凯撒**：下标减 5（模 29）。

```python
ALPHA = "abcdefghijklmnopqrstuvwxyz{}_"
REV = {".-": "A", "-...": "B"}
morse = "<output.txt 里的 ciphertext>"

bits = "".join(REV[t] for t in morse.split("/"))
shifted = "".join(
    ALPHA[int(bits[i:i+5].replace("A", "0").replace("B", "1"), 2)]
    for i in range(0, len(bits), 5)
)
flag = "".join(ALPHA[(ALPHA.index(ch) - 5) % len(ALPHA)] for ch in shifted)
print(flag)
```

```bash
cd crypto-classic
python exp/solve.py
```

```
flag{classic_cipher_warmup}
```

检查点：A/B 串长度是 5 的倍数（flag 27 个字符 → 135 个 5 bit 组）；莫尔斯段数等于 135。

## 四、原理

- **凯撒**是单表代换：字母表整体循环位移 k 位，逆变换就是位移 `-k`。它保持字母频率，一个词长就能认出。
- **培根编码**把每个符号换成 5 个二值符号。这里字母表设成 29 个符号（`a-z` 加 `{}` 加 `_`），
  `2^5 = 32 > 29`，所以每个符号都能塞进 5 bit，且不浪费。
- **莫尔斯**是变长前缀码。为了让解码没有歧义，出题脚本用 `/` 显式分隔每个 A/B，
  否则 `.-...` 这种串在 A/B 两个码字之间无法唯一切分。

三层复合没有增加强度：总变换仍然是一个可枚举的双射，没有任何密钥参与。
古典密码的共同缺陷就是密钥空间小（凯撒 25 种）或根本没有密钥（培根、莫尔斯），
在频率分析和已知格式面前没有抵抗力。

## 五、防御建议

- 古典密码只能当练习题。任何真实场景的机密性需求都应该用 AES-GCM / ChaCha20-Poly1305 之类带认证的现代密码。
- 不要用「多层古典密码叠加」的思路增加强度：复合双射的密钥空间是各层密钥空间之积的极小值，
  且逐层剥离的成本很低。
- 如果为了趣味做多层编码，把它当 puzzle，不当安全机制。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/gen.py` | 出题脚本，三层编码函数与字母表都在文件里；`FLAG=... python gen.py` 可复现 output.txt |
| `files/output.txt` | 莫尔斯形式的最终密文 |
