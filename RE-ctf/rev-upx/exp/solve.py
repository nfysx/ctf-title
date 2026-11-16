#!/usr/bin/env python3
"""rev-upx: UPX 加壳的 Linux ELF。壳只压缩，不加密。

脱壳：upx -d challenge   （附件用 UPX 3.96 打包，3.96 与 4.x 都能解开）
脱壳后的文件里 flag 是明文，strings 直接出。
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "files", "challenge")
UPX_IMAGE = os.environ.get("UPX_IMAGE", "upx4box:latest")


def try_local(upx, dst):
    r = subprocess.run([upx, "-d", dst], capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write("[!] 本机 upx 没能脱壳：%s\n" % (r.stderr.strip().splitlines()[-1:] or r.stdout.strip()[-120:]))
        return False
    return True


def try_docker(workdir):
    try:
        subprocess.run(["docker", "run", "--rm", "-v", workdir + ":/w",
                        UPX_IMAGE, "upx", "-d", "/w/unpacked"],
                       check=True, capture_output=True)
        return True
    except Exception as e:
        sys.stderr.write("[!] docker 兜底也失败：%s\n" % e)
        return False


def unpack(src):
    tmp = tempfile.mkdtemp()
    dst = os.path.join(tmp, "unpacked")
    shutil.copy(src, dst)
    os.chmod(dst, 0o755)
    ok = False
    upx = shutil.which("upx")
    if upx:
        ok = try_local(upx, dst)
    if not ok and shutil.which("docker"):
        ok = try_docker(tmp)
    if not ok:
        sys.exit("没有可用的 upx；装一个（apt install upx-ucl）或用 docker 镜像 " + UPX_IMAGE)
    raw = open(dst, "rb").read()
    shutil.rmtree(tmp, ignore_errors=True)
    return raw


data = open(EXE, "rb").read()
print("[+] UPX marker:", b"UPX!" in data, " size:", len(data))
raw = unpack(EXE)
print("[+] unpacked size:", len(raw))
m = re.search(rb"flag\{[a-z0-9_]+\}", raw)
print(m.group().decode())
