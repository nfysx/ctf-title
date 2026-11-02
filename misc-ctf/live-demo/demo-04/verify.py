# -*- coding: utf-8 -*-
import os, sys, time, zipfile
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
EXPECT = "flag{6_d16175_15_n0th1ng}"
p = os.path.join(HERE, "files", "绝密.zip")
try:
    zipfile.ZipFile(p).read("flag.txt")
    raise AssertionError("没有真加密 —— 演示就不好看了")
except RuntimeError:
    print("  无密码读取被拦（真加密 ✓）")
t0 = time.time()
found = None
with zipfile.ZipFile(p) as f:
    for i in range(1000000):
        pw = ("%06d" % i).encode()
        try:
            d = f.read("flag.txt", pwd=pw)
        except Exception:
            continue
        if d.startswith(b"flag{"):
            found = pw.decode()
            break
print("  爆破命中密码: %s | 耗时 %.2f 秒" % (found, time.time() - t0))
assert found, "六位数字没爆出来"
flag = zipfile.ZipFile(p).read("flag.txt", pwd=found.encode()).decode().strip()
print("  解出:", flag)
assert flag == EXPECT
print("  PASS demo-04")
