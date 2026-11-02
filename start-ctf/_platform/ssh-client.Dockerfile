FROM alpine:3.20
RUN sed -i "s|dl-cdn.alpinelinux.org|mirrors.aliyun.com|g" /etc/apk/repositories \
 && apk add --no-cache openssh-client sshpass \
 && rm -rf /var/cache/apk/*
ENTRYPOINT ["sh"]