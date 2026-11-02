# -*- coding: utf-8 -*-
"""跑一遍 starwalk 系列五关的 verify.py。"""
import os, subprocess, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ROOT = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable
names = sorted((d for d in os.listdir(ROOT)
                if os.path.isdir(os.path.join(ROOT, d))
                and os.path.exists(os.path.join(ROOT, d, "verify.py"))),
               key=lambda d: int(d.replace("Level", "")) if d.startswith("Level") else 99)
ok = 0
for n in names:
    print("== %s" % n)
    r = subprocess.run([PY, os.path.join(ROOT, n, "verify.py")],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    for line in ((r.stdout or "") + (r.stderr or "")).strip().splitlines():
        print("   " + line)
    if r.returncode == 0:
        ok += 1
    else:
        print("   [FAIL] exit=%d" % r.returncode)
print()
print("汇总：%d / %d 通过" % (ok, len(names)))
sys.exit(0 if ok == len(names) else 1)
