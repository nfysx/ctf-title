# 文案索引：改哪句话去哪个文件

仿制版把文字分成**三层**，前一层命中就不看后面：

| 优先级 | 文件 | 管什么 | 数量 |
| --- | --- | --- | --- |
| ①a | **exe 内嵌** `UiStrings.json`（编译时烧进程序） | 界面文字的出厂默认值，删了外部文件也不丢 | 102 键 |
| ①b | `ui.zh-CN.json`（exe 同级，或 `{内容根}/assets/i18n/`） | 覆盖内嵌值；改完重启即生效，不用重编译 | 87 键 |
| ② | `courses/chall1/i18n/zh-CN.json`（课程级） | **课程正文**：关卡标题/目标/步骤标题/步骤正文/完成语 | 77 键 |
| ③ | `assets/i18n/zh-CN.json`（全局） | ②缺失时的兜底（原程序全局文案） | 163 键 |
| 兜底 | C# 源码 `S("键","默认值")` 的第二个参数 | 三层都没有时 | — |

> 改 ① 只影响仿制版；改 ②③ 会**同时影响原程序 RE.Tutorial.exe**（同一份数据根）。
> 只想改仿制版：把内容复制成 `RETutorContent/` 放 exe 同级，或设环境变量 `RETUTOR_CONTENT` 指过去。

## 本目录文件

| 文件 | 用途 |
| --- | --- |
| `ui.zh-CN.json` | ① 界面文案**全量模板**（102 键 = 你的改动 + 19 个 `update.*` 新键） |
| `app-i18n.zh-CN.json` | ③ 原程序全局文案原件（163 键，只读参照/兜底） |
| `app-i18n.en-US.json` | ③ 英文版原件（仿制版当前只用 zh-CN） |
| `course-chall1-i18n.zh-CN.json` | ② chall1 课程文案原件（77 键） |
| `course-chall1-i18n.en-US.json` | ② 英文版原件 |

## ① 界面文案分组（键前缀 → 位置）

| 前缀 | 覆盖的界面位置 | 例 |
| --- | --- | --- |
| `app.*` | 窗口标题、标题栏面包屑 | `app.title`、`app.breadcrumb` |
| `hub.*` | 左侧课程栏与右侧课程页（`hub.eyebrow` 管左上角那行小字，默认 `Reverse`，已去掉 MoeCTF；`hub.title` 是大标题） | `hub.title`、`hub.intro`、`hub.course_list`、`hub.route_header`、`hub.stage_count`、`hub.minutes`、`hub.source`、`hub.enter_course`、`hub.compat`、`hub.requirements_missing`、`hub.check_update`（左下角）、`hub.host_version`/`hub.course_version`/`hub.version_local`（点检查更新后的版本行） |
| `label.*` | 小标签 | `label.course`（" 课程："）、`label.stages`（"关卡"）、`label.auto_guide`（"交互指引"） |
| `btn.*` | 所有按钮 | `btn.prev`、`btn.next`、`btn.next_stage`、`btn.finish_course`、`btn.open_tool` |
| `settings.*` | 设置弹窗 | `settings.title`、`settings.subtitle`、`settings.tools_header`、`settings.choose`（"选择"）、`settings.clear`（"清除"）、`settings.done`（"完成"）、`settings.choose_tool`、`settings.tool_missing`（"未配置"）、`settings.open` |
| `about.*` | 设置里的"关于"块 | `about.header`、`about.title`、`about.version`、`about.author`、`about.design`、`about.font` |
| `res.*` | 资源卡片 | `res.title`、`res.ready`、`res.missing`、`res.local_badge`、`res.pick_tip`、`res.tool_saved`、`res.target_saved`、`res.hash_warn` |
| `stage.*` / `step.*` / `finish.*` | 关卡完成态与课程完成面板 | `stage.done_title`、`stage.done_body`、`stage.done_hint_last`、`finish.panel_title`、`finish.panel_body`、`finish.panel_tip`、`step.counter` |
| `status.*` / `hint.*` / `guide.*` | 底部状态栏、行内提示、HUD 标题 | `status.ready`、`status.locating`、`status.recognized`、`hint.tool_missing`、`guide.host_title` |

## 左下角「检查更新」（已接真实更新源）

| 想改什么 | 改哪里 |
|---|---|
| 文字 | `ui.zh-CN.json` → `hub.check_update` |
| 提示文案 | `ui.zh-CN.json` → `update.*`（19 键：checking / course_available / course_latest / install / installing / files / installed / progress_reset / failed / source_unreachable / host_* …） |
| 行为 | `MainWindow.CheckForUpdates()` + `Services/UpdateService.cs` |
| 去掉入口 | 删 `BuildHub()` 里 `sideBottom.Children.Add(LinkButton(S("hub.check_update", ...), CheckForUpdates));` |

## 旧版「检查更新」占位说明（已被上面替代）

- **改字**：`ui.zh-CN.json` 里的 `hub.check_update`（默认"检查更新"）。
- **改行为**：`MainWindow.CheckForUpdates()` —— 当前不联网，只在卡片下方与状态栏报告
  "程序 0.1.0 · 课程 1.0.2 · 本地内容包（未配置更新源）"。
- **不想要它**：删掉 `BuildHub()` 里 `sideBottom.Children.Add(LinkButton(S("hub.check_update", ...), CheckForUpdates));` 这一行。

## 改文字的步骤

```powershell
# 1) 打开（示例：把设置标题改成"偏好设置"）
notepad D:\ctf\RE-ctf\teach-demo\dist\ui.zh-CN.json
#    { "settings.title": "偏好设置" }
# 2) 重启 RETutor.exe —— 不需要重新编译
```

- 文件里**只留你要改的键**也可以，其余自动用内置默认值。
- JSON 语法错（多逗号、中文引号、缺括号）→ 本次启动回落到内置默认，不会崩；程序里 `UiStrings.SourcePath` 会记录解析失败的文件。
- 想改课程正文（关卡/步骤文案）：改 `②`，键名就是 `stage.json` 里 `titleKey`/`bodyKey` 的值，例如 `chall1.die.open.title`。

## 一眼核对：键名从哪来的

`stage.json` 的 `textKey`/`bodyKey`/`titleKey` → 去 ②③ 找同名字符串；
界面上的固定字（按钮、标签、关于）→ 去 ① 找。
