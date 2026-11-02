#!/bin/sh
set -e

# flag 取值顺序（逻辑沿用 web-ctf，值按 misc-ctf 的写法放在文件里）：
#   1) 环境变量 FLAG —— 平台 checker 的 environ() 注入，比赛时走这条
#   2) /opt/ctf/flag.txt —— 静态可读 flag
#   3) 都没有就随机，写进 /tmp/flag（同一实例内一致）
FLAG="${FLAG:-}"
if [ -z "$FLAG" ] && [ -f /opt/ctf/flag.txt ]; then
  FLAG="$(tr -d "\r\n" < /opt/ctf/flag.txt)"
fi
if [ -z "$FLAG" ]; then
  FLAG="flag{$(head -c 16 /dev/urandom | hexdump -v -e '/1 "%02x"')}"
fi
printf "%s\n" "$FLAG" > /tmp/flag
chmod 644 /tmp/flag

# 主機密钥：每次启动现生成，避免不同实例之间 host key 冲突提示
mkdir -p /run/sshd
ssh-keygen -A >/dev/null 2>&1 || true

# 登录后看到的提示
cp /opt/ctf/motd /etc/motd

echo "[entrypoint] flag ready at /tmp/flag, starting sshd on 2222"
exec /usr/sbin/sshd -D -e -f /opt/ctf/sshd_config