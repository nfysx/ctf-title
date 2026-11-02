# 题解：DNS 里的悄悄话

> 对应第三课 p18 Traffic · p36 流量五步 · 要查 DNS 记录类型

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | DNS、隧道、base32、过滤 |
| 难度 | 3 / 3 |
| 分值 | 260 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

几十条 DNS 查询，只有一组编号子域是真隧道，把分片拼起来 base32 解码。

### 附件

| 文件 | 说明 |
|---|---|
| `files/capture.pcap` | 附件 |

## 二、手把手解题

### Step 1｜先看协议分布

```
dns
```

会发现大量形如 `01.XXXX.t.itns.local` 的查询 —— 正常业务不会这么查。

### Step 2｜分辨真假

`www` / `api` / `mail` 是噪声，另有 `NN.XXXX.cdn.itns.local` 是干扰组。
真正的隧道是 **`NN.<分片>.t.itns.local`** 这一族（后缀是 `t.`）。

### Step 3｜按编号排序拼起来

```python
import re, base64
pat = re.compile(r'^(\d+)\.([A-Z2-7]+)\.t\.itns\.local$')
hits = sorted((int(m.group(1)), m.group(2)) for m in (pat.match(n) for n in names) if m)
b32 = ''.join(h[1] for h in hits)
b32 += '=' * (-len(b32) % 8)
print(base64.b32decode(b32).decode())
```

### Step 4｜为什么是 base32

DNS 标签只允许 `A-Z 0-9 -`，base64 里的 `+ / =` 会被拒 —— 所以隧道一般用 **base32** 编码。


## 三、为什么能成（原理）

DNS 隧道是真实存在的隐蔽信道：把数据编码进子域名，由内网机器不断发起解析，数据就从防火墙的 53 端口漏出去。
**识别特征**：某域名下大量随机化、等长、带编号的子域；查询量大但响应极小。
可以查一下 `iodine`、`dnscat2` 两个工具。

## 四、怎么防（延伸）

出口 DNS 做白名单 + 长度/熵检测；内网禁止绕过内部解析器直连外部 DNS。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{dn5_1s_4_5n34ky_ch4nn3l}`。
