# 内容管线与数据层（Content Pipeline & Data Layer）分析报告

> 目标：`RE.Tutorial.exe`（MoeCTF.RE.Tutorial，.NET 8 / WPF / 单文件）
> 范围：PathService、内嵌内容安装、课程目录、段落模型、进度与需求激活、i18n、内容更新、程序自更新
> 证据基线：`_src/`（ILSpy 反编译，93 个 .cs）、`_bundle_unzip/`（内嵌内容包解开）、`MoeCTF.RE.Tutorial/`（运行期生成目录）
> 约定：【推断】= 由代码语义推断但未运行验证；其余为静态观察。

---

## 1. 子系统职责（一句话）

把「课程内容」当作**外部可热更新、带版本与 SHA-256 契约的文件树**来管理：启动时从程序集内嵌 ZIP 自愈式安装到 exe 同级数据目录，用 `courses/index.json → courses/<id>/course.json → stages/index.json → stages/<id>.json` 四级清单装载课程与段落，把学习进度/需求激活状态以 PascalCase JSON 持久化到 `config/`，并通过「内容清单（content-update）+ 程序清单（app-update）」两套 SemVer 契约完成内容热更新与可回滚的程序自更新。

数据根目录的真实位置（**与题面「LOCALAPPDATA 生成物」不符，实测为 exe 同级**）：

```
PathService.cs:42            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "MoeCTF.RE.Tutorial"))
HostUpdateInstaller.cs:78    GetDefaultDataRoot() => Path.Combine(AppContext.BaseDirectory, "MoeCTF.RE.Tutorial")
```

实测 `D:\ctf\RE-ctf\teach-demo\RE.Tutorial.exe` 旁边就是 `D:\ctf\RE-ctf\teach-demo\MoeCTF.RE.Tutorial`，其中只有 `courses/ stages/ assets/ schemas/ config/`（**没有**顶层 `i18n/`，i18n 在 `assets/i18n/`）。

---

## 2. 内嵌内容包：解开结果、目录树、与运行期目录的对应关系

### 2.1 解包

```powershell
Expand-Archive -Path D:\ctf\RE-ctf\teach-demo\_src\MoeCTF.RE.Tutorial.Host.BundledContent.zip -DestinationPath D:\ctf\RE-ctf\teach-demo\_bundle_unzip -Force
```

内嵌资源名与打包方式（单文件里它是托管资源，不是外挂文件）：

```
MoeCTF.RE.Tutorial.csproj:26   <None Remove="MoeCTF.RE.Tutorial.Host.BundledContent.zip" />
MoeCTF.RE.Tutorial.csproj:34   <EmbeddedResource Include="MoeCTF.RE.Tutorial.Host.BundledContent.zip" LogicalName="MoeCTF.RE.Tutorial.Host.BundledContent.zip" />
BundledContentInstaller.cs:22  internal const string ResourceName = "MoeCTF.RE.Tutorial.Host.BundledContent.zip";
BundledContentInstaller.cs:35  typeof(BundledContentInstaller).Assembly.GetManifestResourceStream(ResourceName)
```

### 2.2 目录树与大小（`_bundle_unzip/`，共 30 个文件；ZIP 1,729,533 B，解开后 2,071,485 B）

| 相对路径 | 字节 |
|---|---:|
| `assets/Fonts/JetBrainsMono-OFL.txt` | 4,398 |
| `assets/i18n/en-US.json` | 18,232 |
| `assets/i18n/zh-CN.json` | 18,223 |
| `courses/index.json` | 381 |
| `courses/chall1/course.json` | 3,768 |
| `courses/chall1/i18n/en-US.json` | 7,853 |
| `courses/chall1/i18n/zh-CN.json` | 9,068 |
| `courses/chall1/stages/index.json` | 67 |
| `courses/chall1/stages/die.json` | 3,602 |
| `courses/chall1/stages/ida-load.json` | 13,878 |
| `courses/chall1/stages/static-read.json` | 14,029 |
| `courses/chall1/assets/media/01-die-analysis.png` | 138,189 |
| `courses/chall1/assets/media/02-ida-about.png` | 55,576 |
| `courses/chall1/assets/media/03-ida-quick-start.png` | 84,348 |
| `courses/chall1/assets/media/04-ida-file-picker.png` | 177,036 |
| `courses/chall1/assets/media/05-ida-load-file.png` | 76,261 |
| `courses/chall1/assets/media/06-ida-analysis-complete.png` | 277,567 |
| `courses/chall1/assets/media/07-ida-view-menu.png` | 8,945 |
| `courses/chall1/assets/media/08-ida-strings.png` | 231,698 |
| `courses/chall1/assets/media/09-ida-input-flag-reference.png` | 355,543 |
| `courses/chall1/assets/media/10-ida-xrefs.png` | 27,666 |
| `courses/chall1/assets/media/11-ida-disassembly-main.png` | 273,358 |
| `courses/chall1/assets/media/12-ida-pseudocode.png` | 245,670 |
| `schemas/app-update.schema.json` | 887 |
| `schemas/content-update.schema.json` | 2,523 |
| `schemas/course-catalog-update.schema.json` | 2,100 |
| `schemas/course-catalog.schema.json` | 1,284 |
| `schemas/course.schema.json` | 11,367 |
| `schemas/guide-stage.schema.json` | 7,949 |
| `stages/index.json` | 19 |

（共 30 条可读文件 + ZIP 目录条目；`Install` 写出的 state 文件正好记录了 30 条相对路径哈希，与上表一一对应。）

### 2.3 与运行期目录的对应关系

- **谁安装谁**：`BundledContentInstaller.Install(destRoot)` 由 `PathService` 构造函数**每次启动无条件调用**（PathService.cs:43）；destRoot = `AppContext.BaseDirectory + "\\MoeCTF.RE.Tutorial"`。
- **对应关系是 1:1 字节级复制**：脚本比对 `_bundle_unzip/` 与 `MoeCTF.RE.Tutorial/`（排除 `config/`）→ `Compare-Object` 结果为空，无差异、无多余文件。抽查哈希逐字节相同：

| 相对路径 | SHA-256（zip 内文件 = 运行期文件 = state 记录值） |
|---|---|
| `courses/index.json` | `DD49F9ACB596B429D864A02D74586CA829891C05034FD45BC04333EA297C3BD9` |
| `courses/chall1/course.json` | `F51022518CD0A0BCE5108ADFA675C0EFB86F7E5625E08669B73AF2AA158EE1B2` |
| `courses/chall1/stages/die.json` | `36F403CF3C80C24D729C1F4789A15A440345C982388A6040616303D0D28D057E` |
| `assets/i18n/zh-CN.json` | `C156080045543FBA85D2489E49C5CA23D1BF9780E32188EF08ECB2C374F0EB52` |
| `stages/index.json` | `8480A931E41917ED57EC4EF60830C499507FE4CD2A04E03AAF996DE7ACABB011` |

- **版本 / 哈希如何记录**：
  - 内容包自身**不带版本号**，唯一身份是逐文件 SHA-256，写入 `config/bundled-content-state.json`（30 条，值为 `Convert.ToHexString` 的**大写**十六进制）。
  - 课程版本号在 `courses/chall1/course.json` 的 `version`（1.0.2）；目录版本在 `courses/index.json` 的 `catalogVersion`（1.0.0）。
  - 学习进度在 `config/progress.json`，同时记录 `CourseVersion` 与 `FlowFingerprint`（段落目录指纹），二者任一不匹配即**重置该课程进度**（ProgressStore.cs:100-111）。

`config/bundled-content-state.json` 结构（首尾）：

```json
{
  "files": {
    "assets/Fonts/JetBrainsMono-OFL.txt": "C1AB7C666206842A02B35B30770DAC0D7A10156ED401C9DEFC3F02A754D89E90",
    "assets/i18n/en-US.json": "9E9E639872D6E51B4845D2F021469386B62D0E3968C764789B800E4517D782B3",
    ...
    "stages/index.json": "8480A931E41917ED57EC4EF60830C499507FE4CD2A04E03AAF996DE7ACABB011"
  }
}
```

`config/progress.json`（全量，14 行）：

```json
{
  "ActiveCourseId": "chall1",
  "Courses": {
    "chall1": {
      "CourseVersion": "1.0.2",
      "FlowFingerprint": "8cb96e947b11bc0d8c3746b9674ba5e1cbec87ad89a3db8e7a6837514c1dfb88",
      "StageId": "chall1.die",
      "StageIndex": 0,
      "ClearedStages": []
    }
  },
  "StageIndex": 0,
  "ClearedStages": []
}
```

> 注：`config/requirements.json` 在样本目录中**不存在**——`RequirementActivationService.Load()` 只在首次 `Save()`（用户选过工具/附件后）才创建。

### 2.4 `courses/index.json` 完整内容

```json
{
  "schemaVersion": "1",
  "catalogVersion": "1.0.0",
  "catalogUpdateManifestUrl": "https://raw.githubusercontent.com/nkanf-dev/moectf-re-tutorial-content/refs/heads/main/catalog-update.json",
  "appUpdateManifestUrl": "https://raw.githubusercontent.com/nkanf-dev/moectf-re-tutorial-content/refs/heads/main/app-update.json",
  "courses": [
    "courses/chall1/course.json"
  ]
}
```

```json
// stages/index.json —— 顶层 stages 为空：内置兜底课程 freshman-re 没有任何段落文件
{ "stages": [] }

// courses/chall1/stages/index.json —— 段落装载顺序的唯一真相
{ "stages": [ "die", "ida-load", "static-read" ] }
```

### 2.5 `courses/chall1/course.json` 完整内容（146 行）

```json
{
  "schemaVersion": "1",
  "id": "chall1",
  "version": "1.0.2",
  "compatibility": {
    "minHostVersion": "0.1.0",
    "maxHostVersionExclusive": "0.2.0"
  },
  "status": "published",
  "updateManifestUrl": "https://raw.githubusercontent.com/nkanf-dev/moectf-re-tutorial-content/refs/heads/main/courses/chall1/update.json",
  "appUpdateManifestUrl": "https://raw.githubusercontent.com/nkanf-dev/moectf-re-tutorial-content/refs/heads/main/app-update.json",
  "metadata": {
    "title": "chall1：第一次逆向实践",
    "subtitle": "从识别文件到读懂第一段伪代码",
    "summary": "用 DIE 认识 chall1，再在 IDA 中沿着字符串、交叉引用和伪代码找到 flag。",
    "track": "Reverse",
    "difficulty": "零基础",
    "estimatedMinutes": 35,
    "tags": ["MoeCTF 2026", "入门", "DIE", "IDA"],
    "authors": ["nkanf"]
  },
  "content": {
    "stagesDir": "courses/chall1/stages",
    "i18nDir": "courses/chall1/i18n",
    "assetsDir": "courses/chall1/assets",
    "stageIndex": "courses/chall1/stages/index.json"
  },
  "requirements": {
    "id": "chall1.requirements",
    "title": "开始前需要准备",
    "description": "提供题目附件 chall1，并选择本机安装的 DIE 与 IDA。",
    "allOf": [
      {
        "id": "target.chall1",
        "kind": "target",
        "label": "chall1",
        "description": "比赛题目附件中的 chall1 文件",
        "targetId": "chall1",
        "match": {
          "fileNames": ["chall1"],
          "sha256": "f86808dd1c38a0b64b843b2f4cdb57d49b688c20c8e6a5ccdf3e08e7e931fa44",
          "size": 16120,
          "recursive": true
        },
        "asset": {
          "path": "courses/chall1/assets/targets/chall1",
          "copyMode": "copy"
        }
      },
      {
        "id": "tool.die", "kind": "tool", "label": "Detect It Easy",
        "description": "用于查看程序文件的基本信息", "toolId": "die",
        "match": { "fileNames": ["die.exe"], "recursive": true }
      },
      {
        "id": "tool.ida", "kind": "tool", "label": "IDA",
        "description": "用于查看反汇编、交叉引用和伪代码", "toolId": "ida",
        "match": { "fileNames": ["ida.exe", "ida64.exe", "idafree.exe"], "recursive": true }
      }
    ]
  },
  "flow": {
    "entry": "chall1.die",
    "stages": [
      { "id": "chall1.die", "path": "courses/chall1/stages/die.json",
        "requires": ["target.chall1", "tool.die"],
        "next": [ { "to": "chall1.ida-load", "when": { "kind": "always" } } ] },
      { "id": "chall1.ida-load", "path": "courses/chall1/stages/ida-load.json",
        "requires": ["target.chall1", "tool.ida"],
        "next": [ { "to": "chall1.static-read", "when": { "kind": "always" } } ] },
      { "id": "chall1.static-read", "path": "courses/chall1/stages/static-read.json",
        "requires": ["target.chall1", "tool.ida"] }
    ]
  },
  "changelog": [
    { "version": "1.0.0", "date": "2026-08-09", "text": "首个正式版本",
      "items": ["加入 DIE 文件识别引导",
                "加入 IDA 载入与可选弹窗引导",
                "加入 Strings、交叉引用、反汇编和伪代码完整路线"] }
  ]
}
```

> **重要观察**：`flow.entry` / `flow.stages[].requires` / `next` / `when` / `content.stageIndex` / `metadata.tags` / `match.recursive` / `asset.copyMode` / `changelog[].items` / `metadata.*Key` / `labelKey` / `schemaVersion` **全部没有对应的 C# 属性**，装配时被 System.Text.Json 静默丢弃。真正决定段落顺序的是 `courses/chall1/stages/index.json` + `StageDefinition.order`（StageRepository.cs:31-43）。`flow` 在图里只是**声明性文档**。

### 2.6 `courses/chall1/stages/die.json` 完整内容（149 行）

```json
{
  "id": "chall1.die",
  "order": 0,
  "titleKey": "chall1.die.title",
  "goalKey": "chall1.die.goal",
  "answerKey": "chall1.answer.external_submit_only",
  "completionMode": "guided",
  "targets": ["chall1"],
  "tools": ["die"],
  "guideMode": "forced",
  "deviations": [],
  "steps": [
    {
      "id": "open_in_die",
      "textKey": "chall1.die.open.title",
      "bodyKey": "chall1.die.open.body",
      "action": "none",
      "tool": "die",
      "file": "chall1",
      "guide": {
        "target": { "scope": "process", "process": "tool:die", "adapter": "die" },
        "locators": [
          { "strategy": "window", "scope": "process", "process": "tool:die", "adapter": "die",
            "titleContains": ["Detect It Easy"], "fallbackToWindow": true, "timeoutMs": 500 }
        ],
        "overlay": { "placement": "right", "alignment": "start", "padding": 6, "gap": 14, "maxWidth": 440 },
        "fallback": { "scope": "host", "key": "coach" }
      }
    },
    {
      "id": "read_die_result",
      "textKey": "chall1.die.result.title",
      "bodyKey": "chall1.die.result.body",
      "image": "courses/chall1/assets/media/01-die-analysis.png",
      "action": "none",
      "tool": "die",
      "file": "chall1",
      "guide": {
        "target": { "scope": "process", "process": "tool:die", "adapter": "die",
                    "titleContains": ["Detect It Easy"] },
        "locators": [
          { "strategy": "uia", "scope": "process", "process": "tool:die", "adapter": "die",
            "role": "analysis_summary", "titleContains": ["Detect It Easy"],
            "documentContains": ["chall1"], "contentContains": ["ELF64", "AMD64", "C/C++"],
            "requireAllNameContains": true, "contentMode": "evidence",
            "fallbackToWindow": false, "maxNodes": 1800, "timeoutMs": 800 },
          { "strategy": "window", "scope": "process", "process": "tool:die", "adapter": "die",
            "titleContains": ["Detect It Easy"], "fallbackToWindow": true, "timeoutMs": 500 }
        ],
        "overlay": { "placement": "right", "alignment": "center", "padding": 8, "gap": 16, "maxWidth": 460 },
        "fallback": { "scope": "host", "key": "coach" }
      },
      "trigger": {
        "kind": "locator_found",
        "locator": { "strategy": "uia", "scope": "process", "process": "tool:die", "adapter": "die",
                     "role": "analysis_summary", "titleContains": ["Detect It Easy"],
                     "documentContains": ["chall1"], "contentContains": ["ELF64", "AMD64", "C/C++"],
                     "requireAllNameContains": true, "contentMode": "evidence",
                     "fallbackToWindow": false, "maxNodes": 1800, "timeoutMs": 800 }
      }
    }
  ]
}
```

`chall1.answer.external_submit_only` 在课程 i18n 里的值就是哨兵字符串 `__external_submit_only__`（`courses/chall1/i18n/zh-CN.json`），配合 `completionMode: guided|external_submit` 表示「不在本程序内判题」。

### 2.7 `ida-load.json` / `static-read.json` 关键片段

`ida-load.json`（543 行）——4 个 deviation 是「用户跑偏就打岔」的分支引导，5 个 step 是主路线：

```json
{
  "id": "chall1.ida-load", "order": 1,
  "titleKey": "chall1.ida.title", "goalKey": "chall1.ida.goal",
  "answerKey": "chall1.answer.external_submit_only",
  "completionMode": "guided",
  "targets": ["chall1"], "tools": ["ida"], "guideMode": "forced",
  "deviations": [
    { "id": "ida_quick_start",
      "textKey": "chall1.ida.deviation.quick_start",
      "bodyKey": "chall1.ida.deviation.quick_start.body",
      "image": "courses/chall1/assets/media/03-ida-quick-start.png",
      "priority": 110,
      "when": { "kind": "locator_found",
                "locator": { "strategy": "uia", "scope": "process", "process": "tool:ida",
                             "adapter": "ida", "state": "quick_start", "role": "quick_start_new",
                             "titleContains": ["IDA: Quick start"],
                             "contentContains": ["New", "Display at startup"],
                             "requireAllNameContains": true, "maxNodes": 900, "timeoutMs": 550 } },
      "guide": { "target": {"scope":"process","process":"tool:ida","adapter":"ida",
                            "titleContains":["IDA: Quick start"]},
                 "locators": [ "uia / state=quick_start / role=quick_start_new / nameContains=[New]" ],
                 "overlay": { "placement": "right", "alignment": "start", "padding": 6, "gap": 12, "maxWidth": 430 },
                 "fallback": { "scope": "host", "key": "coach" } } },

    "…其余 3 个 deviation：ida_about(priority 120)、ida_file_picker(105)、ida_load_file(115)…"
  ],
  "steps": [
    { "id": "launch_ida", "textKey": "chall1.ida.launch.title", "bodyKey": "chall1.ida.launch.body",
      "image": "courses/chall1/assets/media/02-ida-about.png", "action": "none", "tool": "ida", "file": "chall1",
      "guide": { "target": {"scope":"process","process":"tool:ida","adapter":"ida"},
                 "locators": [ { "strategy": "window", "fallbackToWindow": true, "timeoutMs": 500 } ],
                 "overlay": { "placement": "right", "alignment": "start", "gap": 14, "maxWidth": 440 },
                 "fallback": { "scope": "host", "key": "coach" } } },
    { "id": "quick_start_fallback",   "…": "uia / quick_start / quick_start_new / fallbackToWindow=true" },
    { "id": "select_chall1_fallback", "…": "uia / open_file_modal / file_picker_dialog / contentContains=[chall1]" },
    { "id": "confirm_loader_fallback","…": "uia / load_file / confirm_button / nameEquals=OK" },
    { "id": "analysis_ready",
      "textKey": "chall1.ida.ready.title",
      "image": "courses/chall1/assets/media/06-ida-analysis-complete.png",
      "trigger": { "kind": "locator_found",
                   "locator": { "strategy": "uia", "state": "main", "role": "functions_view",
                                "titleContains": ["chall1"],
                                "contentContains": ["main", "strncmp", "fgets"],
                                "requireAllNameContains": true,
                                "maxNodes": 2200, "timeoutMs": 850 } } }
  ]
}
```

`static-read.json`（546 行）——`completionMode: external_submit`，用 `rejectContentContains` 做反例排除防误判：

```json
{ "id": "pseudocode_flag",
  "textKey": "chall1.static.pseudocode.title", "bodyKey": "chall1.static.pseudocode.body",
  "image": "courses/chall1/assets/media/12-ida-pseudocode.png",
  "action": "none", "tool": "ida", "file": "chall1",
  "guide": { "target": { "scope": "process", "process": "tool:ida", "adapter": "ida",
                          "titleContains": ["chall1"] },
    "locators": [
      { "strategy": "uia", "scope": "process", "process": "tool:ida", "adapter": "ida",
        "state": "pseudocode", "role": "pseudocode", "titleContains": ["chall1"],
        "contentContains": ["strcpy(s2", "fgets(s, 128, stdin)",
                            "strcspn(s, "\\n")", "strncmp(s, s2, 0x3Au)"],
        "requireAllNameContains": true, "contentMode": "target",
        "fallbackToWindow": false, "maxNodes": 3000, "timeoutMs": 1000 },
      { "strategy": "uia", "state": "pseudocode", "role": "pseudocode",
        "fallbackToWindow": true, "maxNodes": 2000, "timeoutMs": 800 }
    ],
    "overlay": { "placement": "right", "alignment": "start", "padding": 8, "gap": 14, "maxWidth": 470 },
    "fallback": { "scope": "host", "key": "coach" } },
  "trigger": { "kind": "locator_found", "locator": { "…同上，另加…":
    "rejectContentContains": ["DATA XREF", "Pseudocode-A"] } } }
```

### 2.8 内嵌包解开的额外观察

- `courses/chall1/assets/targets/` **不存在**（`Test-Path → False`）。`course.json` 里的 `asset.path` 是**目标目录**，由用户拖入附件时 `SetTargetPath` 创建（RequirementActivationService.cs:316-318），不会被内容包预置。
- 工作区里的 `D:\ctf\RE-ctf\teach-demo\chall1`：大小 16,120 B（与 `match.size` 一致），但 `SHA-256 = ea730409f02618d07c163551c10f3a6313fd3509cf64e9090dc78ae2c4b825d1`，**与 course.json 声明的 `f86808dd…fa44` 不一致**。即：把这份本地 `chall1` 拖进来会在 `Matches()` 的最后一步（RequirementActivationService.cs:473-480）被拒。见待确认第 4 条。

---

## 3. 数据结构 / JSON 契约

序列化约定总览：

| 类别 | 文件 | JsonSerializerOptions | 命名 |
| --- | --- | --- | --- |
| 内容契约（课程/段落/更新清单） | `course.json`、`stages/*.json`、`update.json` | `PropertyNameCaseInsensitive = true` | 全部显式 `[JsonPropertyName]` → **camelCase** |
| 本地状态 | `config/progress.json`、`config/requirements.json` | ProgressStore：**默认 options（无 case-insensitive）**+`WriteIndented`；RequirementActivationService：`WriteIndented + PropertyNameCaseInsensitive` | **PascalCase（属性名直出）** |
| 安装状态 | `config/bundled-content-state.json` | `PropertyNameCaseInsensitive + WriteIndented` | 单个 `files` 字典 |
| 更新事务/请求 | `.content-update/transaction.json`、`updates/apply-*.json` | `PropertyNameCaseInsensitive + WriteIndented` | camelCase |

### 3.1 内容清单：`courses/index.json` → `CourseCatalog.CourseIndex`（CourseCatalog.cs:17-27）

| JSON 字段 | C# 属性 | 类型 | 可空/默认 | 说明 |
| --- | --- | --- | --- | --- |
| `courses` | `Courses` | `string[]` | 默认 `Array.Empty<string>()` | 课程清单相对路径（相对 RepoRoot），经 `SafeCombine` 校验 |
| `catalogUpdateManifestUrl` | `CatalogUpdateManifestUrl` | `string?` | null | 目录级更新清单 |
| `appUpdateManifestUrl` | `AppUpdateManifestUrl` | `string?` | null | 程序更新清单，优先级高于课程级 |

另有 `ContentUpdater.CourseIndex`（ContentUpdater.cs:27-37）读同一文件，但只取 `catalogVersion / contentVersion / courses` 用于**版本比较**。`schemaVersion` 在 CourseCatalog 里**没有**对应属性（不校验）。

### 3.2 课程清单：`course.json` → `CourseCatalog.CourseManifest`（CourseCatalog.cs:35-234）

**CourseManifest（顶层）**

| JSON | C# | 类型 | 可空/默认 | 备注 |
| --- | --- | --- | --- | --- |
| `id` | `Id` | `string` | `""` | 空 → LoadCourse 返回 null（:669） |
| `version` | `Version` | `string?` | null | **必须** 能 `SemanticVersion.TryParse`，否则整门课被丢弃 |
| `title` / `subtitle` / `summary` | 同名 | `string?` | null | 被 `metadata.*` 覆盖（`First()`） |
| `track` / `difficulty` | 同名 | `string?` | null | 兜底 `"Reverse"` / `"Beginner"` |
| `status` | `Status` | `string?` | `"installed"` | schema 允许 draft/preview/published/archived；程序**不校验** |
| `stageCount` | `StageCount` | `int` | 0 | `<=0` 时回退 `flow.stages.Count` → 再回退 `CountStages()` |
| `estimatedMinutes` | 同名 | `int` | 0 | 0 时回退 `metadata.estimatedMinutes` |
| `authors` | `List<string>` | 默认空表 |  | 仅 `metadata.authors` 被使用；顶层 `authors` 被忽略 |
| `stagesDir` / `i18nDir` / `assetsDir` | 同名 | `string?` | null | 顶层优先，`content.*` 次之，最后默认值 |
| `metadata` | `Metadata` | `CourseMetadataManifest?` | null |  |
| `content` | `Content` | `CourseContentManifest?` | null |  |
| `flow` | `Flow` | `CourseFlowManifest?` | null | **只用到 `stages.Count` 与每项 `id`/`path`** |
| `compatibility` | `Compatibility` | `CourseCompatibilityManifest?` | null |  |
| `updateManifestUrl` / `appUpdateManifestUrl` | 同名 | `string?` | null |  |
| `requirements` | `Requirements` | `CourseRequirementNodeManifest?` | null | 递归树 |
| `changelog` | `List<CourseChangelogEntryManifest>` | 默认空表 |  |  |

**嵌套类型字段**

`CourseMetadataManifest`（:113-135）：`title` / `subtitle` / `summary` / `track` / `difficulty`（string?）、`estimatedMinutes`（int）、`authors`（List<string>，默认空）。**没有** `tags` / `icon` / `homepageUrl` / `*Key`。

`CourseContentManifest`（:137-147）：`stagesDir` / `i18nDir` / `assetsDir`（string?）。**没有** `stageIndex`。

`CourseFlowManifest`（:149-153）：`stages: List<CourseFlowStageManifest>`。**没有** `entry`。`CourseFlowStageManifest`（:155-162）：`id`（string，默认 ""）、`path`（string，默认 ""）。**没有** `requires` / `next` / `when` / `titleKey`。

`CourseRequirementNodeManifest`（:164-201）

| JSON | C# | 类型 | 说明 |
| --- | --- | --- | --- |
| `id` | `Id` | `string` | 空 → `ToRequirement` 返回 null |
| `title` / `description` / `label` | 同名 | `string?` | `title` 仅用于 group 显示名 |
| `allOf` / `anyOf` | 同名 | `List<Node>?` | 递归 |
| `kind` | `Kind` | `string?` | **叶子判据**：非空白即叶子（:840-843）；取值只区分 `tool` / 其它→`target` |
| `toolId` | 同名 | `string?` |  |
| `targetId` | 同名 | `string?` | 当 `fileName` 为空且 kind=target 时充当 `FileName`（:859） |
| `fileName` | `FileName` | `string?` |  |
| `match` | `Match` | `CourseRequirementMatchManifest?` |  |
| `asset` | `Asset` | `CourseRequirementAssetManifest?` | 只取 `path` |

`CourseRequirementMatchManifest`（:203-216）：`fileNames`（string[]?）、`pathContains`（string[]?）、`sha256`（string?）、`size`（long?）。**没有** `recursive`（丢弃）。

`CourseRequirementAssetManifest`（:218-222）：`path`（string?）。**没有** `copyMode` / `companions`。

`CourseChangelogEntryManifest`（:224-234）：`version` / `date` / `text`（string?）。**没有** `items`。

### 3.3 运行期课程模型

**CourseDefinition**（CourseDefinition.cs，全 `init` 只读，由 LoadCourse 构造）

| 成员 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `Id` | `string` | `""` |  |
| `Title` / `Subtitle` / `Summary` | `string` | `""` |  |
| `Version` | `string` | `""` | SemVer |
| `FlowFingerprint` | `string` | `""` | stages 目录 SHA-256（小写 hex） |
| `Track` / `Difficulty` | `string` | Reverse / Beginner |  |
| `Authors` | `IReadOnlyList<string>` | 空 | 去空白 + `Distinct(OrdinalIgnoreCase)` |
| `Status` | `string` | `"installed"` |  |
| `StageCount` / `EstimatedMinutes` | `int` | 0 |  |
| `IsBuiltIn` | `bool` | false | 同 id 版本相同时**内置优先**（:640-644） |
| `ContentRoot` / `StagesDir` / `I18nDir` / `AssetsDir` | `string` | `""` | 绝对路径；`Apply(paths)` 写入 PathService |
| `UpdateManifestUrl` / `AppUpdateManifestUrl` | `string?` | null |  |
| `MinHostVersion` / `MaxHostVersionExclusive` | `string` | `""` |  |
| `HostCompatibilityDisplay` | `string` | 只读 | `">= a, < b"` 或 `"未声明"` |
| `IsCompatibilityRangeValid` / `IsHostCompatible` | `bool` | 只读 | 区间必须 a < b 且宿主 ∈ [a,b) |
| `RequirementGroups` | `IReadOnlyList<CourseRequirementGroup>` | 空 | 扁平化结果 |
| `Changelog` | `IReadOnlyList<CourseChangelogEntry>` | 空 |  |

**CourseRequirementGroup**：`Id`、`Title`、`Description`、`Mode`（默认 `"allOf"`）、`Items`；`ModeNormalized` 只认 `anyOf`，其余一律 allOf。

**CourseRequirement**：`Id` / `Kind`（默认 "target"）/ `Label` / `Description` / `ToolId?` / `FileName?` / `AssetPath?` / `MatchNames` / `MatchPathContains` / `MatchSha256?` / `MatchSize?`；
派生方法：`KindNormalized`、`ToolIdOrDefault()`（回退 Id）、`ToolExecutableName()`（die→die.exe / ida→ida.exe / dnspy→dnSpy.exe / ghidra→ghidraRun.bat / x64dbg→x64dbg.exe / 其它→`{id}.exe`）、`FileNameOrDefault()`、`AssetPathOrDefault()`（默认 `assets/targets/{fileName}`）。

**CourseChangelogEntry**：`Version` / `Date` / `Text`（全 string，默认 ""）。**CourseUpdateEndpoints**：record `(string? CatalogUpdateManifestUrl, string? AppUpdateManifestUrl)`。**RequirementActivationStatus**：record `(CourseRequirement Requirement, bool IsSatisfied, string? Path, string DisplayPath, bool IsSelected)`。

### 3.4 段落模型（`stages/*.json`）

**StageIndex**（StageIndex.cs）：`stages: List<string>`（名字不带 `.json`，装载时拼 `stagesDir/{name}.json`）。

**StageDefinition**（StageDefinition.cs，全部 `[JsonPropertyName]` camelCase，均带默认值）

| JSON | C# | 类型 | 默认 |
| --- | --- | --- | --- |
| `id` | `Id` | `string` | `""` |
| `order` | `Order` | `int` | 0（**排序键**） |
| `titleKey` / `goalKey` / `answerKey` | 同名 | `string` | `""` |
| `completionMode` | 同名 | `string` | `"answer"`（实际值 guided/external_submit） |
| `fakeAnswerKeys` | 同名 | `List<string>` | 空 |
| `targets` / `tools` | 同名 | `List<string>` | 空 |
| `guideMode` | 同名 | `string` | `"forced"` |
| `freeExploreTextKey` | 同名 | `string?` | null |
| `deviations` | 同名 | `List<GuideDeviationSpec>` | 空 |
| `steps` | 同名 | `List<StageStep>` | 空 |

**StageStep**（StageStep.cs）

| JSON | C# | 类型 | 默认 | 说明 |
| --- | --- | --- | --- | --- |
| `id` | `Id` | `string` | `""` |  |
| `textKey` | 同名 | `string` | `""` | i18n key |
| `bodyKey` | 同名 | `string?` | null |  |
| `image` | 同名 | `string?` | null | **相对 ContentRoot 的完整相对路径**（如 `courses/chall1/assets/media/01-x.png`） |
| `action` | 同名 | `string` | `"none"` | schema 里 `const "none"` |
| `tool` / `file` | 同名 | `string?` | null |  |
| `guide` | `GuideSpec?` | null |  | 定位/浮层计划 |
| `trigger` | `GuideConditionSpec?` | null |  | 进入下一步的条件 |
| `branches` | `List<GuideBranchSpec>` | 空 |  |  |
| `highlightScope` / `highlightHost` / `highlightProcess` / `highlightControlType` | 同名 | `string?` | null | **不在 guide-stage.schema.json 里**（schema 有 `additionalProperties:false`，属 schema 落后于实现） |
| `highlightNames` | 同名 | `List<string>` | 空 | 同上 |

**GuideDeviationSpec**：`id` / `textKey` / `bodyKey?` / `image?` / `priority`（int，0）/ `when`（GuideConditionSpec，默认 new）/ `guide?`。

**Guide\* 支撑类型**（跨子系统契约，与 schema 一一对应）：

- `GuideSpec{target, locators, overlay, fallback?}`
- `GuideTargetSpec{scope("host"), key?, process?, adapter?, titleContains[], processNameContains[], exeNameContains[], exePathContains[], windowClassNameContains[], titlePidFromProcess?}`
- `GuideLocatorSpec{strategy("auto"), scope?, key?, process?, adapter?, state?, role?, nameEquals?, nameContains[], contentContains[], documentContains[], contentMode("evidence"), rejectTitleContains[], rejectContentContains[], requireAllNameContains, automationId?, className?, controlType?, titleContains[], processNameContains[], exeNameContains[], exePathContains[], windowClassNameContains[], titlePidFromProcess?, rect?, adjust?, fallbackToWindow, maxNodes?, timeoutMs?}`
- `GuideOverlaySpec{placement("auto"), alignment("center"), padding(6.0), gap(14.0), offsetX, offsetY, maxWidth?}`
- `GuideConditionSpec{kind("always"), not, process?, key?, value?, valueKey?, locator?, all[], any[]}`
- `GuideBranchSpec{trigger("next"), when, next?}`
- `GuideRectSpec{x, y, width, height, space("screen")}`、`GuideRectAdjustSpec{offsetX, offsetY, inflate, width?, height?}`

### 3.5 进度与需求激活状态

**ProgressData**（`config/progress.json`，ProgressData.cs）—— **PascalCase，无 JsonNamingPolicy**

| C# | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `ActiveCourseId` | `string` | `"freshman-re"` | 空则回写该默认值（ProgressStore.cs:73-76） |
| `Courses` | `Dictionary<string, CourseProgressData>` | 空字典（`OrdinalIgnoreCase`） | key = courseId |
| `StageIndex` | `int` | 0 | **遗留镜像字段** |
| `ClearedStages` | `List<string>` | 空 | **遗留镜像字段** |

**CourseProgressData**（CourseProgressData.cs）

| C# | 类型 | 可空 | 说明 |
| --- | --- | --- | --- |
| `CourseVersion` | `string?` | ✔ | 与 CourseDefinition.Version 比对 |
| `FlowFingerprint` | `string?` | ✔ | 与 CourseDefinition.FlowFingerprint 比对 |
| `StageId` | `string?` | ✔ | 当前段落 id（优先于 StageIndex，MainWindow.cs:619-626） |
| `StageIndex` | `int` |  | 段落序号 |
| `ClearedStages` | `List<string>` |  | 已通关段落 id 集合（可乱序） |

**RequirementActivationState**（`config/requirements.json`，RequirementActivationState.cs）—— 三个字典都带 `[JsonPropertyName]`

| JSON | C# | 类型 |
| --- | --- | --- |
| `tools` | `Dictionary<string,string>` | toolId → 可执行文件绝对路径 |
| `courseTargets` | `Dictionary<string, Dictionary<string,string>>` | courseId → requirementId → 目标文件绝对路径 |
| `courseChoices` | `Dictionary<string, Dictionary<string,string>>` | courseId → groupId → 选中的 requirementId |

**BundledContentState**（私有类，BundledContentInstaller.cs:16-20）：`files: Dictionary<string,string>`（相对路径 → 大写 SHA-256，`OrdinalIgnoreCase`）。

### 3.6 更新契约

**ContentUpdateManifest / ContentUpdateFile**（content-update.schema.json 对应）

| JSON | C# | 类型 | 默认 | 校验 |
| --- | --- | --- | --- | --- |
| `contentVersion` | `ContentVersion` | `string` | `""` | 非课程更新时用它比版本 |
| `courseId` | `CourseId` | `string?` | null | 非空 → 走「课程域更新」分支 |
| `courseVersion` | `CourseVersion` | `string?` | null | 课程更新时必须非空 |
| `schemaVersion` | `SchemaVersion` | `string` | `"1"` | 必须 "1"（:491） |
| `minHostVersion` | `MinHostVersion` | `string` | `""` | 空视为 0.0.0 |
| `maxHostVersionExclusive` | `MaxHostVersionExclusive` | `string?` | null | 课程更新必填（requireMaximum） |
| `hostUpdateManifestUrl` | 同名 | `string?` | null | 宿主版本过低时给用户跳转 |
| `notes` / `publishedAt` | 同名 | `string?` | null | 仅展示/去重排序 |
| `prune` | `Prune` | `bool` | false | true → 删除清单外托管文件 |
| `delete` | `Delete` | `List<string>` | 空 | 显式删除 |
| `files[]` | `Files` | `List<ContentUpdateFile>` | 空 | 空 → 清单不完整 |
| `files[].path` | `Path` | `string` | `""` | 过白名单 + 课程命名空间 |
| `files[].url` | `Url` | `string?` | null | 空则按清单 URL 相对推导（:991-1004） |
| `files[].sha256` | `Sha256` | `string?` | null | **必需**，否则抛（:615-618） |

**ContentUpdateResult**：`Success` / `PendingActivation` / `RequiresHostUpdate` / `RequiredHostVersion?` / `HostUpdateManifestUrl?` / `Message` / `Manifest?`（全 `init`）。

**HostUpdateManifest**（app-update.schema.json 对应）：`schemaVersion("1") / appVersion / channel("stable") / mandatory(false) / downloadUrl? / releaseNotesUrl? / sha256? / size?(long?) / publishedAt? / notes?`。
**HostUpdateResult**：`Success` / `UpdateAvailable` / `Message` / `DownloadUrl?` / `Manifest?`。
**HostUpdateInstallResult**：`Success` / `Message` / `DownloadedExecutablePath?` / `RequestPath?`，另有静态工厂 `Failed(msg)`。

**HostUpdateRequest**（`updates/apply-<guid>.json`，HostUpdateInstaller.cs:17-45）：`schemaVersion("1") / dataRoot / downloadedExecutablePath / targetExecutablePath / expectedSize(long) / expectedSha256 / oldProcessId(int) / oldProcessStartTimeUtcTicks(long) / createdAtUtc(DateTimeOffset)`。

**UpdateTransaction / UpdateBackupEntry**（`.content-update/transaction.json`）：`{ backedUp: [{path, backup}], created: [string] }`。

### 3.7 i18n 契约（**扁平字符串字典**，无嵌套）

`I18nService.Load(locale, i18nDir, fallbackDir?)`（I18nService.cs:15-42）：

1. 若给 `fallbackDir` → 先载 `{fallbackDir}/{locale}.json`；
2. 再载 `{i18nDir}/{locale}.json`（**后载覆盖先载**）；文件缺失回退 `zh-CN.json`；
3. 用 `JsonDocument` 遍历**顶层属性**，值必须是字符串（`GetString()`），非字符串会抛 `InvalidOperationException`（未 catch）。

`T(key, args)`（:44-60）：查不到 key **原样返回 key**；`{name}` 占位用正则 `\{(\w+)\}` 替换，缺失的占位保持原样。
`GetRaw(key)`（:62-69）：查不到返回 null（UI 用它做「有翻译才用」的 `TextOr` 双写模式，MainWindow.cs:536-543）。

实测规模：`assets/i18n/zh-CN.json` 163 个键（前缀分布 hub 38 / common 35 / stage0 21 / stage3 20 / stage1 11 / stage2 10 / requirement 6 / course_entry_notice 5 / desktop 5 / final 5 / settings 5 / app 2）；`courses/chall1/i18n/zh-CN.json` 77 个键（其中 `chall1.*` 42 个）。运行时 `locale` **硬编码 `"zh-CN"`**（MainWindow.cs:593、603），`en-US.json` 虽随包发布但当前无切换入口。

---

## 4. 关键流程（按调用顺序）

### 4.1 启动：数据目录定位 + 内嵌内容自愈安装

```
App.OnStartup (App.cs:49)
 ├─ args[0] == "--apply-update"   → HostUpdateInstaller.ApplyUpdateAsync(path)；失败弹 MessageBox；Shutdown(0/1)
 ├─ args[0] == "--bootstrap-only" → new PathService()；校验 courses/index.json + stages/index.json
 │                                  + assets/i18n/zh-CN.json 三者存在 → exit 0 / 2；异常 exit 1
 └─ HostUpdateInstaller.CleanupCompletedUpdates(GetDefaultDataRoot())
    MainWindow = new MainWindow(); Show()

MainWindow ctor (MainWindow.cs:328-336)
 _paths = new PathService()                    // 字段初始化器，:42
   PathService ctor (PathService.cs:40-64)
     root = Path.GetFullPath(BaseDirectory + "\MoeCTF.RE.Tutorial")
     if BundledContentInstaller.Install(root) || LooksLikeBuiltInContentRoot(root)  → RepoRoot = root
     else 向上最多 8 层找含 stages/index.json + assets/i18n/zh-CN.json 的目录
         兜底 RepoRoot = BaseDirectory\..\..\..\..
     _workspaceRoot = FindWorkspaceRoot(...)   // 开发期：找 .slnx + assets/tools，最多 10 层
 _catalog   = new CourseCatalog(_paths)
 _updater   = new ContentUpdater(_paths)        // 内含 GitHubRemoteClient(HttpClient{Timeout=20s})
 _installer = new HostUpdateInstaller()         // HttpClient{Timeout=5min}
 _req       = new RequirementActivationService(_paths)   // ctor 内 Load() 读 config/requirements.json
 _updater.RecoverInterruptedUpdate()            // 回滚上次中断的内容更新
 _progress  = new ProgressStore(_paths.ProgressPath)     // ctor 内 Load()
 LoadCourses(); SelectInitialCourse(); ApplyActiveCourse(loadTexts:true)
```

`Install` 的决策表（BundledContentInstaller.cs:50-65，**核心语义**）：

```
for each entry in zip:
    h = SHA256(entry)                     // zip 里这一条
    newState[rel] = h                     // 无条件记录「我发的是什么」
    EnsureSafeDirectory(...)              // 逐级建目录 + 拒绝 reparse point
    if File.Exists(dest):
        a = SHA256(dest)
        if a == h                     → continue    // 已一致，跳过
        if !oldState.ContainsKey(rel) → continue    // 无历史记录 → 不覆盖用户文件
        if a != oldState[rel]         → continue    // 用户改过 → 保留
    WriteEntryAtomically(entry, dest)     // .name.guid.tmp + WriteThrough + File.Move(overwrite)
WriteJsonAtomically(config/bundled-content-state.json, newState)
return true                               // 只要资源流存在就恒 true，供 PathService 判定 root
```

即：**只有当「磁盘上的文件还完全是上一版内容包写下的原样」时，才会被新版内容包覆盖**；其余一律让位给磁盘现状。

### 4.2 课程目录装载

```
CourseCatalog.LoadCourses()  (CourseCatalog.cs:601-609)
 dict = {}
 foreach c in LoadFromRoot(RepoRoot, isBuiltIn:true):          // :653-662（迭代器状态机 :476-547）
     if !Directory.Exists(root) → 空
     if root/courses/index.json 不存在:
         if IsValidCourseLayout(root/stages, root/assets/i18n, root/assets) → yield BuiltInFallback(root)
         else → 空
     index = deserialize<CourseIndex>（失败吞异常 → courses 为空）
     for rel in index.courses:
         path = SafeCombine(root, rel)                         // 拒绝 rooted / 逃逸
         if path != null && File.Exists(path):
             c = LoadCourse(root, path, isBuiltIn); if c != null → yield c
 AddOrReplaceCourse(dict, c)      // 同 id：SemVer 高者胜；版本相同则 IsBuiltIn 优先
 return dict.Values.OrderBy(IsBuiltIn).ThenBy(Id)              // 内置在前

LoadCourse()  (CourseCatalog.cs:664-732)
 manifest = deserialize<CourseManifest>; 失败 → null
 必须 Id 非空白 且 Version 可 SemVer 解析，否则 null
 stagesDir = ResolveDir(root, First(manifest.stagesDir, content.stagesDir ?? ""), "stages")
 assetsDir = ResolveDir(root, First(manifest.assetsDir, content.assetsDir ?? ""), "assets")
 i18nDir   = ResolveDir(root, First(manifest.i18nDir,  content.i18nDir  ?? ""), "assets/i18n")
   ResolveDir：拒绝 rooted；GetFullPath 后必须仍在 root 下，否则 null
 IsValidCourseLayout(stagesDir, i18nDir, assetsDir) 不通过 → null
   = Directory.Exists(assets) && Directory.Exists(i18n) && File.Exists(i18n/zh-CN.json)
     && File.Exists(stages/index.json)
 StageCount = stageCount>0 ? stageCount : (flow.stages.Count>0 ? that : CountStages(stagesDir))
 EstimatedMinutes = 顶层>0 ? 顶层 : metadata.estimatedMinutes
 FlowFingerprint  = ComputeFlowFingerprint(stagesDir)
 Title/Subtitle/Summary = metadata.* ?? 顶层 ?? 默认（Id）
 Authors = metadata.authors 去空白 + Distinct(OrdinalIgnoreCase)
 RequirementGroups = NormalizeRequirements(manifest.requirements)
 Changelog = manifest.changelog → CourseChangelogEntry[]
 UpdateManifestUrl / AppUpdateManifestUrl / MinHostVersion / MaxHostVersionExclusive
```

`ComputeFlowFingerprint`（:918-939）：把 `{stagesDir}/*.json` 按**文件名**（`OrdinalIgnoreCase`）排序，对每个文件追加 `lower(filename) + 0x00 + bytes + 0x00` 到 `IncrementalHash(SHA256)`，输出**小写** hex；任何异常 → `空串`。

`NormalizeRequirements`（:769-828）把递归 JSON 树拍平成**组列表**：

- 节点是叶子（`kind` 非空）→ 单独成组 `{Mode:"allOf", Items:[1 项]}`；
- 有 `anyOf` → 一组 `Mode:"anyOf"`，Items = 递归 `FlattenLeaves(anyOf)` 后转 requirement；
- 只有 `allOf` → **不建组**，递归展开每个子节点各自成组（所以 `chall1.requirements` 本身不产生组，只产生 `target.chall1` / `tool.die` / `tool.ida` 三组）。

### 4.3 选课 / 段落装载 / i18n

```
SelectInitialCourse()  (MainWindow.cs:575-584)
  if courses.Count == 0 → active = null; return
  active = courses.FirstOrDefault(Id == progress.Data.ActiveCourseId) ?? courses.First()
  progress.SetActiveCourse(active.Id, active.Version, active.FlowFingerprint)
     → Data.ActiveCourseId = id（空则 "freshman-re"）
     → ForCourse(id, version, fp)   // 版本/指纹不符 → 新建 CourseProgressData
     → MirrorLegacy(progress)       // 同步顶层 StageIndex / ClearedStages

ApplyActiveCourse(loadTexts)  (MainWindow.cs:586-608)
  active == null → paths.UseBuiltInContent(); i18n.Load("zh-CN", I18nDir)
  else           → active.Apply(_paths)   // paths.UseContentPaths(ContentRoot, StagesDir, I18nDir, AssetsDir)
                   i18n.Load("zh-CN", courseI18nDir, RepoRoot/assets/i18n)
  _stages = new StageRepository(_paths.StagesDir)
  _stageIndex = ResolveSavedStageIndex()

StageRepository ctor  (StageRepository.cs:17-44)
  read stagesDir/index.json → StageIndex.stages（缺失/空 → 空表）
  for name in stages: 若 stagesDir/{name}.json 存在 → 反序列化 StageDefinition（null 丢弃）
  Stages = list.OrderBy(s => s.Order)
```

`ResolveSavedStageIndex`（MainWindow.cs:610-628）：先按 `progress.StageId` 在 `Stages` 里找下标，找不到用 `progress.StageIndex`；再与「已通关段落推出的最小允许下标」（`MinimumActiveStageIndexFromClearedProgress`）取 max；最后 `Math.Clamp(0, Count-1)`。

进度写入点（MainWindow.cs）：段落切换 `:1490-1492`、通关 `:2082-2090 / 2149-2159`、`external_submit` 自动打钩 `:3018-3023`、重置课程 `:2119`（`ResetCourse` 内部直接 `Save()`）。

### 4.4 需求激活（工具 / 题目附件）

```
StatusFor(courseId, groupId, req)   (RequirementActivationService.cs:328-335)
  kind=tool   → FindToolExecutable(req.ToolIdOrDefault())
                  state.tools 命中且文件存在 ? 用它 : paths.FindBundledToolExecutable(id)
                  （按 toolId 猜一组 exe 名；兜底 assets/tools/{toolId}/**/*.exe 第一个）
  kind=target → FindTarget(courseId, req)  (:287-304)
                  1) state.courseTargets[courseId][req.Id] 且 Matches()
                  2) ResolveTargetAssetPath(...)  // ContentRoot/courses/<id>/assets/targets/<name>
                  3) paths.FindTarget(name)       // assets/targets、targets、workspace 变体
  IsSatisfied = path 非空 && File.Exists(path)
  DisplayPath = 末两级目录 + "\" + 文件名（ShortPath，:524-537）

Matches(req, filePath)  (:439-482)   // 严格顺序
  !File.Exists → false
  names = req.MatchNames.Count>0 ? MatchNames : [FileNameOrDefault(ToolExecutableName())]
  !names.Any(p => FileNameMatches(fileName, p))                → false   // 支持 * ?
  req.MatchPathContains.Count>0 且 不全包含（IgnoreCase）      → false
  req.MatchSize 有值 且 FileInfo.Length 不等                   → false
  req.MatchSha256 非空 且 SHA256(hex, IgnoreCase) 不等         → false
  → true

SetTargetPath(courseId, req, sourceFile)  (:306-326)
  不存在 → FileNotFoundException；!Matches → InvalidDataException("Selected file does not match this course target.")
  dest = ResolveTargetAssetPath(...) ?? throw（逃逸 ContentRoot）
  Directory.CreateDirectory(dir); File.Copy(src, dest, overwrite:true)
  state.courseTargets[courseId][req.Id] = dest; Save()

工具路径：SetToolPath 要求 File.Exists 且写 Path.GetFullPath；ClearToolPath 直接 Remove + Save。

组/整体判定：IsGroupSatisfied = anyOf ? Any(Satisfied) : All(Satisfied)（:337-345）
选择态：SelectRequirement / GetSelectedRequirement / AutoSelectSatisfiedRequirements（:352-398）
拖拽入口：FindMatchingRequirement(course, inputPaths)（:400-415）→ ExpandInputPaths 展开目录 → 第一个命中的 req
```

### 4.5 内容更新（课程域 / 目录域）

```
MainWindow.CheckForUpdatesAsync(automatic)  (MainWindow.cs:1291-1389)
  ep = catalog.LoadUpdateEndpoints()            // courses/index.json 里的两个 URL
  appUrl = ep.AppUpdateManifestUrl ?? active.AppUpdateManifestUrl
  [并行] hostUpdateTask   = updater.CheckHostUpdateAsync(appUrl, ct)
        catalogUpdateTask = ep.CatalogUpdateManifestUrl 为空 ? Success
                                                          : updater.CheckAndDownloadAsync(url, ct)
  await WhenAll
  if catalogResult.PendingActivation → ReloadCoursesAfterContentUpdate(); 提示「已经更新，可以继续了」
  if !catalog.PendingActivation && active != null
        → updater.CheckAndDownloadCourseAsync(active, ct)（课程域更新）
  if 任一结果 RequiresHostUpdate && HostUpdateManifestUrl 非空 → 再查一次程序更新
  messages 汇总到 UpdateStatusText（仅手动/有更新/有失败时显示）

ContentUpdater.CheckAndDownloadCoreAsync(url, expectedCourseId, installedVer, ct)  (:426-598)
  url 空 → 失败「当前课程暂时没有配置在线更新」
  manifest = remote.GetManifestAsync<ContentUpdateManifest>(uri, Options,
                 versionSelector = courseVersion 优先、否则 contentVersion,
                 publishedAtSelector)
  ★ expectedCourseId 非空 且（manifest 缺 courseId/courseVersion 或带了 contentVersion）
      → 拒绝「课程更新地址没有返回独立课程清单，已拒绝旧式全量更新」
  isCourseUpdate = !IsNullOrWhiteSpace(manifest.CourseId)
  remoteVersion  = isCourseUpdate ? courseVersion : contentVersion
  校验：files 非空、remoteVersion 可 SemVer、courseId 与期望一致、schemaVersion == "1"
  EvaluateHostCompatibility(min, maxExclusive, requireMaximum:isCourseUpdate)   (:1111-1144)
     Invalid          → 失败
     BelowMinimum     → RequiresHostUpdate=true + RequiredHostVersion + HostUpdateManifestUrl
     AtOrAboveMaximum → 失败（「无法安装这次课程更新」）
     Compatible       ↓
  localVer = isCourseUpdate ? (installedVer ?? CurrentCourseVersion(courseId)) : CurrentContentVersion()
  remoteVersion <= localVer → 成功但「已是最新版本」
  否则进入更新事务：
    updateRoot = RepoRoot/.content-update ; staging = .../staging ; backup = .../backup
    DeleteDirectoryUnderRepoIfExists(updateRoot)
    files = NormalizeManifestFiles(manifest)      // 路径白名单 + 课程域 + sha256 必填 + 去重
    ValidateRequiredContentSet(manifest, files)   // 课程域：必须含 course.json + stages/index.json
                                                  // 目录域：courses/index.json、stages/index.json、
                                                  //         assets/i18n/zh-CN.json、schemas/course.schema.json、
                                                  //         schemas/guide-stage.schema.json
    for f in files: DownloadRawFile(...)          // remote.DownloadVerifiedAsync（校验 SHA-256）→ staging
    ValidateStagedJson(staging)                   // 所有 .json 可解析
    ValidateStagedCourseManifest(...)             // staged course.json 的 id/version/compatibility/content.* 与清单一致
    ApplyStagedUpdate(...)                        // 备份 → 写 transaction.json → 删 delete/prune → 覆盖复制
    成功 → { Success=true, PendingActivation=true }
  finally: 删除 .content-update
```

`ApplyStagedUpdate`（:753-813）细节：先对 `delete ∪ prune ∪ files` 里**已存在**的路径复制到 `backup/<rel>`（`OrdinalIgnoreCase` 字典），再 `WriteTransactionRecord` 落盘（`backup/../transaction.json`），**之后**才删/写；任何异常 → `RollBackAppliedUpdate`（删 created、还原 backedUp）再 rethrow；`finally` 里 `CleanupEmptyManagedDirectories` 递归删空目录（按长度降序）。

崩溃恢复：`RecoverInterruptedUpdate`（:285-320）读 `transaction.json` → `RollBackAppliedUpdate` → 删 `.content-update`；无 transaction.json 也直接删目录。

路径安全三层（:1044-1090）：

1. `NormalizeRawPath`：`\`→`/`、去前导 `/`、Trim、拒绝 rooted 与空段/`..`；
2. `AllowedContentPath`：白名单前缀 `courses/ stages/ assets/i18n/ assets/media/ schemas/ manifest.json`；**禁止** `assets/targets/`、禁止后缀 `.pdb .id0 .id1 .id2 .nam .til .idb .i64 .exe .dll .deps.json .runtimeconfig.json`；
3. `IsCourseOwnedPath`：课程域只允许 `courses/<id>.json`、`courses/<id>/…`、`stages/<id>/…`、`assets/i18n/<id>/…`、`assets/media/<id>/…`；再叠 `SafeAssetPath` 的 `GetFullPath + StartsWith(RepoRoot + sep)` 兜底。

### 4.6 程序自更新（双进程交接）

```
① 检查：ContentUpdater.CheckHostUpdateAsync(url, ct)  (:322-414)
   url 空 → Success + 「当前版本暂不支持程序更新」
   manifest = remote.GetManifestAsync<HostUpdateManifest>(appVersion / publishedAt)
   schemaVersion != "1" → 失败；appVersion <= 当前 → 「已经是最新版本」
   downloadUrl 空 / sha256 长度≠64 或非 hex / size<=0 → 失败
   DownloadUrl = ResolveManifestRelativeUrl(清单URL, downloadUrl).AbsoluteUri

② 下载：HostUpdateInstaller.DownloadAsync(update, dataRoot, ct)  (:95-164)
   updatesRoot = dataRoot/updates
   finalPath   = updates/host-{Sanitize(version)}-{sha256[0..12].lower}.exe
   已存在且 size+SHA256 通过 → 直接复用
   candidates = remote.GetReleaseDownloadCandidatesAsync(url)   // GitHub Release → 追加 gh-proxy.com 镜像
   逐个下载到 {finalPath}.{guid}.download → File.Move(overwrite)；失败换下一个源

③ 交棒：CreateRequestAndLaunch(...)  (:212-303)
   校验 DownloadUrl 为 http(s)、size>0、sha256 合法
   EnsurePathIsInside(updatesRoot, downloaded)；EnsureTargetExecutable(target)  // 必须叫 MoeCTF.RE.Tutorial.exe
   oldProcessId = 当前进程；oldProcessStartTimeUtcTicks = Process.StartTime.ToUniversalTime().Ticks
   request = updates/apply-{guid}.json（先写 .request.tmp 再 Move）
   Process.Start(downloaded, args: ["--apply-update", requestPath],
                 UseShellExecute:false, WorkingDirectory: targetDir)
   WaitForApplyWorkerReady(proc, requestPath, 10s)：轮询 apply-*.ready（成功）
                / apply-*.failed.txt（失败）/ proc.HasExited（失败并给日志路径）
                超时 → Kill(entireProcessTree:true) 并报错
   → Success：「程序更新已经准备好，关闭当前窗口后会自动安装。」

④ 执行：新进程 App.OnStartup → args[0]=="--apply-update" → HostUpdateInstaller.ApplyUpdateAsync(request)  (:315-408)
   读 request；schemaVersion 必须 "1"
   updatesRoot = GetUpdatesRoot(request.DataRoot)
   校验：request/downloaded 都在 updatesRoot 内；target 必须是 MoeCTF.RE.Tutorial.exe
        size/sha256 合法；**当前进程路径 == request.DownloadedExecutablePath**
        当前 exe 通过 size+SHA256 校验；target 存在
   WriteReadyMarker(request)                    // ← 旧进程据此确认「可以退了」
   WaitForOldProcessAsync(request)              // PID + StartTime ticks 双重校验（防 PID 复用），上限 2 分钟
   replacement = targetDir/.MoeCTF.RE.Tutorial.exe.{guid}.tmp
   CopyAndVerifyAsync(当前 exe → replacement)    // 流式复制 + 边算 SHA256 + FixedTimeEquals
   File.Move(replacement, target, overwrite:true)
   Process.Start(target)                        // 重启
   MarkRequestCompleted → *.completed.json；删 .ready / .failed.txt
   finally：删残留 tmp；失败 → FailApply 写 .failed.txt + 追加 .log
⑤ 收尾：CleanupCompletedUpdates（App.cs:50，每次正常启动）
   删 1 分钟前的 *.completed.json 及其记录的下载文件（跳过当前进程自身）
   删 1 天前的 *.download / *.request.tmp / apply-*.ready|.failed.txt|.log|.json
```

### 4.7 远端访问策略（GitHubRemoteClient）

```
清单（GetManifestAsync, :71-134）
  candidates = BuildManifestCandidates(uri)
     raw.githubusercontent / github.com/.../raw/ / jsDelivr 形态可解析 owner/repo/ref/path (:477-521)
     → 候选 1 = api.github.com/repos/{o}/{r}/contents/{path}?ref={ref}
       （Accept: application/vnd.github.raw+json；UA "MoeCTF.RE.Tutorial/0.1"）
     → 其余 = raw.githubusercontent.com + 3 个 jsDelivr 镜像（cdn/fastly/gcore），IsDirect=false
     非 GitHub → 单候选直连
  direct 候选用 DirectTimeout=1.2s 抢先；镜像并发测速+抓取，1.2s 内成功即取消镜像任务
  direct 失败 → 镜像结果按（SemVer ↓, publishedAt ↓, latency ↑）取第一
  同 URL 记「已见清单」(_seenManifests)：镜像返回更低版本/更旧时间 → 回退已见版本

文件下载（DownloadVerifiedAsync, :136-182）
  direct 1.2s 先试 → 成功即返回（并取消镜像测速）
  失败 → 按 RankHealthyMirrorsAsync 的延迟升序依次试，每个都 EnsureHash
  全失败 → HttpRequestException("No source returned the expected SHA-256.", AggregateException)

测速/限流/防护
  ProbeAsync：Range: bytes=0-4095，读 ≥1 字节，Stopwatch 计延迟，超时 8s（:270-302）
  _speedCache：按 scheme://authority 缓存 5 分钟
  _requestSlots = SemaphoreSlim(4,4)   // 全局最多 4 个并发请求
  ReadLimitedAsync：清单上限 2 MiB、文件上限 128 MiB，超限抛 InvalidDataException
  WithCacheBuster：每次请求加 moectfCacheBust={guid:N} 查询参数（绕 CDN 缓存）
  TrimUtf8Bom：清单先去 UTF-8 BOM 再反序列化
  ManifestLimitBytes=2097152 / RawFileLimitBytes=134217728（:48-50）
```

---

## 5. 关键技术点（每个都说明「为什么这么做」）

### 5.1 单文件发布下的资源读取：内嵌 ZIP + 启动自解包

`csproj` 用 `LogicalName` 把 ZIP 固定成 `MoeCTF.RE.Tutorial.Host.BundledContent.zip`，代码用 `typeof(...).Assembly.GetManifestResourceStream(常量)` 取流（BundledContentInstaller.cs:22、35）。
**为什么**：单文件发布后没有任何外挂目录可依赖；而课程内容需要**可写、可被用户/更新器修改**，不能长期读程序集资源（每次都要解压、无法局部更新）。所以选择「资源只做首次播种（seeding），运行期一切以磁盘为准」。把 `LogicalName` 写死而不是依赖默认推导，是因为本项目 `RootNamespace` 为空、目录层级改动会让默认资源名漂移——名字写死，改工程结构也不会静默失联。

### 5.2 三哈希比较的「非破坏性自愈安装」

`oldState[rel]`（上次我写的）、`h`（这次包里的）、`a`（磁盘现状）三者比较（:55-62）。
**为什么**：需要同时满足三个互相冲突的诉求——① 升级内容包要能更新文件；② 用户手改过的译文/图片不能被抹掉；③ 不能因为「没有记录」就把用户已有的同名文件当成自己的。三哈希刚好区分出「原样未动（可覆盖）」「用户改过（保留）」「无主文件（保留）」三种状态。代价是：**首次**带新 state 的场景下无法升级历史残留文件（见待确认 1）。

### 5.3 原子写 + reparse point 防护

`WriteEntryAtomically` / `WriteJsonAtomically`（:160-206）：写同目录临时文件（`FileMode.CreateNew` + `FileOptions.WriteThrough` + `Flush(flushToDisk:true)`）→ `File.Move(overwrite:true)`。
`EnsureSafeDirectory`（:208-229）逐级建目录并检查 `FileAttributes.ReparsePoint`，安装根目录也查（:42-45）。
**为什么**：① 断电/被杀进程留下的半截文件会直接坏内容，`Move`（同卷 rename）是原子替换；② `WriteThrough` 保证 rename 前数据真的落盘；③ ZIP 解包是经典路径穿越/符号链接攻击面，`ReparsePoint` 检查挡住「预置一个 junction 指向 C:\\Windows 再让解包写入」的手法。

### 5.4 路径白名单 + 前缀逃逸检查（三重）

`ValidateEntries`（:69-107，安装侧）与 `NormalizeRawPath / AllowedContentPath / IsCourseOwnedPath / SafeAssetPath`（更新侧，:1015-1090）。
**为什么**：内容更新清单来自网络（raw.githubusercontent.com），是**不可信输入**。三重限制分别防：穿越（`..` / rooted）、越权目录（不许写 `assets/targets/`——那是用户附件区，写了就等于伪造题目）、越权文件类型（不许投递 `.exe/.dll/.deps.json`——否则内容更新变成任意代码执行通道）。`GetFullPath + StartsWith(root + sep)` 用 `OrdinalIgnoreCase` 是 Windows 语义的正确写法（同时避免 `C:\\foo` 误匹配 `C:\\foobar` 的经典前缀漏洞）。

### 5.5 自实现 SemanticVersion（含 prerelease）

`SemanticVersion.cs`：正则严格（不允许前导零）、prerelease 逐段比较（数字段 < 字母段、字母段 `CompareOrdinal`）、无 prerelease 视为更高（:67-74）。
**为什么**：BCL 没有 SemVer 类型；`System.Version` 不支持 prerelease 且 `1.0` ≠ `1.0.0`；引 `NuGet.Versioning` 会破坏「纯 BCL、无第三方依赖」的发布约束。三处复用：课程优选（CourseCatalog.cs:647-650）、宿主兼容区间（ContentUpdater.cs:1111-1144）、更新可用性（:540）。

### 5.6 FlowFingerprint：内容指纹驱动的进度失效

`ComputeFlowFingerprint`（CourseCatalog.cs:918-939）＝ 对 `stages/*.json` 的 `名字小写 + \0 + 内容 + \0` 求 SHA256。
**为什么**：进度里存的是「第几段 / 段 id」，一旦课程改版（增删段落、改顺序、改 id），旧进度会指向错位置甚至不存在的段落。用**内容指纹**而不是版本号做判据，是因为作者经常只改 JSON 忘了 bump `version`；指纹把「任何段落文件变化」都算作失效，触发 `ForCourse` 的软重置。加入文件名是为了同时捕获「改名/调序」。

### 5.7 扁平 i18n 字典 + 两级覆写

`I18nService` 只支持**一层** key→string；`Load` 先 fallback 目录再课程目录（后者覆盖前者）。
**为什么**：① 扁平结构让 `T("a.b.c")` 变成一次字典查找，WPF 里每次刷新都要取文本，嵌套树遍历太贵；② 课程包只需覆写自己关心的 key（`chall1.*` + 少量 `common.*`），其余落回内置 `assets/i18n`；③ 查不到就返回 key 本身，配合 `GetRaw`，UI 能做「有翻译用翻译、没有就用写死的中文」（MainWindow.cs:536-543），避免缺 key 时界面空白或显示占位符。
`{name}` 占位用正则替换而非 `string.Format`，是为了忽略未知占位而不抛 `FormatException`（内容作者手写 JSON，容错优先）。

### 5.8 两套命名约定共存

内容契约全部显式 `[JsonPropertyName]`（camelCase）；本地状态里 `tools/courseTargets/courseChoices` 是 camelCase，而 `ProgressData` 用属性名直出（PascalCase）。
**为什么**：内容包是**对外协议**，必须与 `schemas/*.schema.json` 严格一致、可被人手写/CI 校验，所以显式标注；本地状态是**内部实现**，早期版本已经写出 PascalCase 文件，改名就等于让所有老用户进度归零，所以 `ProgressData` 上刻意不加任何 `JsonPropertyName`。这种混用是**历史包袱**，不是设计意图。

### 5.9 更新事务（journal + 回滚）与崩溃恢复

`UpdateTransaction{backedUp, created}` 在 `backup/../transaction.json`（ContentUpdater.cs:815-829），写盘时机在**删/写之前**。
**为什么**：内容更新是「多文件 + 删除 + 覆盖」的复合操作，文件系统没有跨文件事务。先备份再记账再动手，崩溃后 `RecoverInterruptedUpdate`（MainWindow 构造里的第一步）就能凭账本把仓库还原到更新前状态，不会留下「index.json 已更新、stages 还是旧的」这种半截状态——那种状态会直接让课程加载失败（`IsValidCourseLayout` 不通过 → 整门课消失）。

### 5.10 清单与内容的一致性交叉校验

`ValidateStagedCourseManifest`（:680-736）要求 staged `course.json` 的 `id`、`version`、`compatibility.{min,max}`、`content.{stagesDir,i18nDir,assetsDir,stageIndex}` 全部与更新清单声明一致，且 content 各目录必须落在 `courses/<id>/…` 命名空间内。
**为什么**：更新清单和内容是**两个不同的 URL**（清单在 `courses/chall1/update.json`，内容散在仓库各处、可能走不同 CDN 与不同 commit）。没有这层交叉校验，一次发布失误（清单说 1.0.2、内容还是 1.0.1；或内容写进别的课程目录）就会静默污染本地，事后极难定位。

### 5.11 课程域更新 vs 目录域更新（拒绝旧式全量）

`expectedCourseId` 非空时，若清单缺 `courseId/courseVersion` 或带了 `contentVersion` → 直接拒绝（:453-461）；`ValidateRequiredContentSet` 对课程域只要求 `course.json + stages/index.json`，对目录域要求 5 个核心文件（:633-659）。
**为什么**：早期协议用一个「全局 contentVersion + 全量文件」描述更新，多课程并存后会导致「更新 A 课程把 B 课程的文件也覆盖/删除」。改成课程域后，每个课程只能动自己的命名空间，目录域只负责公共骨架。显式拒绝旧格式，是为了避免老清单被误当成新协议执行——静默做错误的删除比报错危险。

### 5.12 GitHub 多源并发探测 + 已见清单防回退

`GitHubRemoteClient`（:71-134、:245-302）：直连 1.2s 抢跑、4 个候选并发、延迟缓存 5 分钟、`SemaphoreSlim(4)` 限流、每次请求带 cache-buster、`_seenManifests` 记住已确认版本。
**为什么**：raw.githubusercontent.com 在国内经常超时，jsDelivr 的 cdn/fastly/gcore 三个域可用性各不相同，必须「并发探测 + 谁快用谁」才能在 1~2 秒内完成检查；镜像 CDN 有缓存滞后，可能返回**旧清单**，`_seenManifests` 用 (SemVer, publishedAt) 单调比较把旧版本挡回去，防止「检查更新说已是最新」之后又提示「发现新版本」的抖动，也防止错误/被篡改的镜像把版本降级。首选 `api.github.com/contents` 是因为它比 raw 域更稳、可协商 content type（本项目只用了 raw media type，未用 ETag）。

### 5.13 流式哈希 + 定长校验 + 固定时间比较

`DownloadCandidateAsync`（HostUpdateInstaller.cs:166-210）与 `ReadLimitedAsync`（GitHubRemoteClient.cs:348-366）：边读边 `IncrementalHash.AppendData`、边写文件、累计字节超 `expectedSize` 立刻抛、结束再比 `totalBytes == expectedSize` 与 `CryptographicOperations.FixedTimeEquals`。
**为什么**：程序更新是 ~2.6 MB 的 exe，不能整体读进内存（既浪费也挡不住「服务器返回超大响应」的资源耗尽）；先看 `Content-Length`、再边读边计、最后比总长，形成三层大小防护；`FixedTimeEquals` 避免哈希比较的时序侧信道。

### 5.14 双进程自更新 + 标记文件握手

`CreateRequestAndLaunch`（:212-303）→ 新 exe `--apply-update` → `ApplyUpdateAsync`（:315-408）。

**为什么**：

- **Windows 不能覆盖正在运行的 exe**：必须由另一个进程在旧进程退出后 `File.Move` 替换。这里选择「新版本 exe 自己当安装器」而不是额外 updater.exe，是为了不引入第三个可执行文件（单文件发布友好）。
- **握手而不是盲等**：新进程先把下载件校验通过、request 校验通过，才写 `apply-*.ready`；旧进程轮询 10 秒，看到 ready 才允许用户关窗。这样「下载坏了/参数错了」会在**窗口还开着**的时候暴露，而不是关窗后程序消失。
- **防 PID 复用的等待**：只 `WaitForExit(pid)` 不可靠（PID 会被复用），所以同时记录并比对 `StartTime.ToUniversalTime().Ticks`（:496-501）。
- **替换前再算一次哈希**：`CopyAndVerifyAsync` 复制到目标目录的临时文件并校验，通过后才 `Move` 覆盖——避免跨卷/半写导致 exe 损坏（磁盘满、杀软拦截）。
- **日志与失败标记**：`.log / .failed.txt / .completed.json` 三件套让失败可诊断；`ShouldShowApplyFailureDialog` 决定要不要弹窗（App.cs:25-28）。
- `EnsureTargetExecutable` 强制目标文件名必须是 `MoeCTF.RE.Tutorial.exe`，`currentExecutablePath == request.DownloadedExecutablePath` 强制「只有被下载的那个 exe 能执行安装」，把「用一个 json 让程序替换任意文件」的滥用面关掉。

### 5.15 WPF / 线程相关

- `DispatcherTimer`（200 ms 跟踪窗口、1 s 刷新时钟，MainWindow.cs:353-371）——UI 定时器必须留在 UI 线程，否则跨线程访问控件抛异常；200 ms 是「UIA 扫描不太贵 + 观感不卡」的折中（单次 locator 的 `timeoutMs` 是 500~1000 ms，配合 `maxNodes` 上限）。
- 更新全部 `async Task` + `CancellationToken`；`Closed` 时 `_updateCancellation.Cancel()`（:321-324）——防止窗口关掉后回调还在写 UI。
- 服务层所有 `await` 后接 `.ConfigureAwait(false)`，UI 层不接——服务层不绑 SynchronizationContext，避免死锁。
- `App.OnStartup` 里对 `ApplyUpdateAsync` 用 `Task.Run(...).GetAwaiter().GetResult()`（App.cs:23）——故意同步阻塞：这是「安装器模式」，`ShutdownMode.OnExplicitShutdown` 且不依赖消息循环，阻塞最简单；`--bootstrap-only` 分支只做目录探测（:32-48），供安装包/CI 判断「内容是否就绪」。
- 字体 `assets/fonts/jetbrainsmono-regular.ttf` 也以 `LogicalName` 内嵌（csproj:27），与内容包里的 `JetBrainsMono-OFL.txt` 分属「运行时资源」与「许可证文本」两套。

### 5.16 【推断】为什么内容根目录放在 exe 同级而非 %LOCALAPPDATA%

单文件绿色分发（拷走 exe 就能跑）+ 内容需要被内容更新器与服务层多处写（`config/`、`.content-update/`、`updates/`）+ 教学分发希望所有东西在一个文件夹。代价是**装到 C:\\Program Files 下会因为没有写权限而全盘失败**（见 6.3 第 1 条）。

---

## 6. 复刻要点（.NET 8 WPF 重写）

### 6.1 必须保留的契约（改了就不兼容）

1. **目录布局**：`{DataRoot}/{courses/index.json, courses/<id>/course.json, courses/<id>/stages/index.json, courses/<id>/stages/<name>.json, courses/<id>/i18n/<locale>.json, courses/<id>/assets/…, assets/i18n/<locale>.json, schemas/, config/{progress.json, requirements.json, bundled-content-state.json}}`。DataRoot = `AppContext.BaseDirectory + "\\MoeCTF.RE.Tutorial"`。
2. **JSON 命名与结构**：内容文件 camelCase 且字段名逐字一致（`titleKey / goalKey / maxHostVersionExclusive / fallbackToWindow / requireAllNameContains / rejectContentContains` …）；`progress.json` 必须 PascalCase 且读取时不加 `PropertyNameCaseInsensitive`（否则老进度读不出来）。
3. **课程发现规则**：`courses/index.json.courses[]` 是相对 DataRoot 的路径；课程必须通过 `IsValidCourseLayout`（stages/index.json + i18n/zh-CN.json + assets 目录三者齐备），否则整门课丢弃。
4. **段落装载规则**：顺序 = `stages/index.json.stages[]`（名字无扩展）→ 逐个读 `{name}.json` → 按 `order` 升序稳定排序。**不要**改成读 `course.json.flow`（运行时没实现）。
5. **进度身份**：`(CourseVersion, FlowFingerprint)` 任一不匹配即重置；指纹算法 `lower(filename) \0 bytes \0` 顺序拼接后 SHA256（小写 hex）——要与原版**完全一致**才能复用别人的 progress.json。
6. **i18n 解析**：扁平字典、`{name}` 正则替换、缺 key 返回 key 本身；两级覆写（课程 i18n 覆盖内置 i18n，后载胜）。
7. **需求匹配顺序**：`fileName 通配(*?) → pathContains(all) → size → sha256`，四项全过才算命中；`kind` 只区分 tool/target。
8. **SemVer 与兼容区间**：`[minHostVersion, maxHostVersionExclusive)`，宿主版本 = `Assembly.Version` 的 `Major.Minor.Build`（**不是** InformationalVersion）。
9. **更新协议**：`schemaVersion` 必须 `"1"`；课程域清单必须带 `courseId + courseVersion + min + max`；`files[].sha256` 必需；路径白名单与禁写清单照抄（`assets/targets/`、`.exe/.dll/.pdb/.id0…`）。
10. **程序更新 CLI**：`--apply-update <requestJson>`、`--bootstrap-only`、request 的 `schemaVersion="1"` 与字段名、`.ready / .failed.txt / .log / .completed.json` 标记文件名、目标 exe 名 `MoeCTF.RE.Tutorial.exe`。

### 6.2 可以简化 / 可以砍

- **GitHub 镜像探测**（jsDelivr ×3 + gh-proxy + 延迟缓存 + `_seenManifests`）：自建/单一 CDN 场景可退化为「直连 + 一次重试」；只要保留 `DownloadVerifiedAsync` 的 SHA-256 校验与 `ReadLimitedAsync` 的上限。
- **课程目录域更新**（`catalogUpdateManifestUrl` + 5 个强制文件）：单课程分发时只需课程域更新。
- **`schemas/*.schema.json`**：运行时**完全不读**（只在更新清单的「必须包含文件」列表里被点名）。可只作为 CI 校验资产保留。
- **`course-catalog-update.schema.json`**：C# 里**没有**对应类型（ContentUpdater 的 CourseIndex 只解析 `catalogVersion/contentVersion/courses:string[]`），可砍掉或按新协议重设计。
- **`flow.entry / requires / next / when`**、**`content.stageIndex`**：未被实现的声明，可移除以免误导。
- **双进程自更新**：若允许「下载后让用户手动重开」，可只保留 `DownloadAsync` + 校验。
- **`fakeAnswerKeys` / `completionMode:"answer"`**：当前内容全是 `guided / external_submit`，判题模式可暂不实现（但要保留字段解析，否则老内容加载报错）。

### 6.3 坑（照抄就能踩到）

1. **数据根目录需要写权限**：`config/`、`.content-update/`、`updates/` 全在 `AppContext.BaseDirectory` 下。装到 `C:\\Program Files` 会让内容安装、存进度、更新全部失败。复刻时应显式探测可写性（或改到 `%LOCALAPPDATA%`，但那样就不兼容原版目录契约）。
2. **`Install` 的 `continue` 语义会「卡住」升级**：首次运行（state 空）+ 目标文件已存在且内容不同 → 永不覆盖。用户机器上残留同名文件（或从没带 state 的旧版本升上来）时，新版内置内容**不会生效**，只能手动删目录。
3. **`ProgressStore.Load` 用默认 options**（区分大小写）而 `Save` 写 PascalCase：手改 `progress.json` 时写 camelCase 字段会被静默丢弃（`ActiveCourseId` 变回 `freshman-re`）。而 `CourseCatalog` / `ContentUpdater` 都是 case-insensitive——两套行为别混。
4. **`JsonSerializer` 反序列化会替换字典实例**：`ProgressData.Courses`、`RequirementActivationState.*` 都带 public setter，STJ 会赋一个**默认比较器（Ordinal）**的新字典，字段初始化里的 `OrdinalIgnoreCase` 在 `Load` 之后**丢失**【推断·STJ 行为】。复刻时应在 `Load` 后重建字典，或干脆让 key 统一 `ToLowerInvariant`，不依赖比较器。
5. **同项目内两种路径分段校验不一致**：`NormalizeRawPath`（ContentUpdater.cs:1051）判 `p.Length == 0 || p == ".."`，而 `ResolveTargetAssetPath`（RequirementActivationService.cs:487）用 `IsNullOrWhiteSpace(p)`——含空格的文件名（如 `my tool.exe`）在两处行为不同，复刻时要统一。
6. **`ComputeFlowFingerprint` 与 `IdentityMatches` 的空串陷阱**：指纹计算异常时返回空串，`ForCourse(id, version, "")` 在 `CourseVersion` 相等且 `FlowFingerprint` 都为空时**判定匹配**，即「读不到 stages 目录时不会重置进度」——行为宽容。复刻时别把空串改成 `null` 后又用 `string.IsNullOrEmpty` 提前 return，否则语义变化。
7. **单文件 + `GetManifestResourceStream` 的静默降级**：`Install` 拿不到流就 `return false`（:36-39），PathService 转而向上找目录，开发期会**意外命中源码树**（`FindWorkspaceRoot` 认 `.slnx + assets/tools`）。打包时务必核对 `LogicalName`，并在 CI 里跑 `--bootstrap-only`（exit 2 即内容不就绪）。
8. **`i18n.Load` 对非字符串值会抛异常**（`JsonElement.GetString()`）：内容作者一旦写成嵌套对象或数字，启动即崩，且 `LoadDirectory` 没有 try/catch。复刻时要么加 `ValueKind == JsonValueKind.String` 判断，要么在 CI 里校验扁平性。
9. **`locale` 硬编码** `"zh-CN"`（MainWindow.cs:593、603）：`en-US.json` 是死资产。想加语言切换，需要同时改 `ApplyActiveCourse` 的两处调用与 `I18nService.Locale` 的持久化（当前没有任何设置项）。
10. **i18n / media 有两套路径归属**：`assets/i18n/<courseId>/…` 与 `assets/media/<courseId>/…` 属于课程域，而课程实际用的是 `courses/<id>/assets/media/…`。两套路径同时被 `IsCourseOwnedPath` 承认，容易搞混（当前内容包只用后者）。
11. **`step.image` 是相对 `ContentRoot` 的完整相对路径**（`courses/chall1/assets/media/01-die-analysis.png`），不是相对 `assetsDir`。复刻时若按 `assetsDir` 拼会 404。schema 里 `optionalMediaPath` 的 pattern 允许 `assets/media/` 前缀，与实现不一致。
12. **`maxNodes / timeoutMs` 是每次定位的预算**（50~10000 / 100~10000）：UIA 树遍历在大程序（IDA）上很贵，`maxNodes: 3000 + timeoutMs: 1000` 已经是「卡到能感知」的量级。复刻时若改成同步阻塞 UI，界面会明显卡顿——必须保持「后台扫描 + 结果回 UI」。
13. **DPI / 跨进程窗口句柄**：本子系统只存 `rect.space ∈ {screen, window, target, host, control, anchor}` 与 `adjust{offsetX,offsetY,inflate,width,height}`，**坐标系语义由采集层决定**（Per-Monitor DPI 下 `screen` 是物理像素还是 DIP 必须与写内容的人对齐，否则浮层错位）。复刻时务必保留 `space` 字段并统一按「物理像素（`GetWindowRect` 口径）」解释。
14. **`HttpClient` 生命周期**：`ContentUpdater` 自建 `HttpClient{Timeout=20s}`，`HostUpdateInstaller` 另建 `HttpClient{Timeout=5min}`，两者各自 `new GitHubRemoteClient`。复刻时应注入 `IHttpClientFactory`，否则 DNS 变更/连接泄漏在长跑场景会咬人（原版是桌面短会话，问题不大）。

---

## 7. schema ↔ C# 类型 字段一一对应表

| schema 文件 | 对应 C# 类型（文件:行） | 对齐情况 |
| --- | --- | --- |
| `course-catalog.schema.json` | `CourseCatalog.CourseIndex`（CourseCatalog.cs:17-27） | `courses / catalogUpdateManifestUrl / appUpdateManifestUrl` ✔；`schemaVersion`、`catalogVersion` **无属性**（catalogVersion 由 `ContentUpdater.CourseIndex` 读） |
| `course.schema.json` | `CourseManifest` + 7 个嵌套私有类（CourseCatalog.cs:35-234） | 见 7.1 逐项 |
| `guide-stage.schema.json` | `StageDefinition` + `StageStep` + `GuideDeviationSpec` + `GuideSpec/GuideTargetSpec/GuideLocatorSpec/GuideOverlaySpec/GuideConditionSpec/GuideBranchSpec/GuideRectSpec/GuideRectAdjustSpec` | `target/locators/overlay/fallback/rect/adjust/condition/branch` 全字段 1:1 ✔；**schema 缺** `highlightScope/highlightHost/highlightProcess/highlightNames/highlightControlType`（C# 有 → schema 的 `additionalProperties:false` 会判非法）；schema 的 `optionalMediaPath` pattern 与实现的「相对 ContentRoot 全路径」不一致 |
| `content-update.schema.json` | `ContentUpdateManifest` + `ContentUpdateFile` | 全字段 1:1 ✔（`prune` 默认 false、`delete` 默认空、两个分支（`contentVersion` 或 `courseId+courseVersion`）与 `CheckAndDownloadCoreAsync` 的判断一致） |
| `app-update.schema.json` | `HostUpdateManifest` | 全字段 1:1 ✔（channel 默认 stable、mandatory 默认 false、size long?） |
| `course-catalog-update.schema.json` | **无对应类型** | ✗ `courses[]` 在 schema 里是对象数组（id/version/compatibility/manifest/updateManifestUrl），而 `ContentUpdater.CourseIndex.Courses` 是 `string[]`；该 schema 目前无人消费 |

### 7.1 `course.schema.json` ↔ C# 逐项

| schema 路径 | C# 属性 | 状态 |
| --- | --- | --- |
| `schemaVersion` | — | ✗ 未解析（不校验） |
| `id / version / status` | `Id / Version / Status` | ✔ |
| `compatibility.minHostVersion / maxHostVersionExclusive` | `Compatibility.MinHostVersion / MaxHostVersionExclusive` | ✔ |
| `metadata.title|subtitle|summary|track|difficulty|estimatedMinutes|authors` | `Metadata.*` | ✔ |
| `metadata.titleKey|subtitleKey|summaryKey|tags|icon|homepageUrl` | — | ✗ 未解析 |
| `changelog[].version|date|text` | `CourseChangelogEntryManifest.*` | ✔ |
| `changelog[].items` | — | ✗ 未解析 |
| `content.stagesDir|i18nDir|assetsDir` | `Content.*` | ✔ |
| `content.stageIndex` | — | ✗ 未解析（StageRepository 硬编码 index.json） |
| `requirements.id|title|description|allOf|anyOf` | `CourseRequirementNodeManifest.*` | ✔（`titleKey/descriptionKey` ✗） |
| `requirements[叶子].kind|label|toolId|targetId|fileName|match|asset` | `CourseRequirementNodeManifest.*` | ✔（`labelKey/descriptionKey` ✗） |
| `match.fileNames|pathContains|sha256|size` | `CourseRequirementMatchManifest.*` | ✔ |
| `match.recursive` | — | ✗ 未解析（递归行为写死在 `ExpandInputPaths`） |
| `asset.path` | `CourseRequirementAssetManifest.Path` | ✔ |
| `asset.copyMode|companions` | — | ✗ 未解析（复制行为写死在 `SetTargetPath`） |
| `flow.entry|stages[].id|path` | `CourseFlowManifest.Stages` / `CourseFlowStageManifest.Id|Path` | 部分 ✔（id/path 仅用于 `stageCount` 兜底） |
| `flow.stages[].titleKey|requires|when|next` | — | ✗ 未解析 |
| `updateManifestUrl|appUpdateManifestUrl` | 同名 | ✔ |
| `$defs.relativeDir / contentPath`（含禁写后缀） | `ResolveDir / SafeCombine`（CourseCatalog.cs:872-886、941-954） | 语义对齐（代码用 `GetFullPath + StartsWith` 实现） |

### 7.2 `guide-stage.schema.json` ↔ C# 逐项（关键差异）

| 位置 | schema | C# | 状态 |
| --- | --- | --- | --- |
| 顶层 | `id/order/titleKey/goalKey/answerKey/targets/tools/guideMode/steps` | 同名 | ✔ |
| 顶层 | `completionMode`（默认 answer）、`fakeAnswerKeys`、`freeExploreTextKey`、`deviations` | 同名 | ✔ |
| 顶层 | — | `highlightScope/highlightHost/highlightProcess/highlightNames/highlightControlType` | ✗ schema 缺失 |
| `step` | `id/textKey/bodyKey/image/action/tool/file/guide/trigger/branches` | 同名 | ✔ |
| `step.action` | `const "none"` | `string` 默认 `"none"` | C# 更宽（不做枚举校验） |
| `deviation` | `id/textKey/bodyKey/image/priority/when/guide` | 同名 | ✔ |
| `guide` | `target/locators/overlay/fallback` | 同名 | ✔ |
| `target` / `locator` | 10 / 29 个字段 | 同名同集合（逐项已核） | ✔ |
| `rect` / `adjust` / `overlay` | 全字段 | 同名 | ✔ |
| `branch` | `trigger/when/next` | 同名 | ✔ |
| `condition` | `kind/not/process/key/value/valueKey/locator/all/any` | 同名 | ✔ |
| `scope` / `adapter` 枚举 | `{host,process,screen,none} / {die,ida,ghidra,dnspy,x64dbg,win32}` | `string`（无枚举） | C# 更宽 |
| `optionalMediaPath` | `^(assets/media/)?[^\\:*?"<>|]+\.(png|jpg|jpeg|webp)$` | 直接当相对 ContentRoot 的路径用 | ✗ 语义不一致 |

---

## 8. 待确认问题清单

1. **`BundledContentInstaller.Install` 的「不覆盖」分支是否有意** — `BundledContentInstaller.cs:55-62`：当 `state` 为空（首次运行 / 新机器）且目标文件已存在且内容不同时走 `continue`，即**永远不写**。请确认这是「用户文件优先」的有意设计，还是「没有 state 时也应该覆盖」的遗漏——它决定升级路径是否会出现「新版本内容不生效」。
2. **`ProgressStore.Load` 为何不带 `PropertyNameCaseInsensitive`** — `ProgressStore.cs:66` 用默认选项反序列化，而同项目另外 4 处都开了大小写不敏感（CourseCatalog.cs:591、ContentUpdater.cs:264、RequirementActivationService.cs:218、BundledContentInstaller.cs:28）。请确认是否为有意的严格外部契约；若是，建议在 `Save` 处加注释，否则手改配置会静默失效。
3. **`ContentUpdater.SupportedSchemaVersion` 常量未被引用** — 声明于 `ContentUpdater.cs:260`，实际校验用字面量 `"1"`（:358、:491）。请确认是否有意保留（如为将来多版本分支预留），否则是死代码。
4. **本地 `chall1` 与 `course.json` 声明不一致** — `D:\ctf\RE-ctf\teach-demo\chall1` 大小 16,120 B 匹配 `match.size`，但实测 SHA-256 = `ea730409f02618d07c163551c10f3a6313fd3509cf64e9090dc78ae2c4b825d1`，而 `course.json:50` 声明 `f86808dd…fa44`。请确认哪个才是「正确」的题目附件（`RequirementActivationService.Matches` 的 `:473-480` 会因哈希不符拒绝激活 `target.chall1`）。
5. **`course-catalog-update.schema.json` 的 `courses[]` 谁在消费** — 该 schema 定义 `{id, version, compatibility, manifest, updateManifestUrl}` 对象数组，但 `ContentUpdater.CourseIndex`（ContentUpdater.cs:27-37）只解析 `catalogVersion / contentVersion / courses:string[]`，`CourseCatalog.CourseIndex`（:17-27）也只有 `string[]`。请确认该清单是「未来协议」还是「遗留未实现」。

---

## 附录 A：本报告引用的关键文件与行号索引

| 主题 | 位置 |
| --- | --- |
| 数据根 / 内容根 | `PathService.cs:34-38, 40-64, 76-83` |
| 内嵌包安装 | `BundledContentInstaller.cs:22, 32-67, 55-62, 69-122, 160-206, 208-229` |
| 课程发现 / 校验 | `CourseCatalog.cs:17-234, 589-609, 625-650, 664-732, 769-828, 897-939` |
| 段落装载 | `StageRepository.cs:17-44`；`StageDefinition.cs`；`StageStep.cs`；`GuideDeviationSpec.cs` |
| 进度 | `ProgressStore.cs:20-111`；`ProgressData.cs`；`CourseProgressData.cs` |
| 需求激活 | `RequirementActivationService.cs:231-538`；`RequirementActivationState.cs`；`RequirementActivationStatus.cs` |
| i18n | `I18nService.cs:15-69`；`MainWindow.cs:586-608` |
| 内容更新 | `ContentUpdater.cs:285-320, 426-598, 600-736, 753-829, 879-928, 1015-1144` |
| 远端 | `GitHubRemoteClient.cs:46-64, 71-182, 245-366, 421-547` |
| 程序自更新 | `HostUpdateInstaller.cs:17-63, 95-303, 315-408, 410-453, 488-520, 646-689`；`App.cs:17-53` |
| 版本 | `SemanticVersion.cs:9, 27-103` |
| 打包 | `MoeCTF.RE.Tutorial.csproj:1-37` |

## 附录 B：可复现命令

```powershell
# 1. 解内嵌包
Expand-Archive -Path D:\ctf\RE-ctf\teach-demo\_src\MoeCTF.RE.Tutorial.Host.BundledContent.zip -DestinationPath D:\ctf\RE-ctf\teach-demo\_bundle_unzip -Force

# 2. 列树 + 大小
Get-ChildItem -Recurse -File D:\ctf\RE-ctf\teach-demo\_bundle_unzip |
  Sort-Object FullName | ForEach-Object { '{0}|{1}' -f $_.FullName, $_.Length }

# 3. 校验 zip 内容 == 运行期目录（排除 config），期望无输出
$a='D:\ctf\RE-ctf\teach-demo\_bundle_unzip'; $b='D:\ctf\RE-ctf\teach-demo\MoeCTF.RE.Tutorial'
Compare-Object (Get-ChildItem -Recurse -File $a | ForEach-Object FullName) (Get-ChildItem -Recurse -File $b | Where-Object FullName -notlike '*\config\*' | ForEach-Object FullName)

# 4. 抽查哈希 vs bundled-content-state.json
(Get-FileHash -Algorithm SHA256 D:\ctf\RE-ctf\teach-demo\_bundle_unzip\courses\chall1\course.json).Hash
# → F51022518CD0A0BCE5108ADFA675C0EFB86F7E5625E08669B73AF2AA158EE1B2（与 state 一致）

# 5. 确认数据根在 exe 同级（不是 LOCALAPPDATA）
Test-Path D:\ctf\RE-ctf\teach-demo\MoeCTF.RE.Tutorial\config\bundled-content-state.json   # True

# 6. 校验本地 chall1 是否满足 course.json 的 sha256 声明
(Get-FileHash -Algorithm SHA256 D:\ctf\RE-ctf\teach-demo\chall1).Hash.ToLower()   # ea730409…b825d1 ≠ f86808dd…31fa44（与 course.json 声明不同）
```
