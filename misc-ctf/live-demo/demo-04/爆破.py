# -*- coding: utf-8 -*-
"""现场演示用：六位数字爆破。屏幕会一直滚，三四秒后停住。"""
import os, sys, time, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, "files", "绝密.zip")
t0 = time.time()
found = None
with zipfile.ZipFile(p) as f:
    for i in range(1000000):
        pw = "%06d" % i
        try:
            data = f.read("flag.txt", pwd=pw.encode())
        except Exception:
            if i % 5000 == 0:
                print("  try %s ..." % pw, flush=True)
            continue
        found = pw
        print("")
        print("  密码是: %s  （用了 %.1f 秒）" % (pw, time.time() - t0))
        print("  文件内容:", data.decode(errors="replace").strip())
        break
if not found:
    print("  没爆破出来")
