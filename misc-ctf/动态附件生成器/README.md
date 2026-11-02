# 动态附件生成器

**生成 → 自动校验 → 告诉你怎么传**，一个脚本搞定。

## 先搞清楚平台怎么映射（依据本地源码）

`crates/bucket/src/challenge.rs`：

```rust
pub async fn get_mapped_file(&self, requested_id: i64) -> Result<Option<String>, BucketError> {
  let mut files = self.get_mapped_files().await?;   // 列出 mapped/ 下所有「非 . 开头」的条目
  if files.is_empty() { return Ok(None); }
  files.sort();                                      // 按文件名排序
  let file_index = requested_id as usize % files.len();
  Ok(Some(files[file_index].clone()))
}
```

官方 `mapped/README.md` 也写着：

> only one file will be available for the specific player here.
> **If you have multiple attachments that need to be mapped, you should pack them into a single zipped file.**
> If you upload attachments fewer than players, the attachments will be mapped to the players **in a loop**.

四条结论：

| # | 结论 | 影响 |
|---|---|---|
| 1 | 每人只拿**一个**条目 | 一题的多个附件必须打成一个包 |
| 2 | 映射是 **`id % 文件数`**，文件名排序定下标 | 不是哈希；改动文件名/数量会整体移位 |
| 3 | 条目是**普通文件**，不要求 zip | 单附件题直接传变体文件即可 |
| 4 | 份数少于人数会**循环复用** | 30 人赛建议 ≥32 份 |

> ⚠ **文件名必须纯 ASCII**：平台上传时会重写名字（`crates/bucket/src/challenge.rs` 的 `to_file_name`）
>
> ```rust
> deunicode_with_tofu(file, "_")   // 中文转拼音：作业 → "Zuo Ye"
> → 空格与 \ / : * ? " < > | 变 _   // "Zuo_Ye-15.jpg"
> → 去首尾 _ → 转小写              // "zuo_ye-15.jpg"
> ```
>
> 于是 `作业-15.jpg` 在平台里叫 `zuo_ye_-15.jpg`，而 `flag.json` 里还写着 `作业-15.jpg`
> → 查不到 → 选手提交后报 **`Could not find you flag mapping, please contact admin!`**
>
> 本工具已强制用 ASCII 条目名（`zuoye-01.jpg`、`0001.zip`），并在生成后检查一遍：
> 哪个名字会被平台改名就报警。

> ⚠ **通过 git 同步时**：模版里 `mapped/README.md` 不是隐藏文件，会被算成一个可分配条目
> （有人会拿到 README.md，然后提示 "Could not find you flag mapping"）。
> 上传动态附件前把它删掉；`.gitkeep` 是隐藏文件，**要保留**。

## 用法

### 交互模式（直接运行）

```bash
python make_mapped.py
```

```
直接输入 <题目名> <份数>（例如 demo-01 3），份数不填默认 16
加 --zip 强制打包（一题要发多个附件才需要）· ls 重列 · q 退出

> demo-01 3
  目标：demo-01  改个后缀就露馅
  份数：3
  形态：单文件（每人直接拿一个文件，不打包）
    作业-01.jpg   flag{3xt3n510n_15_4_l13_779b1}
    作业-02.jpg   flag{3xt3n510n_15_4_l13_ef362}
    作业-03.jpg   flag{3xt3n510n_15_4_l13_66d13}
  ── 自动校验 ──────────────────────────────
    mapped/ 3 个 | flag.json 3 条
    ✓ 作业-01.jpg  4_l13_779b1}  ← 内层压缩包 作业-01.jpg → flag.txt
    ✓ 作业-02.jpg  4_l13_ef362}  ← 内层压缩包 作业-02.jpg → flag.txt
    ✓ 作业-03.jpg  4_l13_66d13}  ← 内层压缩包 作业-03.jpg → flag.txt
    → 自动解出 3 / 3，需人工 0，异常 0
  ────────────────────────────────────────
  产物：...\out\demo-01
    mapped/            → 全部上传到 $BUCKET/mapped/
    checker/flag.json  → 上传到 $BUCKET/checker/
  映射：按文件名排序后 玩家 id % 3 取下标（源码是取模，不是哈希）
  校验：全部通过
```

### 命令行

```bash
python make_mapped.py demo-01 3
python make_mapped.py lsb-stego-ctf/Level2 16
python make_mapped.py starwalk-series/Level2 8 --zip     # 强制打包
python make_mapped.py demo-01 3 --nozip                  # 强制不打包
```

## 两种形态怎么选

| 题目附件数 | 脚本行为 | mapped/ 里长什么样 |
|---|---|---|
| **只有 1 个文件** | 不打包（默认） | `作业-01.jpg`、`作业-02.jpg`… 各自一个变体 |
| **多个文件**（如 5 张照片 + readme） | 自动打包 | `0001.zip`、`0002.zip`… |

变体文件名**一律 `chall-NN<后缀>`**，序号补零（`chall-01`…`chall-16`），原因有两条：

- 平台是 `files.sort()` 后按 `id % n` 取，**补零才能让排序顺序等于序号顺序**
- 名字里没有中文和空格，`to_file_name` 不会改写它 → `flag.json` 的 key 永远对得上

`flag.json` 的 key 就是这个文件名，逐字一致。

## 自动校验

1. **结构**：`mapped/` 文件名集合 == `flag.json` 的 key 集合
2. **内容**：把每个条目按题型探一遍
   - 纯文本（`.txt/.md/.json`）直接搜
   - 内层还是压缩包（如 `作业-01.jpg`）→ 解开读 `flag.txt`
   - `.png` → LSB-RGB（交织）与 LSB-R（单通道）
   - `.zip` → 解开后逐个文件探
3. **变体**：比对 sha256，如果 N 份内容一样 → 报「flag 没写进附件，动态化没意义」
   （starwalk 五关、`demo-02/05` 就是这种：flag 靠推算，附件不随 flag 变化）

## 上传步骤

1. 左侧切到 **动态附件**（`$BUCKET/mapped`）→「选择」→ 把 `out/<目标>/mapped/` 里的文件**全部**上传
2. 左侧切到 **评测文件**（`$BUCKET/checker`）→ 上传 `checker/flag.json`
3. 同一处上传平台的 `mapped.rx`
4. 保存

## 体积参考

| 题型 | 单份 | 16 份 |
|---|---|---|
| `demo-01`（单文件 `作业.jpg`） | 0.6 KB | 10 KB |
| `lsb-stego-ctf/Level2`（单文件 PNG） | 1.37 MB | 22 MB |
| `starwalk-series/Level2`（5 图打包） | 1.2 MB | 19 MB |

---

# 方案 C 构造器：`variant_inputs.py`

`make_mapped.py` 是「给 flag 加后缀」，对 starwalk 这类**推算型**题无效（附件一字节不变）。
`variant_inputs.py` 换了个思路：**变体化的是「输入」，flag 由输入推出来**。

**统一入口就是 `make_mapped.py`，加 `--variant` / `-v` 切到策略 C：**

```bash
python make_mapped.py                       # 交互模式，清单里两套策略都会列出
python make_mapped.py trio 4 --variant      # Level1，4 份（变体化输入）
python make_mapped.py all 4 -v              # 四类各 4 份
python make_mapped.py gps 8 -v --zip        # 强制打包
```

`variant_inputs.py` 是这套策略的引擎，也可以单独跑（等价）：

```bash
python variant_inputs.py all 4
```

## 四类各变什么

| 类型 | 对应题 | 随机化的输入 | flag 由什么算出 |
|---|---|---|---|
| `trio` | Level1 属性三件套 | 地标（13 个候选）+ 品牌（5 个）+ 拍摄时间 | `flag{城市_品牌_UTC时刻}` |
| `gps` | Level2 五张照片 | 5 张照片里哪张带 GPS + 指向哪个景区 | `flag{省份_景区}` |
| `hongyadong` | Level3 游客照 | 地标 + 拍摄日期时刻 | `flag{城市_建筑_本地日期_UTC时刻}` |
| `route` | Level5 路线复现 | 三座**不同**城市 + 打乱拍摄时间 | `flag{起_中_终}`（按时间排序） |

Level4（三层线索）没做 —— 现在只有南京东路一套素材，变体化需要多套不同街道的照片。

## 地标库

`LANDMARKS` 里 13 条，每条都是**真实照片 ↔ 真实坐标 ↔ 拼音**：

```python
dict(photo="p_gugong_turtle2.jpg", city="beijing", province="beijing",
     place="gugong", lat=39.9163, lon=116.3972),
dict(photo="p_zjj_gps.jpg", city="zhangjiajie", province="hunan",
     place="zhangjiajie", lat=29.3323, lon=110.4261),
...
```

照片和坐标必须**真对上**，否则题目无解。要加新地标，就往这个表里添一行
（照片放进 `_build/_assets/photos/`）。

## 自检方式不同

`make_mapped.py` 是「探字符串」；这一版是**按题解把 flag 重新算一遍**：

```
读 EXIF → 反查最近地标（容差 0.05°，约 5 km）→ 换算 UTC → 拼 flag → 与 flag.json 比
```

实测输出：

```
── 自检（按题解重新算一遍）─────────────
  ✓ photo-01.jpg  flag{chongqing_xiaomi_0035}             chongqing/hongyadong
  ✓ photo-02.jpg  flag{guilin_oneplus_0208}               guilin/guilin
  ✓ photo-03.jpg  flag{zhangjiajie_oneplus_1228}          zhangjiajie/zhangjiajie
  ✓ photo-04.jpg  flag{suzhou_xiaomi_1440}                suzhou/shizilin
  → 4 / 4 一致
```

`route` 的自检会打印整条路线：

```
✓ 0001.zip  flag{beijing_yangshuo_chongqing}   beijing → yangshuo → chongqing
```

## 产物

```
out/<类型>-variant/
├── mapped/photo-01.jpg …     或 0001.zip …     ← 上传到 $BUCKET/mapped/
├── checker/flag.json                          ← 上传到 $BUCKET/checker/
├── 题面.md                                     ← 贴到平台「题面」里（不进附件）
└── manifest.json                               ← 审计用
```

- **单附件题**（trio / hongyadong）→ 直接是文件，不打包
- **多附件题**（gps / route）→ 自动打成 zip（平台每人只能拿一个条目）

`题面.md` 单独放，是因为平台的题面走 `README.md` / `set_description`，
不该混进附件里 —— 这样「附件数」也算得准。

## 已知取舍

1. **同一张照片可能出现在多份变体里**（地标只有 13 个）。但每份的拍摄时间不同，
   算出来的 flag 也不同，抄了还是错。
2. **反查用最近地标近似**，容差 0.05°。真实解题目要人肉看图 + 地图定位，
   自检只是验证「数据自洽、可解」。
3. 种子固定（`Random(1000+i)`）→ **同样份数重复跑，产出完全一样**，便于复现；
   想换一批就把种子改掉。
