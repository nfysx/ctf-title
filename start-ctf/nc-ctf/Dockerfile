FROM php:7.4-cli-alpine

# 裸 TCP 服务：不起 Apache，只跑一个 PHP CLI 的 socket 循环。
# 端口必须用高位端口（1337）：平台 restricted 模式会丢 CAP_NET_BIND_SERVICE，
# 那时连 root 也绑不了 1024 以下。
COPY src/ /app/
EXPOSE 1337

ENV BIND_ADDR=0.0.0.0 \
    BIND_PORT=1337

CMD ["php", "/app/server.php"]