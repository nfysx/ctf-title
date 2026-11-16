FROM ubuntu:22.04

RUN apt-get update \
 && apt-get install -y --no-install-recommends gcc libc6-dev socat \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY src/pwn.c /app/pwn.c
RUN gcc -o /app/pwn /app/pwn.c -fstack-protector-all -no-pie
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

EXPOSE 1337
ENTRYPOINT ["/app/entrypoint.sh"]
