FROM ubuntu:22.04
RUN apt-get update -qq && apt-get install -y -qq upx-ucl file && rm -rf /var/lib/apt/lists/*
WORKDIR /work
ENTRYPOINT []
