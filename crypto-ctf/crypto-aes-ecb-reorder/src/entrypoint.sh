#!/bin/sh
set -e
# 注意：不要写 printf '%s' "${FLAG:-flag{local_default}}"。
# sh 的 ${VAR:-word} 在第一个 } 处结束 word，flag{...} 里的 } 会被当成收尾，
# 结果多出一个字面量 "}"（实测输出 flag{ok}}）。这里改成先判空再赋值。
if [ -z "${FLAG:-}" ]; then
    FLAG='flag{local_default}'
fi
printf '%s' "$FLAG" > /app/flag
exec socat TCP-LISTEN:1337,reuseaddr,fork EXEC:"python3 /app/server.py",stderr
