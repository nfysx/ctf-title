# file-header-ctf · 文件类型识别（2 关）

后缀会说谎，字节不会。

| 关 | 目录 | 题名 | 难度 | 分值 |
|---|---|---|---|---|
| Level1 | [`Level1/`](Level1/) | 改个后缀就露馅 | 1 | 80 |
| Level2 | [`Level2/`](Level2/) | 被抹掉的文件头 | 2 | 150 |

## 递进关系

`Level1` 最容易，`Level2` 最难（也是这个主题原本那道题）。每一关都能独立做，
但按顺序做更顺 —— 前一关考的东西，后一关会用到。

## 一键校验

```bash
cd Level1
python -c "import os,subprocess,sys;[print('==',d,subprocess.run([sys.executable,os.path.join(d,'verify.py')]).returncode) for d in sorted(os.listdir('.')) if os.path.isdir(d)]"
```
