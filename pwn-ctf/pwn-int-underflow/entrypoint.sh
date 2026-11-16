#!/bin/sh
set -e
# 平台通过环境变量注入 FLAG；未注入时用本地默认值，便于离线自测。
DEFAULT_FLAG='flag{local_default}'
printf '%s' "${FLAG:-$DEFAULT_FLAG}" > /app/flag
chmod 644 /app/flag
exec socat TCP-LISTEN:1337,reuseaddr,fork EXEC:/app/pwn
