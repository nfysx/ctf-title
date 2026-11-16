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
    def has_verify(base):                   # 根目录或 exp/ 下都算
        return os.path.exists(os.path.join(base, "verify.py")) or \
               os.path.exists(os.path.join(base, "exp", "verify.py"))
    if has_verify(p):
        names.append(d)
    else:                                   # 系列子目录
        for sub in sorted(os.listdir(p)):
            sp = os.path.join(p, sub)
            if os.path.isdir(sp) and has_verify(sp):
                names.append("%s/%s" % (d, sub))
ok = 0
for n in names:
    print("== %s" % n)
    vp = os.path.join(ROOT, n, "verify.py")
    if not os.path.exists(vp):
        vp = os.path.join(ROOT, n, "exp", "verify.py")
    r = subprocess.run([PY, vp],
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
