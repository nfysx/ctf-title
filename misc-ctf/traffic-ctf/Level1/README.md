# 题解：流量里的那一次请求

> 对应第三课 p18 Traffic · p36 流量题五步

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 流量分析、pcap、base64 |
| 难度 | 2 / 3 |
| 分值 | 200 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

一个 pcap，几十个包里只有一次请求带着东西 —— 先统计，再过滤，最后解码。

### 附件

| 文件 | 说明 |
|---|---|
| `files/capture.pcap` | 附件 |

## 二、手把手解题

### Step 1｜先看统计（别逐包翻）

Wireshark 打开 `capture.pcap`：

```
统计 → 协议分级
```

会看到 DNS、HTTP（80）、以及一段疑似 TLS（443）。关键在 HTTP。

### Step 2｜过滤 HTTP

```
http.request
```

只有两个请求：一个 `/index.html`（噪声），一个 `/api/log?q=...`。

### Step 3｜看请求行

`/api/log?q=` 后面是一长串 base64。右键 → 跟踪 TCP 流也能看到完整报文。

### Step 4｜解码

```bash
python -c "import base64;print(base64.b64decode('<那串>').decode())"
```


## 三、为什么能成（原理）

流量题的核心不是「读完所有包」，而是**缩小范围**：
协议统计 → 过滤目标流量 → 跟踪单条会话 → 导出/解码。
真实网络里绝大部分包都是噪声，工具的意义就是帮你把这些噪声排掉。

## 四、怎么防（延伸）

明文传输 = 全裸：HTTP 的 URL、Cookie、表单在网络上是可见的。
这就是为什么登录、支付必须走 HTTPS —— 而且敏感参数不该放在 URL 里（会进日志、进 Referer）。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{pcap_n3v3r_l13s}`。
