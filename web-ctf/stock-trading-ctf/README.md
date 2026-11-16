# 股海惊涛

## 一、题目与考点

| 项 | 值 |
|---|---|
| 考点 | HTTP 抓包改包、客户端可控参数、服务端会话状态、条件参数的获取与拼装 |
| 类型 | Web · 抓包改包 |
| 难度 | 3 / 5 |
| 端口 | 8093（容器内 8080） |
| Flag | `flag{change_money_and_carry_both_conditions}`（页面输出的是它的 **Base64**） |

## 题面（直接复制到平台「简介」）

一个全自动的模拟炒股页面：5 万本金，终点一亿，每秒自动过一个交易日。
页面自己跑，你不用操作，但每一拍都会向后端发一个请求。
目标是到达目标金额那一刻，请求里同时带上路上的两个条件，缺一个只会看到「错过了路上的风景」：浏览器打开 http://<地址>:8093/ 。

**提示**：页面报给服务端的余额，服务端会照单全收；另外你路过的那些节点也各留下了一个值。

## 三、解题步骤

### Step 1｜先看清请求长什么样

浏览器打开 `http://<题目地址>:8093/`，F12 → Network，每一拍都有一个：

```http
POST /index.php HTTP/1.1
Content-Type: application/x-www-form-urlencoded; charset=UTF-8

ajax=1&since=4&tick=1&money=96850
```

`money=` 是**余额上报**：服务端会采纳它，所以抓包改这里的值就能改金额。

### Step 2｜抓两个条件参数名

两个条件的参数名是「固定前缀 + 20 位十六进制」，每个会话随机、源码里看不到：

| 条件 | 参数名 | 什么时候出现在包里 |
|---|---|---|
| A · 破产 | `bankrupt_has_<20 hex>` | 破产那一刻的**下一拍**（只出现这一拍） |
| B · 走过四五千万 | `on_the_way_<20 hex>` | 资金落在 4500 万 ~ 5500 万这一档的**每一拍** |

两个条件都不留痕，其他时候请求里没有它们。所以**看到的那一刻就要把名字记下来**。

### Step 3｜把金额送到一亿

截住任意一拍，把 `money` 改成一亿附近（例如 `100000000`）后放行。
服务端采纳上报值后，会在过天时判定"到站"；页面随后就会显示「错过了路上的风景」。

> 金额只认数字。改包时别把界面上的 `¥100,000,000` 原样粘进去。

### Step 4｜把两个条件一次性补上

到站之后，在请求里补上两个条件再发一次：

```http
POST /index.php HTTP/1.1
Content-Type: application/x-www-form-urlencoded

ajax=1&since=0&tick=1&money=100000000&bankrupt_has_<你的A>=true&on_the_way_<你的B>=true
```

响应里的 `flag` 字段就是 Base64 后的 flag：

```bash
echo 'ZmxhZ3tjaGFuZ2VfbW9uZXlfYW5kX2NhcnJ5X2JvdGhfY29uZGl0aW9uc30=' | base64 -d
# flag{change_money_and_carry_both_conditions}
```

## 四、原理

- **服务端信任了客户端上报的余额**：`index.php` 里，请求带 `money` 就直接采纳并判定到站。
  任何"由客户端决定的服务端状态"都能这样被打穿。
- **条件参数的"生命周期"就是它的强度来源**：只在满足时出现 → 正常玩到一亿时两个都不满足 → 必须靠抓包补齐。
  参数名每个会话随机，逼着选手真的去看报文而不是猜。
- **业务状态必须服务端唯一持有**：余额、胜负、任务进度都不该由前端同步。

## 五、防御建议

- 客户端只能报"意图"（买/卖/查询），不能报"结果"（余额/积分/进度）；服务端自己算、自己存。
- 一次性动作加幂等键与服务端时间戳，拒绝回放与超前提交。
- 会话级随机数要用密码学随机（`random_bytes`），不要用可预测的自增/时间戳。

## 六、动态 Flag

`src/flag.inc.php` 优先读环境变量 `FLAG`（填**明文**），未注入时用内置默认值。

```bash
cd 07-stock-trading-web
FLAG='flag{change_money_and_carry_both_conditions}' docker compose up -d --build
# 浏览器打开 http://127.0.0.1:8080/
```

页面输出的是它的 Base64，所以最后一步要解码。

## 七、附件清单

| 文件 | 大小 | SHA-256 |
|---|---|---|
| `src/index.php` | 41,862 B | `82b7a71bd6269ca8d3af0070ccc41ea2468885b81bd5c4ecd1d2a12a8e878e17` |
| `src/flag.inc.php` | 478 B | `4cddaef1acd787a612774e4e0ecbebe14c65765b268d22ea2c71eedac0dca29b` |
| `Dockerfile` | 395 B | `b33ca97ce70d3ea9612031fd17b4c180eee73066b58d032c1a34f1647025fc10` |
| `docker-compose.yml` | 260 B | `d6ab50c1f6bfce79c39d174cdeb3c7fcae90d4c1a2caede60a8c961bda46b220` |

---

## 平台镜像配置（添加镜像时照这张表填）

| 字段 | 填什么 | 说明 |
|---|---|---|
| 容器名称 | `stock-trading-ctf` | 与题目目录名、tar 文件名保持一致 |
| 镜像标签 | `<registry>/<bucket>/stock-trading-ctf` | 仓库名取自 tar 文件名（去扩展名），上传后 UI 自动带出 |
| 镜像版本 | 上传后生成的 Unix 时间戳 | 下拉选最新那条，不要手填 |
| 镜像拉取策略 | `Always` | 与平台默认一致 |
| 服务描述 | HTTP 站点（浏览器访问） | 只是备注，不影响运行 |
| 传输协议 | `tcp` | 全库只有 TCP 服务 |
| 应用协议 | `http` | HTTP 站点选 http |
| **服务端口** | **`8080`** | 容器内实际监听端口，必须与 Dockerfile 的 `EXPOSE` 一致 |
| CPU 限制 | `0.1, 0.5` | 最小, 最大（核） |
| 内存限制（MB） | `32, 128` | 最小, 最大 |
| 存储限制（MB） | `64, 1024` | 最小, 最大 |
| restricted | **勾选** | 平台会 drop `NET_BIND_SERVICE`，所以容器监听的是 `8080` 而不是 80 |
| 出网 | **关闭** | 本题不需要外网 |

> 端口必须填 **8080**。历史上按 80 填过的记录要改过来：容器里 Apache 已改成 `Listen 8080`，填 80 会连不上。
