
import json, os, subprocess, sys, time
TOOLS = r"D:\ctf\misc-ctf\动态附件生成器"
os.chdir(TOOLS)
sys.path.insert(0, TOOLS)
sys.argv = ["x"]
import make_mapped as M

N = int(sys.argv[1]) if len(sys.argv) > 1 else 32
items = M.catalog()
targets, seen = [], set()
for label, it in sorted(items.items()):
    if it["kind"] != "challenge":
        continue
    full = (it["cfg"].get("group") + "/" if it["cfg"].get("group") else "") + (it["cfg"].get("path") or it["key"])
    if label != full or full in seen:
        continue
    seen.add(full)
    if full.startswith("starwalk-series/"):
        continue          # 这五关走 --variant，单独跑
    targets.append((full, []))

for t in ("trio", "gps", "hongyadong", "route"):
    targets.append((t, ["--variant"]))

print("份数 =", N, " 目标 =", len(targets), flush=True)
t0 = time.time()
ok, fail = [], []
for i, (t, extra) in enumerate(targets, 1):
    r = subprocess.run([sys.executable, "make_mapped.py", t, str(N)] + extra,
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    blob = (r.stdout or "") + (r.stderr or "")
    good = ("全部通过" in blob) or ("一致" in blob and "✗" not in blob)
    (ok if good else fail).append(t)
    print("[%2d/%d] %-24s %s  %.0fs" % (i, len(targets), t, "OK" if good else "FAIL", time.time()-t0), flush=True)
    if not good:
        print("   " + blob.strip().replace("\n", " | ")[-250:], flush=True)

# 6 个 demo 里只做 01/03/04/06
for d in ("demo-01", "demo-03", "demo-04", "demo-06"):
    r = subprocess.run([sys.executable, "make_mapped.py", d, str(N)],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    blob = (r.stdout or "") + (r.stderr or "")
    good = "异常 0" in blob
    (ok if good else fail).append(d)
    print("[demo] %-10s %s" % (d, "OK" if good else "FAIL"), flush=True)

print("\n完成 %d / 失败 %d，用时 %.0f 秒" % (len(ok), len(fail), time.time()-t0))
if fail: print("失败:", fail)
