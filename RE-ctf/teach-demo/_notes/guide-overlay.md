# Guide（引导）子系统逆向分析报告

> 分析对象：MoeCTF.RE.Tutorial（RE.Tutorial.exe，.NET 8 / WPF / 单文件框架依赖）
> 证据来源：ILSpy 反编译源码树 D:/ctf/RE-ctf/teach-demo/_src/ ，内容包 D:/ctf/RE-ctf/teach-demo/MoeCTF.RE.Tutorial/ ，BAML 资源 coach/coachoverlaywindow.baml
> 约定：**【观察】** = 源码/资源可直接指认；**【推断】** = 由实现推出的设计意图，无直接注释佐证。
> 命名空间缩写：Guide = MoeCTF.RE.Tutorial.Host.Guide，Services = .Host.Services，Stages = .Host.Stages。

---

## 0. 结论速览

Guide 子系统是一套“外挂式教学陪练”：它**不注入、不 hook、不读对方内存**，仅通过 Win32 窗口枚举 + UI Automation 文本树 + 少量进程信息，判断“用户现在处于 IDA / x64dbg / Ghidra / DIE / dnSpy 的哪一个界面状态”，然后把匹配到的控件矩形**换算成屏幕像素矩形**，交给一个**覆盖整个虚拟屏幕、置顶、点击穿透的透明 WPF 窗口**去画高亮框和气泡；同时用“步骤触发器（trigger）”自动把课程进度跳到用户当前实际所在的步骤。

三块能力必须分开看，很多人会混淆：

| 能力 | 实现位置 | 输出面 |
| --- | --- | --- |
| Host 内引导（高亮自家 UI 控件） | GuideSceneMatcher.ResolveLocator 的 scope==host 分支 + HostGuideOverlayPresenter | 主窗口内的 Canvas（InAppGuideLayer） |
| 外部程序引导（高亮 IDA 等） | 同上 scope==process 分支 + TryResolveProcessControls + ControlLocator | 全屏透明窗 CoachOverlayWindow |
| 自动识步（trigger 反推当前该讲第几步） | GuideSceneMatcher.MatchCourse / GuideRecognitionEngine.Recognize + GuideProgressionPolicy | 改写 _stageIndex/_stepIndex 并落盘进度 |

---

## 1. 该子系统职责（一句话）

**看着别的程序的窗口，把课程脚本里声明的“锚点”（窗口/控件/文本证据/状态）在真实屏幕上定位出来，换算成屏幕坐标后投影成悬浮高亮 + 提示气泡，并用同一套证据反向推断用户当前应该处在哪一步，从而做到“用户自己乱点也不会卡住”的引导。**

---

## 2. 文件地图（只列与 Guide 直接相关）

| 文件 | 行数 | 角色 |
| --- | --- | --- |
| Guide/GuideSceneMatcher.cs | 1773 | 核心：条件求值、窗口打分、控件定位、渲染计划合成、排序与歧义判定 |
| Guide/GuideRuntime.cs | 567 | 门面：Resolve / MatchCourse / 异步包装 / 分支 / 偏离 / 旧字段兼容 |
| Guide/GuideRecognitionEngine.cs | 149 | 跨阶段识别“当前步骤”，唯一胜者判定 |
| Guide/GuideTrackingCoordinator.cs | 319 | 后台扫描调度、防抖、并发门闩 |
| Services/ControlLocator.cs | 2092 | UIA 树快照 + Win32 子窗口回退 + 文本证据 + 评分 + 缓存/退避 |
| Services/WindowTracker.cs | 436 | EnumWindows 枚举 + DWM 边框 + 客户区 + 进程树 |
| Services/WindowChangeMonitor.cs | 79 | SetWinEventHook 变更失效 |
| Services/WindowSnapshotCache.cs | 46 | 120 ms 窗口快照缓存 |
| Guide/GuideAppAdapterBase.cs + 6 个 Adapter | 约 700 | 各工具的“状态/角色”知识库（IDA/x64dbg/Ghidra/DIE/dnSpy/Win32） |
| Coach/CoachOverlayWindow.cs | 419 | 全屏透明置顶穿透窗（HUD） |
| Guide/HostGuideOverlayPresenter.cs | 232 | 主窗口内嵌引导层 |
| Services/NativeWindowChromeService.cs | 440 | DWM 圆角/最大化约束（主窗口外观，间接相关） |

---

## 3. 数据结构与 JSON 契约

### 3.1 内容侧（磁盘 JSON，真正的对外契约）

权威 schema：MoeCTF.RE.Tutorial/schemas/guide-stage.schema.json（210 行，draft 2020-12，additionalProperties=false）。
装载入口：Stages/StageRepository.cs:17-44 —— 读 stages/index.json 得到 stage 文件名列表，逐个反序列化，按 order 升序。

**序列化约定【观察】**
- JsonSerializerOptions 只设了 PropertyNameCaseInsensitive = true（StageRepository.cs:10-13）。没有命名策略、没有缩进要求、没有注释支持、没有 enum 字符串转换（枚举在 C# 侧一律是 string 字段）。
- 所有字段用 [JsonPropertyName] 显式锁定小驼峰（如 GuideTargetSpec.cs:8-36）。
- 缺字段 = 保留 C# 属性初始化器的默认值；多余字段会**被静默忽略**（运行时不做 schema 校验，schema 只服务内容作者/CI）。
- 数字一律 double/int，无单位；矩形坐标是**屏幕物理像素**（“space”:“screen”）或**相对容器归一化**（其它 space）。

### 3.2 GuideSpec 及其子结构（逐字段）

**GuideSpec**（Guide/GuideSpec.cs，19 行）

| 字段 | 类型 | 可空 | 默认 | 说明 |
| --- | --- | --- | --- | --- |
| target | GuideTargetSpec | 否 | new() | 目标标识（作用域 + 身份线索） |
| locators | List<GuideLocatorSpec> | 否 | 空表 | 多个定位器，按序尝试，先命中“精确”就返回 |
| overlay | GuideOverlaySpec | 否 | new() | 气泡排布参数 |
| fallback | GuideTargetSpec? | **是** | null | 全部定位失败后的降级目标；GuideRuntime.Resolve 只在“非 Host、不可显示、且 FailureReason 非空”时才用它 |

**GuideTargetSpec**（GuideTargetSpec.cs:6-37）

| 字段 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| scope | string | “host” | schema 枚举 host/process/screen/none；NormalizeScope 还会把 child/external/target 归一到 process，desktop 归一到 host（GuideSpecHelpers.cs:231-242） |
| key | string? | null | scope=host 时是“热点键”（coach/next/flag/action/taskboard/submit/desktop，见 MainWindow.cs:410-416）；scope=process 时可用 tool:ida / target:chall1.exe 约定 |
| process | string? | null | 托管进程键（tool:xxx 或 target:文件名） |
| adapter | string? | null | die/ida/ghidra/dnspy/x64dbg/win32 |
| titleContains / processNameContains / exeNameContains / exePathContains / windowClassNameContains | List<string> | 空表 | 五类身份线索；类别之间是“与”，类别内 hint 之间是“或”（任一命中即通过） |
| titlePidFromProcess | string? | null | 从该托管进程取 PID，再要求窗口标题里出现这个 PID 数字（IDA/Ghidra 多实例区分，GuideSceneMatcher.cs:1474-1495 / 1523-1540） |

**GuideLocatorSpec**（GuideLocatorSpec.cs:6-94，字段最全）

| 字段 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| strategy | string | “auto” | auto/host/uia/win32/window/process_window/rect/none；只有 win32 会切 ControlLocator 后端，window/process_window 走“整窗高亮”快捷路径 |
| scope | string? | null | 覆盖 target.scope |
| key / process / adapter | string? | null | 覆盖 target 同名字段（MergeTarget，GuideSpecHelpers.cs:28-43，locator 优先） |
| state | string? | null | **适配器状态名**（如 quick_start、open_file_modal、system_break、breakpoint_hit） |
| role | string? | null | **适配器角色名**（如 confirm_button、command_bar、disassembly） |
| nameEquals | string? | null | 控件 Name 精确匹配（+80 分） |
| nameContains | List<string> | 空表 | 控件 Name/Value/Text 包含（每条 10+min(len,24) 分） |
| contentContains | List<string> | 空表 | 内容证据；contentMode=evidence 时只作“状态证据”，=target 时才是高亮目标 |
| documentContains | List<string> | 空表 | 文档级证据（如文件名 chall1）；**要求全部命中**（flag3，GuideSceneMatcher.cs:430） |
| contentMode | string | “evidence” | evidence / target |
| rejectTitleContains / rejectContentContains | List<string> | 空表 | 反证：命中即判“不是这个界面”，产出 reject-title:/reject-content: 证据并把 Contradicted 置真 |
| requireAllNameContains | bool | false | true = 所有 hint 都必须命中 |
| automationId / className / controlType | string? | null | UIA 硬过滤（+70 / +30 / +18 分） |
| rect | GuideRectSpec? | null | 显式矩形 |
| adjust | GuideRectAdjustSpec? | null | 命中后微调 |
| fallbackToWindow | bool | false | 控件找不到时退化成整窗 |
| maxNodes | int? | null→2000（控件）/3000（文本） | 遍历上限，运行时被 clamp 到 50..3000 |
| timeoutMs | int? | null→1200 | 单次查询预算，运行时 clamp 100..1200 |

**GuideRectSpec**（GuideRectSpec.cs）：x/y/width/height（double）+ space（默认 “screen”）。space 语义见 5.3。
**GuideRectAdjustSpec**（GuideRectAdjustSpec.cs）：offsetX/offsetY/inflate（double，默认 0）+ width/height（double?，null=保持）。
**GuideOverlaySpec**（GuideOverlaySpec.cs）：placement（auto）/ alignment（center）/ padding（6.0）/ gap（14.0）/ offsetX、offsetY（0）/ maxWidth（double?，null→420）。

**GuideConditionSpec**（GuideConditionSpec.cs:6-34）

| 字段 | 说明 |
| --- | --- |
| kind | always / all / any / process_running / window_visible / locator_found / host_visible / value_equals / value_contains（GuideSceneMatcher.cs:326-339 的 switch 是权威表；未知 kind 走 _legacyConditionEvaluator） |
| not | 求值后整体取反（GuideSceneMatcher.cs:311-320）；**取反会丢掉 Hard/Application/State 证据计数**，只保留 Matched/Specificity |
| process | 托管进程键（process_running / window_visible 用） |
| key | host_visible 的热点键；value_equals/value_contains 的取值键 |
| value / valueKey | 期望值 / 期望值的 i18n 键（valueKey 优先，经 TextProvider 解析） |
| locator | locator_found 内嵌的定位器 |
| all / any | 子条件表；all 空表=匹配成功，any 空表=匹配失败 |

**GuideBranchSpec**（GuideBranchSpec.cs）：trigger（默认 “next”，“any” 通配）+ when（条件）+ next（目标 step id；查不到就返回 null 表示“不分叉”）。

**挂载点**（Stages/StageStep.cs:30-52）：step 上有 guide、trigger、branches；还有 6 个**旧版兼容字段** highlightScope/highlightHost/highlightProcess/highlightNames/highlightControlType —— GuideRuntime.BuildLegacyGuide（GuideRuntime.cs:408-470）在 step.guide 为空时把它们翻译成等价 GuideSpec（host 默认 key 依次是 highlightHost → tool:xxx → action → target:文件 → coach）。

**GuideDeviationSpec**（Stages/GuideDeviationSpec.cs）：id/textKey/bodyKey/image/priority（默认 0）/when/guide —— 阶段级“走偏了”的插播提示。

### 3.3 运行时类型（不落盘，但复刻必须同构）

| 类型 | 关键字段 | 备注 |
| --- | --- | --- |
| NormalizedRect（record struct） | X,Y,Width,Height | FromScreen 时容器宽/高 ≤1.0 直接返回 (0,0,1,1)；Clamp 保证 0..1 且宽高 ≥0.001（NormalizedRect.cs:8-30） |
| LocatedRect | BoundsScreen（required）、BoundsInWindow?、CoordinateSpace、Source | CoordinateSpace ∈ {screen, window-normalized, client-normalized}；client-normalized 的判定是“参考矩形与 ClientBounds 四边容差 <0.5”（LocatedRect.cs:70-111） |
| GuideRenderPlan | Layer、BoundsScreen?、BoundsInWindow?、TargetWindow?、Overlay、Attachment、Score、CoordinateSpace、Evidence、FailureReason | 三个派生谓词见下 |
| GuideSceneMatch | Stage、Deviation、StageIndex、Step（required）、StepIndex、RenderPlan、Score、Scope、Evidence、IsStepTrigger、HasHardTriggerEvidence、HasContradictoryTriggerEvidence、TriggerSpecificity、TriggerEvaluation | IsExternal = Scope==“process”（GuideSceneMatch.cs:37） |
| GuideConditionEvaluation | Matched、Contradicted、Hard/Application/Document/StateEvidenceCount、Specificity、Evidence、RecognitionPlan | IsConclusive = Matched && !Contradicted && ApplicationEvidence>0 && StateEvidence>0 && HardEvidence>0（GuideConditionEvaluation.cs:26-36） |
| GuideWindowCandidate | Window、Score、Evidence | |
| AppControlMatch | Bounds、Score、AdapterId、State、Role、UsedFallbackRegion、Control、Reason | |
| ControlMatch | Bounds、Score、VisitedNodes、Name、AutomationId、ClassName、ControlType、Provider（默认 uia）、MatchedHints、Reasons | |
| ControlTextEvidence | Score、VisitedNodes、TimedOut、Provider、MatchedHints、SampleNames | |
| WindowInfo | Hwnd、Pid、ThreadId、OwnerHwnd、RootOwnerHwnd、LastActivePopupHwnd、IsForeground、Title、ProcessName、ClassName、ExePath、Bounds、ClientBounds；ExeName 为 ExePath 去扩展名派生 | WindowInfo.cs:6-44 |
| TrackedWindowSnapshot | record (Id, Pid?, Hwnd?, Title, Width, Height, Waiting) | 仅用于 UI 上的“已跟踪窗口”列表 |
| GuideProgressPosition | readonly record struct (StageIndex, StepIndex) | |
| GuideRecognitionMatch | (Scene, Confidence, Evidence) | Confidence ∈ 0..100 |

**GuideRenderPlan 的三个门槛（最关键的三段代码）**【观察】

~~~
IsExternalDisplayable = Layer==Screen && TargetWindow!=null && BoundsScreen.HasValue
                        && FailureReason==null
                        && Attachment in {Window, VerifiedRegion, Control, Content}   // GuideRenderPlan.cs:30-43
IsPreciseExternal     = IsExternalDisplayable && Attachment in {Control, Content}     // :45-58
IsAnchoredExternalPlan= IsExternalDisplayable && Attachment != Window                // GuideSceneMatcher.cs:687-694
~~~

注意 **Attachment==Region（落点已知但未“验证”）不在可显示集合内**：一个只有 rect、没有文本证据的定位器，最终 plan 会被判为不可显示，直接不进 HUD。这是刻意的“宁可不说，也不瞎指”。

GuideAttachmentState 枚举顺序（GuideAttachmentState.cs）：None, Host, Window, Region, VerifiedRegion, Control, Content —— 上面用 (uint)(x-4)<=2 / (x-5)<=1 的位段写法就是靠这个顺序。

---

## 4. 关键流程

### 4.1 装配（MainWindow 构造函数，MainWindow.cs:320-385）

~~~
_windowSnapshots = new WindowSnapshotCache(_tracker)                  // :325  TTL 120ms
_hostGuideOverlay = new HostGuideOverlayPresenter(this, InAppGuideLayer, ...)  // :333
_guide = new GuideRuntime(new GuideRuntimeContext{                     // :339-350
    HostWindow=this, HostHotspots=_hostHotspots, Processes=_processes.Processes,
    WindowTracker=_tracker, WindowSnapshots=_windowSnapshots,
    ControlLocator=_locator, AppLocator=new GuideAppLocator(),
    ValueProvider=GuideValue, TextProvider=_i18n.GetRaw })              // 条件求值只依赖两个 Func<string,string?> 回调，便于脱耦
_guideTracking = new GuideTrackingCoordinator(_guide, ..., _processAccessLock)  // :351
_trackTimer = new DispatcherTimer{ Interval=200ms }; _trackTimer.Tick += RefreshTrackedWindows  // :353-361
RegisterStaticHotspots()                                               // :381 → :408-417 建立 7 个 host 热点
~~~

_overlay（CoachOverlayWindow）是**字段初始化器**（:68），因此在主窗口构造阶段、UI 线程上就已创建（ctor 里调 InitializeComponent + CoverVirtualScreen，CoachOverlayWindow.cs:79-86），但直到第一次 ShowGuide 才 Show()。

### 4.2 每 200 ms 一拍：调度（GuideTrackingCoordinator）

~~~
RefreshTrackedWindows()                                   // MainWindow.cs:2711
 若 !_interactiveGuideEnabled / 无课程 / 不在课程页 → 只更新状态文字
 否则 StartTrackingScanIfIdle()                           // :2727-2739
     request = new GuideTrackingRequest(stages, Current, _stageIndex, _stepIndex, revision)
     _guideTracking.RequestScan(request, locateOverlay: CurrentStepUsesExternalGuide(),
                                 OnOverlayScanCompleted, OnRecognitionCompleted)

RequestScan(track, locateOverlay, onOverlay, onRecog)     // GuideTrackingCoordinator.cs:139-149
 if locateOverlay: StartOverlay(...)                      // 独立 in-flight 门闩 _overlayInFlight
 总是 StartRecognition(...)                              // _recognitionInFlight + 240ms 节流
~~~

- **StartOverlay**（:157-184）：Task.Run 里 RefreshProcessesIfDue → BuildTrackedWindows → _runtime.LocateExternalOverlay(stage, idx, step, 30, token) → 回调 GuideOverlayScanResult(windows, overlay, revision)。任何异常都回调“空结果”，绝不把异常抛回 UI。
- **StartRecognition**（:186-217）：节流 240ms（RecognitionInterval, :86）；RecognizeCourse(stages, 140) → RecognitionStabilizer.Observe(match, revision) → 只有稳定器放行才回调。
- **RefreshProcessesIfDue**（:219-238）：300ms 一次（ProcessRefreshInterval, :84），在 _processGate 锁内做 ProcessManager.PruneExited + TargetProcessObserver.ObserveCurrentTools/Targets，然后 _windowSnapshots.Invalidate()。这是“新开了 IDA 也能被发现”的机制。
- **RecognitionStabilizer**（:15-82）：同一 (revision, stage, step, pid, hwnd) 键**连续观测 2 次**才放行一次（RequiredObservations=2），该键 1400ms 不刷新就作废（ObservationLifetime, :21），放行后 _delivered=true 不再重复。

**【推断】为什么是 2 次 + 1.4 s**：UIA 首次访问一个刚创建的 Qt 窗口会拿到半成品树（控件还没布局完），单次观测极易误判；连续两拍（约 480ms）都一致才算稳定。

### 4.3 单次定位主链（GuideSceneMatcher.ResolveSpec → ResolveLocator）

~~~
ResolveSpec(spec, windows, out score, out evidence, out scope)          // :1288-1384
 locators = spec.Locators.Count>0 ? spec.Locators : [TargetToLocator(spec.Target)]
 specHasRecognitionLocator = locators.Any(HasRecognitionAnchor)
 for each locator:
    plan = ResolveLocator(locator, spec.Target, spec.Overlay, windows, ...)   // :756
    if plan.Layer == None           -> 立刻返回（“明确的失败”短路，不再试下一个）
    if plan.Layer == Host && 有矩形  -> 记为 host 候选（取首个），继续
    if plan.IsPreciseExternal       -> 立刻返回（精确命中优先，绝不降级）
    if Attachment==VerifiedRegion && 可显示 && score>best -> 记候选 A
    elif Attachment==Window && 可显示 && CanUseWindowPlan(...) && score>best -> 记候选 B
 返回顺序：A（验证区域）> B（整窗）> Host 候选 > Layer=None + “guide target not attached”
~~~

ResolveLocator 的分派（:767-967）：

1. **scope==none** → Layer=None，score=1（:770-779），用于“这一步不要高亮”。
2. **scope==host**（:780-795）→ ResolveHost：有 rect 就按 host 窗口客户区解析；否则取 key（默认 coach）在 _hostHotspots 里找 FrameworkElement，用 PointToScreen(0,0) 与 PointToScreen(ActualWidth,ActualHeight) 两点求矩形（:1643-1670）。**必须 IsVisible 且 ActualWidth/Height ≥1**，否则 null。score=70。
3. **scope==screen** → ResolveExplicitRect(locator, null)，绝对屏幕矩形，score=50，Attachment=Region。
4. **process** → FindWindowCandidates（:1248-1280）按 ScoreWindow 打分，降序 + 面积降序。
   - 无候选 → Layer=Screen + FailureReason “process window not found”。
   - strategy ∈ {window, process_window} → TrySelectReliableWindow，命中则整窗（Attachment=Window，score+24，CoordinateSpace=window）。
   - 有 rect 且不是 control/anchor 且无识别锚点 → 直接用 rect 相对窗口参考矩形解析（score+36）。
   - 否则对**每个候选窗口**跑 TryResolveProcessControls，取最高分；若“最佳分与其它候选相差 <25 的候选数 >1”则判歧义，返回 Layer=None + “ambiguous precise target”（:921-948）。

### 4.4 控件级定位（TryResolveProcessControls，:970-1203）

顺序非常关键（**先适配器知识，再通用 UIA，最后整窗兜底**）：

~~~
1) AppLocator.Locate(window, locator, ControlLocator)      // 只有 locator.adapter 或 locator.role 非空才进
   -> 命中：score = 窗口分 + AppMatch.Score + 内容证据分 + contentTarget 分
      Attachment = Content（contentMode=target）
                 | Control（非 fallback 区域）
                 | VerifiedRegion / Region（用了适配器给的固定百分比区域）
2) 无适配器命中，但声明了 adapter/state/role 且又没内容线索 -> 直接失败 “adapter-control:not-found”
3) 全部 rejectContentContains 再查一次（反证优先于一切）
4) 内容证据：FindTextEvidence(DocumentContains+ContentContains) -> 缺失即失败 “content:not-found”
5) 有内容证据却没有控件锚点 -> 直接给“整窗 + VerifiedRegion + content:distributed-evidence”（+36）
6) 通用控件匹配：ControlLocator.FindChildControlMatch(hwnd, ToControlOptions(locator))
7) 失败且 fallbackToWindow=false -> “control:not-found”
8) contentMode=target -> FindContentTarget（文本控件定位），Attachment=Content
9) 其余 -> 控件矩形（或整窗），Attachment=Control / Window
~~~

**GuideSpecHelpers.ToControlOptions**（:45-78）的隐蔽细节：NameContains 取 locator.NameContains；若为空且**不是** contentMode=target，则**退化为用 ContentContains 当控件名线索**（所以“内容词”也能命中命名相同的控件）；MinScore 固定 14，MaxNodes 默认 2000，TimeoutMs 默认 1200，Backend 只认 strategy==win32。

### 4.5 识别（决定“现在该讲第几步”）

**MatchCourse**（:77-102）：先对**所有阶段的 trigger** 求值，收集 → RankDeterministicTriggers → 取第一名 → ResolveWinningTrigger 得到带渲染计划的场景；没有任何 trigger 命中时才退回收集 deviations。

**RankDeterministicTriggers**（:295-301）与 **GuideRecognitionEngine.SelectUnambiguousWinner**（:86-108）是两套并列排序，“步骤越靠后越优先”是明确设计（进度只前进）：

~~~
RankDeterministicTriggers: where IsStepTrigger && HasHardTriggerEvidence && !HasContradictory
                           order by StageIndex desc, StepIndex desc, TriggerSpecificity desc,
                                    RenderPlan.IsPreciseExternal desc, Score desc

SelectUnambiguousWinner : 先按 EvidenceStrength desc
                          = 1000*App + 400*Doc + 120*State + (precise?80:0)      // :110-118
                          再 TriggerSpecificity, IsPreciseExternal, StageIndex, StepIndex desc
                          仅当 前二名 SameScene(同 Pid 且同 Hwnd) && Dominates(...) 才认第一名，
                          否则返回 null（宁可不判断）
Dominates = EvidenceStrength 更高 || TriggerSpecificity 差 >=36 || (winner precise && loser 不 precise)
~~~

**置信度**（GuideRecognitionEngine.cs:78-83）：

~~~
confidence = clamp(35*App + 30*State + 25*Doc + (precise?10:0), 0, 100)
confidence < 65 -> 判定为“没认出来”，返回 null
~~~

**识别阈值恒定 140**（Recognize 默认参数，Coordinator 也传 140，:198）：trigger 命中的场景 Score = max(plan.Score, 140) + min(Specificity,100)，所以“能进候选”的都是这个量级。

**回写进度**（MainWindow.TrySyncDetectedScene，:2964-3026）——自动跟进的落地：

~~~
拒绝条件：!交互模式 / StageIndex 越界 / !IsExternal / IsDeviation /
          !IsStepTrigger / !IsReliableExternalRecognition /
          (同阶段且 scene.StepIndex < _stepIndex)  // 只前进
          scene.StageIndex < MinimumActiveStageIndexFromClearedProgress(...)
pos = GuideProgressionPolicy.SynchronizeToRecognition(stage, step, stepsCount)   // step clamp 到 [0, n-1]
若已在该位置且 _guideUnlocked -> 无操作
否则：改 _stageIndex/_stepIndex、_guideUnlocked=true、AdvanceGuideRevision()、
      把途经阶段记入 ClearedStages、按 external_submit 规则记通关、Save()、RenderCurrentGuideState()
~~~

### 4.6 渲染（两条完全独立的显示通路）

**MainWindow.ShowGuide**（:2909-2933）是唯一分发点：

~~~
if plan.Layer == Host:
    _overlay.ClearGuide(); _hostGuideOverlay.Show(plan, text, title)     // 画在主窗口 Canvas 里
else:
    _hostGuideOverlay.Clear()
    if plan.Layer == Screen:
        if !IsDisplayableExternalPlan(plan) || !TargetWindowStillVisible(plan): _overlay.ClearGuide(); return
        ActivateExternalGuideTargetOnce(plan)      // 每个 (课程:step:hwnd) 只 SetForegroundWindow 一次
        _overlay.ShowGuide(plan.BoundsScreen, text, title, plan.Overlay)
    else: _overlay.ClearGuide()
~~~

TargetWindowStillVisible（:3069-3084）用窗口快照按 (Hwnd, Pid) 双键复核，防止“目标窗口已关闭但坐标还在”。

**CoachOverlayWindow.ShowGuide**（CoachOverlayWindow.cs:112-170）：

~~~
CoverVirtualScreen(); ApplyClickThrough();       // 每次重设，防止显示器变化后错位
BubbleTitle.Text = title ?? “COACH”; BubbleText.Text = text
if 有有效目标矩形:
   overlayRect  = ScreenPixelsToOverlay(target)          // 设备像素 -> DIP，再减去窗口原点
   visibleArea  = MonitorWorkAreaToOverlay(target)       // MonitorFromRect(MONITOR_DEFAULTTONEAREST) + GetMonitorInfo(rcWork)
   Highlight 用 SetAnimated 动画到 (left,top,w,h)（左上有有效值时才 animate）
   Pulse(Highlight)                                      // 0.55->1.0, 700ms, AutoReverse, Forever
   Bubble.MaxWidth = Max(260, Min(overlay.MaxWidth ?? 420, visibleArea.Width-24))
   PositionBubble(target, overlay, visibleArea)
else:
   Highlight/Arrow 隐藏，气泡固定到右上角
if !IsVisible: Show(); ApplyClickThrough();
~~~

**PositionBubble**（:223-278）：placement==auto 时按 **右 → 下 → 上 → 左 → 内** 的可用空间顺序选（判据是 visibleArea 四条边各留 gap；下/上要求 >=116+gap，116 是气泡高度假设常量）；选定后 AlignX/AlignY（start/center/end），箭头用 Polygon + RotateTransform（up:-90, down:90, left:180）；最后把气泡 clamp 进 workArea（12px 边距、底部再留 116）。

**HostGuideOverlayPresenter.Show**（HostGuideOverlayPresenter.cs:38-77）：与上面几乎同构，但 ① 坐标系是 ScreenPixelsToHost（用 HwndSource.CompositionTarget.TransformFromDevice 把屏幕设备像素转 DIP，再减去 _host.PointToScreen(0,0) 的结果，:193-218）；② auto 只有 右/下/上/左 四向（无 inside）；③ 只在 BoundsScreen 有效时画高亮，否则整层退到右上角。

### 4.7 条件求值（EvaluateCondition，:303-475）

~~~
EvaluateCondition: windows==null 时用 _context.VisibleWindows()（走 120ms 快照缓存）
  core = EvaluateConditionCore
  if condition.Not: 返回 { Matched = !core.Matched, Specificity 继承, Evidence 前置 “condition:not” }
                    // 注意：Hard/App/Doc/State 计数全部丢弃 -> 取反后基本不可能 IsConclusive

EvaluateConditionCore switch(kind):
  all           -> EvaluateAll：Matched=All，Contradicted=Any，各类计数求和，Specificity 求和；
                         RecognitionPlan 取“Specificity 最大”的那个子条件的 plan
  any           -> EvaluateAny：取第一个 (Matched && !Contradicted) 且按 IsConclusive desc, Specificity desc 排序的子项
  locator_found -> EvaluateLocatorCondition（唯一会产生 RecognitionPlan 的叶子条件）
  value_equals / value_contains -> EvaluateValueCondition
  process_running -> ManagedProcess.Process.HasExited 取反
  window_visible  -> 该进程有可见窗口
  host_visible    -> HostHotspots[key] 能算出矩形
  always          -> 恒真，Specificity=1（故意低，避免挤掉真证据）
  default         -> _legacyConditionEvaluator?.Invoke(condition) ?? false

EvaluateLocatorCondition (:411-444):
  plan   = ResolveLocator(locator, target, new GuideOverlaySpec(), windows, ...)
  flag   = scope==host ? plan.BoundsScreen.HasValue : IsRecognizedExternalPlan(plan)   // 必须是“锚定”的外部计划
  contradicted = evidence.Any(IsContradictoryEvidence)     // reject-title:/reject-content: 前缀
  App   = flag ? (HasStrongWindowEvidence(evidence) ? 1 : 0)
  Doc   = flag ? 命中的 DocumentContains 条数
  State = flag ? 内容证据条数（+state、+role、+nameEquals 各 1）
  docOk = DocumentContains 为空 || Doc == DocumentContains.Count
  Hard  = (flag && docOk && App>0 && State>0 && HasHardTriggerLocator(locator)) ? CountHardLocatorEvidence(locator) : 0
  Specificity = Hard*100 + sum(min(len,32)) + (requireAll?40) + (nameEquals?50) + (automationId?40) + min(score,100)
~~~

HasHardTriggerLocator（:548-555）要求同时有“应用身份”（process/adapter/过程名/exe/窗口类之一）与“状态证据”（长度 >=3 的 content/name/state/role/automationId/title 之一）——这就是“能定位到窗口”与“认出了状态”的分界线。

### 4.8 分支与偏离

- 分支：_guide.ResolveBranchIndex(Current, step, trigger)（GuideRuntime.cs:383-398），按 branches 顺序，trigger 相同或分支 trigger==“any”，且 when 求值为真；next 为空 → 不分叉；next 找不到同名 step → 不分叉。调用点：ActionButton_OnClick（“action”）/ NextStepButton_OnClick（“next”），MainWindow.cs:2042-2075。
- 偏离：AddDeviationMatches（:235-272）按 priority 降序遍历；when 命中就解析其 guide 并合成一个**伪 StageStep**（Id=“deviation:xxx”，Action=none，StepIndex=-1），Score 额外 +clamp(priority,0,120)。展示时用 deviation 的文案替换正文（ShowExternalScene，:2859-2878）。

---

## 5. 关键技术点与“为什么这么做”

### 5.1 窗口枚举：EnumWindows + DWM 扩展边框 + 客户区双矩形

WindowTracker.EnumerateVisibleWindows（:194-263）：

- EnumWindows 全屏扫，过滤：IsWindowVisible、标题非空白、GetWindowRect 结果宽高**都必须 >=50**。
- 矩形优先取 DwmGetWindowAttribute(hwnd, 9 / DWMWA_EXTENDED_FRAME_BOUNDS)，失败才退回 GetWindowRect（:375-406）。
  **为什么**：Win10+ 的 GetWindowRect 含不可见的 DWM 阴影/缩放边框，比视觉边界外扩 7~8 px（Win11 圆角窗口更明显）。高亮框若用 GetWindowRect 就会“框比窗口大一圈”，这是此类引导程序的头号视觉 bug 来源。
- 同时取 GetClientRect + ClientToScreen 得到客户区屏幕矩形（:408-435）。**为什么**：下文 5.3 的归一化要以“客户区”为参考系才对得上 Qt/Java 这类自绘窗口。
- 记录 OwnerHwnd（GetWindow(GW_OWNER=4)）、RootOwnerHwnd（GetAncestor(GA_ROOTOWNER=3)）、LastActivePopupHwnd（GetLastActivePopup）、IsForeground。
  **为什么**：模态对话框是独立顶层窗，靠 RootOwner/LastActivePopup 才能知道“它属于 IDA”；打分里“前台 +30 / LastActivePopup +12 / 模态 +140”全靠这几个字段。

### 5.2 进程树：NtQueryInformationProcess 而非 .NET 父进程

CollectProcessTreeIds（:282-333）用 NtQueryInformationProcess(ProcessBasicInformation=0) 取 InheritedFromUniqueProcessId，构建父子表后 BFS。

**为什么**：.NET 没有父 PID API；WMI（Win32_Process.ParentProcessId）慢一个数量级且依赖 WMI 服务。Ghidra（javaw 启动器 + java 子进程）和某些工具的 launcher 结构需要整棵树。找窗口时按“树里所有 PID”匹配，避免只认 launcher 的单 PID 而漏窗。

### 5.3 坐标体系：三套并存 + 归一化回退

| 名称 | 产生处 | 含义 |
| --- | --- | --- |
| screen | ResolveLocator scope=screen / 所有最终 BoundsScreen | **物理像素**，虚拟屏幕坐标系（多屏可为负） |
| window / client-normalized | LocatedRect.FromWindow | 相对“窗口参考矩形”的 0..1 比例；参考矩形默认取 ClientBounds（>1px 时），否则 Bounds（LocatedRect.cs:50-68） |
| host | Layer=Host | 主窗口 DIP |

NormalizedRect.FromScreen 与 ToScreen 严格互逆（ToScreen 里 Max(1.0, ...) 保证不退化）。
**为什么需要归一化**：内容作者在 1080p 下拖出的矩形，要在 4K/150% 缩放下仍落在同一控件上；更重要的是**服务“适配器固定百分比区域”**——6 个 Adapter 的 FallbackRegion 全是 Region(w, x%, y%, w%, h%)（GuideAppAdapterBase.cs:166-170），例如 x64dbg 命令栏是 (0.006, 0.916, 0.987, 0.045)：窗口一缩放，区域跟着缩放，永远贴在底边。
**注意**：BoundsInWindow 目前**只写不读**（全仓 9 处引用均为赋值，见 6.3）。

### 5.4 变更感知：SetWinEventHook 四个区间 + GetAncestor 归并

WindowChangeMonitor（:42-78）注册：

~~~
SetWinEventHook(EVENT_OBJECT_SHOW=0x8002 .. EVENT_OBJECT_FOCUS=0x8005, WINEVENT_OUTOFCONTEXT|WINEVENT_SKIPOWNPROCESS)
SetWinEventHook(EVENT_OBJECT_STATECHANGE=0x800A)
SetWinEventHook(EVENT_OBJECT_NAMECHANGE=0x800C)
SetWinEventHook(EVENT_OBJECT_VALUECHANGE=0x800E)
回调：GetAncestor(hwnd, GA_ROOT=2) 归到顶层窗口，再通知
~~~

回调链（GuideTrackingCoordinator.cs:132-136）：_windowSnapshots.Invalidate(); controlLocator.InvalidateWindow(hwnd)。
**为什么用 out-of-context 而非 in-context**：in-context 要求回调在目标进程的 UI 线程执行，必须注入 DLL——教学程序不能注入别人的 IDA。OUTOFCONTEXT 由系统回调到本进程的消息循环，零注入。
**为什么还要 GetAncestor**：Qt/Java 会把大量子窗口的 state/name 事件冒上来，不归并到顶层就会把“某个按钮改名”当成“窗口变了”，导致 UIA 快照被无意义地反复重建（每次重建百毫秒级）。

### 5.5 UI Automation：快照 + BFS + 信号量 + 超时 + 退避

ControlLocator 是工程含量最高的部分，核心是“**一次遍历，多次复用**”：

1. **快照**（GetUiaSnapshot, :479-506 → BuildUiaSnapshot, :508-551）
   - AutomationElement.FromHandle(hwnd) 取根，TreeWalker.ControlViewWalker 做 BFS（ControlView 而非 RawView：RawView 会把 Qt/Java 的大量内部 panel 全吐出来，节点数爆炸且没意义）。
   - 每节点抓 8 个字段存成 UiaNodeSnapshot：Depth/Name/AutomationId/ClassName/ControlType(ProgrammaticName)/IsOffscreen/Bounds/**Text 片段**。
   - 双上限：节点 3000、时间 1200ms；超时置 TimedOut=true 但仍返回**已抓到的部分**。
   - 缓存 450ms（SnapshotCacheTtlMilliseconds），按 hwnd 键；每窗口独立 object 门（_snapshotGates）+ 修订号（_snapshotRevisions）：抓取期间若 InvalidateWindow 递增了修订号，本次结果**直接丢弃**（:499-502），避免旧树覆盖新树。
2. **全局并发闸**（:247）：UiaGate = new SemaphoreSlim(clamp(ProcessorCount/2, 2, 6))。**为什么**：UIA 是跨进程 COM 调用，对端（尤其 IDA 的 Qt、Java 的 AWT）UI 线程一卡，我方 Wait 就挂；并发过高会把自己的线程池和对方 UI 线程一起拖死。RunUiaQuery（:1410-1445）用 UiaGate.Wait(0) 非阻塞抢闸——抢不到直接判超时返回，**绝不排队**（排队会让 200ms 节拍累积成雪崩）。
3. **退避**（:1920-1947）：某窗口/某查询超时后写 5 s 退避键；退避期内 UIA 直接跳过，只走 Win32 枚举（且 timeout 压到 400ms）。**为什么**：某些工具窗口 UIA 永久不可用（自绘、无 WM_GETOBJECT），不退避就会每 200ms 付一次 1200ms 超时代价，整个引导卡成 PPT。
4. **文本多源抽取**（ReadUiaTextPieces / ReadSnapshotTextPieces，:553-564 / 1702-1717），按需逐级升级：
   - Current.Name（永远取）
   - ValuePattern.Value（输入框内容）
   - LegacyIAccessiblePattern：AutomationPattern.LookupById(10018) + 反射读 Current.Name/Value/Description（:1763-1782）——**为什么**：老式 Win32 控件（IDA 部分自绘控件、DIE 的 Qt 控件）只暴露 MSAA 文本，UIA Name 为空，Legacy pattern 是唯一通道。
   - TextPattern.DocumentRange.GetText(800~1200)（仅当 ControlType ∈ {Document,Edit,Text} 或 ClassName/AutomationId 含 text/edit/code，ShouldProbeTextPattern :1734-1747）——**为什么**：这正是“反编译伪代码里出现了 flag 字符串”这类判定的来源（dnSpy 的 code_view、IDA 的 Pseudocode）。限 800/1200 字符防大文档拖死。
5. **Win32 回退**（EnumChildWindows + GetWindowText + GetClassName，:909-1084 / 961-1034）：UIA 拿不到或为空时，按子窗口类名映射成 controlType（Win32ControlTypeName, :2001-2057：Button→button、Edit/RichEdit→edit、Static→text、SysTreeView→treeitem…），再走同一套评分。
6. **快路径**（FindFastTextEvidence / FindFastTextControlMatch, :1162-1229）：先用 OrCondition(PropertyCondition(NameProperty, hint), PropertyCondition(ValuePattern.ValueProperty, hint)) 让 UIA **引擎侧**做精确匹配，一次 FindAll(TreeScope.Descendants)。**为什么**：把“遍历 3000 节点”降级成“引擎内部一次比较”，对 IDA 这种几万节点窗口是数量级差别；只在 hint <=128 字符且 <=8 条时才用（防条件爆炸）。

### 5.6 评分体系（全部是“权重 + min(长度,18)”的线性模型）

**窗口打分**（GuideSceneMatcher.ScoreWindow, :1399-1472）

| 项 | 分值 |
| --- | --- |
| 前置硬门槛 | rejectTitleContains 命中 → 直接 0；五类 title/process/exeName/exePath/className 若声明了 hint 但一个都没命中 → 0（window:required-identity-mismatch） |
| 适配器签名（GuideAppSignature.Score） | process 46 / exe 46 / title 38 / class 18，每项 +min(len,18)；若三类身份分合计 >0 再 +8（adapter:id） |
| title / process / exe-name / exe-path / window-class | 32 / 34 / 34 / 24 / 24，每项 +min(len,18) |
| 前台窗口 | +30（foreground-window） |
| LastActivePopup == 自身 | +12 |
| state 含 modal 且窗口像模态（类名 #32770 或标题含 “Please confirm”/“Load a new file”/“DWARF info found”） | **+140** |
| 托管进程 PID 命中 | +52（managed-pid:key） |
| 托管 TitleHints / ProcessHints 命中 | +30 / +34 |
| key=tool:xxx 且适配器还没算过 | 再叠加一次适配器分（防重复计分，:1575-1587） |
| key=target:name 且 ProcessName/ExeName 精确等于 | +48 / +48 |
| titlePidFromProcess：标题里出现该进程 PID（数字两侧不是数字） | **+130** |
| key=target:x 且进程精确匹配且标题含 x | +44 |

**歧义判定**（TrySelectReliableWindow, :722-749）：候选 >=2 时，若“只有 1 个候选带精确证据（managed-pid/title-pid/target-process-exact/target-exe-exact）”或“第一名领先 >=60 且带强证据”才采信；否则产出 ambiguous-window-candidates:N 证据并**放弃**。之后 IsReliableExternalRecognition / IsPreciseRecognition 都会因为这个前缀 token 直接否决（:659-675）。

**控件打分**（ControlLocator）

| 项 | 分值 |
| --- | --- |
| nameEquals | 80（name-equals） |
| automationId | 70 |
| className | 30 |
| identity-only（AllowTypeOnlyMatch 且给了 automationId/className） | 20 |
| 每条命中 hint | 10 + min(len,24)（来源前缀 name:/value:/text:/legacy-*） |
| requireAllNameContains 且全中 | +20 |
| controlType 命中 | +18 |
| 精确控件类型（Button/MenuItem/TabItem/ListItem/TreeItem/Edit/Document） | +4 |
| **内容目标**（ScoreContentTarget） | 42 + Σ(18+min(len,24))，精确类型再 +4 |

门槛：通用控件 MinScore=14（GuideSpecHelpers）/ 8（默认）；**强匹配** IsStrongMatch（:1578-1590）= score>=64 且（name: 或 name-equals）且（automation-id|class-name|control-type），或 score>=64 且 identity-only 且（automation-id|class-name）。命中强匹配就**提前停止遍历**（EnumChildWindows 回调返回 false）。
**可用矩形**（IsUsableContentRect, :1380-1408）：宽高 >3 且 <3500x2200（剔除整窗/桌面级节点）；给了 withinBounds 时，要求中心点在内，或**交集面积占比 >=25%**——这就是“内容在锚点控件内部”的判据。

### 5.7 模板化知识：Adapter（GuideAppAdapterBase + 6 个具体实现）

IGuideAppAdapter 只有 3 个成员（IGuideAppAdapter.cs）：Signature / CanHandle(adapterId, window) / Locate(window, locator, ControlLocator)。

每个 Adapter = 一个 **Signature**（进程/可执行/标题/类名线索）+ 一个 **States 字典**（界面状态 → AppStateLocator）+ 一个 **Roles 字典**（语义角色 → AppRoleLocator）。

以 IDA 为例（IdaGuideAdapter.cs:17-102）共 13 个状态：
quick_start / open_file_modal / load_file / database_exists_modal / database_repair_modal / problem_modal / debug_info_modal / analysis_modal / main / strings / xrefs / disassembly / pseudocode。
每个状态用 TitleHints + ContentHints + RejectTitleHints + RejectContentHints + MinContentMatches + MinScore 做“与/非”布尔判定（GuideAppAdapterBase.Locate, :42-76）。

- **为什么要 RejectContentHints**：IDA 的“打开文件”对话框和“加载文件(Load a new file)”对话框长得都像文件选择器，标题都可能含 Open。靠 rejectContent（“DWARF debug information”、“Import file names/line numbers”）才能区分 DWARF 询问框与文件框。
- **为什么要 MinContentMatches / MinScore**：单个词“确定”在几乎所有对话框里都有，必须要求 >=2 个同时命中且分数过线，否则 IDA 的任意弹窗都会被当成“确认框”。
- **为什么每个 Role 都带 FallbackRegion + FallbackFirst**：UIA 在 Qt 上经常拿不到可靠 Name（Qt 的 accessible name 往往为空或重复），此时直接按**窗口百分比**画框（例如 IDA 的 pseudocode 区域 (0.36,0.14,0.60,0.76)）。这是“功能性正确优于精确”的取舍：框稍偏也比不框好。代价是这套比例只在默认布局下成立。
- x64dbg 的 AutomationId MainWindow.cmdBar（X64DbgGuideAdapterStable.cs:88）说明作者抓过真实 AutomationId；Ghidra 走 SunAwtFrame 类名（Java AWT 的固定类名）——**Java 程序的顶层窗类名就是 SunAwtFrame/SunAwtDialog**，这是 Ghidra 能被认出的唯一可靠线索。

### 5.8 点击穿透的 HUD 窗口（CoachOverlayWindow）

构造与样式（:79-110）：

~~~
InitializeComponent();   // BAML 已声明：WindowStyle=None, AllowsTransparency=True, Topmost=True,
                         //   ShowInTaskbar=False, ShowActivated=False, ResizeMode=NoResize
ShowActivated = false; Focusable = false; IsHitTestVisible = false;
CoverVirtualScreen();
OnSourceInitialized -> ApplyClickThrough():
    GetWindowLong(hwnd, GWL_EXSTYLE=-20)
    SetWindowLong(hwnd, -20, old | WS_EX_TRANSPARENT(0x20) | WS_EX_TOOLWINDOW(0x80)
                                    | WS_EX_NOACTIVATE(0x08000000) | WS_EX_LAYERED(0x00080000))
~~~

BAML 证据（coachoverlaywindow.baml 字符串表，字节偏移）：[716] WindowStyle / [725] None、[753] AllowsTransparency / [761] True、[790] Topmost / [798] True、[822] ShowInTaskbar / [831] False、[855] ShowActivated / [864] False、[933] ResizeMode / [946] NoResize；视觉元素 [998] RootCanvas、[1061] Highlight（Border，边框 #FF4CC2FF，填充 #334CC2FF，Collapsed）、[1194] Arrow（Polygon，填充 #FF4CC2FF）、[1304] Bubble（Border，背景 #B3121824，边框 #FF4CC2FF/1.5，Padding 14，MaxWidth 420）、[1468] BubbleTitle（#7DD3FF，12，SemiBold，文本 COACH）、[1560] BubbleText（14，Wrap）。

**每一层为什么**：

| 手段 | 作用 | 少了会怎样 |
| --- | --- | --- |
| AllowsTransparency + WindowStyle=None | 无边框、可透明 | 带标题栏/不透明底，毁掉悬浮提示感 |
| Topmost | 压住 IDA 等目标窗口 | 目标窗口一激活就盖住气泡 |
| ShowActivated=false + WS_EX_NOACTIVATE | 显示时不抢焦点 | 每次刷新都把焦点从 IDA 输入框抢走，用户没法打字 |
| IsHitTestVisible=false + WS_EX_TRANSPARENT | 鼠标事件穿透到下层 | 全屏透明窗挡住整个桌面，程序直接不可用（最致命） |
| WS_EX_TOOLWINDOW | 不出现在 Alt-Tab / 任务栏 | 任务栏多一个空白窗，Alt-Tab 列表被污染 |
| CoverVirtualScreen（SystemParameters.VirtualScreen*） | 一窗覆盖所有显示器 | 目标在副屏时气泡画在屏外 |
| WS_EX_LAYERED | 显式声明分层窗口 | 【推断】WPF 在 AllowsTransparency 时本会自动设，这里冗余设置是防御性写法（兼容外部改过 exstyle 的情况） |

另外 ShowGuide 里**每次都重新 CoverVirtualScreen + ApplyClickThrough**（:128-129），因为显示器热插拔/分辨率变化后窗口矩形与 exstyle 都可能被系统重置。

MonitorFromRect(MONITOR_DEFAULTTONEAREST=2) + GetMonitorInfo 取 rcWork（:349-375）：气泡被限制在**目标所在显示器的工作区**内，而不是整个虚拟屏幕——否则多屏下气泡会跑到另一块屏上。

### 5.9 DPI：TransformFromDevice 而不是手算 96/dpi

两处都把**屏幕物理像素**转成 WPF DIP：

~~~
// CoachOverlayWindow.ScreenPixelsToOverlay  (:326-347)
m = (PresentationSource.FromVisual(this) as HwndSource)?.CompositionTarget?.TransformFromDevice ?? Identity
pointTL = m.Transform(目标左上);  pointBR = m.Transform(目标右下)
rect = (TL - this.Left, TL - this.Top, BR-TL 宽, BR-TL 高)

// HostGuideOverlayPresenter.ScreenPixelsToHost  (:193-218)
同一套 m，但原点用 this.PointToScreen(0,0) 再经过 m 变换
~~~

**为什么**：UIA 的 BoundingRectangle 返回**物理像素**，WPF 的 Canvas 用 DIP。手写 dpi/96 在“每显示器不同 DPI”（PerMonitorV2）下会错；TransformFromDevice 取的是当前窗口实际生效的变换矩阵，天然正确。_host.PointToScreen() 的结果还要再乘一次 m，因为 PointToScreen 也返回物理像素。

### 5.10 并发与生命周期

| 位置 | 手段 | 目的 |
| --- | --- | --- |
| GuideTrackingCoordinator:159 / :188 | Interlocked.CompareExchange(ref _xInFlight, 1, 0) | 单飞：上一轮没回来就跳过本轮，绝不堆积 |
| 同上 :181 / :214 | Volatile.Write(ref _xInFlight, 0)（放 finally） | 释放 |
| :192 | _lastRecognitionTimestamp = Stopwatch.GetTimestamp() | 240ms 节流 |
| :225-237 | lock(_processGate) + 二次检查时间戳 | 进程刷新与 UI 线程的进程操作互斥（MainWindow 用同一把 _processAccessLock） |
| :170 / :198 | _lifetime.Token（CancellationTokenSource） | Dispose 统一取消；OperationCanceledException 单独 catch 且**不当作失败**（不重置稳定器） |
| MainWindow:2747 / :2771 | Dispatcher.BeginInvoke(..., DispatcherPriority=4) | 后台结果回 UI 线程（4=Background，不抢输入/渲染） |
| 各处 | revision 比对（result.Revision == Volatile.Read(ref _guideRevision)） | 丢弃过时扫描结果——用户已点下一步，旧结果不许覆盖新 UI |
| ControlLocator:247 | 静态 SemaphoreSlim UiaGate | 跨进程 COM 并发上限 |
| ControlLocator:486 | lock(_snapshotGates.GetOrAdd(hwnd, ...)) | 单窗口快照串行 |
| ControlLocator:287-298, 1868-1918 | 全局 _cacheLock + 字典缓存 | 350ms 结果缓存（键含 hwnd/选项哈希/矩形） |

### 5.11 缓存键设计

ControlLocator 的四类缓存与键格式（:1949-1974）：

~~~
control|<hwnd>|ControlOptionsKey(...)          // 全部选项字段 join(“|”)
text-evidence|<hwnd>|ListKey(hints)|maxNodes|timeoutMs|stopAfterFirstMatch
text-control|<hwnd>|ListKey(hints)|maxNodes|timeoutMs|requireAll|backend|RectKey(withinBounds)
snapshot:<hwnd>                                // 独立字典，450ms
ListKey 用 \u001f 作分隔符（防 hint 含 | 造成键碰撞）；RectKey 用 0.### 定点格式
InvalidateWindow 用 “|<hwnd64>|” 作为子串 token 批量删除前三个字典（:282-308）
~~~

### 5.12 单文件/内嵌资源与内容目录

PathService（:40-83）：先 Path.GetFullPath(AppContext.BaseDirectory + “MoeCTF.RE.Tutorial”)，交给 BundledContentInstaller.Install(...)（把内嵌 MoeCTF.RE.Tutorial.Host.BundledContent.zip 解出来），或该目录已“像内置内容根”（同时存在 stages/index.json 与 assets/i18n/zh-CN.json）就直接用；否则从 BaseDirectory 向上找 8 层；再不行退回 ../../../../。
**为什么这么绕**：同一份 EXE 要同时支持“用户双击单文件运行（解包到 exe 旁）”和“开发者从 bin/Debug 运行（沿目录树找到仓库根）”两种场景。
Guide 子系统只消费 StagesDir / AssetsDir / I18nDir，不直接触碰 zip。

### 5.13 主窗口外观（与 Guide 的耦合点）

NativeWindowChromeService（Attach(this, 12.0)，MainWindow.cs:327）只做两件事，但都是“引导框能对齐”的前提：
1. DWM 圆角（DWMWA_WINDOW_CORNER_PREFERENCE=33，值 2=Round），失败则用 CreateRoundRectRgn + SetWindowRgn 做像素级圆角（半径 = ceil(radiusDip * dpi / 96)），最大化/最小化时改为 DoNotRound 并清区域。
2. Hook WM_GETMINMAXINFO(36)，把最大化尺寸限制到 rcWork（:322-344）。
**为什么**：Win11 上最大化窗口默认会溢出工作区约 8px（边框阴影），高亮热点用 PointToScreen 算出的绝对坐标会把框画到屏幕外沿。

### 5.14 主窗口内引导层（HostGuideOverlayPresenter）

与 Coach 窗共用同一套几何算法但坐标系不同（DIP + 相对窗口），并额外做了 **Pulse** 呼吸动画（0.62->1.0，720ms）与 _bubble.MaxWidth = Max(260, Min(overlay.MaxWidth ?? 420, layer.Width-24)) 的自适应宽度。它**没有** SetAnimated 补间（Coach 窗有 220ms CubicEase 补间），因为主窗口内目标一般不动。

---

## 6. 复刻要点（.NET 8 WPF 重写）

### 6.1 必须保留的契约（改了内容就不兼容）

1. **JSON 字段名与默认值**：全部小驼峰 [JsonPropertyName] 原样；默认值靠属性初始化器（scope=host、strategy=auto、padding=6、gap=14、placement=auto、alignment=center、MinScore 系列不可外泄到 JSON）。反序列化只用 PropertyNameCaseInsensitive=true。
2. **scope 归一化映射**：child/external/target → process，desktop → host（GuideSpecHelpers.NormalizeScope）。内容作者会写 external。
3. **渲染计划的四态门槛**：Layer ∈ {None,Host,Screen}；Attachment 的 7 个值与**顺序**（顺序被位运算依赖！）；只有 {Window, VerifiedRegion, Control, Content} 可显示；只有 {Control, Content} 算 precise。
4. **证据字符串的词法**：managed-pid: / managed-title: / managed-process: / title-pid: / target-process-exact: / target-exe-exact: / adapter: / process: / exe: / exe-name: / title: / window-class: / class: / exe-path: / modal-window / foreground-window / last-active-popup / reject-title: / reject-content: / ambiguous-window-candidates: / content: / content-target: / :state: / :content: / deviation: / step-trigger: / scope:none / host: / screen:rect / locator:window / locator:rect / locator:control-relative-rect / control:window-fallback / control:adapter-fallback-region / content-anchor-relaxed / content:distributed-evidence / condition:*。**大量判定是对这些前缀做 StartsWith/IndexOf**（OrdinalIgnoreCase）。重写时若改词法，语义门槛会静默失效。
5. **托管进程键约定**：tool:<toolId> / target:<文件名>，且 xxx.exe 会自动补成 target:xxx.exe 再查（GuideSceneMatcher.cs:524-528、1500-1503）。
6. **host 热点键**：coach / next / flag / action / taskboard / submit / desktop（MainWindow.cs:410-416）。
7. **进度同步只前进**：同阶段 step 回退被拒；跨阶段要过 MinimumActiveStageIndexFromClearedProgress。
8. **阈值常数**：识别阈值 140 / 置信度阈值 65 / 歧义领先 60 或 specificity 差 36 / 精确歧义 25 分带 —— 这些决定“会不会乱跳步”，要么原样保留，要么整体重新标定。

### 6.2 可以简化（不影响内容兼容）

- **Adapter 的 Role/State 知识库**可砍到只剩当前课程真正用到的（chall1 只用 die + ida；die.json 只用 role=analysis_summary 与 strategy=window）。13 个 IDA 状态是给后续课程准备的。
- **Win32 回退后端**（EnumChildWindows 那套）在只用 UIA 够用时可以删；但保留它能让 DIE/Qt 自绘控件有兜底。
- **RecognitionStabilizer 的 2 次**可降为 1 次 + 1 次重试——代价是偶发误跳步。
- **BoundsInWindow / CoordinateSpace** 这条归一化链路目前无消费方，可只保留字段不实现计算。
- **GuideRecognitionMatch.Confidence** 只用于“过不过 65”，可以直接内联。
- FindChildControlMatchCore（ControlLocator.cs:396-404）是**死代码**（全仓仅此一处定义，无调用），不必复刻。

### 6.3 坑（按踩到的痛苦程度排序）

1. **UI Automation 权限与跨进程限制**
   - UIA 客户端在**低完整性级别进程**里看不到高完整性级别进程的控件（UIPI）。教学程序若普通用户态、目标 IDA 也普通用户态则没问题；但目标以管理员运行时本程序必须同样提权，否则整棵树是空的（且**不报错**，只是返回 0 个节点）。
   - 32/64 位不匹配不影响 UIA（跨位宽可用），但 NtQueryInformationProcess 的 struct 布局要按目标位宽小心处理（这里用 nint，宿主与目标同为 x64 才稳）。
   - 会话 0 隔离/服务进程完全无窗口可枚举。
2. **UIA 会挂死你的线程**：对端 UI 线程忙（IDA 正在分析、Ghidra 在 GC）时，读 node.Current 可能阻塞数百毫秒到数秒。必须像本项目一样：非阻塞抢信号量 + Task.Run + Task.Wait(timeout)，**超时后放弃该次结果**（那个 Task 会漏在后台，是本设计接受的代价）。绝不要在 UI 线程同步调 UIA。
3. **DPI**：UIA 给物理像素，WPF 用 DIP。必须走 CompositionTarget.TransformFromDevice；**不能用 SystemParameters.PrimaryScreenWidth 之类的单屏假设**（多屏且缩放不同时会整体偏）。还要在 WM_DPICHANGED 或 SystemParameters.StaticPropertyChanged 时重算 HUD 窗口尺寸，否则拖到另一块不同缩放的屏后高亮整体偏移（原实现只在下一次 ShowGuide 时重算 CoverVirtualScreen，属于“下次刷新才修”）。
4. **跨进程窗口句柄（HWND）会失效**：目标退出后 HWND 可能被复用。每次显示前必须 TargetWindowStillVisible 按 **(Hwnd, Pid) 双键**复核（MainWindow.cs:3069-3084），只比 Hwnd 会在极端情况下高亮到别人的窗口上。
5. **点击穿透 + 置顶 + 不抢焦点是“三件套”**：AllowsTransparency=true 之外必须同时设 ShowActivated=false、IsHitTestVisible=false 和 WS_EX_TRANSPARENT|WS_EX_NOACTIVATE，缺一个就会出现“鼠标点不到下层”或“焦点被抢走”。WS_EX_LAYERED 对 WPF 的 AllowsTransparency 窗口是自动的，但**不要**用 SetLayeredWindowAttributes 之类别的手法和 WPF 合成器抢（会打架）。
6. **SetForegroundWindow 有前台锁限制**：ActivateExternalGuideTargetOnce 每步只激活一次，且必须接受“系统可能拒绝激活（返回 false）”。反复调用会导致焦点抖动 + 用户输入丢失。生产实现若要强制激活需 AttachThreadInput 或 AllowSetForegroundWindow，本项目故意不做。
7. **单文件资源读取**：内嵌 zip 必须先落盘成目录（BundledContentInstaller），不能每次从 Assembly.GetManifestResourceStream 现读——i18n/课程 JSON 是**频繁小文件读取**，且内容更新（ContentUpdater）需要可写目录。坑在于“安装到 exe 同目录”在 Program Files 下会因权限失败，需要回退到 LocalAppData。
8. **模态对话框的归属**：只按 PID 找窗口会把 IDA 的模态框和主窗当成两个“候选”，必须用 LastActivePopupHwnd / RootOwnerHwnd 加权重（模态 +140）才能选对。
9. **固定百分比 FallbackRegion 的脆弱性**：面板一拖、主题一换、字体一改，百分比区域就偏。这是“演示要过”的临时手段【推断】，正式产品应尽量用 AutomationId/ClassName。
10. **缓存键必须含“导致结果变化的所有输入”**：RectKey 用 0.### 定点格式，若用默认 double.ToString 会出现 1.2345678 与 1.2345679 的键抖动，缓存命中率崩掉。
11. **EnumWindows 的“宽高 >=50 且标题非空”过滤会漏掉真实窗口**（无标题浮层、细长工具条），但放宽又会让候选爆炸并引入歧义。这个 50px 阈值是经验值。
12. **中英双语 hint**：所有 hint 都同时写中英（“内存布局”/“Memory Map”、“确定”/“OK”），因为目标工具 UI 语言跟系统语言走。重写时不要“本地化”掉任何一侧。

---

## 7. 待确认问题

1. **ControlLocator.cs:396-404 FindChildControlMatchCore 是死代码吗？** 全仓 grep 仅此一处定义、无调用方；同期 FindTextEvidenceCore(:754) / FindTextControlMatchCore(:841) 也无调用方。需确认是否为重构后残留（若是，删掉可减少“UIA 直连”与“快照”两条并行实现带来的混淆）。
2. **GuideSpecHelpers.ResolveExplicitRect(:80-143) 与 schema 的 space 枚举不一致。** 代码接受 outer-window/window-frame/dwm-frame（并与 window/target/anchor/client/host/control 一并走“相对 + 归一化当比例”分支），而 guide-stage.schema.json:145 的 space 枚举只有 screen/window/target/host/control/anchor，且 GuideSceneMatcher.WindowReferenceBounds(:1686-1707) 又只对 outer-window/window-frame/dwm-frame 取整窗 Bounds。需确认合同以 schema 为准还是以代码为准（内容作者若写 client，代码走相对分支但 schema 会拒绝）。
3. **归一化坐标目前没有消费者。** BoundsInWindow 全仓只有赋值（GuideRenderPlan.cs:14 / GuideSceneMatcher.cs:883,1073,1124,1176,1195 / MainWindow.cs:2437），渲染端只用 BoundsScreen。需确认这是为“目标窗口移动/缩放后按比例重投影”预留的未完成能力，还是遗留字段。
4. **strategy=“rect” 的内容是否永远不显示？** scope=screen 的显式矩形得到 Attachment=Region(:806)，而 Region 被 GuideRenderPlan.IsExternalDisplayable 排除；scope=process 的显式矩形因 HasVerifiedRegionProof 通常为假也落到 Region(:886)。需确认“只给坐标不给文本证据”是否被有意设计为不可见（宁可不指），还是应该把 Region 纳入可显示集。
5. **HUD 窗口对显示器拓扑/DPI 变化的响应时机。** CoachOverlayWindow 只在 ShowGuide 被调用时 CoverVirtualScreen + ApplyClickThrough，没有订阅 SystemEvents.DisplaySettingsChanged / WM_DPICHANGED；若用户在两拍刷新之间拔掉副屏，窗口矩形会短暂错位（下一次刷新自愈）。需确认是否要求即时应答。
