# -*- coding: utf-8 -*-
"""跑一遍全部题目的 verify.py，汇总结果。"""
import os, subprocess, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ROOT = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable
names = []
for d in sorted(os.listdir(ROOT)):
    if d.startswith("_"):
        continue
    p = os.path.join(ROOT, d)
    if not os.path.isdir(p):
        continue
    if os.path.exists(os.path.join(p, "verify.py")):
        names.append(d)
    else:                                   # 系列子目录
        for sub in sorted(os.listdir(p)):
            sp = os.path.join(p, sub)
            if os.path.isdir(sp) and os.path.exists(os.path.join(sp, "verify.py")):
                names.append("%s/%s" % (d, sub))
ok = 0
for n in names:
    print("== %s" % n)
    r = subprocess.run([PY, os.path.join(ROOT, n, "verify.py")],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (r.stdout or "") + (r.stderr or "")
    for line in out.strip().splitlines():
        print("   " + line)
    if r.returncode == 0:
        ok += 1
    else:
        print("   [FAIL] exit=%d" % r.returncode)
print()
print("汇总：%d / %d 通过" % (ok, len(names)))
sys.exit(0 if ok == len(names) else 1)
