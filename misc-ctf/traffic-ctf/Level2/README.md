# 题解：分块传输的日志

> 对应第三课 p18 Traffic · p36 流量五步

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | HTTP、chunked、TCP 重组 |
| 难度 | 2 / 3 |
| 分值 | 220 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

响应用了 chunked 编码，flag 被切三段夹在日志行里；TCP 段还乱序、重传过一次。

### 附件

| 文件 | 说明 |
|---|---|
| `files/capture.pcap` | 附件 |

## 二、手把手解题

### Step 1｜先过滤出 HTTP

```
http.response
```

能看到 `Transfer-Encoding: chunked` —— 说明**没有 Content-Length**，body 是一块一块发过来的。

### Step 2｜跟着 TCP 流看

右键 → Follow → TCP Stream。Wireshark 会按 seq 自动重排，乱序和重传都不是障碍。

### Step 3｜读懂分块格式

```
<长度(十六进制)>
<数据>
<长度(十六进制)>
<数据>
0

```

每块开头那行是**十六进制长度**（比如 `1a` = 26 字节），最后一块长度是 `0`。

### Step 4｜筛掉噪声再拼

分块是交替的：一块 flag 片段、一块 `-- log line N -- 日志行`。
**丢掉以 `-- ` 开头的块**，剩下的按顺序接起来就是完整 flag。
Wireshark 里可以右键 → Export Objects，或自己写二十行脚本。


## 三、为什么能成（原理）

chunked 是 HTTP/1.1 的流式传输：服务器不知道总长度时，就一块块发，末尾用 0 长度收尾。
初学者常在包里直接 `strings` 找，看到半截就以为没有 —— **必须把分块拆干净再拼**。
另外这题故意让 TCP 段乱序加重传：**抓包顺序不等于真实顺序**，要按 seq 排。

## 四、怎么防（延伸）

日志类接口别用 chunked 传敏感数据；代理/网关要能正确重组后再做内容检测。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{chunk3d_4nd_r34553mbl3d}`。
