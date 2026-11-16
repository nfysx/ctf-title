# -*- coding: utf-8 -*-
"""4 份变体自测（模拟 make_mapped.py 的用法）。"""
import hashlib
import os
import shutil
import sys
import tempfile

import builders_re as M

N = int(sys.argv[1]) if len(sys.argv) > 1 else 4
keys = sys.argv[2:] or sorted(M.BUILDERS)


def variant_flag(base, i):
    inner = base[len("flag{"):-1]
    return "flag{%s_%05x}" % (inner, (i * 2654435761) & 0xFFFFF)


print("%-20s %-2s %-58s %-6s %-6s %9s %s" %
      ("challenge", "#", "variant flag", "good", "bad", "bytes", "sha[:12]"))
allok = True
for key in keys:
    base = M._orig_flag(key)
    for i in range(1, N + 1):
        flag = variant_flag(base, i)
        tmp = tempfile.mkdtemp(prefix="v_")
        try:
            cfg = {"key": key, "flag": flag, "dir": tmp}
            data = M.BUILDERS[key](cfg)
            files = os.listdir(os.path.join(tmp, "files"))
            name = files[0]
            # 再建一次，验证确定性
            tmp2 = tempfile.mkdtemp(prefix="v2_")
            try:
                cfg2 = {"key": key, "flag": flag, "dir": tmp2}
                data2 = M.BUILDERS[key](cfg2)
                same = hashlib.sha256(data).hexdigest() == hashlib.sha256(data2).hexdigest()
            finally:
                shutil.rmtree(tmp2, ignore_errors=True)
            g, b = M.LAST_RUN.get(key, ("?", "?"))
            print("%-20s %-2d %-58s %-6s %-6s %9d %s%s" %
                  (key, i, flag, g.splitlines()[-1].split(": ")[-1] if g else "?",
                   b.splitlines()[-1].split(": ")[-1] if b else "?", len(data),
                   hashlib.sha256(data).hexdigest()[:12],
                   "" if same else "  !! 非确定性"))
            allok &= same
        except Exception as e:
            allok = False
            print("%-20s %-2d FAIL %s" % (key, i, e))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

print("\nVARIANTS", "PASS" if allok else "FAIL")
