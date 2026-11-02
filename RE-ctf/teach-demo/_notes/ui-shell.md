# UI 外壳与运行编排 — RE.Tutorial.exe（MoeCTF.RE.Tutorial）逆向说明

> 分析范围：App（入口/启动编排）、MainWindow（主状态机）、NativeWindowChromeService（无边框窗口 chrome）、CoachOverlayWindow + HostGuideOverlayPresenter（置顶悬浮指引层）、ToolAdapter/ToolSpec/ProcessManager/ManagedProcess（工具发现与进程托管）、PathService、ProgressStore、RequirementActivationService、BundledContentInstaller、I18nService、R2SCard/R2STag 与两套 *.baml 样式字典。
> 证据：D:\ctf\RE-ctf\teach-demo\_src\（ILSpy 反编译树，只读）、D:\ctf\RE-ctf\teach-demo\_bundle\MoeCTF.RE.Tutorial.dll（原始程序集）。标注【推断】处为语义可确定但字面未被直接读到的结论。

---

## 1. 子系统职责（一句话）

用一个**无边框深色 WPF 客户端 + 全屏点击穿透悬浮层**，把"课程内容（内嵌 JSON 包）→ 关卡/步骤状态机 → 启动/追踪 IDA/x64dbg/DIE/dnSpy/Ghidra 窗口 → 在真实工具窗口上贴高亮与气泡"串成一条线，并把"手动点下一步"与"视觉识别自动同步"两种推进方式统一到同一个 (stageIndex, stepIndex, guideUnlocked) 状态里持久化。

---

## 2. 数据结构与 JSON 契约

### 2.1 进度：config/progress.json（ProgressData / CourseProgressData / ProgressStore）

Services\ProgressData.cs:6-15、Services\CourseProgressData.cs:5-16、Services\ProgressStore.cs:60-98

```csharp
sealed class ProgressData {
    string ActiveCourseId = "freshman-re";                  // 缺省课程 id
    Dictionary<string, CourseProgressData> Courses;          // key = courseId, OrdinalIgnoreCase
    int StageIndex;                                          // legacy 镜像
    List<string> ClearedStages = new();                      // legacy 镜像
}
sealed class CourseProgressData {
    string? CourseVersion;      // 课程 version（来自 course.json）
    string? FlowFingerprint;    // stages/*.json 内容 SHA256
    string? StageId;            // 当前关卡 id（优先于 StageIndex 定位）
    int StageIndex;
    List<string> ClearedStages;
}
```

- 序列化：%%JsonSerializer.Serialize(Data, new JsonSerializerOptions{ WriteIndented = true })%%，**未设置 PropertyNamingPolicy**，磁盘为 PascalCase（实测 MoeCTF.RE.Tutorial\config\progress.json 即 "ActiveCourseId"/"Courses"/"ClearedStages"）。
- 可空/默认：ActiveCourseId 空 → 回落 "freshman-re"（ProgressStore.cs:73-76）；ClearedStages 反序列化为 null 时上层用 ?? new List<string>() 兜底（MainWindow.cs:641）。
- 时机：构造即 Load()（ProgressStore.cs:14-18）；Save() 在关卡切换、提交 flag、重置、识别同步时同步写（File.WriteAllText，非原子写、无节流）。
- **身份-失效契约**（核心不变量）：ForCourse(courseId, version, fingerprint)（ProgressStore.cs:20-32）在 CourseVersion/FlowFingerprint 不匹配时**丢弃旧进度、重建空进度** —— 即"课程内容更新 → 该课进度归零"。IdentityMatches（:100-111）用 Ordinal 比版本、OrdinalIgnoreCase 比指纹。
- FlowFingerprint 生成：CourseCatalog.ComputeFlowFingerprint（Services\CourseCatalog.cs:918-939）—— stagesDir 下 *.json 按文件名排序，逐个追加 "文件名小写 + 0x00 + 文件字节 + 0x00" 做增量 SHA256，输出小写 hex。

### 2.2 需求激活：config/requirements.json（RequirementActivationState）

Services\RequirementActivationState.cs:7-17、RequirementActivationService.cs:231-398

```csharp
sealed class RequirementActivationState {
    [JsonPropertyName("tools")]         Dictionary<string,string> Tools;                            // toolId -> exe 绝对路径
    [JsonPropertyName("courseTargets")] Dictionary<string,Dictionary<string,string>> CourseTargets;  // courseId -> requirementId -> 已复制到的目标路径
    [JsonPropertyName("courseChoices")] Dictionary<string,Dictionary<string,string>> CourseChoices;  // courseId -> groupId -> 选中的 requirementId
}
```

- 这里**显式用 [JsonPropertyName]**，磁盘是小驼峰（与 progress.json 风格不一致，复刻别混用）。
- Tools 的路径经 Path.GetFullPath（:277）；courseTargets 的值必须落在 ContentRoot 内的 courses/<courseId>/assets/targets/ 前缀下，ResolveTargetAssetPath（:484-508）做前缀+越界校验，否则返回 null。

### 2.3 课程/关卡内容契约

- 目录入口 MoeCTF.RE.Tutorial\courses\index.json：{schemaVersion, catalogVersion, catalogUpdateManifestUrl, appUpdateManifestUrl, courses:["courses/chall1/course.json"]}（实测 courses\index.json:1-9）→ CourseIndex（CourseCatalog.cs:19-25）。
- 单课 courses/<id>/course.json → CourseManifest（CourseCatalog.cs:37-111）：id / version / compatibility{minHostVersion,maxHostVersionExclusive} / status / updateManifestUrl / appUpdateManifestUrl / metadata{title,subtitle,summary,track,difficulty,estimatedMinutes,tags,authors} / content{stagesDir,i18nDir,assetsDir,stageIndex} / requirements(allOf|anyOf 递归) / flow{entry,stages[{id,path,requires,next[{to,when}]}]} / changelog[]。
- 需求节点 CourseRequirementNodeManifest（:166-216）→ 归一化为扁平 CourseRequirementGroup[]，字段 id / kind / label / toolId / targetId / fileName / match{fileNames,pathContains,sha256,size,recursive} / asset{path,copyMode}。
- CourseRequirement（Services\CourseRequirement.cs:9-85）：Id、Kind("target"|"tool")、Label、Description、ToolId?、FileName?、AssetPath?、MatchNames[]、MatchPathContains[]、MatchSha256?、MatchSize?；ToolExecutableName() 内置 die→die.exe / ida→ida.exe / dnspy→dnSpy.exe / ghidra→ghidraRun.bat / x64dbg→x64dbg.exe。
- 关卡 stages/index.json → StageIndex.stages[]（文件名）→ 逐个 <id>.json → StageDefinition（Host.Stages\StageDefinition.cs:6-46）：id、order、titleKey、goalKey、answerKey、completionMode("answer"|"external_submit"|其它)、fakeAnswerKeys[]、targets[]、tools[]、guideMode("forced"|"none"|"on_fake_or_next")、freeExploreTextKey?、deviations[]、steps[]。
- 步骤 StageStep（StageStep.cs:7-53）：id、textKey、bodyKey?、image?、action("none"|"launch_tool"|"open_in_tool"|"launch_target")、tool?、file?、guide{...}、trigger{...}、branches[]、highlightScope/highlightHost/highlightProcess/highlightNames[]/highlightControlType。
- StageRepository（StageRepository.cs:17-44）：选项 PropertyNameCaseInsensitive = true，按 Order 升序。
- 文案：assets/i18n/zh-CN.json（163 键，前缀分组 app/hub/settings/requirement/desktop/common/course_entry_notice/stage0..stage3/final），I18nService.T(key,args) 用正则 \{(\w+)\} 做占位替换（I18nService.cs:44-60），缺键回落为 key 本身。

### 2.4 UI 内部状态（MainWindow 字段，MainWindow.cs:38-296）

| 字段 | 含义 / 不变量 |
|---|---|
| _courses / _activeCourse | 课程列表（同 id 取 Version 最大者，内置优先，CourseCatalog.cs:625-645）与当前课程 |
| _stages / _stageIndex / _stepIndex | 当前关卡与步骤；**\_stepIndex == Steps.Count 是合法终态**（本关引导走完） |
| _guideUnlocked | Current.GuideMode == "forced" 时为 true（MainWindow.cs:1494）；否则首次推进后置 true |
| _interactiveGuideEnabled | 标题栏 Toggle，默认 true；false 时清空两层 overlay 并停扫描 |
| _guideRevision | long，Interlocked.Increment（:477-485），所有异步扫描结果靠它做**代际校验** |
| _latestExternalScene | 最近一次可渲染的"外部工具窗口场景"缓存 |
| _pendingExplorerTargetPath / _pendingExplorerGuideUntilUtc | 打开资源管理器后 18 秒内优先贴"文件项"高亮（:2272-2273、:2384-2392） |
| _hostHotspots | Dictionary<string, FrameworkElement>：action/next/flag/submit/taskboard/coach/desktop + tool:<id> / target:<file>，供 GuideRuntime 反查屏幕矩形 |
| _processAccessLock | 保护 ProcessManager 的跨线程访问 |
| _updateCancellation | 窗口 Closed 时 Cancel（:321-324） |

---

## 3. 关键流程

### 3.1 启动时序（App → 内容安装 → 进度/课程 → 主窗口 → 引导运行时）

```
Main()                                          App.cs:70-75
 └ new App(); InitializeComponent();            App.cs:57-65   // Application.LoadComponent("...;component/app.xaml")
   └ app.Run()
     └ App.OnStartup(e)                         App.cs:17-53
        ① 分支 A  --apply-update <request.json>  App.cs:19-31
           ShutdownMode = OnExplicitShutdown
           HostUpdateInstaller.ApplyUpdateAsync(path)   // Task.Run(...).GetAwaiter().GetResult()
           失败且 ShouldShowApplyFailureDialog → MessageBox("MoeCTF 逆向工程指北更新失败")
           Shutdown(成功?0:1); return                 // 不起 UI
        ② 分支 B  --bootstrap-only               App.cs:32-48
           new PathService()                      // 构造即触发内嵌内容安装
           校验 courses/index.json + stages/index.json + assets/i18n/zh-CN.json
           Shutdown(齐?0:2); 异常 Shutdown(1)
        ③ 正式启动                               App.cs:49-52
           base.OnStartup(e)
           HostUpdateInstaller.CleanupCompletedUpdates(GetDefaultDataRoot())
             // GetDefaultDataRoot = AppContext.BaseDirectory\MoeCTF.RE.Tutorial  HostUpdateInstaller.cs:78-81
             // 清理 *.completed.json（>1 分钟）、*.download、apply-*.{ready,failed.txt,log,json}
           new MainWindow()   ← 字段初始化器先于构造函数体执行：
                 _paths = new PathService()                     MainWindow.cs:42
                   → BundledContentInstaller.Install(BaseDirectory\MoeCTF.RE.Tutorial)
                     （Install 为 true 或目录形似 → RepoRoot = 该目录）  PathService.cs:40-64
                 _i18n / _tools / _processes / _targetObserver / _tracker   MainWindow.cs:44-52
                 _windowSnapshots / _locator / _overlay(=new CoachOverlayWindow())  :54-68
           mainWindow.Show()
```

MainWindow 构造函数内部顺序（MainWindow.cs:312-385，**顺序即语义**）：

```
320 InitializeComponent()                        // 加载 mainwindow.baml，Connect 绑定 1..93 号控件与 Click
321 Closed += delegate { _updateCancellation.Cancel(); }
325 _windowSnapshots = new WindowSnapshotCache(_tracker)
326 Loaded += MainWindow_OnLoaded                  // → FitWindowToWorkArea + 自动检查更新
327 _nativeChrome = NativeWindowChromeService.Attach(this, 12.0)   // 圆角 12 DIP
328-331 _catalog / _contentUpdater / _hostUpdateInstaller / _requirements(读 requirements.json)
332 _contentUpdater.RecoverInterruptedUpdate()    // 内容更新中断恢复
333 _hostGuideOverlay = new HostGuideOverlayPresenter(this, InAppGuideLayer, InAppGuideHighlight,
                                                     InAppGuideArrow, InAppGuideBubble, InAppGuideTitle, InAppGuideText)
334 _progress = new ProgressStore(_paths.ProgressPath)   // 读 progress.json
335 _tools = new ToolAdapter(_paths)
336-338 LoadCourses() → SelectInitialCourse() → ApplyActiveCourse(loadTexts:true)
339-350 _guide = new GuideRuntime(new GuideRuntimeContext{ HostWindow=this, HostHotspots=_hostHotspots,
                  Processes=_processes.Processes, WindowTracker=_tracker, WindowSnapshots=_windowSnapshots,
                  ControlLocator=_locator, AppLocator=new GuideAppLocator(),
                  ValueProvider=GuideValue, TextProvider=_i18n.GetRaw })
351 _guideTracking = new GuideTrackingCoordinator(...)
352 _stageIndex = ResolveSavedStageIndex()
353-361 _trackTimer(200ms) → RefreshTrackedWindows()      // 轮询式追踪
362-371 _clockTimer(1s)    → TaskbarClockText = "HH:mm:ss"
372-379 Closing/Closed → CleanupOverlayAndChildren()
380 ApplyStaticTexts()      // 一次性灌所有静态文案（含 AboutTextBlock 的版本号/作者/字体声明）
381 RegisterStaticHotspots()// action/next/flag/submit/taskboard/coach/desktop 入 _hostHotspots
382 RebuildCourseHub()      // 课程列表 + 当前课程卡（含需求分组重建）
383 LoadStage()             // 读关卡 → 写回进度 → 重建桌面图标 → RefreshCoach
384 ShowHub()               // 显示 Hub、隐藏 Desk、清两层 overlay
```

Loaded（MainWindow.cs:402-406）→ FitWindowToWorkArea()（按 SystemParameters.WorkArea 夹到 12px 边距，必要时下调 MinWidth/MinHeight，:387-400）→ CheckForUpdatesAsync(automatic:true)（无任何更新源时立即返回，:1299-1302）。

> 单实例：**没有** Mutex/命名管道等任何机制（全树无 Mutex/SingleInstance），多开直接允许。
> 全局异常处理：**没有** DispatcherUnhandledException / AppDomain.UnhandledException；只有事件内部零散 try/catch（如 :1005、:1038、:1411、:2294）。

### 3.2 主状态机（Hub ⇄ Desk，关卡/步骤推进）

```
ShowHub()  MainWindow.cs:1178-1188
  AdvanceGuideRevision(); CourseCompletionOverlay.Collapsed;
  CourseHubView.Visible; CourseDeskView.Collapsed; BackToHubButton.Collapsed;
  _hostGuideOverlay.Clear(); _overlay.ClearGuide();
  TaskbarStageText = _activeCourse?.Title ?? "MoeCTF Reverse"

EnterCourseButton_Click :1218-1228
  若 无课程 / 不兼容 / 无关卡 / 需求未满足 → ShowDesk()（内部弹回 Hub 并给原因）
  否则 → CourseEntryNoticeOverlay.Visible（"交互式指引仍在持续完善"确认框）

ConfirmCourseEntryButton_Click :1235-1239 → ShowDesk()

ShowDesk()  :1190-1216   —— 四重 gate
  ① _activeCourse == null || !HasLoadedStage      → ShowHub + "点一下检查更新"
  ② !_activeCourse.IsHostCompatible               → ShowHub + 版本区间提示
  ③ !_requirements.AreRequirementsSatisfied(...)  → ShowHub + "把缺少的文件或工具拖到这里"
  ④ 通过：CourseHubView.Collapsed; CourseDeskView.Visible; BackToHubButton.Visible;
          AdvanceGuideRevision(); RefreshCoach(); ShowCourseCompletionIfFinished()
```

关卡装载 LoadStage()（:1470-1509）—— **进关即写盘**：

```
CourseCompletionOverlay.Collapsed
若 !HasLoadedStage：清桌面图标、置空文案、return
_stageIndex = Clamp(_stageIndex, 0, count-1)
p = CurrentProgress(); p.StageIndex = _stageIndex; p.StageId = Current.Id
_progress.MirrorLegacy(p); _progress.Save()        ← 关卡切换先落盘
_stepIndex = 0
_guideUnlocked = Current.GuideMode == "forced"
AdvanceGuideRevision()                              ← 代际 +1，作废在途识别结果
FeedbackText=""; FlagInput.Text=""; _lastActivatedGuideTarget=null
ProgressText = i18n("common.progress", {current:_stageIndex+1, total:count})
StageTitleText / StageGoalText / TaskbarStageText
RebuildDesktopIcons()   // 生成 tool:/target: 图标
RefreshCoach()
```

步骤推进 NextStepButton_OnClick（:2054-2101）：

```
if (!_guideUnlocked) { _guideUnlocked = true; _stepIndex = 0; AdvanceGuideRevision(); RefreshCoach(); return; }
if (_stepIndex < Steps.Count && ApplyGuideBranch(Current.Steps[_stepIndex], "next")) return;   // 分支优先
if (_stepIndex < Steps.Count-1) { _stepIndex++; AdvanceGuideRevision(); RefreshCoach(); }
else if (CompletionMode != "answer" && _stageIndex < count-1) {        // 非答题关：末步 = 通关
      p.ClearedStages += Current.Id; _stageIndex++; p.StageIndex/StageId 同步; Save(); LoadStage(); }
else { _stepIndex = Steps.Count; AdvanceGuideRevision(); RefreshCoach(); ShowCourseCompletionIfFinished(); }
```

**完成判定与 CourseRequirement 的关系（两条互不相干的判定链）**：

1. **"能否进入课程"（课前准备）**：RequirementActivationService.AreRequirementsSatisfied（RequirementActivationService.cs:337-350）= 每个 group 内 allOf 全满足 / anyOf 任一满足；StatusFor（:328-335）对 tool 走 FindToolExecutable（用户配置 → 内嵌 assets/tools/<id>/），对 target 走 FindTarget（已存路径 → 复制到 courses/<id>/assets/targets/），并用 Matches（:439-482）校验 fileNames 通配 + pathContains + size + sha256。AutoSelectSatisfiedRequirements（:372-398）在 Hub 刷新时把已满足项自动设为选中。
2. **"这一关是否完成"（流程推进）**：只看 completionMode。
   - "answer"：SubmitButton_OnClick（:2126-2200）把输入与 i18n.GetRaw(Current.AnswerKey) 做 StringComparison.Ordinal 比较（先经 NormalizeFlagSubmission 去空白、包含匹配、(flag|moectf)\{...\} 正则抽取，:2202-2233）；命中则 ClearedStages += Current.Id 并进下一关或完成页；命中 fakeAnswerKeys → 提示假 flag，并按 guideMode == "on_fake_or_next" 解锁引导；否则提示错误并尝试 wrong_flag 分支。
   - "external_submit"：不自判，靠"识别到工具里已完成最后一步"标记（TrySyncDetectedScene :3018-3021）。
   - 其它：NextStep 走到末尾即算通关（:2079-2092）。
   - 已通关关卡的"最低水位"：MinimumActiveStageIndexFromClearedProgress（:635-655）返回第一个未通关关卡下标（末关已通关则返回末关），与存档值取 Math.Max（:627），保证**不回退到已通关关卡**。

自动推进 TrySyncDetectedScene（:2964-3026）：把 (stageIndex, stepIndex) 对齐到识别结果（GuideProgressionPolicy.SynchronizeToRecognition），并**补记被跨过的关卡**为 cleared（:3008-3017）；受 IsReliableExternalRecognition、MinimumActiveStageIndexFromClearedProgress、!scene.IsStepTrigger 等多重守卫。

### 3.3 工具的发现 / 启动 / "附着" / 终止

**发现**（两级：约定目录 + 白名单文件名）：

```
RequirementActivationService.FindToolExecutable(toolId)      :253-260
  ├ requirements.json 的 tools[toolId] 且 File.Exists → 用它
  └ PathService.FindBundledToolExecutable(toolId)             PathService.cs:138-169
      搜索根：RepoRoot\assets\tools、<workspaceRoot>\assets\tools        :183-190
      每个根下 <toolId>\ 子目录（须存在）
      白名单顺序：die→die.exe/DIE.exe；ida→ida.exe/ida64.exe/idat.exe；
                  ghidra→ghidraRun.bat/support\launch.bat；
                  dnspy→dnSpy.exe/dnspy.exe；
                  x64dbg→release\x64\x64dbg.exe | x64\x64dbg.exe | x64dbg.exe | x96dbg.exe
      全不命中 → 该目录下递归第一个 *.exe（兜底，可能误判）
```

ToolSpec（Services\ToolSpec.cs:5-16）只有 5 个字段：Id / DisplayName / Glyph / WindowTitleHints[] / ProcessNameHints[]，**不含任何路径、参数、命令行模板**。实际取值硬编码在 ToolAdapter.Specs（ToolAdapter.cs:9-51）：

| Id | DisplayName | Glyph | WindowTitleHints | ProcessNameHints |
|---|---|---|---|---|
| die | DIE | DIE | Detect It Easy / DIE / die | die / diec / diel |
| ida | IDA | IDA | IDA / Hex-Rays / idapro / Please confirm / Load a new file | ida / ida64 / idat |
| ghidra | Ghidra | GHD | Ghidra / CodeBrowser / Decompiler / Listing | ghidra / java / javaw |
| dnspy | dnSpy | NET | dnSpy | dnspy / dnSpy |
| x64dbg | x64dbg | DBG | x64dbg / x96dbg / x32dbg | x64dbg / x96dbg / x32dbg |

ToolAdapter 构造函数接收 PathService 但**完全不用**（:55-57 空构造体）。

**启动** OpenToolResource(toolId)（MainWindow.cs:2307-2366）：

```
lock(_processAccessLock) 取 _processes["tool:"+toolId]
若已登记且 FindWindowsForManaged 找到窗口 → TryActivate(第一个) → 提示"已为你切换到 X" → return
exe = _requirements.FindToolExecutable(toolId)；null → 提示"没找到工具"
Process.Start(new ProcessStartInfo {
    FileName = exe,
    WorkingDirectory = Path.GetDirectoryName(exe) ?? _paths.ToolsDir,
    UseShellExecute = 扩展名 == ".bat"        // ghidraRun.bat 必须走 shell
})
_processes.ObserveExternal("tool:"+id, process, exe, id, spec.WindowTitleHints, spec.ProcessNameHints)
若返回对象不是本次 process（复用/已存在）→ 释放本次句柄
```

**注意：启动参数为空** —— 程序从不把目标文件作为命令行参数传给工具。目标文件只通过两条路交给用户：
(a) explorer.exe /select,"<path>"（OpenTargetFolderResource :2260-2305）；
(b) **桌面图标拖出**（MakeCanvasIcon 的 PreviewMouseMove 分支 :1601-1636，超过 SystemParameters.MinimumHorizontal/VerticalDragDistance 后 DataObject{FileDrop:[target]} + DragDrop.DoDragDrop）。

**"附着" = 窗口附着，不是调试器附着**（WindowTracker.cs:121-175）：

```
FindWindowsForManaged(mp):
  ① mp.Process 未退出 → FindWindowsForProcess → CollectProcessTreeIds(pid)
       （NtQueryInformationProcess(0) 取 InheritedFromUniqueProcessId，BFS 全子树；
         ghidraRun.bat → java 的 pid 靠这一步接上）
       → EnumerateVisibleWindows 按 pid 过滤 → 按 (W*H) 降序
  ② ①为空且配置了 hints → FindWindowsByHints(title/process 包含匹配，忽略大小写)
  ③ 按 Hwnd 去重
```

EnumerateVisibleWindows（:194-263）过滤：IsWindowVisible、GetWindowText 非空、窗口宽高均 ≥ 50；Bounds 优先 DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS=9) 回退 GetWindowRect，另存 ClientBounds（DWM 扩展边界是"视觉边界"，与 GetWindowRect 差一圈阴影 —— 这是高亮对齐的关键）。TryActivate（:265-280）= ShowWindow(hwnd, SW_RESTORE=9) + SetForegroundWindow。

**终止** ProcessManager.KillAll（ProcessManager.cs:112-147）：

```
foreach mp in _procs.Values:
    if (mp.Kind == "observed") { mp.Process.Dispose(); continue; }   // 只放句柄，不杀
    if (!mp.Process.HasExited) mp.Process.Kill(entireProcessTree: true);
```

但 ObserveExternal（:17-75）是**唯一**登记入口且写死 Kind = "observed"（:54），因此运行时**没有任何工具会被本程序 kill**；关窗只 Dispose 句柄（MainWindow.CleanupOverlayAndChildren :465-474）。这是有意为之：不越权结束用户自己的 IDA/x64dbg。PruneExited/Forget 仅维护字典。

### 3.4 悬浮指引的两条渲染路径

- **Host 层**（在自己窗口里贴）：HostGuideOverlayPresenter.Show（HostGuideOverlayPresenter.cs:38-77），画在 InAppGuideLayer（Canvas，Panel.ZIndex=1000）上；坐标 = ScreenPixelsToHost（:193-218）：CompositionTarget.TransformFromDevice 物理像素→DIP，再减 _host.PointToScreen(0,0)。
- **Screen 层**（跨进程贴到别人窗口上）：CoachOverlayWindow.ShowGuide（CoachOverlayWindow.cs:112-170），见 4.2。
- 选择逻辑 MainWindow.ShowGuide（:2909-2933）：plan.Layer == Host → 用宿主层并清屏层；Screen → 校验 GuideSceneMatcher.IsDisplayableExternalPlan + TargetWindowStillVisible，再 ActivateExternalGuideTargetOnce（同一 "stage:step:hwnd" 只抢一次焦点，:2935-2947）+ 屏层显示。

---

## 4. 关键技术点（含"为什么"）

### 4.1 无边框窗口 + 原生 chrome 圆角（NativeWindowChromeService）

- XAML（mainwindow.baml 字符串表，确定观察）：WindowStyle=None、ResizeMode=CanResize、WindowStartupLocation=CenterScreen、Title=MoeCTF、Width=1280 Height=800 MinWidth=960 MinHeight=600；<WindowChrome CaptionHeight=52 ResizeBorderThickness=... CornerRadius=... GlassFrameThickness=... UseAeroCaptionButtons=False>；根 Border Name=WindowShellBorder；标题栏内交互元素带 WindowChrome.IsHitTestVisibleInChrome=True。**没有** AllowsTransparency（关键：AllowsTransparency=True 会禁用 WindowChrome 的缩放/玻璃行为）。
- Attach(window, 12.0)（:96-120）：挂 SourceInitialized/SizeChanged/StateChanged/Closed，句柄已存在则立即初始化；Dispatcher.VerifyAccess() 强制 UI 线程；半径必须有限非负。
- HwndSource.AddHook 处理 5 类消息（:182-201）：WM_GETMINMAXINFO(0x24) → ConstrainMaximizedWindowToWorkArea；WM_SIZE/WM_SETTINGCHANGE/WM_DISPLAYCHANGE/WM_DPICHANGED → 重算圆角；WM_DWMCOMPOSITIONCHANGED(798) → 先清 _dwmCornersAvailable 缓存再算（DWM 重启后能力可能变化）。
- 圆角策略：优先 DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE=33, ROUND=2)（Win11 原生，无锯齿、不裁内容）；最大化/最小化/半径≤0 时反而设 DO_NOT_ROUND=1（否则最大化后四角露桌面/黑边）；API 不存在（Win10）→ 回退 CreateRoundRectRgn + SetWindowRgn，半径按 GetDpiForWindow()/96 换算，恢复时 SetWindowRgn(hwnd, NULL) 清除（:216-320）。
- 为什么必须处理 WM_GETMINMAXINFO（:322-344）：WindowChrome + WindowStyle=None 最大化时窗口会外扩一个 resize border 盖住任务栏；用 MonitorFromWindow(MONITOR_DEFAULTTONEAREST) + GetMonitorInfo.rcWork 改写 MaxPosition/MaxSize。
- 圆角更新合并：_cornerUpdatePending + BeginInvoke(DispatcherPriority.Render)（:203-214）；Dispose 支持跨线程（非 UI 线程时投递到 Send 优先级）。

### 4.2 置顶半透明悬浮窗 + 点击穿透（CoachOverlayWindow）

XAML（coachoverlaywindow.baml）：WindowStyle=None、AllowsTransparency=True、Topmost=True、ShowInTaskbar=False、ShowActivated=False、Focusable=False、IsHitTestVisible=False、ResizeMode=NoResize；根 Canvas Name=RootCanvas；Highlight(Border, 边框 #FF4CC2FF、底 #334CC2FF)、Arrow(Polygon, #FF4CC2FF)、Bubble(Border, #B3121824、圆角 14、MaxWidth 420、标题 #7DD3FF)。

代码侧（CoachOverlayWindow.cs）：

- CoverVirtualScreen()（:94-100）：Left/Top/Width/Height = SystemParameters.VirtualScreen*，覆盖**整个虚拟桌面**，多显示器一次到位。
- ApplyClickThrough()（:102-110）：GetWindowLong(GWL_EXSTYLE) | WS_EX_TRANSPARENT(0x20) | WS_EX_TOOLWINDOW(0x80) | WS_EX_NOACTIVATE(0x8000000) | WS_EX_LAYERED(0x80000)；在 OnSourceInitialized 和**每次 Show() 之后**都调用（ShowGuide:167-169），因为 WPF 层窗口重建时会丢样式。四件套缺一不可：TRANSPARENT 让点击落到下面的 IDA；NOACTIVATE 防止抢焦点；TOOLWINDOW 不进 Alt-Tab/任务栏；LAYERED 是 AllowsTransparency 合成前提。
- 跟随动画 SetAnimated（:192-221）：仅在"已有旧位置且位移 ≥ 0.5 DIP"时做 220ms CubicEase(EaseOut) 的 DoubleAnimation，HandoffBehavior.SnapshotAndReplace（避免动画叠加跳动）；首次定位直接 SetValue（否则从 0,0 飞入）。Pulse（:179-190）是 0.55↔1.0 / 700ms / AutoReverse / Forever 的呼吸，用于在复杂界面上"提醒眼睛"。
- 气泡摆放 PositionBubble（:223-278）：placement: auto|top|bottom|left|right|inside；auto 判据 = 右侧剩余 ≥ maxWidth+gap → right，否则下方 ≥ 116+gap → bottom，否则上方 ≥ 116+gap → top，否则左侧 → left，否则 inside（116 是气泡最小高度占位）；最后用显示器工作区 rcWork 做 Clamp 保证不出屏（MonitorWorkAreaToOverlay :349-375）。
- 坐标系（最易翻车）：ScreenPixelsToOverlay（:326-347）= CompositionTarget.TransformFromDevice 物理像素→DIP，再减本窗口 Left/Top（本窗口以 DIP 定位在虚拟屏幕左上）；输入侧是别的进程的**物理像素**（DWM 扩展边框）。

### 4.3 文件/进程监控与识别调度

- 200ms DispatcherTimer 轮询 RefreshTrackedWindows（MainWindow.cs:2711-2725）→ StartTrackingScanIfIdle（:2727-2739）→ GuideTrackingCoordinator.RequestScan(stages, Current, stage, step, revision, usesExternal, cbOverlay, cbRecognition)；结果在**后台线程**回调，再 Dispatcher.BeginInvoke(DispatcherPriority.Background) 回 UI 并校验 result.Revision == Volatile.Read(ref _guideRevision) 且 CourseDeskView.Visibility == Visible（:2741-2778）。
- 为什么用代际号而不是取消令牌：识别是"多发、可丢弃"的短任务，Interlocked.Increment + Volatile.Read 比 CTS 轻，且能一次性作废所有在途结果（切关卡/切步骤/Toggle 交互指引都会 AdvanceGuideRevision）。
- WindowChangeMonitor（Services\WindowChangeMonitor.cs:42-68）是可选事件源：SetWinEventHook 挂 EVENT_OBJECT_SHOW/FOCUS/STATECHANGE/NAMECHANGE/VALUECHANGE，WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS，回调里 GetAncestor(GA_ROOT) 归一到顶层窗口。主循环仍以 200ms 轮询为准。
- WindowSnapshotCache（WindowSnapshotCache.cs:25-37）：120ms TTL 的窗口快照，避免一次渲染里多次 EnumWindows。

### 4.4 WPF 细节 / 图标 / 图片

- TryExtractIcon（MainWindow.cs:2616-2655）：ExtractIconEx(path,0,large,small,1) 取小图标 → Imaging.CreateBitmapSourceFromHIcon(...,32×32) → Freeze()；finally 里 DestroyIcon（必须，否则 GDI 句柄泄漏；重建桌面图标一次取十几个）。
- TryLoadStepImage（:1890-1934）：多候选路径（绝对路径 / AssetsDir / ContentRoot / RepoRoot / 去掉 assets\ 前缀再拼），命中后 BitmapImage 用 CacheOption=OnLoad + Freeze()，避免锁文件且可跨线程共享。
- AutomationProperties.SetAutomationId/Name/HelpText（:729-730、:1594-1596）不只无障碍：Guide 的 host scope 定位器按 AutomationId/key 命中宿主控件（action/next/flag/submit…），即**命名即是契约**。
- 自定义控件：R2SCard : HeaderedContentControl，Level DP 默认 "default"（R2SCard.cs:8）；R2STag : ContentControl，Level DP 默认 "layer"（R2STag.cs:8）。样式 Trigger 用 Binding TemplatedParent.Level + Value=primary/info/success/warning/error 切换前景/背景/圆点（ret2shellcomponents.baml 中可见 Level+、Dot、Chrome 等记录）。

### 4.5 主题 token 与字体

ret2shelltokens.baml（app.baml 里 MergedDictionaries 先 token 后 components）：

| token | 值（观察） |
|---|---|
| R2SLayerColor / R2SLayerBrush | #1A1A1A |
| R2SLayerDeepColor/Brush | #101112 |
| R2SLayerRaisedColor/Brush | #262626 |
| R2SLayerContentColor/Brush | #FFFDFD |
| R2SLayerMutedColor/Brush | #A6A6A6 |
| R2SBorderSoftColor/Brush | #26FFFFFF |
| R2SPrimaryColor/Brush、R2SInfoColor/Brush | #1E9DE0 |
| R2SSuccessColor/Brush | #2DBE76 |
| R2SWarningColor/Brush | #E87900 |
| R2SErrorColor/Brush | #E45B5F |
| 白色 alpha 阶 | Content05 #0DFFFFFF / 10 #1A / 15 #26 / 20 #33 / 40 #66 / 60 #99 |
| R2SLayer60Brush #991A1A1A、R2SLayerOverlayBrush #E6101112 | 模态遮罩 |
| hover/pressed/soft 组 | #CC1E9DE0（hover）/ #B31E9DE0（pressed）/ #4D1E9DE0（soft），其余色同规律；R2SNextStepBrush #331B1207、R2SScrollHandleBrush #40FFFFFF、R2SScrollTrackBrush Transparent【token↔值一一对应为推断，色值本身为确定观察】 |
| R2SFontFamily / R2SMonoFontFamily | FontFamily，源串 pack://application:,,,/Assets/Fonts/#JetBrains Mono, Cascadia Mono, Cascadia Code, Microsoft YaHei UI, Consolas（等宽版把 YaHei 放最后） |

**字体加载是纯声明式的，没有任何 C# 字体代码**（.cs 里 FontFamily/GlyphTypeface/pack:// 全部 0 命中）。R2SFontFamily 是一个 FontFamily 资源，URI 指向 pack://application:,,,/Assets/Fonts/ —— WPF 在程序集编译资源（MoeCTF.RE.Tutorial.g.resources，DLL 中可见该流名与 .NET ResourceReader 头 CE CA EF BE @451744）的 Assets/Fonts/ 目录里找 .ttf，并按 #JetBrains Mono 取字体族名，缺失时按逗号后候选逐个回落。字号全是 DIP 常量（9.5/10/10.5/11/11.5/12/12.5/13/13.5/14/15/18/19/20/21/24/27/30）。

ret2shellcomponents.baml 提供的样式键（MainWindow 里全部用 FindResource("...") 按名取）：R2STextMuted / R2SSectionTitle / R2SLabel / R2SButtonBase / R2SButtonPrimary|Success|Warning|Danger|Ghost|Selected|Small|ExtraSmall|Square|SquareSmall / R2SWindowButton / R2SWindowCloseButton / R2STitleToggle / R2SInput / R2SInputSmall / R2SScrollBar(+Vertical/HorizontalScrollBarTemplate, ScrollThumb*, ScrollBarPageButton) / R2SPanel / R2SMetric / R2SDivider / R2SDividerVertical / R2SSegmentGroup / R2SSegmentRadio / R2SCard / R2STag。

### 4.6 单文件/内嵌资源读取

- 内容包：typeof(BundledContentInstaller).Assembly.GetManifestResourceStream("MoeCTF.RE.Tutorial.Host.BundledContent.zip")（BundledContentInstaller.cs:35，常量 ResourceName 见 :22，与 csproj 的 LogicalName 必须一致）。
- 安装算法（:32-67）：读 config/bundled-content-state.json 的 {relativePath: sha256}；对 zip 每条算 SHA256；**仅当**"磁盘 hash == 包内 hash"（已最新）或"磁盘 hash != 上次安装记录的 hash"（没人改过它）时跳过，其余覆盖写 → 语义：**用户手动改过、且被上次记录覆盖的文件不会被回滚**。
- 原子写：.<name>.<guid>.tmp + FileOptions.WriteThrough + Flush(true) + File.Move(overwrite)（:160-206）。
- 安全校验：拒绝绝对路径、./..、末尾空格或点、非法文件名字符、重复路径、GetFullPath 越界、ReparsePoint（符号链接）逃逸（:69-122、:208-229）。
- BAML/字体：Application.LoadComponent(this, new Uri("/MoeCTF.RE.Tutorial;V0.1.0.0;component/mainwindow.xaml", UriKind.Relative))（MainWindow.cs:3104、App.cs:62、CoachOverlayWindow.cs:384）。

### 4.7 HTTP / 版本比较（仅与 UI 编排相关的部分）

- CheckForUpdatesAsync（MainWindow.cs:1291-1389）：Task.WhenAll(hostUpdateTask, catalogUpdateTask) 并行；随后**串行**补一次课程更新；若结果 RequiresHostUpdate 再对 HostUpdateManifestUrl 二次查 host；_updateCheckInProgress 去重、CheckUpdateButton.IsEnabled 锁 UI；automatic == true 且没有任何更新源时直接返回（:1299-1302），避免启动时空转。
- InstallPendingHostUpdateAsync（:1396-1435）：Environment.ProcessPath → DownloadAndLaunchAsync(pending, RepoRoot, exePath, token)（在 Task.Run 里跑，不阻塞 UI）→ 成功后 Application.Current.Shutdown()，交由新进程以 --apply-update <request.json> 接管（App.cs:19-31）。
- SemanticVersion（Services\SemanticVersion.cs:7-103）：严格 semver 正则 + prerelease 数字/字母序比较；CourseDefinition.IsHostCompatible（CourseDefinition.cs:76-90）要求 min ≤ host < max，区间非法或解析失败一律 false —— 这是 Hub 上"兼容程序版本"文案红/绿与 EnterCourseButton.IsEnabled 的唯一依据。

### 4.8 并发与线程

| 位置 | 机制 | 为什么 |
|---|---|---|
| 识别/扫描回调 | 后台线程 → Dispatcher.BeginInvoke(DispatcherPriority.Background) | 不阻塞输入与渲染，后台优先级让 UI 先响应 |
| _guideRevision | Interlocked.Increment / Volatile.Read | 作废在途结果，旧关卡高亮不会盖到新关卡 |
| _processes | ConcurrentDictionary + 外部 _processAccessLock | 定时器、UI 事件、清理路径三处并发访问 |
| 更新检查 | Task.WhenAll + CancellationTokenSource（窗口关闭时 Cancel） | 关窗即中止下载，不留悬挂 Task |
| 圆角更新 | _cornerUpdatePending + BeginInvoke(Render) | 尺寸/DPI/重排连续触发合并为一次 |
| 窗口快照 | lock(_gate) + 120ms TTL | 一次渲染多处查窗口，避免 N 次 EnumWindows |
| DispatcherTimer 200ms/1s | 单 UI 线程 | 轮询窗口状态；用成本换简单（不依赖 WinEventHook 时序） |

---

## 5. 复刻要点（.NET 8 WPF 重写）

### 5.1 必须保留的契约

1. **磁盘契约**：progress.json 的 PascalCase 字段名与 ActiveCourseId/Courses{Version,FlowFingerprint,StageId,StageIndex,ClearedStages} 结构；requirements.json 的小驼峰 tools/courseTargets/courseChoices；courses/index.json → course.json → stages/index.json → <stage>.json 的引用链；内容目录名固定 MoeCTF.RE.Tutorial 且与 AppContext.BaseDirectory 同级（PathService.cs:42、HostUpdateInstaller.cs:80）。任一改动都会让老存档失效或内容装到别处。
2. **进度失效语义**：课程 version 或 stages 内容指纹变化 → 该课进度清零；已通关关卡不可回退（Max(MinimumActiveStageIndex, clamped)）。
3. **工具识别契约**：assets/tools/<toolId>/ 目录 + 白名单文件名 + WindowTitleHints/ProcessNameHints 两组模糊串；改成注册表/where.exe 会让"内嵌工具包"方案失效。
4. **不杀外部进程**：只 Dispose 句柄。
5. **引导热点的 AutomationId 与 _hostHotspots key 一致**（action/next/flag/submit/taskboard/coach/desktop、tool:<id>、target:<file>），因为 step.highlightHost 里存的就是这些字符串。
6. **completionMode 三种取值与 answerKey/fakeAnswerKeys 的比对语义**（Ordinal，且允许"输入包含答案"的宽松归一化）。

### 5.2 可以简化

- ProcessManager 的 Kind 分支（实际只有 "observed"）与 PruneExited/Forget 可合并。
- WindowChangeMonitor（WinEvent 钩子）可删 —— 200ms 轮询已够；反之亦可。
- CourseCatalog 的多根扫描（RepoRoot + workspace root）、PathService.FindWorkspaceRoot（找 .slnx + assets/tools）是开发期遗留，发布版可只留 RepoRoot。
- ToolAdapter 的 PathService 参数是死参数，可去掉。
- MainWindow 里大量内联 FindResource/Geometry.Parse 可换成资源字典 + DataTemplate，减少每次重建控件的开销。
- 顶栏/Hub 的手写 vector 图标（CreateCourseVectorIcon/CreateDesktopVectorIcon）可改为 Path 资源 + Style。

### 5.3 坑（按踩中概率排序）

1. **AllowsTransparency 与 WindowChrome 互斥**：主窗千万不要开 AllowsTransparency（否则 CaptionHeight 拖拽、ResizeBorderThickness 缩放全失效）；悬浮层反过来**必须**开（否则 #B3121824 会渲染成不透明黑）。两者用两个窗口，别试图一个窗口兼顾。
2. **WindowStyle=None 最大化溢出**：不做 WM_GETMINMAXINFO 钳制，最大化会盖住任务栏；同时该消息里**不要设 handled = true**，只改结构体。
3. **DWM 圆角在 Win10 不存在**：DwmSetWindowAttribute(33) 返回负值要缓存"不可用"并回退 SetWindowRgn；切最大化/最小化/跨显示器时必须清或重设 region，否则残影；WM_DWMCOMPOSITIONCHANGED 之后要重测能力。
4. **点击穿透样式要在每次 Show() 后重设**：WS_EX_LAYERED 在 WPF 层窗口重置时可能丢；IsHitTestVisible=False + ShowActivated=False + WS_EX_NOACTIVATE 三者缺一，就会出现"点一下工具结果悬浮层抢走焦点/闪一下"。
5. **DPI 混算**：屏幕坐标来自别的进程（物理像素、DWM 扩展边界），悬浮层坐标是 DIP；用 CompositionTarget.TransformFromDevice 换算并减去本窗口 Left/Top。多显示器不同缩放时该矩阵只对**本窗口所在显示器**有效 —— 跨屏高亮要按目标窗口所在显示器的 DPI（GetDpiForMonitor）换算，或进程级声明 PER_MONITOR_AWARE_V2 后统一换算。另外 SystemParameters.WorkArea 只是主屏工作区，FitWindowToWorkArea 在副屏上会误夹窗口。
6. **窗口尺寸阈值过滤**：EnumerateVisibleWindows 丢掉 <50px 的窗口（滤掉 tooltip/浮动小窗），但也可能滤掉某些工具的窄面板；要认 x64dbg 的 CPU 子窗口就得放宽或用类名。
7. **UIA 权限（UIPI）**：若 IDA/x64dbg 以管理员运行而本程序不是（或反之），UIAutomation 拿不到对方控件，只能退化为"整窗高亮"。复刻时要显式检测双方完整性级别并提示"请以相同权限运行"，否则用户只会觉得"指引不准"。
8. **句柄时效**：HWND 会复用、窗口会移动；每次渲染前用 WindowSnapshotCache 校验 Hwnd+Pid 仍存在（TargetWindowStillVisible，MainWindow.cs:3069-3084），否则清 overlay，别把高亮贴在已关闭窗口的位置上。
9. **DragDrop.DoDragDrop 必须 UI 线程**且不能在 PreviewMouseMove 里做重活；DataObject 里只放真实存在的路径（_paths.FindTarget 为 null 时直接跳过，MainWindow.cs:1625-1632）。
10. **图标 DestroyIcon 与 BitmapImage Freeze**：前者漏了是 GDI 句柄泄漏（重建桌面图标很频繁），后者漏了会锁住图片文件且不能跨线程。
11. **资源打包**：.baml 必须走 WPF 的 Page/编译资源（<AssemblyName>.g.resources），用 Application.LoadComponent("/Asm;component/mainwindow.xaml") 加载；把它当 EmbeddedResource + LogicalName（_src\MoeCTF.RE.Tutorial.csproj:21-34 的现状）是**跑不起来**的。字体同理：作为 WPF Resource 放在 Assets/Fonts/*.ttf，FontFamily 用 pack://application:,,,/Assets/Fonts/#JetBrains Mono；路径大小写与目录层级敏感。
12. **内嵌 zip 的"只覆盖未被用户改过且有变化"策略**（BundledContentInstaller.cs:55-62）要照抄，否则用户手改的 i18n 会被每次启动静默覆盖。
13. **命名即契约**：加了 x:Name 却不加 AutomationProperties.AutomationId，宿主热点就定位不到；复刻时改了控件命名，既有 step.json 里的 highlightHost 全部失效。
14. **主窗口字段初始化器里做 I/O**（_paths = new PathService() 会装内容）意味着"内容安装发生在构造函数体之前、UI 尚未出现时"；若改成异步安装，必须同步调整 --bootstrap-only 的语义（它正是用来"先装内容、不建 UI"）。

### 5.4 关键实现片段（可直接照抄的结构）

```csharp
// 1) 点击穿透（CoachOverlayWindow.cs:102-110）
var h = new WindowInteropHelper(this).Handle;
SetWindowLong(h, GWL_EXSTYLE,
    GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW
    | WS_EX_NOACTIVATE | WS_EX_LAYERED);

// 2) 原生圆角（NativeWindowChromeService.cs:238-259）
int pref = 2; // DWMWA_WINDOW_CORNER_PREFERENCE = 33, ROUND
_dwmCornersAvailable = DwmSetWindowAttribute(hwnd, 33, ref pref, 4) >= 0;

// 3) 物理像素 → 悬浮层 DIP（CoachOverlayWindow.cs:326-347）
var m = (PresentationSource.FromVisual(this) as HwndSource)
        ?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
var p1 = m.Transform(new Point(r.Left, r.Top));
var p2 = m.Transform(new Point(r.Right, r.Bottom));
var local = new Rect(p1.X - Left, p1.Y - Top, p2.X - p1.X, p2.Y - p1.Y);

// 4) 拖出文件（MainWindow.cs:1625-1632）
var data = new DataObject();
data.SetData(DataFormats.FileDrop, new[] { targetPath });
DragDrop.DoDragDrop(icon, data, DragDropEffects.Copy);
```

---

## 6. 待确认问题（≤5，均定位到代码）

1. **mainwindow.baml 的精确视觉树未逐记录还原**：目前只能由 IComponentConnector.Connect 的 1..93 顺序（MainWindow.cs:3119-3421）+ BAML 字符串表推断分区（标题栏 / Hub / Desk / TaskBoard / CoachCard / 任务栏 / InAppGuideLayer / 三个模态层）。需要 ilspycmd --ilcode 或 Baml2006Reader 把 _src\mainwindow.baml 反编译成 XAML，才能确认 CourseDeskView 内左右栏的行列定义与 ScrollViewer 层级（本机无 ilspycmd，且 dotnet --list-sdks 为空）。
2. **JetBrains Mono 的确切打包形式**：DLL 内检索不到 jetbrainsmono-regular.ttf / "ttf" 明文资源名（0 命中），而 _src\assets\fonts\jetbrainsmono-regular.ttf 存在 270224 字节。需解析 MoeCTF.RE.Tutorial.g.resources 的 ResourceReader 名字表确认 ttf 是否真在编译资源内；否则会静默回落 Cascadia/Consolas，主题观感不同。
3. **ContentUpdater.RecoverInterruptedUpdate()（MainWindow.cs:332）的行为边界**：它在主窗口构造期执行，但中断标记/暂存目录定义在 ContentUpdater.cs（39KB，属内容更新子系统）。需确认首启是否可能删除已下载内容或改写 PathService 的 UseBuiltInContent 状态，以免复刻时漏掉这一步。
4. **GuideTrackingCoordinator.RequestScan 的节流/重入策略**（GuideTrackingCoordinator.cs）：200ms tick 是否每次都真正发起 UIA 扫描、是否有 in-flight 合并。这决定复刻时的 CPU 占用与"高亮滞后 1~2 秒"观感。
5. **标题栏最大化按钮的状态反馈**：BAML 里只有 3 个匿名 Button（Connect 6/7/8，MainWindow.cs:3140-3148）接最小化/最大化/关闭，MaximizeButton_OnClick（:1460-1463）只切 WindowState，未见"最大化↔还原"图标切换；ret2shellcomponents.baml 中也不见 WindowState 字符串，倾向"图标不变"。需确认是否由样式 Trigger 处理。
