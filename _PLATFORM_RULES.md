# ret2shell 平台 · 收题规则（改造/新增题目都必须遵守）

> 依据：`D:\DSH\ret2shell\upstream` 源码 + `start-ctf/_platform/平台上传参考.md`

## 一、两条硬约束（违反就不能上平台）

### 1. 容器必须监听 8080，绝不能是 80

平台起容器时（`crates/cluster/src/manager.rs:533`）以 restricted 方式运行并 **drop `NET_BIND_SERVICE`**：

```rust
let restricted_security_context = SecurityContext {
    capabilities: Some(Capabilities {
        drop: Some(vec!["NET_BIND_SERVICE".to_owned()]),
    ...
```

丢掉这个 capability 后，**连 root 都绑不了 1024 以下的端口**，Apache 直接起不来
（Pod 显示 Running，但端口拒连，日志 `Permission denied: AH00072: make_sock`）。

标准 Dockerfile（php:7.4-apache）：

```dockerfile
FROM php:7.4-apache

# 平台（k8s + restricted）会丢掉 CAP_NET_BIND_SERVICE，root 也绑不了 80，统一监听 8080
RUN sed -ri 's/^Listen 80$/Listen 8080/' /etc/apache2/ports.conf \
 && sed -ri 's/<VirtualHost \*:80>/<VirtualHost *:8080>/' /etc/apache2/sites-available/000-default.conf

COPY src/ /var/www/html/
EXPOSE 8080
```

docker-compose.yml 里映射右侧也要改成 8080，宿主机端口保持不变：

```yaml
ports:
- 8085:8080        # 左边是本地端口（各题不同，别改），右边必须是容器内的 8080
```

非 apache 的镜像同理：只要容器内的监听端口 ≥1024 且与 `env.toml` 的 `port` 一致即可。

### 2. 每道题必须有 `checker/main.rx`

平台判题只跑这个脚本（`crates/checker/src/lib.rs`）。缺了就等于**无法判定对错**，
提交什么都判错。必须同时导出 `check` 与 `environ` 两个函数。

## 二、两种 checker 模板（照抄，别自己发明）

### A. 静态 flag（附件型：misc / RE）

> ⚠ **最后一行不要写分号**。Rune 里 `Ok(...);` 是语句，函数返回 `()`，平台 `parse_value` 会按
> `(bool, String, Option<Object>)` 解析失败 → 所有人判错。平台自带的 `dynamic-uuid.rx` 与
> `example_challenge/checker/main.rx` 都是不带分号的写法。

```rhai
// checker/main.rx · <题目名>
// 静态 flag 版本：check 比对提交内容，environ 返回空对象（附件型题目没有容器）
const FLAG = "<本题真实 flag>";

pub fn check(bucket, user, team, submission) {
    if submission.content == FLAG {
        return Ok((true, "Correct!", None));
    }
    Ok((false, "Incorrect!", None));
}

pub async fn environ(bucket, user, team) {
    Ok(#{})
}
```

### B. 动态 flag（有容器的 Web 题）

照抄平台自带模板 `web/src/lib/blocks/challenge/scripts/dynamic-uuid.rx`，
**只把两个密钥换成该题自己的随机串**（ENCRYPT_KEY 24 位、HASH_KEY 32 位，每题不同）。
`environ` 会按队伍 id 生成互不相同的 `FLAG`，容器里 `getenv('FLAG')` 读到即可；
`check` 会反解出队伍 id，别人把 flag 给你会被判作弊审计。

## 三、目录约定

```text
<分类>/<题目名>/
├── checker/main.rx      ← 必须有
├── Dockerfile           ← 有容器的题
├── docker-compose.yml   ← 有容器的题
├── src/                 ← 有容器的题的源码（含 flag.inc.php）
├── files/               ← 附件型题目的附件
└── README.md            ← 题解（考点/难度/端口/Flag/解题/原理/防御）
```

## 四、验收（每题都要过）

1. `docker compose up -d --build` 起得来；
2. `docker run --cap-drop NET_BIND_SERVICE -e FLAG='flag{ok}' -p <port>:8080 <image>` 后端口有响应
   （**必须带 `--cap-drop NET_BIND_SERVICE`**，这才是平台的真实姿势）；
3. `checker/main.rx` 语法自检：`check`/`environ` 都在，标记为 `pub`。
