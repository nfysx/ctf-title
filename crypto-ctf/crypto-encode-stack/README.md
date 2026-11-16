# 题解：多层编码（crypto-encode-stack）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 方向 / 难度 | Crypto · 易 |
| 考点 | base64 / hex / rot13 的识别与还原；反序 |
| 附件 | `files/gen.py`、`files/output.txt` |
| Flag | 静态，比对逻辑在 `checker/main.rx` |

`output.txt` 里是一串 base64 风格的字符串。flag 依次经过 **反序 → rot13 → hex → base64** 四层处理。

## 题面（直接复制到平台「简介」）

一段内部脚本把 flag 反复换了四种写法才落盘，写脚本的人已经不记得顺序。`output.txt` 里只剩最外面一层，看起来是一串 base64。逐层还原，得到 flag。

**提示**：每一层的字符集都不一样：先看结尾的 `=` 去掉一层，再判断剩下的是不是全是十六进制数字。

## 三、解题步骤

```python
import base64, codecs
blob = "<output.txt 里的 data>"
step3 = base64.b64decode(blob).decode()   # base64
step2 = bytes.fromhex(step3).decode()     # hex
step1 = codecs.decode(step2, "rot13")     # rot13
print(step1[::-1])                        # 反序
```

```bash
cd crypto-encode-stack
python exp/solve.py
```

```
flag{stacked_encodings_all_the_way_down}
```

每层解完的特征：base64 解出来全是 `0-9a-f`（hex），hex 解出来全是字母（rot13 文本），rot13 之后反着读就是 flag。

## 四、原理

四种变换都是**双射**，信息量在每一层都没有损失，所以逐层取逆就一定回到原文。
识别靠的是每层输出的字符集特征：

| 层 | 输出字符集 | 判据 |
|---|---|---|
| base64 | `A-Za-z0-9+/` 加 `=` 填充，长度是 4 的倍数 | 结尾 `=`、无空格 |
| hex | `0-9a-f`，长度为偶数 | 全十六进制字符 |
| rot13 | 字母，字母频率不变 | 单表代换，caesar 位移 13 |
| 反序 | 任意 | 只有整体逆序才对 |

编码不是加密：没有密钥，任何人拿到都能还原。这题考的是「看到一串字符先判断它是什么编码」的习惯。

## 五、防御建议

- 编码只解决传输/存储格式问题，不提供机密性；敏感数据要加密而不是编码。
- 不要把「套了几层 base64」当成防护手段，解码脚本十行就能写完。
- 需要完整性时叠加 HMAC 或签名，防止编码层被替换。

## 六、附件清单

| 文件 | 说明 |
|---|---|
| `files/gen.py` | 出题脚本，编码流水线写在文件里；`FLAG=... python gen.py` 可复现 output.txt |
| `files/output.txt` | 四层编码后的 base64 串 |
