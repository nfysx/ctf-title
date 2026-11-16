FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive \
    PYTHONUNBUFFERED=1

RUN apt-get update \
 && apt-get install -y --no-install-recommends python3 python3-pip socat \
 && rm -rf /var/lib/apt/lists/* \
 && pip3 install --no-cache-dir pycryptodome

WORKDIR /app
COPY src/server.py /app/server.py
COPY src/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh \
 && python3 -c "import os; open('/app/key','wb').write(os.urandom(16))"

# 平台会 drop NET_BIND_SERVICE，1337 > 1024，不受影响
EXPOSE 1337
ENTRYPOINT ["/app/entrypoint.sh"]
