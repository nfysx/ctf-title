# RETutor —— RE.Tutorial.exe 的仿制实现

对照分析报告：`../逆向分析报告-功能实现与仿制.md`

## 编译 / 运行

```powershell
& 'D:\apk\_tools\dotnet\dotnet.exe' build .\RETutor.csproj -c Debug
.\bin\Debug\net8.0-windows\RETutor.exe
```

内容根解析顺序：`RETUTOR_CONTENT` 环境变量 → 向上 10 层找 `courses/index.json` →
同级 `MoeCTF.RE.Tutorial` → 同级 `RETutorContent`。
默认会直接命中 `../MoeCTF.RE.Tutorial/`（原程序生成的内容目录），即同一份内容包两个宿主。

## 已实现（对应原程序模块）

| RETutor | 原程序 |
| --- | --- |
| `Models/ContentModels.cs` | CourseCatalog / StageDefinition / StageStep / GuideSpec 全字段 |
| `Services/ContentStore.cs` | PathService + CourseCatalog + StageRepository + I18nService |
| `Services/ProgressStore.cs` | ProgressStore（含课程版本失效） |
| `Services/WindowTracker.cs` | WindowTracker（EnumWindows + DWM 扩展边框 + 客户区 + 进程树 BFS） |
| `Services/UiaLocator.cs` | ControlLocator（UIA 快照 + 打分 + 4 级文本抽取） |
| `Services/ToolCatalog.cs` | ToolAdapter / ToolSpec |
| `Guide/GuideEngine.cs` | GuideSceneMatcher + GuideRuntime（打分 / 定位 / 计划 / 条件） |
| `Guide/OverlayWindow.cs` | CoachOverlayWindow（全屏置顶点击穿透 HUD） |
| `MainWindow.cs` | MainWindow（Hub 课程页 ⇄ Desk + 200ms 追踪 + 自动推进 + 资源卡片 + 自定义标题栏/状态栏） |
| `Services/ResourceRegistry.cs` | 用户自选资源（工具路径 / 附件 + sha256）持久化 |
| `Services/ContentInstaller.cs` | 内嵌 `Content.zip` 解包 + 指纹校验（包变了自动刷新） |
| `Services/UpdateService.cs` | 在线更新：清单拉取、版本比较、逐文件下载 + SHA-256 校验、落盘 |

## 在线更新（真联网）

`MainWindow.CheckForUpdates()` → `Services/UpdateService.cs`：

| 动作 | 实现 |
| --- | --- |
| 查课程更新 | GET `update-config.json → courseUpdateUrl` → 比 `courseVersion` |
| 查程序更新 | GET `update-config.json → appUpdateUrl` → 比 `appVersion`，并校验清单 `channel` 与本地渠道一致 |
| 镜像回退 | 每个 URL 先直连，再自动试 `cdn.jsdelivr.net/gh/...`、`cdn.statically.io/gh/...`（raw.githubusercontent 常被墙/限流） |
| 安装课程更新 | 逐文件下载 → **逐个 SHA-256 校验** → 先全落到 `.update-staging`，全部通过才覆盖内容根 → 处理清单里的 `delete` → 重载课程 |
| 程序更新 | 下载到 `%TEMP%\RETutor-update\RETutor-<ver>.exe` 并校验 SHA-256，**不自动替换正在运行的 exe**（提示手动替换） |

- 第一次点「检查更新」= 只检查；若课程有新版本，按钮会变成「安装课程更新」，再点一次才下载安装。
- 版本比较用 `VersionUtil`（数字段比较，够用）；清单不可达/校验失败都会在卡片下方与状态栏给出原因，不会动内容根。
- 课程版本变化会让该课进度按原有契约重置（`CourseVersion` 变了）——这是原程序的行为，更新完成会明确提示。

## 更新渠道与原版完全分离

仿制版**不读、不写、不请求原版的任何东西**：

| 项 | 原版 | RETutor |
| --- | --- | --- |
| 数据目录 | `MoeCTF.RE.Tutorial\`（exe 同级） | `RETutorContent\`（exe 同级，内嵌包解出；代码里已删掉对原版目录的探测） |
| 更新清单 | `course.json` 里的 `updateManifestUrl` / `appUpdateManifestUrl`（指向原版仓库） | **只用** <exe>\update-config.json（或 %LOCALAPPDATA%\RETutor\update-config.json、环境变量 `RETUTOR_UPDATE_CONFIG`） |
| 渠道校验 | 无 | 程序更新清单的 `channel` 必须等于配置里的 `channel`（默认 `retutor`），否则忽略并提示"渠道不符" |
| 进度 / 工具路径 | `config\progress.json`、`config\requirements.json` | `%LOCALAPPDATA%\RETutor\progress.json`、`resources.json` |

没有 `update-config.json` 时点「检查更新」只提示"未配置更新源（只使用内嵌内容包）"，**不会**去访问任何原版地址。
示例见 `update-config.sample.json`（改名为 `update-config.json` 即生效）：

```json
{ "channel": "retutor",
  "courseUpdateUrl": "https://你的域名/retutor/courses/chall1/update.json",
  "appUpdateUrl":    "https://你的域名/retutor/app-update.json" }
```

清单格式与原版一致（`schemaVersion` / `courseVersion` / `files[{path,url,sha256}]` / `delete[]`），
所以你把内容包重新发一份到自己的仓库，改这两个地址就能用。

## 关卡推进规则

- **中间关卡（第一段、第二段）**：引导走完直接进下一关，**不显示**"本关引导已走完"那一页，
  只在状态栏提示"本段已完成，已自动进入下一段"（键 `stage.auto_next`）。
- **最后一段**：走完才停在收尾页（`stage.done_*`），再点「完成课程」弹出课程完成面板（`finish.*`）。

## 设置窗口

左侧栏「设置」→ 浮层弹窗（对应原程序那版）：`工具路径` 区列出 DIE / IDA / Ghidra / dnSpy / x64dbg 五行，
每行 `选择`（打开文件框，写入 resources.json）/ `清除`（删记录），状态实时显示"未配置"或已选路径；
下方 `关于` 区（标题/版本/作者/设计说明/字体）；底部 `完成` 或右上 `✕` 关闭。

## 界面文案：内嵌进 exe + 外部可覆盖 + 内容包也内嵌

- exe 里同时内嵌两份东西：`UiStrings.json`（界面文案 102 键）与 `Content.zip`（内容包，
  其中 `assets/i18n/zh-CN.json` 与 `courses/chall1/i18n/zh-CN.json` 已替换为你改过的版本）。
- 内容包解包带**指纹**：`RETutorContent/.retutor-content-version` 与内嵌包的 SHA-256 一致就跳过，
  不一致就整包重解 —— 所以你改了语言文件重新打包后，老用户目录也会自动刷成新文案。

- 编译时把 `UiStrings.json`（87 键）作为 `EmbeddedResource`（`RETutor.UiStrings.json`）**烧进 exe**，
  换台机器、删掉外部文件，文字都不丢。
- exe 同级的 `ui.zh-CN.json` 会**覆盖**内嵌值（只写要改的键即可），改完重启生效。
- 再往下才是代码里 `S("键","默认值")`。

## 怎么改程序里显示的文字（三层，前一层命中就不看后面）

| 层 | 改哪里 | 影响范围 | 要不要重编译 |
| --- | --- | --- | --- |
| ① 界面文案覆盖 | `{exe 目录}/ui.zh-CN.json` 或 `{内容根}/assets/i18n/ui.zh-CN.json` | 窗口标题、按钮、提示语、完成面板等**界面自身**文案 | 不要，改完重启即可 |
| ② 课程文案 | `MoeCTF.RE.Tutorial/courses/chall1/i18n/zh-CN.json`（课程级）或 `MoeCTF.RE.Tutorial/assets/i18n/zh-CN.json`（全局） | 关卡标题、目标、步骤正文、完成语等**内容作者写的**文案 | 不要 |
| ③ 代码内置默认 | `MainWindow.cs` 里 `S("key", "默认值")` 的第二个参数 | 前两层都没有时的兜底 | 要 |

### ① 界面文案：只写要改的键

`bin/Debug/net8.0-windows/ui.zh-CN.json`：

```json
{ "app.title": "RETutor 仿制版（文案已外部化）",
  "app.header": "逆向指北 · 文案来自 ui.zh-CN.json",
  "btn.next": "继续 →" }
```

全部可改键与默认值见 `../text-json/ui.zh-CN.json`（86 键，已按 `app./hub./label./btn./settings./about./res./stage./finish./status./hint./guide.` 分组）。
`dist/` 与 `dist-standalone/` 里已经各放了一份 `ui.zh-CN.json`，直接改那个即可。改完重启程序生效，无需重新编译。

### ② 课程文案：改 i18n，段落正文就变了

课程的每个 `textKey` / `bodyKey` / `titleKey` 都指向这些 JSON 的键，例如：

```json
// courses/chall1/i18n/zh-CN.json
"chall1.die.title": "第一段：先认清 chall1",
"common.external_submit": "找到完整 flag 后，请回到 MoeCTF 比赛平台提交。指北不会替你提交答案。",
"course_completion.title": "课程完成"
```

> ⚠️ 这个目录是**原程序 RE.Tutorial.exe 共用的数据根**，改它原程序也会变。
> 只想改仿制版：把整个 `MoeCTF.RE.Tutorial/` 复制成 `RETutorContent/` 放到程序目录，
> 再设置环境变量 `RETUTOR_CONTENT=D:\...\RETutorContent`（或把它放到 exe 同级），然后改副本。

### ③ 代码默认值

`MainWindow.cs` 里搜 `S("` 就能看到所有 key 与默认文案；`UiStrings.T(key, fallback)` 的 fallback 就是第三层。

## 打包成单文件 exe

```powershell
# 框架依赖单文件（约 2 MB，需要本机装 .NET 8 Desktop Runtime）
dotnet publish .\RETutor.csproj -c Release -o ..\dist
# 自包含单文件（约 90 MB，不依赖本机运行时）
dotnet publish .\RETutor.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ..\dist-standalone
```

- 课程内容（`Content.zip`，1.73 MB）作为**内嵌资源**打进 exe；
  首次运行自动解到 `<exe 目录>\RETutorContent`（对应原程序的 `BundledContentInstaller`），
  之后直接复用，删目录即恢复原状。
- 用户自选的工具路径 / 题目附件记在 `%LOCALAPPDATA%\RETutor\resources.json`，卸载 exe 不影响。
- 进度仍在 `%LOCALAPPDATA%\RETutor\progress.json`。

## 资源卡片（题目附件 + 每个工具一张）

Desk 页底部不再是单个「打开工具」按钮，而是照原程序 Hub 页的样子摊成一排卡片：

| 卡片 | 来源 | 状态判定 | 点击 |
| --- | --- | --- | --- |
| 题目附件（如 chall1） | `course.json` 的 `requirements` 里 `kind: "target"` | 记录里的路径 / `assets/targets/` / `courses/<id>/assets/targets/` / 内容根上一级 找到同名文件 → 已就绪（显示 sha256 前 8 位） | 资源管理器 `/select` 定位该文件 |
| 工具（DIE / IDA / …） | `requirements` 里 `kind: "tool"` | 记录里的路径 / `assets/tools/<id>/` 白名单 exe → 已就绪 | 空参数启动该工具 |

**未配置时**：点卡片弹文件选择框；也可以把文件**直接拖到卡片上**。
- 工具卡：记住 exe 路径（不复制）。
- 附件卡：复制到 `<内容根>/courses/<课程>/assets/targets/<名字>` 并记录 **sha256 + size**。

**sha256 以本地文件为准**：卡片显示"已就绪 <hash 前 8 位>"；
若与 `course.json` 声明的哈希不同，卡片标"（本地）"，tooltip 里同时列出
`sha256(本地)` 与 `sha256(课程声明)` —— 不再因为声明哈希不符而判"未配置"。

- 当前关卡用到的卡片会**高亮边框 + 蓝色标题**（对应原程序"本段需要"）。
- 课程没写 requirements 时，自动退回"本关 tools/targets"生成卡片。
- 卡片标题与状态文案来自 `res.*` 键，可在 `ui.zh-CN.json` 里改。

## 与原程序的故意差异

- **不做站内提交 flag**：原程序有输入框 + 站内判题；本版去掉输入框，
  走完本关停在「本关引导已走完」，走完最后一关弹出完成面板：把 flag 复制到比赛平台提交（本程序不收集、不判题）。

## 未实现（原程序有，本骨架刻意省略）

内容更新（课程域/目录域）、程序双进程自更新、Adapter 的 state/role 知识库、
歧义判定分带、桌面图标拖出（已改为卡片点选/拖拽）、内容与程序在线更新。
