FROM ubuntu:22.04
RUN apt-get update -qq && apt-get install -y -qq gcc binutils gdb file upx-ucl make python3 && rm -rf /var/lib/apt/lists/*
WORKDIR /w
