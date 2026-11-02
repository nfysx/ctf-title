# lsb-stego-ctf · LSB 隐写（2 关）

从单通道到三通道交织。

| 关 | 目录 | 题名 | 难度 | 分值 |
|---|---|---|---|---|
| Level1 | [`Level1/`](Level1/) | 红色通道的悄悄话 | 1 | 100 |
| Level2 | [`Level2/`](Level2/) | 最低有效位 | 2 | 150 |

## 递进关系

`Level1` 最容易，`Level2` 最难（也是这个主题原本那道题）。每一关都能独立做，
但按顺序做更顺 —— 前一关考的东西，后一关会用到。

## 一键校验

```bash
cd Level1
python -c "import os,subprocess,sys;[print('==',d,subprocess.run([sys.executable,os.path.join(d,'verify.py')]).returncode) for d in sorted(os.listdir('.')) if os.path.isdir(d)]"
```
