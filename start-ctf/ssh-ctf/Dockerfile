FROM alpine:3.20

# 国内构建：走阿里云 apk 源（本地已缓存 alpine:3.20，构建不再联网拉基础镜像）
RUN sed -i "s|dl-cdn.alpinelinux.org|mirrors.aliyun.com|g" /etc/apk/repositories \
 && apk add --no-cache openssh \
 && rm -rf /var/cache/apk/*

# 题目用户：xaiu / sec（密码就是题面里写死的那个）
RUN adduser -D -s /bin/sh xaiu && echo "xaiu:sec" | chpasswd

COPY src/ /opt/ctf/
RUN chmod +x /opt/ctf/entrypoint.sh

EXPOSE 2222
ENTRYPOINT ["/opt/ctf/entrypoint.sh"]