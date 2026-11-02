# 题解：自己逆的私有协议

> 对应第三课 p18 Traffic · 协议逆向（要查资料 + 写解析器）

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | 协议逆向、帧结构、CRC、XOR |
| 难度 | 3 / 3 |
| 分值 | 300 |
| 附件 | 见下 |
| Flag | 静态（`flag{...}`），checker 见 `checker/main.rx` |

非标端口上的私有协议：帧头要自己猜出来，payload 还做了异或。

### 附件

| 文件 | 说明 |
|---|---|
| `files/capture.pcap` | 附件 |

## 二、手把手解题

### Step 1｜别用 HTTP 视角看

端口 9001，Follow TCP Stream 一看就是乱码 —— 不是文本协议。

### Step 2｜找规律

```bash
tshark -r files/capture.pcap -q -z follow,tcp,raw,1
```

或者导出 raw 后 `xxd`：会反复看到 `49 54 4e 53`（ASCII 的 `ITNS`）—— 这是**魔数**，每一帧都从它开始。

### Step 3｜猜帧结构

```
ITNS | type(1) | seq(2,BE) | len(2,BE) | payload(len) | crc16(2,BE)
```

验证方法：`len` 字段和下一个 `ITNS` 的间距对得上；末尾两字节用 CRC16-CCITT 能算出同样的值。

### Step 4｜payload 还异或了

字节分布明显偏离文本（`flag` 的 `66 6c 61 67` 变成 `3c 36 3b 3d`）→ 试单字节异或 `0x5A`。

### Step 5｜写解析器

```python
i, out = 0, b''
while i + 11 <= len(s):
    if s[i:i+4] != b'ITNS':
        i += 1
        continue
    plen = struct.unpack_from('>H', s, i + 7)[0]
    body = s[i+9:i+9+plen]
    out += bytes(b ^ 0x5A for b in body)
    i += 9 + plen + 2
print(out)
```


## 三、为什么能成（原理）

这是最接近**真实逆向**的一类流量题：没有文档，只能从字节里找规律。
套路固定：①找重复出现的魔数 ②量字段宽度（2 字节长度？大端还是小端？）③校验字段试常见算法（CRC16 / CRC32 / 求和）④内容不像明文就试单字节异或。
CRC16 的常见变体（CCITT / IBM / MODBUS）都能在网上查到参数表，逐个试。

## 四、怎么防（延伸）

自己设计协议也要加魔数 + 长度 + 校验；但**别指望私有协议当安全**，抓包就能逆。

## 五、平台接入（ret2shell）

- `checker/main.rx` 是可直接用的静态 flag 版本：`check()` 比对答案，`environ()` 返回空对象
- 想改成**动态附件评测**：用平台自带的 `mapped` 预设替换这个文件，
  由平台把附件按用户映射并注入每用户唯一的 flag，本文件的 `check` 逻辑就不再需要
- 附件型题目不需要容器镜像，直接把 `files/` 挂到题目附件上就行

## 六、自校验

```bash
python verify.py
```

脚本会按上面的解法跑一遍并断言结果等于 `flag{r3v3r53_th3_fr4m3}`。
