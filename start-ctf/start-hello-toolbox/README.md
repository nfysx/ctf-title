# 先把环境检查一遍

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 赛前环境自检、命令行参数与环境变量 |
| 难度 | 1 / 5 |
| 附件 | `files/check_env.py` |
| Flag | `flag{environment_ready_before_the_game}` |

## 题面（直接复制到平台「简介」）

附件是一个环境自检脚本。跑起来它会逐项检查这台机器能不能参加比赛：Python 版本、curl、nc、git、pwntools、pycryptodome。

**任务**：把必需项补齐，并按它最后给出的提示完成最后一步，脚本才会打印 flag。

**提示**：必需项全绿之后脚本会停下来，让你设一个环境变量再跑第二次，这个动作在后面很多题里都会用到。
## 三、解题步骤

### Step 1｜先跑一遍

```bash
cd files
python check_env.py
```

输出是一张检查表：

```text
========================================================
赛前环境自检  |  Windows AMD64
========================================================
  [OK  ] Python 解释器     必需     Python 3.12.0
  [MISS] curl              可选     未找到
         └─ 安装 curl，或用 Python 的 urllib 代替
  ...
--------------------------------------------------------
必需项全部就绪。

最后一步：把 CTF_READY 设成 1 再跑一次。
  Linux / macOS :  CTF_READY=1 python check_env.py
  ...
```

### Step 2｜按提示设置环境变量再跑

三个平台写法不同，选自己那个：

```bash
# Linux / macOS
CTF_READY=1 python check_env.py

# Windows CMD
set CTF_READY=1 && python check_env.py

# Windows PowerShell
$env:CTF_READY=1; python check_env.py
```

这一次最后一行就是：

```text
flag{environment_ready_before_the_game}
```

### Step 3｜缺什么装什么

如果脚本停在"还差必需项"，按它给的提示装。几个常见的：

```bash
pip install pwntools pycryptodome
```

## 四、原理

脚本里的 flag 不是明文常量：它先和 `0xA7` 逐字节异或、base64 编码，再切成四段打乱顺序存放，
运行时才按 `_ORDER` 拼回来。所以 `grep flag{ check_env.py` 是空的，直接读源码也看不出答案，必须真的把它跑起来。

脚本做三件事：`shutil.which()` 查命令在不在 `PATH` 上、`__import__` 查 Python 包装没装、比较 `sys.version_info`。
前两个都是"能不能被找到"，所以装了但没加进 `PATH` 一样会报 MISS。

`CTF_READY` 这一层是故意的：它把"改环境变量"变成拿 flag 的最后一步，
因为这个动作在后面每道需要注入 `FLAG` 的题里都会反复用到。

## 五、防御建议

这条不是安全题，谈防御有点硬凑，说一个真问题：**别把自检脚本当成安全机制**。
它只看"名字在不在 `PATH` 上"，不看版本对不对、是不是被同名程序顶替。
真要确认工具可用，应该实际执行一次并检查退出码。

## 六、附件清单

| 文件 | 大小 | 说明 |
|---|---|---|
| `files/check_env.py` | 2627 B | 环境自检脚本 |
