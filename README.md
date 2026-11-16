# CTF 题库

> 面向 **ret2shell** 比赛平台（`http://10.24.10.232/`，游戏 `id=4`「2026 迎新赛」，team_size=3）的完整题库。
> 共 **94** 道题、6 个分类，其中 **12 道是各方向的「引入题」**（表中带 ☆），每题都带平台判题脚本 `checker/main.rx`。
>

---

## 一、分类总览

| 分类 | 题数 | 引入题 | 形态 | 说明 |
|---|---|---|---|---|
| [`start-ctf/`](./start-ctf/) | 5 | 2 | 容器 + 附件 | 平台入门三连（HTTP / nc / ssh）+ 读规则、查环境 |
| [`web-ctf/`](./web-ctf/) | 15 | 2 | 容器 + 附件 | 看源码、读报文、Cookie、SQLi、上传、XSS、信息泄露 |
| [`pwn-ctf/`](./pwn-ctf/) | 12 | 2 | 容器 + 附件 | 工具四件套、gdb 入门，再到栈溢出 / ret2libc / UAF |
| [`crypto-ctf/`](./crypto-ctf/) | 12 | 2 | 附件 + 容器 | 编码与逆元入门，再到 RSA 三种攻击、ECB 块重排、LCG |
| [`misc-ctf/`](./misc-ctf/) | 35 | 2 | 附件 | strings / xxd 入门，图片隐写、文件类型、流量分析 |
| [`RE-ctf/`](./RE-ctf/) | 15 | 2 | 附件 | 工具链与反编译入门，UPX、SMC、反调试、字节码 VM |

**☆ 引入题**的定位是该方向的**第一课**：教工具链和思维方式，不考漏洞或算法。
每个分类里先做这两道，再进主干题，坡度才顺。

---

## 二、目录结构

```text
D:\ctf\
├── README.md                  ← 本文件
├── _PLATFORM_RULES.md         ← 收题硬约束（端口 / checker 模板 / 目录约定）
├── _tools/                    ← 出题工具（UPX 打包镜像的 Dockerfile）
└── start-ctf/  web-ctf/  pwn-ctf/  crypto-ctf/  misc-ctf/  RE-ctf/
    └── <题目>/
        ├── checker/main.rx    ← 必须有
        ├── README.md          ← 题解（六节）
        ├── Dockerfile / docker-compose.yml / src/   ← 容器题
        ├── files/             ← 附件题下发给选手的东西
        └── exp/               ← 解题脚本，不对外发布
```

---

## 三、平台收题的六条硬约束

前三条从平台源码（`D:\DSH\ret2shell\upstream`）读出并实测；后三条是本轮出题时踩出来的。

### 1｜容器必须监听 8080（HTTP）或 ≥1024 的高位端口

平台以 restricted 起容器并 **drop `NET_BIND_SERVICE`**（`crates/cluster/src/manager.rs:533`），
丢掉后 **root 也绑不了 1024 以下端口**。HTTP 题统一 8080，socat/nc 题统一 1337：

```dockerfile
FROM php:7.4-apache
RUN sed -ri 's/^Listen 80$/Listen 8080/' /etc/apache2/ports.conf \
 && sed -ri 's/<VirtualHost \*:80>/<VirtualHost *:8080>/' /etc/apache2/sites-available/000-default.conf
COPY src/ /var/www/html/
EXPOSE 8080
```

### 2｜每题必须有 `checker/main.rx`，且 `check`/`environ` 都要导出

判题只跑这个脚本，缺了等于提交什么都判错。

### 3｜`check` 的最后一行**不要写分号**

Rune 里 `Ok((false, "Incorrect!", None));` 带分号是**语句**，函数返回 `()`；
平台随后拿 `(bool, String, Option<Object>)` 去 `parse_value`，直接报错 → 全员判错。

```rhai
pub fn check(bucket, user, team, submission) {
    if submission.content == FLAG {
        return Ok((true, "Correct!", None));
    }
    Ok((false, "Incorrect!", None))     // ← 这里不能有分号
}
```

### 4｜entrypoint 写 flag 时不能用 `${FLAG:-flag{...}}`

`${VAR:-word}` 在**第一个 `}`** 处结束 word，flag 自带的 `}` 被当收尾，剩下一个 `}` 变字面量：

```bash
docker run --rm -e "FLAG=flag{ok}" ubuntu:22.04 sh -c 'printf "%s" "${FLAG:-flag{local_default}}"'
# 输出 flag{ok}}     ← 多一个右花括号
```

正确写法（默认值用 decoy，不要用真 flag）：

```sh
if [ -z "${FLAG:-}" ]; then FLAG='flag{local_default}'; fi
printf '%s' "$FLAG" > /app/flag
```

### 5｜Pwn 的 backdoor 函数要加 `force_align_arg_pointer`

ret 进入函数时栈少对齐 8 字节，glibc 2.35 的 `system`/`printf` 会死在 `movaps`。
另外 glibc 2.34+ 不再生成 `__libc_csu_init`，小二进制里没有现成的 `pop rdi; ret`，ret2libc 题要自己留一个。

### 6｜动态 flag 的密钥每题必须不同

`dynamic-uuid` 模板里的 `ENCRYPT_KEY`(24 位) 与 `HASH_KEY`(32 位) 一旦复制成同一份，
所有队伍拿到的 flag 就一样了，`check` 里的作弊审计也失效。

---

## 四、题目清单

### start-ctf（5 题，☆ 为引入题）

| 题目 | 形态 | 端口 | 难度 | Flag |
|---|---|---|---|---|
| `connect-ctf` | 容器 | — | — | 动态（按队伍） |
| `nc-ctf` | 容器 | — | — | 动态（按队伍） |
| `ssh-ctf` | 容器 | — | — | 动态（按队伍） |
| `start-hello-flag` ☆ | 附件 | — | 1 / 5 | `flag{you_read_the_rules_before_playing}` |
| `start-hello-toolbox` ☆ | 附件 | — | 1 / 5 | `flag{environment_ready_before_the_game}` |

### web-ctf（15 题，☆ 为引入题）

| 题目 | 形态 | 端口 | 难度 | Flag |
|---|---|---|---|---|
| `backup-ctf` | 容器 | 8083 | 1 | 动态（按队伍） |
| `cookie-ctf` | 容器 | 8085 | 1 | 动态（按队伍） |
| `encode-ctf` | 容器 | 8089 | 1 | 动态（按队伍） |
| `header-ctf` | 容器 | 8087 | 1 | 动态（按队伍） |
| `http-message-ctf` | 容器 | 8080 | 2 | 动态（按队伍） |
| `infoleak-demo` | 容器 | — | — | 动态（按队伍） |
| `robots-ctf` | 容器 | 8082 | 1 | 动态（按队伍） |
| `sqli-ctf` | 容器 | 8084 | 2 | 动态（按队伍） |
| `status-ctf` | 容器 | 8088 | 1 | 动态（按队伍） |
| `stock-trading-ctf` | 容器 | 8093（容器内 8080） | 3 / 5 | 动态（按队伍） |
| `upload-ctf-1` | 容器 | 8086 | 1 | 动态（按队伍） |
| `upload-ctf-2` | 容器 | 8091 | 2 | 动态（按队伍） |
| `web-hello-headers` ☆ | 附件 | 无（纯附件，无容器） | 1 | `flag{read_the_response_headers}` |
| `web-hello-source` ☆ | 附件 | 无（纯附件，无容器） | 1 | `flag{view_source_is_the_first_move}` |
| `xss-reflect-ctf` | 容器 | 8081 | 1 | 动态（按队伍） |

### pwn-ctf（12 题，☆ 为引入题）

| 题目 | 形态 | 端口 | 难度 | Flag |
|---|---|---|---|---|
| `pwn-canary-leak` | 容器 | — | 中 | 动态（按队伍） |
| `pwn-fmt-leak` | 容器 | — | 易 | 动态（按队伍） |
| `pwn-fmt-write` | 容器 | — | 中 | 动态（按队伍） |
| `pwn-hello-gdb` ☆ | 附件 | — | 1 | `flag{gdb_is_your_steering_wheel}` |
| `pwn-hello-tools` ☆ | 附件 | — | 1 | `flag{file_checksec_strings_objdump}` |
| `pwn-int-underflow` | 容器 | — | 易 | 动态（按队伍） |
| `pwn-one-byte` | 容器 | — | 易 | 动态（按队伍） |
| `pwn-ret2libc` | 容器 | — | 难 | 动态（按队伍） |
| `pwn-ret2shellcode` | 容器 | — | 中 | 动态（按队伍） |
| `pwn-ret2text` | 容器 | — | 易 | 动态（按队伍） |
| `pwn-stack-check` | 容器 | — | 易 | 动态（按队伍） |
| `pwn-uaf` | 容器 | — | 难 | 动态（按队伍） |

### crypto-ctf（12 题，☆ 为引入题）

| 题目 | 形态 | 端口 | 难度 | Flag |
|---|---|---|---|---|
| `crypto-aes-ecb-reorder` | 容器 | — | — | `flag{aes_ecb_block_reorder}` |
| `crypto-classic` | 附件 | — | — | `flag{classic_cipher_warmup}` |
| `crypto-dh-leak` | 附件 | — | — | `flag{diffie_hellman_leaked_private_key}` |
| `crypto-encode-stack` | 附件 | — | — | `flag{stacked_encodings_all_the_way_down}` |
| `crypto-lcg-recover` | 附件 | — | — | `flag{lcg_state_recovery}` |
| `crypto-lesson-encoding` ☆ | 附件 | — | — | `flag{encoding_is_not_encryption}` |
| `crypto-lesson-inverse` ☆ | 附件 | — | — | `flag{769663931782882031}` |
| `crypto-otp-reuse` | 附件 | — | — | `flag{otp_key_reuse}` |
| `crypto-rsa-common-modulus` | 附件 | — | — | `flag{rsa_common_modulus_attack}` |
| `crypto-rsa-small-e` | 附件 | — | — | `flag{rsa_small_exponent_root}` |
| `crypto-rsa-wiener` | 附件 | — | — | `flag{rsa_wiener_small_private_exponent}` |
| `crypto-xor-known` | 附件 | — | — | `flag{known_plaintext_xor}` |

### misc-ctf（35 题，☆ 为引入题）

| 题目 | 形态 | 端口 | 难度 | Flag |
|---|---|---|---|---|
| `append-zip-ctf` | 附件 | — | 2 / 3 | `flag{4ppend3d_d4t4_4fter_1end}` |
| `enc-chain-ctf/Level1` | 附件 | — | 1 / 3 | `flag{b4s364_15_ju57_4_5t3p}` |
| `enc-chain-ctf/Level2` | 附件 | — | 1 / 3 | `flag{h3x_15_e4sy_t00}` |
| `enc-chain-ctf/Level3` | 附件 | — | 1 / 3 | `flag{3nc0d1ng_1s_n0t_3ncrypt10n}` |
| `exif-erase-ctf` | 附件 | — | 3 / 3 | `flag{3r4s3_3xif_4ft3r_dump}` |
| `exif-meta-ctf` | 附件 | — | 1 / 3 | `flag{m3t4d4t4_t4lks}` |
| `exif-thumb-ctf` | 附件 | — | 2 / 3 | `flag{thumbn41l_n3v3r_l13s}` |
| `fake-zip-ctf/Level1` | 附件 | — | 1 / 3 | `flag{c0mm3n7_15_4_f13ld}` |
| `fake-zip-ctf/Level2` | 附件 | — | 2 / 3 | `flag{f4k3_3ncrypt10n_b1t}` |
| `file-header-ctf/Level1` | 附件 | — | 1 / 3 | `flag{r3n4m3_1t_4nd_5e3}` |
| `file-header-ctf/Level2` | 附件 | — | 2 / 3 | `flag{h3ader_1s_th3_truth}` |
| `live-demo/demo-01` | 附件 | — | — | `flag{3xt3n510n_15_4_l13}` |
| `live-demo/demo-02` | 附件 | — | — | `flag{f1l3_n3v3r_l13s}` |
| `live-demo/demo-03` | 附件 | — | — | `flag{dr4g_1t_1nt0_7z1p}` |
| `live-demo/demo-04` | 附件 | — | — | `flag{6_d16175_15_n0th1ng}` |
| `live-demo/demo-05` | 附件 | — | — | `flag{y0ur_ph0t0_t4lk5}` |
| `live-demo/demo-06` | 附件 | — | — | `flag{thumbn41l_b3tr4y5_y0u}` |
| `lsb-stego-ctf/Level1` | 附件 | — | 1 / 3 | `flag{0n3_ch4nn3l_15_3n0ugh}` |
| `lsb-stego-ctf/Level2` | 附件 | — | 2 / 3 | `flag{lsb_1s_1n_th3_l4st_b1t}` |
| `magic-bytes-ctf` | 附件 | — | 1 | `flag{magic_bytes_reveal_the_real_type}` |
| `misc-hello-hexdump` ☆ | 附件 | — | 1 | `flag{hex_is_just_another_view}` |
| `misc-hello-strings` ☆ | 附件 | — | 1 | `flag{strings_shows_everything}` |
| `pcap-file-read-ctf` | 附件 | — | 3 / 5 | `flag{pcap_arbitrary_file_read_then_pbe_decrypt}` |
| `png-hidden-qr-ctf` | 附件 | — | 2 / 5 | `flag{png_height_and_trailing_whitespace_qr}` |
| `png-ihdr-ctf` | 附件 | — | 1 | `flag{png_ihdr_height_holds_the_flag}` |
| `png-text-ctf` | 附件 | — | 2 / 3 | `flag{t3xt_chunk_1s_4_th1ng}` |
| `starwalk-series/Level1` | 附件 | — | 1 / 3 | `flag{beijing_xiaomi_0926}` |
| `starwalk-series/Level2` | 附件 | — | 2 / 3 | `flag{hunan_zhangjiajie}` |
| `starwalk-series/Level3` | 附件 | — | 1 / 3 | `flag{chongqing_hongyadong_20241009_1325}` |
| `starwalk-series/Level4` | 附件 | — | 3 / 3 | `flag{shanghai_nanjingdonglu_yongan}` |
| `starwalk-series/Level5` | 附件 | — | 3 / 3 | `flag{shanghai_zhangjiajie_chongqing}` |
| `traffic-ctf/Level1` | 附件 | — | 2 / 3 | `flag{pcap_n3v3r_l13s}` |
| `traffic-ctf/Level2` | 附件 | — | 2 / 3 | `flag{chunk3d_4nd_r34553mbl3d}` |
| `traffic-ctf/Level3` | 附件 | — | 3 / 3 | `flag{dn5_1s_4_5n34ky_ch4nn3l}` |
| `traffic-ctf/Level4` | 附件 | — | 3 / 3 | `flag{r3v3r53_th3_fr4m3}` |

### RE-ctf（15 题，☆ 为引入题）

| 题目 | 形态 | 端口 | 难度 | Flag |
|---|---|---|---|---|
| `custom-b64-ctf` | 附件 | — | 2 | `flag{custom_b64_alphabet}` |
| `rev-anti-debug` | 附件 | — | 难 | `flag{anti_debug_hides_the_real_path}` |
| `rev-flip-branch` | 附件 | — | 易 | `flag{flip_the_branch_and_win}` |
| `rev-hello-decompile` ☆ | 附件 | — | 1 | `flag{decompiler_reads_the_logic}` |
| `rev-hello-toolchain` ☆ | 附件 | — | 1 | `flag{toolchain_in_three_commands}` |
| `rev-math-check` | 附件 | — | 中 | `flag{follow_the_transform_backwards}` |
| `rev-rc4` | 附件 | — | 中 | `flag{rc4_with_a_known_key}` |
| `rev-serial-eq` | 附件 | — | 易 | `flag{the_serial_is_a_system_of_equations}` |
| `rev-smc` | 附件 | — | 中 | `flag{self_modifying_code_at_runtime}` |
| `rev-strings` | 附件 | — | 易 | `flag{strings_is_enough}` |
| `rev-tiny-vm` | 附件 | — | 难 | `flag{tiny_vm_trace_it_out}` |
| `rev-upx` | 附件 | — | 易 | `flag{upx_packed_not_protected}` |
| `rev-xor-loop` | 附件 | — | 易 | `flag{xor_with_a_rolling_key}` |
| `stl-map-path-ctf` | 附件 | — | 5 / 5 | `flag{8b587367b99e5e2fcbdb6598da14b9bc}` |
| `teach-demo` | 附件 | — | — | `moectf{c0ngrats_y0u_l34rn3d_r3v3rs3}` |

> `teach-demo` 的 flag 是 `moectf{c0ngrats_y0u_l34rn3d_r3v3rs3}`，源自 MoeCTF 逆向教学工具的内嵌文案，前缀由原件决定，未改动。

---

## 五、本地怎么跑

```bash
cd web-ctf/cookie-ctf && docker compose up -d --build   # http://127.0.0.1:8085/
cd pwn-ctf/pwn-ret2text && docker compose up -d --build # nc 127.0.0.1 9102
```

**按平台真实姿势验证**（必须带 `--cap-drop`，否则测不出端口那个坑）：

```bash
docker run --rm -d --cap-drop NET_BIND_SERVICE -e "FLAG=flag{ok}" -p 18085:8080 cookie-ctf-web
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:18085/
```

> 基础镜像拉不动（Docker Hub 直连常超时）时走镜像站再打 tag：
> ```bash
> docker pull docker.m.daocloud.io/library/php:7.4-apache
> docker tag  docker.m.daocloud.io/library/php:7.4-apache php:7.4-apache
> ```

**附件题**：直接看题，不需要起服务。

---

## 六、怎么上传到 ret2shell 平台

「上传」= 传容器镜像 + 建题目定义。

```bash
pwsh ./start-ctf/_platform/upload-image.ps1 -Challenge pwn-ctf/pwn-ret2text -Token <token>
# 后台 /games/4/challenges → 创建题目
#   简介    <- README.md 正文（不要把题名写成一级标题）
#   Checker <- checker/main.rx 全文
#   环境    <- 镜像 + 端口（HTTP 8080 / TCP 1337）+ CPU/内存 + 勾 restricted + 关出网
#   答案    <- README.md 的题解部分
```

批量走 **git 题库仓库直推**，见 [`start-ctf/_platform/平台上传参考.md`](./start-ctf/_platform/)。

---

## 七、校验记录

| 项 | 结论 |
|---|---|
| **容器端口** | 全部 apache 容器题 `Listen 8080`；13 道 web 题逐个 build + `--cap-drop NET_BIND_SERVICE` 实跑，全部 HTTP 200 |
| **Pwn 12 题** | 10 道主干题逐个 build → 平台姿势起容器 → pwntools exp 走 TCP 打到 `flag{test123}`，10/10；2 道引入题在 `ubuntu:22.04` 容器里按 README 路线实跑取到 flag |
| **Crypto 12 题** | 11 道附件题跑 `exp/solve.py` 与 checker 逐字比对；`crypto-aes-ecb-reorder` 是容器题（TCP oracle），exp 走 TCP 做块重排取证 |
| **Reverse 12 题** | 10 道主干题 + 2 道引入题全部跑通；引入题的 KEY1/KEY2 分别从 `strings` 与 `objdump` 输出取出后拼接 |
| **12 道引入题** | 逐题用 README 里的命令重跑一遍，全部能取出 checker 里的 flag；附件 61 B ~ 57 KB |
| **checker 覆盖** | 全部 94 题都有 `checker/main.rx`；**修正了 64 个文件的分号问题**（见约束 3） |
| **文案** | 全库 README 过了一遍「去 AI 味」：正文破折号 0 处（`teach-demo/RETutor` 是上游原件，未动）、空话套话 0 处；清理时对代码块 / flag / 端口做改动前后指纹比对，技术内容零改动 |

**已知取舍**：

1. `pwn-uaf` 按题单允许的方式降级：glibc 2.35 已删 `__free_hook`，完整 tcache poisoning 要改
   `_IO_2_1_stdout_` 并叠加 libc+heap 泄露，超出入门定位。现版本是 note 结构体的函数指针覆写。
2. `rev-upx` 特意按 **UPX 3.96** 打包（静态非 PIE），代价是包从 5.8 KB 变成 360 KB，换来的是**任何发行版 `apt install upx-ucl` 就能脱壳**，4.x 也能解。
3. `rev-hello-decompile` 有意保留符号表（不是 `-s`）：第一课要能"找到 main 看伪代码"，
   去掉符号会让它退化成"跟启动链"。
4. `crypto-aes-ecb-reorder` 的密钥在构建时随机生成，重新 build 后密文会变，README 已注明。

---

## 八、容器题的平台填写速查表

后台「题目 → 环境 → 右上角上传」传完 `<题目目录名>.tar` 后，点「添加」填这张表。

**三条固定规则**

1. **镜像仓库名 = tar 文件名去掉扩展名**（服务端 `to_image_name(name.split('.').next())`）。
   所以打包一律命名成 `<题目目录名>.tar`，否则仓库名和题目对不上。
2. **镜像版本是服务端生成的时间戳**，下拉选最新那条，不要手填。
3. **传输协议只有 `tcp`**（枚举里还有 `stcp` / `udp`，本库用不到）；
   **应用协议**只有两种：HTTP 站点选 `http`，nc / socat / SSH 选 `raw`。


### 上传前 30 秒自检（专治 ERR_EMPTY_RESPONSE）

隧道连上了但页面报 `ERR_EMPTY_RESPONSE`，只有一个原因：**平台连到 Pod 的 8080，容器里没人在听**。
先在本地确认镜像本身对不对：

```bash
cd web-ctf/upload-ctf-1
docker build -t upload-ctf-1 .

# ① 镜像里 Apache 到底听哪个端口
docker run --rm upload-ctf-1 sh -c "grep '^Listen' /etc/apache2/ports.conf"
# 期望：Listen 8080

# ② 用平台真实姿势跑一遍
docker run --rm -d --name t --cap-drop NET_BIND_SERVICE -e "FLAG=flag{x}" -p 18080:8080 upload-ctf-1
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:18080/
# 期望：200
docker rm -f t
```

两条都过，说明镜像没问题，问题在上平台那一步。按顺序排查：

| 检查项 | 错了会怎样 |
|---|---|
| 上传的 tar 是不是**这次重新构建**的 | 旧 tar 里 Apache 还是 `Listen 80`，平台探 8080 就是空响应 |
| 「服务端口」有没有真的改成 `8080` | 截图里那条 `tcp:80 (http)` 就是没改的典型 |
| 「镜像版本」下拉是不是选了**最新时间戳** | 选了旧 tag 等于跑旧镜像 |
| 改完配置后有没有**重启环境** | 不重启 Pod 还是老配置，改了个寂寞 |

三步修正：删掉旧环境 → 重新 `docker save` 打包成 `<题目目录名>.tar` → 上传 → 端口填 `8080` → 镜像版本选最新 → 重新启动环境。

**全库 27 道容器题**

| 分类 | 容器名称 / 镜像仓库 | 服务端口 | 传输协议 | 应用协议 | CPU | 内存(MB) | 存储(MB) |
|---|---|---|---|---|---|---|---|
| start | `connect-ctf` | `connect-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| start | `nc-ctf` | `nc-ctf` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| start | `ssh-ctf` | `ssh-ctf` | **`2222`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| web | `backup-ctf` | `backup-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `cookie-ctf` | `cookie-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `encode-ctf` | `encode-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `header-ctf` | `header-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `http-message-ctf` | `http-message-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `infoleak-demo` | `infoleak-demo` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `robots-ctf` | `robots-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `sqli-ctf` | `sqli-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `status-ctf` | `status-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `stock-trading-ctf` | `stock-trading-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `upload-ctf-1` | `upload-ctf-1` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `upload-ctf-2` | `upload-ctf-2` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| web | `xss-reflect-ctf` | `xss-reflect-ctf` | **`8080`** | `tcp` | `http` | 0.1, 0.5 | 32, 128 | 64, 1024 |
| pwn | `pwn-canary-leak` | `pwn-canary-leak` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-fmt-leak` | `pwn-fmt-leak` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-fmt-write` | `pwn-fmt-write` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-int-underflow` | `pwn-int-underflow` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-one-byte` | `pwn-one-byte` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-ret2libc` | `pwn-ret2libc` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-ret2shellcode` | `pwn-ret2shellcode` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-ret2text` | `pwn-ret2text` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-stack-check` | `pwn-stack-check` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| pwn | `pwn-uaf` | `pwn-uaf` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |
| crypto | `crypto-aes-ecb-reorder` | `crypto-aes-ecb-reorder` | **`1337`** | `tcp` | `raw` | 0.1, 0.3 | 64, 128 | 64, 1024 |

> `restricted` 一律**勾选**、出网一律**关闭**。
> 端口填的是**容器内**监听端口，不是宿主机映射端口，宿主机那侧由平台隧道转发，不用填。

> ⚠ **已经建过的题要改端口**：早期按 `80` 填过的记录（如 `upload-1` 那条 `tcp:80 (http)`），
> 现在容器里 Apache 是 `Listen 8080`，必须改成 `8080`，否则选手连上去是空的。

---

## 九、常见坑

| 现象 | 原因 |
|---|---|
| Pod Running 但连不上端口 | 容器监听 80 或其它低位端口，被 drop 了 `NET_BIND_SERVICE` |
| 题目能开，提交 flag 永远判错 | `checker/main.rx` 缺失，或 `check` 尾行带分号（约束 3），或 `environ` 没返回 `FLAG` |
| 容器输出的 flag 多一个 `}` | entrypoint 用了 `${FLAG:-flag{...}}`（约束 4） |
| 隧道能通但页面 `ERR_EMPTY_RESPONSE` | 平台连 Pod 的 8080，容器里没在听，旧镜像（还听 80）／端口没改／镜像版本选旧／没重启环境，见第八节自检 |
| 换支队伍 flag 应该不同却没变 | `dynamic-uuid` 的两把密钥被复制成同一份（约束 6） |
| `ImagePullBackOff` | `env.toml` 里的 `tag` 不是 registry 中真实存在的时间戳链接 |
| 提交看起来一样却判错 | 模板 `CASE_SENSITIVE=true`，flag 内容不能有大写；复制时前后带空格也判错 |
| pwntools exp 本地能打、平台打不通 | 本地没带 `--cap-drop` 所以容器能起，平台下起不来 |
| PowerShell 里 `curl -H` 报"无法绑定参数 Headers" | 5.1 里 `curl` 是 `Invoke-WebRequest` 的别名，改用 `curl.exe` |
