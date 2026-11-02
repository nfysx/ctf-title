# traffic-ctf · 流量分析（4 关）

从看一条 HTTP 请求，到逆一个私有协议。

| 关 | 目录 | 题名 | 难度 | 分值 |
|---|---|---|---|---|
| Level1 | [`Level1/`](Level1/) | 流量里的那一次请求 | 2 | 200 |
| Level2 | [`Level2/`](Level2/) | 分块传输的日志 | 2 | 220 |
| Level3 | [`Level3/`](Level3/) | DNS 里的悄悄话 | 3 | 260 |
| Level4 | [`Level4/`](Level4/) | 自己逆的私有协议 | 3 | 300 |

## 递进关系

`Level1` 最容易，`Level4` 最难（也是这个主题原本那道题）。每一关都能独立做，
但按顺序做更顺 —— 前一关考的东西，后一关会用到。

## 一键校验

```bash
cd Level1
python -c "import os,subprocess,sys;[print('==',d,subprocess.run([sys.executable,os.path.join(d,'verify.py')]).returncode) for d in sorted(os.listdir('.')) if os.path.isdir(d)]"
```
