#!/usr/bin/env python3
"""赛前环境自检。

用法：
    python check_env.py

它会逐项检查这台机器能不能参加比赛，最后告诉你还差什么。
"""
import base64
import os
import platform
import shutil
import sys

# 答案在运行时才拼回来，源码里没有可直接读到的 flag。
# 顺序表说明这四段该按什么次序拼：_ORDER[i] 是"第 i 个位置放 _PARTS 里的第几段"。
_PARTS = [
    "jVwsbD3vjFw",
    "wcvGwNzCydH",
    "sHI1cL408/C+MDGysLa",
    "O1cjJysLJ0/",
]
_ORDER = (1, 3, 0, 2)
_MASK = 0xa7


def _reveal():
    raw = "".join(_PARTS[i] for i in _ORDER)
    return bytes(b ^ _MASK for b in base64.b64decode(raw)).decode("utf-8")


REQUIRED = []


def check_python():
    v = sys.version_info
    ok = v >= (3, 8)
    return ok, "Python %d.%d.%d" % (v[0], v[1], v[2]), (None if ok else "需要 Python >= 3.8")


def check_cmd(name, hint):
    path = shutil.which(name)
    return (path is not None), (path or "未找到"), (None if path else hint)


def check_module(mod, hint):
    try:
        __import__(mod)
        return True, "已安装", None
    except Exception:
        return False, "未安装", hint


def main():
    items = [
        ("Python 解释器", check_python(), True),
        ("curl", check_cmd("curl", "安装 curl，或用 Python 的 urllib 代替"), False),
        ("nc / netcat", check_cmd("nc", "Windows 可在「设置 → 应用 → 可选功能」里装，或用 pwntools 的 remote()"), False),
        ("git", check_cmd("git", "装 git，后面拉题库、对脚本都用得上"), False),
        ("pwntools", check_module("pwn", "pip install pwntools（Pwn 方向要用）"), False),
        ("pycryptodome", check_module("Crypto", "pip install pycryptodome（Crypto 方向要用）"), False),
    ]

    print("=" * 56)
    print("赛前环境自检  |  %s %s" % (platform.system(), platform.machine()))
    print("=" * 56)

    blocking = []
    for label, (ok, detail, hint), required in items:
        mark = "OK  " if ok else "MISS"
        tag = "必需" if required else "可选"
        print("  [%s] %-14s %-8s %s" % (mark, label, tag, detail))
        if not ok:
            if required:
                blocking.append((label, hint))
            elif hint:
                print("         └─ %s" % hint)

    print("-" * 56)

    if blocking:
        print("还差必需项，先把这些装上：")
        for label, hint in blocking:
            print("  · %s：%s" % (label, hint))
        return 1

    print("必需项全部就绪。")

    if os.environ.get("CTF_READY") != "1":
        print()
        print("最后一步：把 CTF_READY 设成 1 再跑一次。")
        print("  Linux / macOS :  CTF_READY=1 python check_env.py")
        print("  Windows(CMD)  :  set CTF_READY=1 && python check_env.py")
        print("  Windows(PS)   :  $env:CTF_READY=1; python check_env.py")
        return 0

    print()
    print(_reveal())
    return 0


if __name__ == "__main__":
    sys.exit(main())
