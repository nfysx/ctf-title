# teach-demo 目录说明

RE.Tutorial.exe（MoeCTF 逆向教学指北）逆向分析 + 仿制工程的根目录。

## 目录布局

| 路径 | 是什么 | 能否重建 |
| --- | --- | --- |
| `RE.Tutorial.exe` | **原始样本**（.NET 8 单文件，2.53 MB） | 原始文件，勿删 |
| `chall1` | 课程用的题目附件（ELF，16 KB） | 原始文件 |
| `MoeCTF.RE.Tutorial\` | **原程序**运行时的数据根（内容 + config 进度） | 原程序首次运行会重建 |
| `_bundle\` | 从单文件里切出来的 `MoeCTF.RE.Tutorial.dll` / `.runtimeconfig.json` / `.deps.json` | 用拆包脚本可重建 |
| `_src\` | ILSpy 反编译出的完整 C# 工程（93 个 .cs + 5 个 BAML） | 由 `_bundle\*.dll` 反编译可重建 |
| `_bundle_unzip\` | 内嵌内容包解包结果（30 个文件，课程/文案/截图/schema） | 由 `Content.zip` 或原程序内嵌资源解出 |
| `_notes\` | 三份子系统分析报告（content-core / guide-overlay / ui-shell）+ 截图 | 文档，勿删 |
| `_notes\screenshots\` | 过程截图（hub / settings / update / 各阶段） | 重新运行程序可截 |
| `text-json\` | **语言配置**：界面文案 `ui.zh-CN.json`（113 键）、原版 i18n 各 4 份 + 文案索引 | 手工维护，勿删 |
| `RETutor\` | **仿制工程源码**（.NET 8 WPF）+ `Content.zip`（内嵌内容包）+ `UiStrings.json`（内嵌文案） | 源码，勿删；`bin/obj` 可重建 |
| `dist\` | 发行版：框架依赖单文件 `RETutor.exe`（2 MB）+ `ui.zh-CN.json` + 配置示例 | `dotnet publish` 可重建 |
| ~~`dist-standalone\`~~ | 自包含发行版，**已按清理计划删除**（需要时重新 publish 即可生成） | `dotnet publish -r win-x64 --self-contained` |
| `逆向分析报告-功能实现与仿制.md` | 主报告 | 文档 |

## 重建命令

```powershell
$dotnet = 'D:\apk\_tools\dotnet\dotnet.exe'   # 本机没有全局 SDK，装在 D:\apk\_tools

# 1) 反编译（需要 ilspycmd，已解到 D:\apk\_tools\ilspy）
& $dotnet D:\apk\_tools\ilspy\v9007889\tools\net8.0\any\ilspycmd.dll -p -o _src _bundle\MoeCTF.RE.Tutorial.dll

# 2) 编译 / 打包仿制版
& $dotnet publish RETutor\RETutor.csproj -c Release -o dist
& $dotnet publish RETutor\RETutor.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist-standalone
```

## 改文案

1. 界面文字：改 `text-json\ui.zh-CN.json` → 复制到 `dist\`（同名覆盖内嵌值，重启生效）。
2. 课程正文：改 `text-json\course-chall1-i18n.zh-CN.json` → 覆盖进内容根 `courses\chall1\i18n\zh-CN.json`。
3. 要"烧进 exe"：把改好的 `ui.zh-CN.json` 复制为 `RETutor\UiStrings.json`，重新 publish；
   课程文案同理，先覆盖到内容目录再重新生成 `RETutor\Content.zip`。

## 可清理项（删前需确认）

见根目录 `清理清单.md`。
