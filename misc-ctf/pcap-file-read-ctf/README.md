# 那一次入侵

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | HTTP 流量分析、目录爆破识别、任意文件读取、Spring Boot fat jar 结构、Java PBEWithMD5AndDES（PBES1）解密 |
| 类型 | Misc · 流量分析 |
| 难度 | 3 / 5 |
| 附件 | `SU_Checkin.pcapng`（10,778,924 B） |
| Flag | `flag{pcap_arbitrary_file_read_then_pbe_decrypt}` |

## 题面（直接复制到平台「简介」）

一次针对内部系统的入侵，流量留在了一份 `SU_Checkin.pcapng` 里（约 10 MB）。里面有目录扫描，也有若干次带着文件名的 HTTP 请求。目标：从流量里还原攻击者做了什么、拿到了什么，最后取出 flag。

**提示**：请求参数里带文件名的时候，值得多想一步它会指向哪里。

## 三、解题步骤

### Step 1｜先筛掉噪音

包里 45,268 个数据包，绝大多数是目录爆破。用 Wireshark 过滤：

```text
http.response.code == 200
```

全包只有 **8** 条 200。挨个看，发现一条明显的任意文件读取：

```http
GET /download?filename=../../../../../../../../../root/flag.txt HTTP/1.1
```

响应是 `nonono`（诱饵）。但这个 `/download?filename=` 确实是任意文件读取。

### Step 2｜沿着任意文件读取把情报挖干净

| 读到的文件 | 关键内容 |
|---|---|
| `/proc/self/cmdline` | `java\0-jar\0suctf-0.0.1-SNAPSHOT.jar\0--password=SePassWordLen23SUCT` |
| `/proc/self/cwd/BOOT-INF/classes/hint` | `algorithm=PBEWithMD5AndDES` |
| `/proc/self/cwd/BOOT-INF/classes/application.properties` | `OUTPUT=ElV+...` |

`/proc/self/cmdline` 里的口令**是完整的**（末尾那个 `\0` 是 cmdline 的分隔符，不是截断）：
**`SePassWordLen23SUCT`**，18 个字符。

### Step 3｜解开 OUTPUT

`OUTPUT` 做 Base64 解码得到 **56 字节** = `8 字节 salt + 48 字节 DES-CBC 密文`
（48 是 8 的整数倍；明文经 PKCS#5 填充后正好 48，说明明文在 41~48 字节之间）。

Java 的 `PBEWithMD5AndDES` 就是 PKCS#5 的 PBES1：

```text
T1  = MD5(password || salt)
Ti  = MD5(T(i-1))          , i = 2 .. iterationCount
key = T(iterationCount)[0:8]     # 8 字节 DES 密钥
IV  = key                        # PBES1 里 IV 和 key 相同
```

口令已知，**只剩迭代次数**：从 1 往上试（本题是 1000，也是 Java 示例里最常见的值）。

```python
import hashlib, base64
from Crypto.Cipher import DES

PW, ITERS = 'SePassWordLen23SUCT', 1000
blob = base64.b64decode('ElV+bGCnJYHVR8m23GLhprTGY0gHi/tNXBkGBtQusB/zs0uIHHoXMJoYd6oSOoKuFWmAHYrxkbg=')
salt, ct = blob[:8], blob[8:]

key = hashlib.md5(PW.encode() + salt).digest()
for _ in range(ITERS - 1):
    key = hashlib.md5(key).digest()
key = key[:8]

pt = DES.new(key, DES.MODE_CBC, key).decrypt(ct)
print(pt[:-pt[-1]].decode())        # 去掉 PKCS#5 填充
```

输出：`flag{pcap_arbitrary_file_read_then_pbe_decrypt}`

### Step 4｜不想写脚本就用 Wireshark 定位

```text
http.response.code == 200 && http.content_type contains "octet-stream"
```

8 条 200 里有 7 条是 `/download` 的返回，右键任一包 → Follow → HTTP Stream 就能看到完整响应体。

## 四、原理

- **任意文件读取的危害不是"读 flag.txt"**：`flag.txt` 被换成 `nonono` 一样能被打穿：
  读 `/proc/self/cmdline` 拿启动参数（含口令）、读 `/proc/self/cwd/` 拿工作目录里的配置与源码，
  组合起来等于拿到了服务端的全部内情。
- **Java 的 PBEWithMD5AndDES 是 PBES1**：密钥和 IV 都由"口令 + salt + 迭代次数"派生，导出的是 base64(salt||密文)。
  参数固定时它是可逆推的；口令能读到时，剩下的只是一次字典/范围爆破。
- **Spring Boot fat jar 的目录结构是可预测的**：`BOOT-INF/classes/` 下就是业务配置，
  路径固定 → 拿到任意文件读取后几乎必然能读到。

## 五、防御建议

- `filename` 这类参数必须白名单：解析后取 basename，并校验规范化路径仍在允许目录内。
- 不要把敏感信息放在命令行参数里（`ps` / `/proc/*/cmdline` 会暴露）；用环境变量文件或密钥管理服务。
- 加密用的 salt / 迭代次数可以公开，但**口令不能**；更不要用 DES 这类已淘汰的算法，换 AES-GCM。
- 容器内禁止 `/proc/self/cwd`、`/proc/self/environ` 等路径被业务逻辑读到。

## 六、附件清单

| 文件 | 大小 | SHA-256 |
|---|---|---|
| `SU_Checkin.pcapng` | 10,778,924 B | `e94df614d88641d5a5f8133950f5d0a8754a7974edd871b877bf28c7da78a34a` |

---


