# enc-chain-ctf · 编码与进制转换（3 关）

从「认出一层编码」到「连解三层」。

| 关 | 目录 | 题名 | 难度 | 分值 |
|---|---|---|---|---|
| Level1 | [`Level1/`](Level1/) | 只有 base64 | 1 | 80 |
| Level2 | [`Level2/`](Level2/) | 只有十六进制 | 1 | 80 |
| Level3 | [`Level3/`](Level3/) | 编码接力 | 1 | 100 |

## 递进关系

`Level1` 最容易，`Level3` 最难（也是这个主题原本那道题）。每一关都能独立做，
但按顺序做更顺 —— 前一关考的东西，后一关会用到。

## 一键校验

```bash
cd Level1
python -c "import os,subprocess,sys;[print('==',d,subprocess.run([sys.executable,os.path.join(d,'verify.py')]).returncode) for d in sorted(os.listdir('.')) if os.path.isdir(d)]"
```
