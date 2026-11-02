# -*- coding: utf-8 -*-
"""救现场：把 flag.json 的 key 对齐成平台上「实际的文件名」。

场景：已经上传了附件，但平台重写/改名导致 key 对不上，
      选手提交时一律报 `Could not find you flag mapping`。

用法：
    python fix_flag_keys.py checker/flag.json 实际名1 实际名2 ...
    python fix_flag_keys.py checker/flag.json --from-file names.txt
    python fix_flag_keys.py checker/flag.json --prefix chall-    # 只把 key 换成 chall-NN

配对规则（依次尝试）：
    1) 按名字里的「最后一段数字」配对（作业-15.jpg ↔ zuo_ye_-15.jpg ↔ chall-15.jpg）
    2) 数字配不上时，按排序后逐个位置配对
原文件会备份成 flag.json.bak。
"""
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def last_num(s):
    nums = re.findall(r"\d+", s)
    return int(nums[-1]) if nums else None


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return 1
    path = args[0]
    rest = args[1:]

    names = []
    if "--prefix" in rest:
        # 把现有 key 按顺序改造成 <prefix><NN>
        prefix = rest[rest.index("--prefix") + 1]
        old = json.load(open(path, encoding="utf-8"))
        keys = list(old)
        width = max(2, len(str(len(keys))))
        pairs = [(k, "%s%0*d%s" % (prefix, width, i + 1,
                                   os.path.splitext(k)[1].lower()))
                 for i, k in enumerate(keys)]
    else:
        if "--from-file" in rest:
            fp = rest[rest.index("--from-file") + 1]
            names = [l.strip() for l in open(fp, encoding="utf-8") if l.strip()]
        else:
            names = rest
        if not names:
            print("  没给实际文件名。要么列在命令行，要么用 --from-file，或用 --prefix chal-")
            return 1
        old = json.load(open(path, encoding="utf-8"))
        keys = list(old)
        if len(keys) != len(names):
            print("  ⚠ 条目数不一致：flag.json %d 条，实际文件名 %d 条" % (len(keys), len(names)))
            print("    请核对后重试（宁可不改，也别乱配）")
            return 1
        # 1) 按最后一段数字配对
        by_num = {}
        dup = False
        for k in keys:
            n = last_num(k)
            if n is None or n in by_num:
                dup = True
                break
            by_num[n] = k
        new_by_num = {}
        for nm in names:
            n = last_num(nm)
            if n is None or n in new_by_num:
                dup = True
                break
            new_by_num[n] = nm
        if not dup and set(by_num) == set(new_by_num):
            pairs = [(by_num[n], new_by_num[n]) for n in sorted(by_num)]
            how = "按名字里的数字配对"
        else:
            pairs = list(zip(sorted(keys), sorted(names)))
            how = "数字对不上，按排序位置配对"
        print("  配对方式：%s" % how)

    new_map = {}
    print("  %-22s ← %-22s %s" % ("新 key（平台上的名字）", "旧 key", "flag"))
    for old_k, new_k in pairs:
        new_map[new_k] = old[old_k]
        print("  %-22s ← %-22s %s" % (new_k, old_k, old[old_k][-14:]))

    bak = path + ".bak"
    if os.path.exists(path):
        os.replace(path, bak)
    json.dump(new_map, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print()
    print("  已写入 %s（旧文件备份为 .bak）" % path)
    print("  共 %d 条" % len(new_map))
    return 0


if __name__ == "__main__":
    sys.exit(main())
