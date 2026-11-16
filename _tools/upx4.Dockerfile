FROM ubuntu:24.04
RUN apt-get update -qq && apt-get install -y -qq upx-ucl && rm -rf /var/lib/apt/lists/*
