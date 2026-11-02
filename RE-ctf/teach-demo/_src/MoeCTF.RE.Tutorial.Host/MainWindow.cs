using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using MoeCTF.RE.Tutorial.Host.Coach;
using MoeCTF.RE.Tutorial.Host.Controls;
using MoeCTF.RE.Tutorial.Host.Guide;
using MoeCTF.RE.Tutorial.Host.I18n;
using MoeCTF.RE.Tutorial.Host.Services;
using MoeCTF.RE.Tutorial.Host.Stages;

namespace MoeCTF.RE.Tutorial.Host;

public class MainWindow : Window, IComponentConnector
{
	private sealed record RequirementButtonTag(CourseRequirementGroup Group, CourseRequirement Requirement);

	private sealed record FlagSubmission(string Value, bool AutoTrimmed);

	private readonly PathService _paths = new PathService();

	private readonly I18nService _i18n = new I18nService();

	private readonly ToolAdapter _tools;

	private readonly ProcessManager _processes = new ProcessManager();

	private readonly TargetProcessObserver _targetObserver = new TargetProcessObserver();

	private readonly WindowTracker _tracker = new WindowTracker();

	private readonly WindowSnapshotCache _windowSnapshots;

	private readonly ControlLocator _locator = new ControlLocator();

	private readonly ProgressStore _progress;

	private readonly CourseCatalog _catalog;

	private readonly ContentUpdater _contentUpdater;

	private readonly HostUpdateInstaller _hostUpdateInstaller;

	private readonly RequirementActivationService _requirements;

	private readonly CoachOverlayWindow _overlay = new CoachOverlayWindow();

	private readonly HostGuideOverlayPresenter _hostGuideOverlay;

	private readonly GuideRuntime _guide;

	private readonly GuideTrackingCoordinator _guideTracking;

	private readonly NativeWindowChromeService _nativeChrome;

	private readonly DispatcherTimer _trackTimer;

	private readonly DispatcherTimer _clockTimer;

	private readonly CancellationTokenSource _updateCancellation = new CancellationTokenSource();

	private readonly object _processAccessLock = new object();

	private readonly Dictionary<string, FrameworkElement> _hostHotspots = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, Button> _desktopIcons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

	private IReadOnlyList<CourseDefinition> _courses = Array.Empty<CourseDefinition>();

	private CourseDefinition? _activeCourse;

	private StageRepository _stages;

	private int _stageIndex;

	private int _stepIndex;

	private bool _guideUnlocked;

	private string? _lastActivatedGuideTarget;

	private string? _pendingExplorerTargetPath;

	private DateTime _pendingExplorerGuideUntilUtc;

	private long _guideRevision;

	private GuideSceneMatch? _latestExternalScene;

	private string? _activeDeviationFallbackId;

	private bool _interactiveGuideEnabled = true;

	private bool _updateCheckInProgress;

	private HostUpdateResult? _pendingHostUpdate;

	internal Border WindowShellBorder;

	internal TextBlock TitleText;

	internal TextBlock SubtitleText;

	internal Button BackToHubButton;

	internal ToggleButton InteractiveModeToggle;

	internal Grid CourseHubView;

	internal TextBlock HubEyebrowText;

	internal TextBlock HubTitleText;

	internal TextBlock HubIntroText;

	internal Button CheckUpdateButton;

	internal Button SettingsButton;

	internal TextBlock UpdateStatusText;

	internal TextBlock CourseListHeaderText;

	internal StackPanel CourseListPanel;

	internal Border ActiveCourseCard;

	internal ContentControl ActiveCourseIconHost;

	internal TextBlock ActiveCourseTitleText;

	internal TextBlock ActiveCourseSubtitleText;

	internal TextBlock ActiveCourseCompatibilityText;

	internal TextBlock ActiveCourseSummaryText;

	internal R2STag ActiveCourseTrackTag;

	internal R2STag ActiveCourseVersionTag;

	internal Border CourseRouteCard;

	internal TextBlock CourseRouteText;

	internal TextBlock CourseStageMetricLabelText;

	internal TextBlock CourseStageCountText;

	internal TextBlock CourseMinutesMetricLabelText;

	internal TextBlock CourseMinutesText;

	internal TextBlock CourseSourceMetricLabelText;

	internal TextBlock CourseSourceText;

	internal Border CourseUpdateCard;

	internal TextBlock CourseUpdateText;

	internal Border RequirementCard;

	internal WrapPanel RequirementGroupsPanel;

	internal TextBlock RequirementStatusText;

	internal Button EnterCourseButton;

	internal Grid CourseDeskView;

	internal Canvas DesktopCanvas;

	internal TextBlock DragHintText;

	internal TextBlock ProgressText;

	internal TextBlock StageTitleText;

	internal TextBlock StageGoalText;

	internal Border StepImageFrame;

	internal Image StepImage;

	internal TextBlock StepMediaEmptyText;

	internal TextBlock StepArticleText;

	internal Border TaskBoard;

	internal TextBlock TaskHeader;

	internal Button ResetButton;

	internal Border FlagSubmissionPanel;

	internal TextBox FlagInput;

	internal Button SubmitButton;

	internal Border ExternalCompletionPanel;

	internal TextBlock ExternalCompletionText;

	internal TextBlock FeedbackText;

	internal TextBlock TrackedWindowsText;

	internal Border CoachCard;

	internal TextBlock CoachHeader;

	internal TextBlock CoachText;

	internal Button ActionButton;

	internal Button NextStepButton;

	internal TextBlock StartBadge;

	internal TextBlock TaskbarStageText;

	internal TextBlock TaskbarTrackText;

	internal Border ProbeStatusBadge;

	internal TextBlock TaskbarClockText;

	internal Canvas InAppGuideLayer;

	internal Border InAppGuideHighlight;

	internal Polygon InAppGuideArrow;

	internal Border InAppGuideBubble;

	internal TextBlock InAppGuideTitle;

	internal TextBlock InAppGuideText;

	internal Grid CourseCompletionOverlay;

	internal TextBlock CourseCompletionTitleText;

	internal TextBlock CourseCompletionBodyText;

	internal TextBlock CourseCompletionContactText;

	internal Button CourseCompletionBackButton;

	internal Grid SettingsModalOverlay;

	internal TextBlock SettingsTitleText;

	internal TextBlock SettingsSubtitleText;

	internal StackPanel ToolSettingsPanel;

	internal TextBlock AboutTextBlock;

	internal Grid CourseEntryNoticeOverlay;

	internal TextBlock CourseEntryNoticeTitleText;

	internal TextBlock CourseEntryNoticeBodyText;

	internal TextBlock CourseEntryNoticeFallbackText;

	internal Button CancelCourseEntryButton;

	internal Button ConfirmCourseEntryButton;

	private bool _contentLoaded;

	private StageDefinition Current => _stages.Stages[_stageIndex];

	private bool HasLoadedStage
	{
		get
		{
			if (_stages != null)
			{
				return _stages.Stages.Count > 0;
			}
			return false;
		}
	}

	public MainWindow()
	{
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_029e: Expected O, but got Unknown
		//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Expected O, but got Unknown
		InitializeComponent();
		base.Closed += delegate
		{
			_updateCancellation.Cancel();
		};
		_windowSnapshots = new WindowSnapshotCache(_tracker);
		base.Loaded += MainWindow_OnLoaded;
		_nativeChrome = NativeWindowChromeService.Attach(this, 12.0);
		_catalog = new CourseCatalog(_paths);
		_contentUpdater = new ContentUpdater(_paths);
		_hostUpdateInstaller = new HostUpdateInstaller();
		_requirements = new RequirementActivationService(_paths);
		_contentUpdater.RecoverInterruptedUpdate();
		_hostGuideOverlay = new HostGuideOverlayPresenter(this, InAppGuideLayer, InAppGuideHighlight, InAppGuideArrow, InAppGuideBubble, InAppGuideTitle, InAppGuideText);
		_progress = new ProgressStore(_paths.ProgressPath);
		_tools = new ToolAdapter(_paths);
		LoadCourses();
		SelectInitialCourse();
		ApplyActiveCourse(loadTexts: true);
		_guide = new GuideRuntime(new GuideRuntimeContext
		{
			HostWindow = this,
			HostHotspots = _hostHotspots,
			Processes = _processes.Processes,
			WindowTracker = _tracker,
			WindowSnapshots = _windowSnapshots,
			ControlLocator = _locator,
			AppLocator = new GuideAppLocator(),
			ValueProvider = GuideValue,
			TextProvider = _i18n.GetRaw
		});
		_guideTracking = new GuideTrackingCoordinator(_guide, _processes, _targetObserver, _tracker, _windowSnapshots, _locator, _tools, _requirements, _paths, _processAccessLock);
		_stageIndex = ResolveSavedStageIndex();
		_trackTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(200.0)
		};
		_trackTimer.Tick += delegate
		{
			RefreshTrackedWindows();
		};
		_trackTimer.Start();
		_clockTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_clockTimer.Tick += delegate
		{
			TaskbarClockText.Text = DateTime.Now.ToString("HH:mm:ss");
		};
		_clockTimer.Start();
		TaskbarClockText.Text = DateTime.Now.ToString("HH:mm:ss");
		base.Closing += delegate
		{
			CleanupOverlayAndChildren();
		};
		base.Closed += delegate
		{
			CleanupOverlayAndChildren();
		};
		ApplyStaticTexts();
		RegisterStaticHotspots();
		RebuildCourseHub();
		LoadStage();
		ShowHub();
	}

	private void FitWindowToWorkArea()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		double val = Math.Max(760.0, ((Rect)(ref workArea)).Width - 24.0);
		double val2 = Math.Max(520.0, ((Rect)(ref workArea)).Height - 24.0);
		base.MinWidth = Math.Min(base.MinWidth, val);
		base.MinHeight = Math.Min(base.MinHeight, val2);
		base.Width = Math.Min(base.Width, val);
		base.Height = Math.Min(base.Height, val2);
		base.Left = Math.Max(((Rect)(ref workArea)).Left + 12.0, Math.Min(base.Left, ((Rect)(ref workArea)).Right - base.Width - 12.0));
		base.Top = Math.Max(((Rect)(ref workArea)).Top + 12.0, Math.Min(base.Top, ((Rect)(ref workArea)).Bottom - base.Height - 12.0));
	}

	private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
	{
		FitWindowToWorkArea();
		await CheckForUpdatesAsync(automatic: true);
	}

	private void RegisterStaticHotspots()
	{
		_hostHotspots["action"] = ActionButton;
		_hostHotspots["next"] = NextStepButton;
		_hostHotspots["flag"] = FlagInput;
		_hostHotspots["submit"] = SubmitButton;
		_hostHotspots["taskboard"] = TaskBoard;
		_hostHotspots["coach"] = CoachCard;
		_hostHotspots["desktop"] = DesktopCanvas;
	}

	private void CleanupOverlayAndChildren()
	{
		try
		{
			_trackTimer.Stop();
		}
		catch
		{
		}
		try
		{
			_clockTimer.Stop();
		}
		catch
		{
		}
		try
		{
			_hostGuideOverlay.Clear();
		}
		catch
		{
		}
		try
		{
			_overlay.ClearGuide();
			_overlay.Hide();
			_overlay.Close();
		}
		catch
		{
		}
		try
		{
			_nativeChrome.Dispose();
		}
		catch
		{
		}
		try
		{
			_guideTracking.Dispose();
		}
		catch
		{
		}
		try
		{
			lock (_processAccessLock)
			{
				_processes.Dispose();
			}
		}
		catch
		{
		}
	}

	private long AdvanceGuideRevision()
	{
		_latestExternalScene = null;
		_lastActivatedGuideTarget = null;
		_hostGuideOverlay.Clear();
		_overlay.ClearGuide();
		_guideTracking.ResetRecognition();
		return Interlocked.Increment(ref _guideRevision);
	}

	private void ApplyStaticTexts()
	{
		base.Title = _i18n.T("app.title");
		TitleText.Text = _i18n.T("app.title");
		SubtitleText.Text = _i18n.T("app.subtitle");
		BackToHubButton.Content = TextOr("hub.back", "返回");
		SettingsButton.Content = TextOr("settings.open", "设置");
		TaskHeader.Text = _i18n.T("desktop.task");
		CoachHeader.Text = _i18n.T("common.coach_title");
		SubmitButton.Content = _i18n.T("common.submit");
		NextStepButton.Content = _i18n.T("common.fallback_next");
		ResetButton.Content = _i18n.T("common.reset_progress");
		DragHintText.Text = _i18n.T("common.drag_hint");
		FlagInput.ToolTip = _i18n.T("common.flag_placeholder");
		StartBadge.Text = _i18n.T("desktop.start_badge");
		HubEyebrowText.Text = TextOr("hub.eyebrow", "MoeCTF 2026 / Reverse");
		HubTitleText.Text = TextOr("hub.title", "MoeCTF逆向工程指北");
		HubIntroText.Text = TextOr("hub.intro", "第一次做 Reverse，不知道工具怎么开、该看哪里？选一门课，跟着提示自己动手，一步步把 flag 找出来。比赛期间还会更新更多课程。");
		CourseListHeaderText.Text = TextOr("hub.course_list", "课程");
		EnterCourseButton.Content = TextOr("hub.enter_course", "开始课程");
		UpdateCheckButtonText();
		CourseRouteText.Text = TextOr("hub.route_text", "先认识工具，再跟着线索读懂程序，最后亲手找到 flag。");
		CourseStageMetricLabelText.Text = TextOr("hub.stage_count", "关卡");
		CourseMinutesMetricLabelText.Text = TextOr("hub.minutes", "预计分钟");
		CourseSourceMetricLabelText.Text = TextOr("hub.source", "来源");
		CourseUpdateText.Text = TextOr("hub.update_text", "比赛期间还会发布新课程。检查更新后就能看到，已经完成的进度不会丢。");
		SettingsTitleText.Text = TextOr("settings.title", "设置");
		SettingsSubtitleText.Text = TextOr("settings.subtitle", "这里会记住你电脑上的工具位置，换课程时不用重新选择。");
		InteractiveModeToggle.Content = TextOr("common.interactive_mode", "交互指引");
		InteractiveModeToggle.ToolTip = TextOr("common.interactive_mode_tooltip", "让提示跟着工具窗口移动");
		CourseEntryNoticeTitleText.Text = TextOr("course_entry_notice.title", "交互式指引仍在持续完善");
		CourseEntryNoticeBodyText.Text = TextOr("course_entry_notice.body", "交互式指引会跟着你的操作定位提示，但偶尔可能认错当前进度。");
		CourseEntryNoticeFallbackText.Text = TextOr("course_entry_notice.fallback", "如果提示和你看到的内容不一致，回到指北点击“我已经完成这一步”，或在标题栏关闭交互式指引。");
		CancelCourseEntryButton.Content = TextOr("course_entry_notice.cancel", "暂不进入");
		ConfirmCourseEntryButton.Content = TextOr("course_entry_notice.confirm", "进入课程");
		CourseCompletionTitleText.Text = TextOr("course_completion.title", "课程完成");
		CourseCompletionBodyText.Text = TextOr("course_completion.body", "你已经完成这门课程。");
		CourseCompletionContactText.Text = CourseContactText();
		CourseCompletionBackButton.Content = TextOr("course_completion.back", "返回课程主页");
		Version version = typeof(MainWindow).Assembly.GetName().Version;
		string text = (((object)version == null) ? "" : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}");
		AboutTextBlock.Text = "MoeCTF逆向工程指北 " + text + "\n制作：@nkanf\n\n界面设计参考 ret2shell/ret2shell，并按 WPF 重新实现。\n字体：JetBrains Mono（OFL）。";
	}

	private void LoadCourses()
	{
		_courses = _catalog.LoadCourses();
	}

	private string TextOr(string key, string fallback)
	{
		if (!string.IsNullOrWhiteSpace(_i18n.GetRaw(key)))
		{
			return _i18n.T(key);
		}
		return fallback;
	}

	private string CourseAuthors()
	{
		CourseDefinition? activeCourse = _activeCourse;
		if (activeCourse == null || activeCourse.Authors.Count <= 0)
		{
			return string.Empty;
		}
		return string.Join(" / ", _activeCourse.Authors);
	}

	private string CourseContactText()
	{
		string text = CourseAuthors();
		if (string.IsNullOrWhiteSpace(text))
		{
			return TextOr("course_completion.contact_unknown", "遇到问题可以联系课程制作者。");
		}
		return TextOr("course_completion.contact", "遇到问题可以联系课程制作者 {authors}。").Replace("{authors}", text, StringComparison.Ordinal);
	}

	private string CourseNoMoreGuideText()
	{
		string text = CourseAuthors();
		if (string.IsNullOrWhiteSpace(text))
		{
			return TextOr("common.no_more_guide_unknown", "当前段落的引导已经结束。遇到问题可以联系课程制作者。");
		}
		return TextOr("common.no_more_guide", "当前段落的引导已经结束。遇到问题可以联系课程制作者 {authors}。").Replace("{authors}", text, StringComparison.Ordinal);
	}

	private void SelectInitialCourse()
	{
		if (_courses.Count == 0)
		{
			_activeCourse = null;
			return;
		}
		_activeCourse = _courses.FirstOrDefault((CourseDefinition c) => string.Equals(c.Id, _progress.Data.ActiveCourseId, StringComparison.OrdinalIgnoreCase)) ?? _courses.First();
		_progress.SetActiveCourse(_activeCourse.Id, _activeCourse.Version, _activeCourse.FlowFingerprint);
	}

	private void ApplyActiveCourse(bool loadTexts)
	{
		if (_activeCourse == null)
		{
			_paths.UseBuiltInContent();
			if (loadTexts)
			{
				_i18n.Load("zh-CN", _paths.I18nDir);
			}
			_stages = new StageRepository(_paths.StagesDir);
			_stageIndex = 0;
		}
		else
		{
			_activeCourse.Apply(_paths);
			if (loadTexts)
			{
				_i18n.Load("zh-CN", _paths.I18nDir, System.IO.Path.Combine(_paths.RepoRoot, "assets", "i18n"));
			}
			_stages = new StageRepository(_paths.StagesDir);
			_stageIndex = ResolveSavedStageIndex();
		}
	}

	private int ResolveSavedStageIndex()
	{
		if (_stages == null || _stages.Stages.Count == 0)
		{
			return 0;
		}
		CourseProgressData progress = CurrentProgress();
		int val = MinimumActiveStageIndexFromClearedProgress(progress);
		int value = progress.StageIndex;
		if (!string.IsNullOrWhiteSpace(progress.StageId))
		{
			int num = _stages.Stages.ToList().FindIndex((StageDefinition s) => string.Equals(s.Id, progress.StageId, StringComparison.OrdinalIgnoreCase));
			if (num >= 0)
			{
				value = num;
			}
		}
		return Math.Max(val, Math.Clamp(value, 0, Math.Max(0, _stages.Stages.Count - 1)));
	}

	private CourseProgressData CurrentProgress()
	{
		return _progress.ForCourse(_activeCourse?.Id ?? _progress.Data.ActiveCourseId, _activeCourse?.Version, _activeCourse?.FlowFingerprint);
	}

	private int MinimumActiveStageIndexFromClearedProgress(CourseProgressData progress)
	{
		if (_stages == null || _stages.Stages.Count == 0)
		{
			return 0;
		}
		HashSet<string> hashSet = (progress.ClearedStages ?? new List<string>()).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		IReadOnlyList<StageDefinition> stages = _stages.Stages;
		if (hashSet.Contains(stages[stages.Count - 1].Id))
		{
			return _stages.Stages.Count - 1;
		}
		for (int i = 0; i < _stages.Stages.Count; i++)
		{
			if (!hashSet.Contains(_stages.Stages[i].Id))
			{
				return i;
			}
		}
		return _stages.Stages.Count - 1;
	}

	private void RebuildCourseHub()
	{
		CourseListPanel.Children.Clear();
		if (_courses.Count == 0)
		{
			CourseListPanel.Children.Add(new TextBlock
			{
				Text = TextOr("hub.empty_course_list", "课程还没发布。稍后再来看看，或点一下“检查更新”。"),
				Foreground = (Brush)FindResource("R2SLayerMutedBrush"),
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
			});
		}
		else
		{
			foreach (CourseDefinition course in _courses)
			{
				CourseListPanel.Children.Add(MakeCourseButton(course));
			}
		}
		UpdateActiveCourseCard();
	}

	private Button MakeCourseButton(CourseDefinition course)
	{
		TextBlock element = new TextBlock
		{
			Text = course.Title,
			Foreground = (Brush)FindResource("R2SLayerContentBrush"),
			FontWeight = FontWeights.Bold,
			TextWrapping = TextWrapping.Wrap
		};
		TextBlock element2 = new TextBlock
		{
			Text = $"{course.Track} / {course.Difficulty} / {course.StageCount} stages\n兼容程序版本 {course.HostCompatibilityDisplay}",
			Foreground = (Brush)FindResource("R2SLayerMutedBrush"),
			FontSize = 11.0,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			TextWrapping = TextWrapping.Wrap
		};
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(element);
		stackPanel.Children.Add(element2);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		FrameworkElement frameworkElement = CreateCourseVectorIcon(38.0);
		frameworkElement.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		Grid.SetColumn(frameworkElement, 0);
		Grid.SetColumn(stackPanel, 1);
		grid.Children.Add(frameworkElement);
		grid.Children.Add(stackPanel);
		Button button = new Button();
		button.Content = grid;
		button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
		button.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		button.Padding = new Thickness(12.0, 10.0, 12.0, 10.0);
		button.Tag = course;
		button.Style = (Style)FindResource(string.Equals(course.Id, _activeCourse?.Id, StringComparison.OrdinalIgnoreCase) ? "R2SButtonSelected" : "R2SButtonGhost");
		button.Click += delegate
		{
			SelectCourse(course);
		};
		AutomationProperties.SetAutomationId((DependencyObject)(object)button, "course:" + course.Id);
		AutomationProperties.SetName((DependencyObject)(object)button, course.Title);
		return button;
	}

	private void SelectCourse(CourseDefinition course)
	{
		_activeCourse = course;
		_progress.SetActiveCourse(course.Id, course.Version, course.FlowFingerprint);
		ApplyActiveCourse(loadTexts: true);
		ApplyStaticTexts();
		UpdateActiveCourseCard();
		LoadStage();
		ShowHub();
	}

	private FrameworkElement CreateCourseVectorIcon(double size)
	{
		Canvas canvas = new Canvas
		{
			Width = 64.0,
			Height = 64.0
		};
		Brush stroke = (Brush)FindResource("R2SPrimaryBrush");
		Brush stroke2 = (Brush)FindResource("R2SSuccessBrush");
		Brush stroke3 = (Brush)FindResource("R2SWarningBrush");
		SolidColorBrush fill = new SolidColorBrush(Color.FromArgb(40, 88, 183, byte.MaxValue));
		canvas.Children.Add(new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse("M10,12 L54,12 L54,52 L10,52 Z"),
			Stroke = stroke,
			StrokeThickness = 3.0,
			Fill = fill,
			StrokeLineJoin = PenLineJoin.Round
		});
		canvas.Children.Add(new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse("M19,23 L28,32 L19,41 M33,42 L46,42"),
			Stroke = stroke3,
			StrokeThickness = 4.0,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round
		});
		canvas.Children.Add(new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse("M44,12 L54,22 L44,22 Z"),
			Stroke = stroke2,
			StrokeThickness = 2.4,
			Fill = new SolidColorBrush(Color.FromArgb(50, 77, 226, 143)),
			StrokeLineJoin = PenLineJoin.Round
		});
		return new Viewbox
		{
			Width = size,
			Height = size,
			Child = canvas,
			Stretch = Stretch.Uniform
		};
	}

	private void UpdateActiveCourseCard()
	{
		if (_activeCourse == null)
		{
			ActiveCourseTitleText.Text = TextOr("hub.empty_title", "还没有课程");
			ActiveCourseSubtitleText.Text = TextOr("hub.empty_subtitle", "新课程发布后会出现在这里。");
			ActiveCourseCompatibilityText.Text = "";
			ActiveCourseSummaryText.Text = TextOr("hub.empty_summary", "比赛期间会陆续更新课程，记得回来看看。");
			ActiveCourseIconHost.Content = CreateCourseVectorIcon(72.0);
			ActiveCourseTrackTag.Content = "Reverse";
			ActiveCourseVersionTag.Content = "pending";
			CourseStageCountText.Text = "0";
			CourseMinutesText.Text = "-";
			CourseSourceText.Text = TextOr("hub.source_pending", "待发布");
			EnterCourseButton.IsEnabled = false;
			RequirementGroupsPanel.Children.Clear();
			RequirementStatusText.Text = TextOr("hub.empty_update_ready", "点一下“检查更新”，看看有没有新课程。");
			UpdateStatusText.Text = TextOr("hub.empty_update_ready", "点一下“检查更新”，看看有没有新课程。");
			return;
		}
		ActiveCourseTitleText.Text = _activeCourse.Title;
		ActiveCourseSubtitleText.Text = _activeCourse.Subtitle;
		ActiveCourseCompatibilityText.Text = "兼容程序版本 " + _activeCourse.HostCompatibilityDisplay;
		ActiveCourseCompatibilityText.Foreground = (Brush)FindResource(_activeCourse.IsHostCompatible ? "R2SSuccessBrush" : "R2SWarningBrush");
		ActiveCourseSummaryText.Text = _activeCourse.Summary;
		ActiveCourseIconHost.Content = CreateCourseVectorIcon(72.0);
		ActiveCourseTrackTag.Content = _activeCourse.Track;
		ActiveCourseVersionTag.Content = _activeCourse.Version;
		CourseUpdateText.Text = ((_activeCourse.Changelog.Count == 0) ? TextOr("hub.changelog_empty", "暂无更新记录。") : string.Join(Environment.NewLine, _activeCourse.Changelog.Select((CourseChangelogEntry entry) => string.Join(" ", new string[3] { entry.Version, entry.Date, entry.Text }.Where((string v) => !string.IsNullOrWhiteSpace(v))))));
		CourseStageCountText.Text = Math.Max(_activeCourse.StageCount, _stages?.Stages.Count ?? 0).ToString();
		CourseMinutesText.Text = ((_activeCourse.EstimatedMinutes > 0) ? _activeCourse.EstimatedMinutes.ToString() : "-");
		CourseSourceText.Text = (_activeCourse.IsBuiltIn ? TextOr("hub.source_builtin", "随程序提供") : TextOr("hub.source_updated", "已同步更新"));
		_requirements.AutoSelectSatisfiedRequirements(_activeCourse.Id, _activeCourse.RequirementGroups);
		RebuildRequirementGroups();
		bool flag = _requirements.AreRequirementsSatisfied(_activeCourse.Id, _activeCourse.RequirementGroups);
		Button enterCourseButton = EnterCourseButton;
		StageRepository stages = _stages;
		enterCourseButton.IsEnabled = stages != null && stages.Stages.Count > 0 && flag && _activeCourse.IsHostCompatible;
		RequirementStatusText.Text = ((!_activeCourse.IsCompatibilityRangeValid) ? "这门课程没有有效的程序兼容版本声明，暂时无法开始。" : ((!_activeCourse.IsHostCompatible) ? $"这门课程需要程序版本 {_activeCourse.HostCompatibilityDisplay}；当前版本是 {CourseDefinition.CurrentHostVersion}，暂时无法开始。" : (flag ? TextOr("hub.requirements_ready", "都准备好了，开始吧。") : TextOr("hub.requirements_missing", "把缺少的文件或工具拖到这里，也可以点击对应项选择。"))));
		UpdateStatusText.Text = (string.IsNullOrWhiteSpace(_activeCourse.UpdateManifestUrl) ? TextOr("hub.update_none", "这门课暂时没有更新。") : TextOr("hub.update_ready", "可以检查这门课有没有新内容。"));
	}

	private void RebuildRequirementGroups()
	{
		RequirementGroupsPanel.Children.Clear();
		if (_activeCourse == null)
		{
			return;
		}
		if (_activeCourse.RequirementGroups.Count == 0)
		{
			RequirementGroupsPanel.Children.Add(new TextBlock
			{
				Text = TextOr("hub.requirements_none", "这门课不需要额外准备文件。"),
				Foreground = (Brush)FindResource("R2SLayerMutedBrush"),
				TextWrapping = TextWrapping.Wrap
			});
			return;
		}
		foreach (CourseRequirementGroup requirementGroup in _activeCourse.RequirementGroups)
		{
			StackPanel stackPanel = new StackPanel
			{
				Width = Math.Min(360, Math.Max(124, requirementGroup.Items.Count * 118)),
				Margin = new Thickness(0.0, 0.0, 10.0, 6.0)
			};
			stackPanel.Children.Add(new TextBlock
			{
				Text = requirementGroup.Title,
				Foreground = (Brush)FindResource("R2SLayerContentBrush"),
				FontWeight = FontWeights.Bold,
				FontSize = 13.0,
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
			});
			if (!string.IsNullOrWhiteSpace(requirementGroup.Description))
			{
				stackPanel.ToolTip = requirementGroup.Description;
			}
			UniformGrid uniformGrid = new UniformGrid
			{
				Rows = 1,
				Columns = Math.Max(1, requirementGroup.Items.Count)
			};
			for (int i = 0; i < requirementGroup.Items.Count; i++)
			{
				CourseRequirement requirement = requirementGroup.Items[i];
				RequirementActivationStatus status = _requirements.StatusFor(_activeCourse.Id, requirementGroup.Id, requirement);
				uniformGrid.Children.Add(MakeRequirementButton(requirementGroup, requirement, status, i, requirementGroup.Items.Count));
			}
			Border border = new Border
			{
				Child = uniformGrid
			};
			border.Style = (Style)FindResource("R2SSegmentGroup");
			stackPanel.Children.Add(border);
			RequirementGroupsPanel.Children.Add(stackPanel);
		}
	}

	private RadioButton MakeRequirementButton(CourseRequirementGroup group, CourseRequirement requirement, RequirementActivationStatus status, int index, int count)
	{
		bool value = status.IsSelected || (group.ModeNormalized == "allOf" && status.IsSatisfied);
		Grid grid = new Grid
		{
			Margin = new Thickness(4.0),
			MinHeight = 56.0
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		FrameworkElement frameworkElement = RequirementIcon(requirement, status.Path);
		frameworkElement.Width = 24.0;
		frameworkElement.Height = 24.0;
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.Margin = new Thickness(0.0, 1.0, 0.0, 4.0);
		Grid.SetRow(frameworkElement, 0);
		grid.Children.Add(frameworkElement);
		TextBlock element = new TextBlock
		{
			Text = (string.IsNullOrWhiteSpace(requirement.Label) ? requirement.Id : requirement.Label),
			Foreground = (Brush)FindResource("R2SLayerContentBrush"),
			FontWeight = FontWeights.Bold,
			FontSize = 11.0,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap
		};
		Grid.SetRow(element, 1);
		grid.Children.Add(element);
		TextBlock element2 = new TextBlock
		{
			Text = (status.IsSatisfied ? status.DisplayPath : TextOr("requirement.missing", "未配置")),
			Foreground = (status.IsSatisfied ? ((Brush)FindResource("R2SLayerMutedBrush")) : ((Brush)FindResource("R2SWarningBrush"))),
			FontSize = 9.5,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		Grid.SetRow(element2, 2);
		grid.Children.Add(element2);
		RadioButton radioButton = new RadioButton();
		radioButton.Content = grid;
		radioButton.MinHeight = 68.0;
		radioButton.Padding = new Thickness(0.0);
		radioButton.GroupName = "requirement:" + group.Id;
		radioButton.IsChecked = value;
		radioButton.Opacity = (status.IsSatisfied ? 1.0 : 0.62);
		radioButton.Tag = new RequirementButtonTag(group, requirement);
		radioButton.Style = (Style)FindResource("R2SSegmentRadio");
		radioButton.Click += RequirementButton_OnClick;
		return radioButton;
	}

	private FrameworkElement RequirementIcon(CourseRequirement requirement, string? path)
	{
		ImageSource imageSource = TryExtractIcon(path);
		if (imageSource != null)
		{
			return new Image
			{
				Source = imageSource,
				Stretch = Stretch.Uniform
			};
		}
		return CreateDesktopVectorIcon((requirement.KindNormalized == "tool") ? requirement.Label : "EXE", requirement.KindNormalized == "tool");
	}

	private void RequirementButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (_activeCourse != null && sender is RadioButton { Tag: RequirementButtonTag tag })
		{
			if (_requirements.StatusFor(_activeCourse.Id, tag.Group.Id, tag.Requirement).IsSatisfied)
			{
				_requirements.SelectRequirement(_activeCourse.Id, tag.Group.Id, tag.Requirement.Id);
				UpdateActiveCourseCard();
			}
			else
			{
				ActivateRequirementViaDialog(tag.Group, tag.Requirement);
			}
		}
	}

	private void ActivateRequirementViaDialog(CourseRequirementGroup group, CourseRequirement requirement)
	{
		if (_activeCourse == null)
		{
			return;
		}
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = ((requirement.KindNormalized == "tool") ? TextOr("requirement.choose_tool", "选择工具可执行文件") : TextOr("requirement.choose_target", "选择题目附件")),
			Filter = ((requirement.KindNormalized == "tool") ? "Executable files|*.exe;*.bat;*.cmd|All files|*.*" : "All files|*.*"),
			CheckFileExists = true
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			UpdateActiveCourseCard();
			return;
		}
		try
		{
			_requirements.ActivateRequirement(_activeCourse, requirement, openFileDialog.FileName);
			_requirements.SelectRequirement(_activeCourse.Id, group.Id, requirement.Id);
			RequirementStatusText.Text = TextOr("requirement.activated", "已经准备好了。");
		}
		catch (Exception ex)
		{
			RequirementStatusText.Text = TextOr("requirement.activate_failed", "激活失败：{error}").Replace("{error}", ex.Message);
		}
		UpdateActiveCourseCard();
	}

	private void RequirementsPanel_OnDragOver(object sender, DragEventArgs e)
	{
		e.Effects = ((_activeCourse != null && e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: true)) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private void RequirementsPanel_OnDrop(object sender, DragEventArgs e)
	{
		e.Handled = true;
		if (_activeCourse == null || !e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: true))
		{
			return;
		}
		string[] inputPaths = (e.Data.GetData(DataFormats.FileDrop, autoConvert: true) as string[]) ?? Array.Empty<string>();
		string matchedPath;
		CourseRequirement requirement = _requirements.FindMatchingRequirement(_activeCourse, inputPaths, out matchedPath);
		if (requirement == null)
		{
			RequirementStatusText.Text = TextOr("requirement.no_match", "没有在拖入内容中找到当前课程需要的文件。");
			return;
		}
		CourseRequirementGroup courseRequirementGroup = _activeCourse.RequirementGroups.FirstOrDefault((CourseRequirementGroup g) => g.Items.Any((CourseRequirement item) => string.Equals(item.Id, requirement.Id, StringComparison.OrdinalIgnoreCase)));
		if (courseRequirementGroup != null)
		{
			try
			{
				_requirements.ActivateRequirement(_activeCourse, requirement, matchedPath);
				_requirements.SelectRequirement(_activeCourse.Id, courseRequirementGroup.Id, requirement.Id);
				RequirementStatusText.Text = TextOr("requirement.activated", "已经准备好了。");
			}
			catch (Exception ex)
			{
				RequirementStatusText.Text = TextOr("requirement.activate_failed", "激活失败：{error}").Replace("{error}", ex.Message);
			}
			UpdateActiveCourseCard();
		}
	}

	private void RebuildToolSettings()
	{
		ToolSettingsPanel.Children.Clear();
		foreach (ToolSpec item in _tools.All.OrderBy<ToolSpec, string>((ToolSpec t) => t.DisplayName, StringComparer.OrdinalIgnoreCase))
		{
			ToolSettingsPanel.Children.Add(MakeToolSettingRow(item));
		}
	}

	private FrameworkElement MakeToolSettingRow(ToolSpec tool)
	{
		string configuredToolPath = _requirements.GetConfiguredToolPath(tool.Id);
		string text = _requirements.FindToolExecutable(tool.Id);
		Grid obj = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = GridLength.Auto
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			}
		};
		FrameworkElement frameworkElement = RequirementIcon(new CourseRequirement
		{
			Id = tool.Id,
			Kind = "tool",
			Label = tool.DisplayName,
			ToolId = tool.Id
		}, text);
		frameworkElement.Width = 30.0;
		frameworkElement.Height = 30.0;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		Grid.SetColumn(frameworkElement, 0);
		obj.Children.Add(frameworkElement);
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = tool.DisplayName,
			Foreground = (Brush)FindResource("R2SLayerContentBrush"),
			FontWeight = FontWeights.Bold
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = (configuredToolPath ?? text ?? TextOr("requirement.missing", "未配置")),
			Foreground = (string.IsNullOrWhiteSpace(text) ? ((Brush)FindResource("R2SWarningBrush")) : ((Brush)FindResource("R2SLayerMutedBrush"))),
			FontSize = 11.0,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		Grid.SetColumn(stackPanel, 1);
		obj.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		Button button = new Button
		{
			Content = TextOr("settings.choose", "选择"),
			Style = (Style)FindResource("R2SButtonGhost"),
			Padding = new Thickness(10.0, 5.0, 10.0, 5.0),
			Tag = tool.Id
		};
		button.Click += ChooseToolPathButton_OnClick;
		stackPanel2.Children.Add(button);
		Button button2 = new Button
		{
			Content = TextOr("settings.clear", "清除"),
			Style = (Style)FindResource("R2SButtonGhost"),
			Padding = new Thickness(10.0, 5.0, 10.0, 5.0),
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsEnabled = (configuredToolPath != null),
			Tag = tool.Id
		};
		button2.Click += ClearToolPathButton_OnClick;
		stackPanel2.Children.Add(button2);
		Grid.SetColumn(stackPanel2, 2);
		obj.Children.Add(stackPanel2);
		return obj;
	}

	private void ChooseToolPathButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button { Tag: string tag }))
		{
			return;
		}
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = TextOr("requirement.choose_tool", "选择工具可执行文件"),
			Filter = "Executable files|*.exe;*.bat;*.cmd|All files|*.*",
			CheckFileExists = true
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			try
			{
				_requirements.SetToolPath(tag, openFileDialog.FileName);
			}
			catch
			{
			}
			RebuildToolSettings();
			UpdateActiveCourseCard();
		}
	}

	private void ClearToolPathButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: string tag })
		{
			_requirements.ClearToolPath(tag);
			RebuildToolSettings();
			UpdateActiveCourseCard();
		}
	}

	private void ShowHub()
	{
		AdvanceGuideRevision();
		CourseCompletionOverlay.Visibility = Visibility.Collapsed;
		CourseHubView.Visibility = Visibility.Visible;
		CourseDeskView.Visibility = Visibility.Collapsed;
		BackToHubButton.Visibility = Visibility.Collapsed;
		_hostGuideOverlay.Clear();
		_overlay.ClearGuide();
		TaskbarStageText.Text = _activeCourse?.Title ?? "MoeCTF Reverse";
	}

	private void ShowDesk()
	{
		if (_activeCourse == null || !HasLoadedStage)
		{
			ShowHub();
			UpdateStatusText.Text = TextOr("hub.empty_update_ready", "点一下“检查更新”，看看有没有新课程。");
		}
		else if (!_activeCourse.IsHostCompatible)
		{
			ShowHub();
			RequirementStatusText.Text = (_activeCourse.IsCompatibilityRangeValid ? $"这门课程需要程序版本 {_activeCourse.HostCompatibilityDisplay}；当前版本是 {CourseDefinition.CurrentHostVersion}，暂时无法开始。" : "这门课程没有有效的程序兼容版本声明，暂时无法开始。");
		}
		else if (!_requirements.AreRequirementsSatisfied(_activeCourse.Id, _activeCourse.RequirementGroups))
		{
			ShowHub();
			RequirementStatusText.Text = TextOr("hub.requirements_missing", "把缺少的文件或工具拖到这里，也可以点击对应项选择。");
		}
		else
		{
			CourseHubView.Visibility = Visibility.Collapsed;
			CourseDeskView.Visibility = Visibility.Visible;
			BackToHubButton.Visibility = Visibility.Visible;
			AdvanceGuideRevision();
			RefreshCoach();
			ShowCourseCompletionIfFinished();
		}
	}

	private void EnterCourseButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (_activeCourse == null || !_activeCourse.IsHostCompatible || !HasLoadedStage || !_requirements.AreRequirementsSatisfied(_activeCourse.Id, _activeCourse.RequirementGroups))
		{
			ShowDesk();
		}
		else
		{
			CourseEntryNoticeOverlay.Visibility = Visibility.Visible;
		}
	}

	private void CancelCourseEntryButton_OnClick(object sender, RoutedEventArgs e)
	{
		CourseEntryNoticeOverlay.Visibility = Visibility.Collapsed;
	}

	private void ConfirmCourseEntryButton_OnClick(object sender, RoutedEventArgs e)
	{
		CourseEntryNoticeOverlay.Visibility = Visibility.Collapsed;
		ShowDesk();
	}

	private void InteractiveModeToggle_OnClick(object sender, RoutedEventArgs e)
	{
		_interactiveGuideEnabled = InteractiveModeToggle.IsChecked == true;
		AdvanceGuideRevision();
		if (!_interactiveGuideEnabled)
		{
			HideProbeStatus();
			TrackedWindowsText.Text = TextOr("common.interactive_mode_disabled", "交互指引已关闭。你仍然可以按步骤继续。");
			TaskbarTrackText.Text = TextOr("common.interactive_mode_disabled_short", "交互式指引已关闭");
		}
		if (CourseDeskView.Visibility == Visibility.Visible)
		{
			RefreshCoach();
		}
	}

	private void BackToHubButton_OnClick(object sender, RoutedEventArgs e)
	{
		ShowHub();
	}

	private void CourseCompletionBackButton_OnClick(object sender, RoutedEventArgs e)
	{
		ShowHub();
	}

	private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
	{
		RebuildToolSettings();
		SettingsModalOverlay.Visibility = Visibility.Visible;
	}

	private void CloseSettingsButton_OnClick(object sender, RoutedEventArgs e)
	{
		SettingsModalOverlay.Visibility = Visibility.Collapsed;
		UpdateActiveCourseCard();
	}

	private async void CheckUpdateButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (_pendingHostUpdate != null)
		{
			await InstallPendingHostUpdateAsync();
		}
		else
		{
			await CheckForUpdatesAsync(automatic: false);
		}
	}

	private async Task CheckForUpdatesAsync(bool automatic)
	{
		if (_updateCheckInProgress)
		{
			return;
		}
		CourseUpdateEndpoints courseUpdateEndpoints = _catalog.LoadUpdateEndpoints();
		string text = courseUpdateEndpoints.AppUpdateManifestUrl ?? _activeCourse?.AppUpdateManifestUrl;
		if (automatic && string.IsNullOrWhiteSpace(courseUpdateEndpoints.CatalogUpdateManifestUrl) && string.IsNullOrWhiteSpace(_activeCourse?.UpdateManifestUrl) && string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		_updateCheckInProgress = true;
		CheckUpdateButton.IsEnabled = false;
		if (!automatic)
		{
			UpdateStatusText.Text = TextOr("hub.update_checking", "正在检查更新...");
		}
		try
		{
			List<string> messages = new List<string>();
			CancellationToken cancellationToken = _updateCancellation.Token;
			Task<HostUpdateResult> hostUpdateTask = _contentUpdater.CheckHostUpdateAsync(text, cancellationToken);
			Task<ContentUpdateResult> catalogUpdateTask = (Task<ContentUpdateResult>)(string.IsNullOrWhiteSpace(courseUpdateEndpoints.CatalogUpdateManifestUrl) ? ((Task)Task.FromResult(new ContentUpdateResult
			{
				Success = true
			})) : ((Task)_contentUpdater.CheckAndDownloadAsync(courseUpdateEndpoints.CatalogUpdateManifestUrl, cancellationToken)));
			await Task.WhenAll(hostUpdateTask, catalogUpdateTask);
			HostUpdateResult hostResult = await hostUpdateTask;
			if (hostResult.Success && hostResult.UpdateAvailable)
			{
				_pendingHostUpdate = hostResult;
				UpdateCheckButtonText();
			}
			if (!string.IsNullOrWhiteSpace(hostResult.Message))
			{
				messages.Add(hostResult.Message);
			}
			ContentUpdateResult catalogResult = await catalogUpdateTask;
			if (!string.IsNullOrWhiteSpace(catalogResult.Message))
			{
				messages.Add(catalogResult.Message);
			}
			if (catalogResult.Success && catalogResult.PendingActivation)
			{
				ReloadCoursesAfterContentUpdate();
				messages.Add(TextOr("hub.update_activated", "已经更新，可以继续了。"));
			}
			ContentUpdateResult courseResult = new ContentUpdateResult
			{
				Success = true
			};
			if (!catalogResult.PendingActivation && _activeCourse != null)
			{
				courseResult = await _contentUpdater.CheckAndDownloadCourseAsync(_activeCourse, cancellationToken);
				if (!string.IsNullOrWhiteSpace(courseResult.Message))
				{
					messages.Add(courseResult.Message);
				}
				if (courseResult.Success && courseResult.PendingActivation)
				{
					ReloadCoursesAfterContentUpdate();
					messages.Add(TextOr("hub.update_activated", "已经更新，可以继续了。"));
				}
			}
			ContentUpdateResult contentUpdateResult = new ContentUpdateResult[2] { catalogResult, courseResult }.FirstOrDefault((ContentUpdateResult result) => result.RequiresHostUpdate && !string.IsNullOrWhiteSpace(result.HostUpdateManifestUrl));
			if (contentUpdateResult != null)
			{
				HostUpdateResult hostUpdateResult = await _contentUpdater.CheckHostUpdateAsync(contentUpdateResult.HostUpdateManifestUrl, cancellationToken);
				if (hostUpdateResult.Success && hostUpdateResult.UpdateAvailable)
				{
					_pendingHostUpdate = hostUpdateResult;
					UpdateCheckButtonText();
				}
				if (!string.IsNullOrWhiteSpace(hostUpdateResult.Message))
				{
					messages.Add(hostUpdateResult.Message);
				}
			}
			string text2 = string.Join(Environment.NewLine, messages.Where((string m) => !string.IsNullOrWhiteSpace(m)));
			bool flag = catalogResult.PendingActivation || courseResult.PendingActivation;
			if (!automatic || flag || hostResult.UpdateAvailable || !catalogResult.Success || !courseResult.Success || !hostResult.Success)
			{
				UpdateStatusText.Text = text2;
			}
		}
		catch (OperationCanceledException) when (_updateCancellation.IsCancellationRequested)
		{
		}
		catch (Exception ex2)
		{
			UpdateStatusText.Text = TextOr("hub.update_failed", "检查更新失败：") + ex2.Message;
		}
		finally
		{
			_updateCheckInProgress = false;
			CheckUpdateButton.IsEnabled = true;
		}
	}

	private void UpdateCheckButtonText()
	{
		CheckUpdateButton.Content = ((_pendingHostUpdate == null) ? TextOr("hub.check_update", "检查更新") : TextOr("hub.install_update", "安装程序更新"));
	}

	private async Task InstallPendingHostUpdateAsync()
	{
		if (_pendingHostUpdate == null || _updateCheckInProgress)
		{
			return;
		}
		string targetExecutablePath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(targetExecutablePath) || !File.Exists(targetExecutablePath))
		{
			UpdateStatusText.Text = TextOr("hub.update_install_failed", "无法安装程序更新：") + TextOr("hub.update_executable_unknown", "找不到当前程序文件。");
			return;
		}
		_updateCheckInProgress = true;
		CheckUpdateButton.IsEnabled = false;
		UpdateStatusText.Text = TextOr("hub.update_downloading", "正在下载并校验程序更新...");
		try
		{
			HostUpdateResult pending = _pendingHostUpdate;
			HostUpdateInstallResult hostUpdateInstallResult = await Task.Run(() => _hostUpdateInstaller.DownloadAndLaunchAsync(pending, _paths.RepoRoot, targetExecutablePath, _updateCancellation.Token));
			if (!hostUpdateInstallResult.Success)
			{
				UpdateStatusText.Text = TextOr("hub.update_install_failed", "无法安装程序更新：") + hostUpdateInstallResult.Message;
				return;
			}
			UpdateStatusText.Text = TextOr("hub.update_restarting", "更新已校验，正在重新启动...");
			Application.Current.Shutdown();
		}
		catch (OperationCanceledException) when (_updateCancellation.IsCancellationRequested)
		{
		}
		catch (Exception ex2)
		{
			UpdateStatusText.Text = TextOr("hub.update_install_failed", "无法安装程序更新：") + ex2.Message;
		}
		finally
		{
			_updateCheckInProgress = false;
			CheckUpdateButton.IsEnabled = true;
		}
	}

	private void ReloadCoursesAfterContentUpdate()
	{
		string activeId = _activeCourse?.Id;
		AdvanceGuideRevision();
		_paths.UseBuiltInContent();
		LoadCourses();
		_activeCourse = _courses.FirstOrDefault((CourseDefinition course) => string.Equals(course.Id, activeId, StringComparison.OrdinalIgnoreCase)) ?? _courses.FirstOrDefault();
		if (_activeCourse != null)
		{
			_progress.SetActiveCourse(_activeCourse.Id, _activeCourse.Version, _activeCourse.FlowFingerprint);
		}
		ApplyActiveCourse(loadTexts: true);
		ApplyStaticTexts();
		RebuildCourseHub();
		LoadStage();
		ShowHub();
	}

	private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void CloseButton_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void LoadStage()
	{
		CourseCompletionOverlay.Visibility = Visibility.Collapsed;
		if (!HasLoadedStage)
		{
			ClearDesktopIcons();
			ProgressText.Text = TextOr("hub.empty_progress", "进度：暂无课程");
			StageTitleText.Text = TextOr("hub.empty_title", "还没有课程");
			StageGoalText.Text = TextOr("hub.empty_summary", "比赛期间会陆续更新课程，记得回来看看。");
			CoachText.Text = TextOr("hub.empty_update_ready", "点一下“检查更新”，看看有没有新课程。");
			ActionButton.Visibility = Visibility.Collapsed;
			NextStepButton.Visibility = Visibility.Collapsed;
			FeedbackText.Text = "";
			FlagInput.Text = "";
			TaskbarStageText.Text = _activeCourse?.Title ?? "MoeCTF Reverse";
			return;
		}
		_stageIndex = Math.Clamp(_stageIndex, 0, _stages.Stages.Count - 1);
		CourseProgressData courseProgressData = CurrentProgress();
		courseProgressData.StageIndex = _stageIndex;
		courseProgressData.StageId = Current.Id;
		_progress.MirrorLegacy(courseProgressData);
		_progress.Save();
		_stepIndex = 0;
		_guideUnlocked = Current.GuideMode == "forced";
		AdvanceGuideRevision();
		FeedbackText.Text = "";
		FlagInput.Text = "";
		_lastActivatedGuideTarget = null;
		ProgressText.Text = _i18n.T("common.progress", new Dictionary<string, string>
		{
			["current"] = (_stageIndex + 1).ToString(),
			["total"] = _stages.Stages.Count.ToString()
		});
		StageTitleText.Text = _i18n.T(Current.TitleKey);
		StageGoalText.Text = _i18n.T(Current.GoalKey);
		TaskbarStageText.Text = StageTitleText.Text;
		RebuildDesktopIcons();
		RefreshCoach();
	}

	private void ClearDesktopIcons()
	{
		foreach (KeyValuePair<string, Button> item in _desktopIcons.ToList())
		{
			DesktopCanvas.Children.Remove(item.Value);
		}
		foreach (string item2 in _hostHotspots.Keys.Where((string k) => k.StartsWith("tool:") || k.StartsWith("target:")).ToList())
		{
			_hostHotspots.Remove(item2);
		}
		_desktopIcons.Clear();
	}

	private void RebuildDesktopIcons()
	{
		ClearDesktopIcons();
		double num = 18.0;
		foreach (string item in Current.Tools.Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			Button icon = MakeCanvasIcon("tool:" + item, _tools.FormatLabel(item), _tools.Glyph(item), isTool: true, item);
			PlaceIcon(icon, num, 12.0);
			num += 112.0;
		}
		foreach (string target in Current.Targets)
		{
			Button icon2 = MakeCanvasIcon("target:" + target, target, "EXE", isTool: false, target);
			PlaceIcon(icon2, num, 12.0);
			num += 112.0;
		}
		HighlightStepIcons();
	}

	private void PlaceIcon(Button icon, double x, double y)
	{
		Canvas.SetLeft(icon, x);
		Canvas.SetTop(icon, y);
		if (!DesktopCanvas.Children.Contains(icon))
		{
			DesktopCanvas.Children.Add(icon);
		}
	}

	private Button MakeCanvasIcon(string key, string label, string glyph, bool isTool, string resourceId)
	{
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		string path = (isTool ? _requirements.FindToolExecutable(resourceId) : _paths.FindTarget(resourceId));
		FrameworkElement frameworkElement = RequirementIcon(new CourseRequirement
		{
			Id = resourceId,
			Kind = (isTool ? "tool" : "target"),
			Label = label,
			ToolId = (isTool ? resourceId : null)
		}, path);
		frameworkElement.Width = 30.0;
		frameworkElement.Height = 30.0;
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.Margin = new Thickness(0.0, 0.0, 0.0, 5.0);
		stackPanel.Children.Add(frameworkElement);
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			Foreground = Brushes.White,
			FontSize = 12.0,
			FontWeight = FontWeights.SemiBold,
			HorizontalAlignment = HorizontalAlignment.Center,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap
		});
		Button button = new Button
		{
			Width = 102.0,
			Height = 88.0,
			Content = stackPanel,
			Cursor = Cursors.Hand,
			MinHeight = 88.0,
			Padding = new Thickness(0.0),
			Tag = key
		};
		button.Style = (Style)FindResource("R2SButtonGhost");
		AutomationProperties.SetAutomationId((DependencyObject)(object)button, key);
		AutomationProperties.SetName((DependencyObject)(object)button, label);
		AutomationProperties.SetHelpText((DependencyObject)(object)button, key);
		button.Click += delegate
		{
			ActivateDesktopResource(isTool, resourceId);
		};
		if (!isTool)
		{
			Point? dragStart = null;
			button.PreviewMouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
			{
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				dragStart = e.GetPosition(button);
			};
			button.PreviewMouseLeftButtonUp += delegate
			{
				dragStart = null;
			};
			button.PreviewMouseMove += delegate(object _, MouseEventArgs e)
			{
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_0036: Unknown result type (might be due to invalid IL or missing references)
				if (e.LeftButton == MouseButtonState.Pressed && dragStart.HasValue)
				{
					Point valueOrDefault = dragStart.GetValueOrDefault();
					Point position = e.GetPosition(button);
					if (!(Math.Abs(((Point)(ref position)).X - ((Point)(ref valueOrDefault)).X) < SystemParameters.MinimumHorizontalDragDistance) || !(Math.Abs(((Point)(ref position)).Y - ((Point)(ref valueOrDefault)).Y) < SystemParameters.MinimumVerticalDragDistance))
					{
						string text = _paths.FindTarget(resourceId);
						if (text != null)
						{
							dragStart = null;
							DataObject dataObject = new DataObject();
							dataObject.SetData(DataFormats.FileDrop, new string[1] { text });
							DragDrop.DoDragDrop((DependencyObject)(object)button, dataObject, DragDropEffects.Copy);
						}
					}
				}
			};
		}
		_desktopIcons[key] = button;
		_hostHotspots[key] = button;
		return button;
	}

	private void ActivateDesktopResource(bool isTool, string resourceId)
	{
		if (isTool)
		{
			OpenToolResource(resourceId);
		}
		else
		{
			OpenTargetFolderResource(resourceId);
		}
	}

	private FrameworkElement CreateDesktopVectorIcon(string glyph, bool isTool)
	{
		SolidColorBrush stroke = (isTool ? new SolidColorBrush(Color.FromRgb(88, 183, byte.MaxValue)) : new SolidColorBrush(Color.FromRgb(77, 226, 143)));
		Brush stroke2 = (Brush)FindResource("R2SWarningBrush");
		SolidColorBrush stroke3 = new SolidColorBrush(Color.FromRgb(232, 236, 242));
		Canvas canvas = new Canvas
		{
			Width = 48.0,
			Height = 42.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 2.0, 0.0, 8.0)
		};
		if (!isTool)
		{
			canvas.Children.Add(new System.Windows.Shapes.Path
			{
				Data = Geometry.Parse("M10,3 L30,3 L40,13 L40,39 L10,39 Z M30,3 L30,13 L40,13"),
				Stroke = stroke,
				StrokeThickness = 2.4,
				Fill = new SolidColorBrush(Color.FromArgb(32, 77, 226, 143)),
				StrokeLineJoin = PenLineJoin.Round
			});
			canvas.Children.Add(new System.Windows.Shapes.Path
			{
				Data = Geometry.Parse("M17,21 L31,21 M17,28 L35,28"),
				Stroke = stroke2,
				StrokeThickness = 2.2,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round
			});
			return canvas;
		}
		string text = glyph.Trim().ToLowerInvariant();
		if (text.Contains("ida"))
		{
			canvas.Children.Add(new System.Windows.Shapes.Path
			{
				Data = Geometry.Parse("M9,32 L24,7 L39,32 Z"),
				Stroke = stroke,
				StrokeThickness = 2.4,
				Fill = new SolidColorBrush(Color.FromArgb(24, 88, 183, byte.MaxValue)),
				StrokeLineJoin = PenLineJoin.Round
			});
			canvas.Children.Add(new System.Windows.Shapes.Path
			{
				Data = Geometry.Parse("M18,25 L30,25 M21,20 L27,20"),
				Stroke = stroke2,
				StrokeThickness = 2.2,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round
			});
		}
		else if (text.Contains("net"))
		{
			canvas.Children.Add(new Ellipse
			{
				Width = 34.0,
				Height = 34.0,
				Stroke = stroke,
				StrokeThickness = 2.4,
				Fill = new SolidColorBrush(Color.FromArgb(24, 88, 183, byte.MaxValue))
			});
			UIElementCollection children = canvas.Children;
			Canvas.SetLeft(children[children.Count - 1], 7.0);
			UIElementCollection children2 = canvas.Children;
			Canvas.SetTop(children2[children2.Count - 1], 4.0);
			canvas.Children.Add(new System.Windows.Shapes.Path
			{
				Data = Geometry.Parse("M16,28 L16,15 L32,28 L32,15"),
				Stroke = stroke3,
				StrokeThickness = 2.2,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round,
				StrokeLineJoin = PenLineJoin.Round
			});
		}
		else if (text.Contains("x64"))
		{
			canvas.Children.Add(new System.Windows.Shapes.Path
			{
				Data = Geometry.Parse("M8,11 L40,11 L40,33 L8,33 Z M14,17 L21,22 L14,27 M25,28 L34,28"),
				Stroke = stroke,
				StrokeThickness = 2.4,
				Fill = new SolidColorBrush(Color.FromArgb(24, 88, 183, byte.MaxValue)),
				StrokeLineJoin = PenLineJoin.Round,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round
			});
		}
		else
		{
			canvas.Children.Add(new Ellipse
			{
				Width = 30.0,
				Height = 30.0,
				Stroke = stroke,
				StrokeThickness = 2.4,
				Fill = new SolidColorBrush(Color.FromArgb(24, 88, 183, byte.MaxValue))
			});
			UIElementCollection children3 = canvas.Children;
			Canvas.SetLeft(children3[children3.Count - 1], 7.0);
			UIElementCollection children4 = canvas.Children;
			Canvas.SetTop(children4[children4.Count - 1], 4.0);
			canvas.Children.Add(new System.Windows.Shapes.Path
			{
				Data = Geometry.Parse("M30,28 L41,38 M18,19 L30,19 M24,13 L24,25"),
				Stroke = stroke2,
				StrokeThickness = 2.4,
				StrokeStartLineCap = PenLineCap.Round,
				StrokeEndLineCap = PenLineCap.Round
			});
		}
		return canvas;
	}

	private void HighlightStepIcons()
	{
		foreach (Button value2 in _desktopIcons.Values)
		{
			value2.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 194, byte.MaxValue));
			value2.BorderThickness = new Thickness(1.0);
		}
		if (!_guideUnlocked || Current.Steps.Count == 0 || _stepIndex >= Current.Steps.Count)
		{
			return;
		}
		StageStep stageStep = Current.Steps[_stepIndex];
		foreach (string item in GuideRuntime.HostKeysFor(stageStep))
		{
			if (item.StartsWith("tool:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("target:", StringComparison.OrdinalIgnoreCase))
			{
				glow(item);
			}
		}
		if (!string.IsNullOrWhiteSpace(stageStep.Tool))
		{
			glow("tool:" + stageStep.Tool);
		}
		if (!string.IsNullOrWhiteSpace(stageStep.File))
		{
			glow("target:" + stageStep.File);
		}
		void glow(string key)
		{
			if (_desktopIcons.TryGetValue(key, out Button value))
			{
				value.BorderBrush = new SolidColorBrush(Color.FromRgb(byte.MaxValue, 200, 87));
				value.BorderThickness = new Thickness(2.0);
			}
		}
	}

	private void RefreshCoach()
	{
		if (_stages.Stages.Count != 0)
		{
			RefreshStepFallback();
			if (Current.GuideMode == "none")
			{
				CoachText.Text = ((Current.FreeExploreTextKey == null) ? CourseNoMoreGuideText() : _i18n.T(Current.FreeExploreTextKey));
				ActionButton.Visibility = Visibility.Collapsed;
				NextStepButton.Visibility = Visibility.Collapsed;
				HighlightStepIcons();
				UpdateOverlayForCurrentStep();
			}
			else if (!_guideUnlocked)
			{
				CoachText.Text = ((Current.FreeExploreTextKey == null) ? CourseNoMoreGuideText() : _i18n.T(Current.FreeExploreTextKey));
				ActionButton.Visibility = Visibility.Collapsed;
				NextStepButton.Content = _i18n.T("common.fallback_next");
				NextStepButton.Visibility = Visibility.Visible;
				HighlightStepIcons();
				UpdateOverlayForCurrentStep();
			}
			else if (Current.Steps.Count == 0 || _stepIndex >= Current.Steps.Count)
			{
				CoachText.Text = CourseNoMoreGuideText();
				ActionButton.Visibility = Visibility.Collapsed;
				NextStepButton.Visibility = Visibility.Collapsed;
				HighlightStepIcons();
				UpdateOverlayForCurrentStep();
			}
			else
			{
				StageStep step = Current.Steps[_stepIndex];
				CoachText.Text = StepText(step);
				ActionButton.Visibility = Visibility.Collapsed;
				NextStepButton.Content = _i18n.T("common.fallback_next");
				NextStepButton.Visibility = Visibility.Visible;
				HighlightStepIcons();
				UpdateOverlayForCurrentStep();
			}
		}
	}

	private string StepText(StageStep step)
	{
		string text = _i18n.T(step.TextKey);
		if (!string.IsNullOrWhiteSpace(step.BodyKey))
		{
			string text2 = _i18n.T(step.BodyKey);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				text = (string.IsNullOrWhiteSpace(text) ? text2 : (text + Environment.NewLine + Environment.NewLine + text2));
			}
		}
		return text;
	}

	private void RefreshStepFallback()
	{
		_activeDeviationFallbackId = null;
		string text = Current.CompletionMode.Trim().ToLowerInvariant();
		bool flag = text == "external_submit";
		bool flag2 = text == "answer";
		FlagSubmissionPanel.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
		ExternalCompletionPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		if (flag)
		{
			ExternalCompletionText.Text = TextOr("common.external_submit", "找到 flag 后，请回到比赛平台提交。指北不会替你提交答案。");
		}
		if (Current.Steps.Count == 0)
		{
			StepArticleText.Text = ((Current.FreeExploreTextKey == null) ? CourseNoMoreGuideText() : _i18n.T(Current.FreeExploreTextKey));
			StepImage.Source = null;
			StepImageFrame.Visibility = Visibility.Collapsed;
			return;
		}
		int index = Math.Clamp(_stepIndex, 0, Current.Steps.Count - 1);
		StageStep stageStep = Current.Steps[index];
		StepArticleText.Text = StepText(stageStep);
		StepImage.Source = TryLoadStepImage(stageStep.Image);
		StepMediaEmptyText.Visibility = ((StepImage.Source != null) ? Visibility.Collapsed : Visibility.Visible);
		StepImageFrame.Visibility = (string.IsNullOrWhiteSpace(stageStep.Image) ? Visibility.Collapsed : Visibility.Visible);
	}

	private ImageSource? TryLoadStepImage(string? rawPath)
	{
		if (string.IsNullOrWhiteSpace(rawPath))
		{
			return null;
		}
		string text = rawPath.Replace('/', System.IO.Path.DirectorySeparatorChar).TrimStart(System.IO.Path.DirectorySeparatorChar);
		List<string> list = new List<string>();
		if (System.IO.Path.IsPathRooted(rawPath))
		{
			list.Add(rawPath);
		}
		else
		{
			list.Add(System.IO.Path.Combine(_paths.AssetsDir, text));
			list.Add(System.IO.Path.Combine(_paths.ContentRoot, text));
			list.Add(System.IO.Path.Combine(_paths.RepoRoot, text));
			if (text.StartsWith("assets" + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				string assetsDir = _paths.AssetsDir;
				string text2 = text;
				int length = ("assets" + System.IO.Path.DirectorySeparatorChar).Length;
				list.Add(System.IO.Path.Combine(assetsDir, text2.Substring(length, text2.Length - length)));
			}
		}
		string text3 = list.Select(System.IO.Path.GetFullPath).FirstOrDefault(File.Exists);
		if (text3 == null)
		{
			return null;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.UriSource = new Uri(text3, UriKind.Absolute);
			bitmapImage.EndInit();
			((Freezable)bitmapImage).Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private void UpdateOverlayForCurrentStep()
	{
		if (_stages.Stages.Count == 0 || CourseDeskView.Visibility != 0)
		{
			return;
		}
		if (!_interactiveGuideEnabled)
		{
			HideProbeStatus();
			_hostGuideOverlay.Clear();
			_overlay.ClearGuide();
		}
		else
		{
			if (TryShowPendingExplorerGuide())
			{
				return;
			}
			if (Current.GuideMode == "none")
			{
				ShowGuide(_guide.Resolve(HostGuide("taskboard")), CoachText.Text, _i18n.T("common.coach_title"));
			}
			else if (!_guideUnlocked)
			{
				ShowGuide(_guide.Resolve(HostGuide("next", "left")), CoachText.Text, _i18n.T("common.coach_title"));
			}
			else if (Current.Steps.Count == 0 || _stepIndex >= Current.Steps.Count)
			{
				ShowGuide(_guide.Resolve(HostGuide("flag", "top")), CoachText.Text, _i18n.T("common.coach_title"));
			}
			else if (!TryShowCachedExternalScene())
			{
				StageStep step = Current.Steps[_stepIndex];
				if (UsesProcessGuide(step))
				{
					ShowProbeStatus();
					ShowUnanchoredExternalGuide();
				}
				else
				{
					HideProbeStatus();
					string title = _i18n.T("common.coach_title");
					ShowGuide(_guide.Resolve(step), CoachText.Text, title);
				}
			}
		}
	}

	private void ShowUnanchoredExternalGuide()
	{
		if (HasLoadedStage && CourseDeskView.Visibility == Visibility.Visible && Current.Steps.Count != 0 && _stepIndex >= 0 && _stepIndex < Current.Steps.Count && UsesProcessGuide(Current.Steps[_stepIndex]))
		{
			_hostGuideOverlay.Clear();
			StageStep stageStep = Current.Steps[_stepIndex];
			string title = _i18n.T("common.coach_title");
			_overlay.ShowGuide(null, CoachText.Text, title, stageStep.Guide?.Overlay);
		}
	}

	private bool TryShowCachedExternalScene()
	{
		GuideSceneMatch latestExternalScene = _latestExternalScene;
		if (latestExternalScene == null || !latestExternalScene.RenderPlan.BoundsScreen.HasValue)
		{
			return false;
		}
		if (latestExternalScene.StageIndex != _stageIndex)
		{
			return false;
		}
		if (!latestExternalScene.IsDeviation && latestExternalScene.StepIndex != _stepIndex)
		{
			return false;
		}
		if (!IsReusableExternalScene(latestExternalScene))
		{
			return false;
		}
		if (!IsRenderableExternalScene(latestExternalScene))
		{
			return false;
		}
		ShowExternalScene(latestExternalScene);
		return true;
	}

	private void ShowProbeStatus()
	{
		ProbeStatusBadge.Visibility = Visibility.Visible;
	}

	private void HideProbeStatus()
	{
		ProbeStatusBadge.Visibility = Visibility.Collapsed;
	}

	private static bool UsesProcessGuide(StageStep step)
	{
		GuideSpec spec = step.Guide ?? GuideRuntime.BuildLegacyGuide(step);
		if (GuideSpecHelpers.NormalizeScope(spec.Target.Scope) == "process")
		{
			return true;
		}
		return spec.Locators.Any((GuideLocatorSpec locator) => GuideSpecHelpers.NormalizeScope(locator.Scope ?? spec.Target.Scope) == "process");
	}

	private void ActionButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (HasLoadedStage && _guideUnlocked && _stepIndex < Current.Steps.Count)
		{
			StageStep step = Current.Steps[_stepIndex];
			if (!ApplyGuideBranch(step, "action"))
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(UpdateOverlayForCurrentStep), (DispatcherPriority)4, Array.Empty<object>());
			}
		}
	}

	private void NextStepButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (!HasLoadedStage)
		{
			return;
		}
		if (!_guideUnlocked)
		{
			_guideUnlocked = true;
			_stepIndex = 0;
			AdvanceGuideRevision();
			RefreshCoach();
		}
		else
		{
			if (_stepIndex < Current.Steps.Count && ApplyGuideBranch(Current.Steps[_stepIndex], "next"))
			{
				return;
			}
			if (_stepIndex < Current.Steps.Count - 1)
			{
				_stepIndex++;
				AdvanceGuideRevision();
				RefreshCoach();
			}
			else if (!string.Equals(Current.CompletionMode, "answer", StringComparison.OrdinalIgnoreCase) && _stageIndex < _stages.Stages.Count - 1)
			{
				CourseProgressData courseProgressData = CurrentProgress();
				if (!courseProgressData.ClearedStages.Contains(Current.Id))
				{
					courseProgressData.ClearedStages.Add(Current.Id);
				}
				_stageIndex++;
				courseProgressData.StageIndex = _stageIndex;
				courseProgressData.StageId = _stages.Stages[_stageIndex].Id;
				_progress.MirrorLegacy(courseProgressData);
				_progress.Save();
				LoadStage();
			}
			else
			{
				_stepIndex = Current.Steps.Count;
				AdvanceGuideRevision();
				RefreshCoach();
				ShowCourseCompletionIfFinished();
			}
		}
	}

	private void ShowCourseCompletionIfFinished()
	{
		if (HasLoadedStage && _stageIndex == _stages.Stages.Count - 1 && _stepIndex >= Current.Steps.Count)
		{
			HideProbeStatus();
			_hostGuideOverlay.Clear();
			_overlay.ClearGuide();
			CourseCompletionContactText.Text = CourseContactText();
			CourseCompletionOverlay.Opacity = 0.0;
			CourseCompletionOverlay.Visibility = Visibility.Visible;
			CourseCompletionOverlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(240.0)));
		}
	}

	private void ResetButton_OnClick(object sender, RoutedEventArgs e)
	{
		_progress.ResetCourse(_activeCourse?.Id ?? _progress.Data.ActiveCourseId, _activeCourse?.Version, _activeCourse?.FlowFingerprint);
		_stageIndex = 0;
		LoadStage();
		FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 192));
		FeedbackText.Text = _i18n.T("common.reset_progress");
	}

	private void SubmitButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (!HasLoadedStage)
		{
			return;
		}
		string text = _i18n.GetRaw(Current.AnswerKey) ?? string.Empty;
		HashSet<string> hashSet = (from k in Current.FakeAnswerKeys
			select _i18n.GetRaw(k) into x
			where !string.IsNullOrWhiteSpace(x)
			select (x)).ToHashSet<string>(StringComparer.Ordinal);
		FlagSubmission flagSubmission = NormalizeFlagSubmission(FlagInput.Text ?? string.Empty, text, hashSet);
		string value = flagSubmission.Value;
		if (flagSubmission.AutoTrimmed)
		{
			FlagInput.Text = value;
		}
		if (string.Equals(value, text, StringComparison.Ordinal))
		{
			CourseProgressData courseProgressData = CurrentProgress();
			FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(111, 220, 140));
			string text2 = (flagSubmission.AutoTrimmed ? TextOr("common.flag_extra_trimmed", "已自动去掉多余内容，识别到正确 flag。") : ((Current.Id == "final") ? _i18n.T("final.cleared") : _i18n.T("common.correct")));
			FeedbackText.Text = text2;
			if (!courseProgressData.ClearedStages.Contains(Current.Id))
			{
				courseProgressData.ClearedStages.Add(Current.Id);
			}
			if (_stageIndex < _stages.Stages.Count - 1)
			{
				_stageIndex++;
				courseProgressData.StageIndex = _stageIndex;
				courseProgressData.StageId = _stages.Stages[_stageIndex].Id;
				_progress.MirrorLegacy(courseProgressData);
				_progress.Save();
				LoadStage();
				FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(111, 220, 140));
				FeedbackText.Text = text2;
			}
			else
			{
				courseProgressData.StageIndex = _stageIndex;
				courseProgressData.StageId = Current.Id;
				_progress.MirrorLegacy(courseProgressData);
				_progress.Save();
				_stepIndex = Current.Steps.Count;
				AdvanceGuideRevision();
				RefreshCoach();
				ShowCourseCompletionIfFinished();
			}
		}
		else if (hashSet.Contains(value))
		{
			if (flagSubmission.AutoTrimmed)
			{
				FlagInput.Text = value;
			}
			FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(byte.MaxValue, 180, 84));
			FeedbackText.Text = _i18n.T("common.fake_flag");
			if ((_stepIndex >= Current.Steps.Count || !ApplyGuideBranch(Current.Steps[_stepIndex], "fake_flag")) && Current.GuideMode == "on_fake_or_next")
			{
				_guideUnlocked = true;
				_stepIndex = 0;
				RefreshCoach();
			}
		}
		else
		{
			FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(byte.MaxValue, 123, 123));
			FeedbackText.Text = _i18n.T("common.wrong");
			if (_stepIndex < Current.Steps.Count)
			{
				ApplyGuideBranch(Current.Steps[_stepIndex], "wrong_flag");
			}
		}
	}

	private static FlagSubmission NormalizeFlagSubmission(string rawInput, string answer, IReadOnlySet<string> fakeAnswers)
	{
		string text = (rawInput ?? string.Empty).Trim();
		if (!string.IsNullOrWhiteSpace(answer))
		{
			if (string.Equals(text, answer, StringComparison.Ordinal))
			{
				return new FlagSubmission(answer, AutoTrimmed: false);
			}
			if (text.Contains(answer, StringComparison.Ordinal))
			{
				return new FlagSubmission(answer, AutoTrimmed: true);
			}
		}
		foreach (string fakeAnswer in fakeAnswers)
		{
			if (string.Equals(text, fakeAnswer, StringComparison.Ordinal))
			{
				return new FlagSubmission(fakeAnswer, AutoTrimmed: false);
			}
			if (text.Contains(fakeAnswer, StringComparison.Ordinal))
			{
				return new FlagSubmission(fakeAnswer, AutoTrimmed: true);
			}
		}
		Match match = Regex.Match(text, "(?:flag|moectf)\\{[^}\\r\\n]{1,160}\\}", RegexOptions.IgnoreCase);
		if (!match.Success || match.Value.Length >= text.Length)
		{
			return new FlagSubmission(text, AutoTrimmed: false);
		}
		return new FlagSubmission(match.Value, AutoTrimmed: true);
	}

	private void ShowResourceHint(bool isTool, string resourceId)
	{
		FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 192));
		if (isTool)
		{
			string text = _requirements.FindToolExecutable(resourceId);
			FeedbackText.Text = ((text == null) ? _i18n.T("common.tool_missing", new Dictionary<string, string> { ["tool"] = _tools.FormatLabel(resourceId) }) : _i18n.T("common.tool_required", new Dictionary<string, string>
			{
				["tool"] = _tools.FormatLabel(resourceId),
				["path"] = text
			}));
			RefreshTrackedWindows();
		}
		else
		{
			string text2 = _paths.FindTarget(resourceId);
			FeedbackText.Text = ((text2 == null) ? _i18n.T("common.target_missing", new Dictionary<string, string> { ["file"] = resourceId }) : _i18n.T("common.target_required", new Dictionary<string, string>
			{
				["file"] = resourceId,
				["path"] = text2
			}));
			RefreshTrackedWindows();
		}
	}

	private void OpenTargetFolderResource(string fileName)
	{
		FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 192));
		string text = _paths.FindTarget(fileName);
		if (text == null)
		{
			FeedbackText.Text = _i18n.T("common.target_missing", new Dictionary<string, string> { ["file"] = fileName });
			RefreshTrackedWindows();
			return;
		}
		try
		{
			_pendingExplorerTargetPath = text;
			_pendingExplorerGuideUntilUtc = DateTime.UtcNow.AddSeconds(18.0);
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "/select,\"" + text + "\"",
				UseShellExecute = true
			});
			FeedbackText.Text = _i18n.T("common.target_folder_opened", new Dictionary<string, string>
			{
				["file"] = fileName,
				["tool"] = CurrentToolLabel(),
				["path"] = text
			});
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				if (!TryShowPendingExplorerGuide())
				{
					UpdateOverlayForCurrentStep();
				}
			}, (DispatcherPriority)3, Array.Empty<object>());
		}
		catch (Exception ex)
		{
			_pendingExplorerTargetPath = null;
			FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(byte.MaxValue, 123, 123));
			FeedbackText.Text = _i18n.T("common.target_folder_failed", new Dictionary<string, string>
			{
				["file"] = fileName,
				["error"] = ex.Message
			});
		}
		RefreshTrackedWindows();
	}

	private void OpenToolResource(string toolId)
	{
		FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 192));
		ToolSpec toolSpec = _tools.Get(toolId);
		ManagedProcess managedProcess;
		lock (_processAccessLock)
		{
			managedProcess = (_processes.Processes.TryGetValue("tool:" + toolId, out ManagedProcess value) ? value : null);
		}
		if (managedProcess != null)
		{
			IReadOnlyList<WindowInfo> readOnlyList = _tracker.FindWindowsForManaged(managedProcess);
			if (readOnlyList.Count > 0)
			{
				_tracker.TryActivate(readOnlyList[0]);
				FeedbackText.Text = _i18n.T("common.tool_activated", new Dictionary<string, string> { ["tool"] = _tools.FormatLabel(toolId) });
				RefreshTrackedWindows();
				return;
			}
		}
		string text = _requirements.FindToolExecutable(toolId);
		if (text == null)
		{
			FeedbackText.Text = _i18n.T("common.tool_missing", new Dictionary<string, string> { ["tool"] = _tools.FormatLabel(toolId) });
			RefreshTrackedWindows();
			return;
		}
		try
		{
			Process process = Process.Start(new ProcessStartInfo
			{
				FileName = text,
				WorkingDirectory = (System.IO.Path.GetDirectoryName(text) ?? _paths.ToolsDir),
				UseShellExecute = string.Equals(System.IO.Path.GetExtension(text), ".bat", StringComparison.OrdinalIgnoreCase)
			});
			if (process != null && toolSpec != null)
			{
				ManagedProcess managedProcess2;
				lock (_processAccessLock)
				{
					managedProcess2 = _processes.ObserveExternal("tool:" + toolSpec.Id, process, text, toolSpec.Id, toolSpec.WindowTitleHints, toolSpec.ProcessNameHints);
				}
				if (managedProcess2?.Process != process)
				{
					process.Dispose();
				}
			}
			FeedbackText.Text = _i18n.T("common.tool_launching", new Dictionary<string, string> { ["tool"] = _tools.FormatLabel(toolId) });
		}
		catch (Exception ex)
		{
			FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(byte.MaxValue, 123, 123));
			FeedbackText.Text = _i18n.T("common.tool_launch_failed", new Dictionary<string, string>
			{
				["tool"] = _tools.FormatLabel(toolId),
				["error"] = ex.Message
			});
		}
		RefreshTrackedWindows();
	}

	private bool TryShowPendingExplorerGuide()
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		if (!_interactiveGuideEnabled)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(_pendingExplorerTargetPath))
		{
			return false;
		}
		if (DateTime.UtcNow > _pendingExplorerGuideUntilUtc)
		{
			_pendingExplorerTargetPath = null;
			return false;
		}
		string pendingExplorerTargetPath = _pendingExplorerTargetPath;
		string fileName = System.IO.Path.GetFileName(pendingExplorerTargetPath);
		WindowInfo windowInfo = FindExplorerWindowForTarget(pendingExplorerTargetPath);
		if (windowInfo == null)
		{
			return false;
		}
		ControlMatch controlMatch = _locator.FindTextControlMatch(windowInfo.Hwnd, new string[1] { fileName ?? "" }, 1000, 500, requireAllHints: false, "uia", null);
		Rect? boundsScreen = controlMatch?.Bounds;
		GuideAttachmentState attachment = GuideAttachmentState.Control;
		List<string> list = new List<string> { "explorer-window" };
		if (boundsScreen.HasValue)
		{
			Rect value = boundsScreen.Value;
			if (!(((Rect)(ref value)).Width < 2.0))
			{
				value = boundsScreen.Value;
				if (!(((Rect)(ref value)).Height < 2.0))
				{
					list.Add("explorer:file-item:" + controlMatch.Provider);
					goto IL_0133;
				}
			}
		}
		boundsScreen = windowInfo.Bounds;
		attachment = GuideAttachmentState.Window;
		list.Add("explorer:file-item-fallback-window");
		goto IL_0133;
		IL_0133:
		GuideRenderPlan obj = new GuideRenderPlan
		{
			Layer = GuideLayerKind.Screen,
			BoundsScreen = boundsScreen
		};
		NormalizedRect? boundsInWindow;
		if (boundsScreen.HasValue)
		{
			Rect valueOrDefault = boundsScreen.GetValueOrDefault();
			boundsInWindow = NormalizedRect.FromScreen(valueOrDefault, windowInfo.Bounds).Clamp();
		}
		else
		{
			boundsInWindow = null;
		}
		obj.BoundsInWindow = boundsInWindow;
		obj.TargetWindow = windowInfo;
		obj.Overlay = new GuideOverlaySpec
		{
			Placement = "right",
			Alignment = "center",
			Padding = 8.0,
			Gap = 14.0,
			MaxWidth = 480.0
		};
		obj.Attachment = attachment;
		obj.Score = 120;
		obj.CoordinateSpace = "explorer";
		obj.Evidence = list;
		GuideRenderPlan plan = obj;
		ShowGuide(plan, _i18n.T("common.explorer_target_guide", new Dictionary<string, string>
		{
			["file"] = fileName,
			["tool"] = CurrentToolLabel()
		}), _i18n.T("common.coach_title"));
		return true;
	}

	private WindowInfo? FindExplorerWindowForTarget(string targetPath)
	{
		string folder = System.IO.Path.GetDirectoryName(targetPath) ?? string.Empty;
		WindowInfo windowInfo = FindExplorerWindowByShellFolder(folder);
		if (windowInfo != null)
		{
			return windowInfo;
		}
		string folderName = System.IO.Path.GetFileName(folder.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
		return (from x in (from w in _tracker.EnumerateVisibleWindows()
				where string.Equals(w.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase) || string.Equals(w.ClassName, "CabinetWClass", StringComparison.OrdinalIgnoreCase)
				where (!string.IsNullOrWhiteSpace(folderName) && w.Title.IndexOf(folderName, StringComparison.OrdinalIgnoreCase) >= 0) || (!string.IsNullOrWhiteSpace(folder) && w.Title.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0)
				select new
				{
					Window = w,
					Score = ScoreExplorerWindow(w, folder, folderName)
				} into x
				where x.Score > 0
				orderby x.Score descending
				select x).ThenByDescending(x =>
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_000b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0019: Unknown result type (might be due to invalid IL or missing references)
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				Rect bounds = x.Window.Bounds;
				double width = ((Rect)(ref bounds)).Width;
				bounds = x.Window.Bounds;
				return width * ((Rect)(ref bounds)).Height;
			})
			select x.Window).FirstOrDefault();
	}

	private WindowInfo? FindExplorerWindowByShellFolder(string folder)
	{
		string text = NormalizeFsPath(folder);
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		object obj = null;
		object obj2 = null;
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("Shell.Application");
			if ((object)typeFromProgID == null)
			{
				return null;
			}
			obj = Activator.CreateInstance(typeFromProgID);
			if (obj == null)
			{
				return null;
			}
			obj2 = typeFromProgID.InvokeMember("Windows", BindingFlags.InvokeMethod, null, obj, Array.Empty<object>());
			if (!(obj2 is IEnumerable enumerable))
			{
				return null;
			}
			Dictionary<nint, WindowInfo> dictionary = (from w in _tracker.EnumerateVisibleWindows()
				where string.Equals(w.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase) || string.Equals(w.ClassName, "CabinetWClass", StringComparison.OrdinalIgnoreCase)
				select w).ToDictionary((WindowInfo w) => w.Hwnd, (WindowInfo w) => w);
			foreach (object item in enumerable)
			{
				nint num = TryReadShellHwnd(item);
				if (num != IntPtr.Zero && string.Equals(NormalizeFsPath(TryReadShellFolderPath(item)), text, StringComparison.OrdinalIgnoreCase) && dictionary.TryGetValue(num, out var value))
				{
					return value;
				}
			}
		}
		catch
		{
			return null;
		}
		finally
		{
			TryReleaseCom(obj2);
			TryReleaseCom(obj);
		}
		return null;
	}

	private static nint TryReadShellHwnd(object window)
	{
		try
		{
			object obj = window.GetType().InvokeMember("HWND", BindingFlags.GetProperty, null, window, null);
			return (obj is int value) ? new IntPtr(value) : ((obj is long value2) ? new IntPtr(value2) : IntPtr.Zero);
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	private static string TryReadShellFolderPath(object window)
	{
		try
		{
			object obj = window.GetType().InvokeMember("Document", BindingFlags.GetProperty, null, window, null);
			object obj2 = obj?.GetType().InvokeMember("Folder", BindingFlags.GetProperty, null, obj, null);
			object obj3 = obj2?.GetType().InvokeMember("Self", BindingFlags.GetProperty, null, obj2, null);
			string text = obj3?.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, obj3, null) as string;
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		catch
		{
		}
		try
		{
			string text2 = window.GetType().InvokeMember("LocationURL", BindingFlags.GetProperty, null, window, null) as string;
			if (!string.IsNullOrWhiteSpace(text2) && Uri.TryCreate(text2, UriKind.Absolute, out Uri result) && result.IsFile)
			{
				return result.LocalPath;
			}
		}
		catch
		{
		}
		return string.Empty;
	}

	private static string NormalizeFsPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		try
		{
			return System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
		}
		catch
		{
			return path.Trim().TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
		}
	}

	private static void TryReleaseCom(object? value)
	{
		try
		{
			if (value != null && Marshal.IsComObject(value))
			{
				Marshal.FinalReleaseComObject(value);
			}
		}
		catch
		{
		}
	}

	private static ImageSource? TryExtractIcon(string? path)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return null;
		}
		nint[] array = new nint[1] { IntPtr.Zero };
		nint[] array2 = new nint[1] { IntPtr.Zero };
		try
		{
			if (ExtractIconEx(path, 0, array, array2, 1) <= 0)
			{
				return null;
			}
			nint num = ((array2[0] != IntPtr.Zero) ? array2[0] : array[0]);
			if (num == IntPtr.Zero)
			{
				return null;
			}
			BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(num, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
			((Freezable)bitmapSource).Freeze();
			return bitmapSource;
		}
		catch
		{
			return null;
		}
		finally
		{
			if (array2[0] != IntPtr.Zero)
			{
				DestroyIcon(array2[0]);
			}
			if (array[0] != IntPtr.Zero)
			{
				DestroyIcon(array[0]);
			}
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int ExtractIconEx(string szFileName, int nIconIndex, nint[]? phiconLarge, nint[]? phiconSmall, int nIcons);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool DestroyIcon(nint hIcon);

	private static int ScoreExplorerWindow(WindowInfo window, string folder, string folderName)
	{
		int num = 0;
		if (string.Equals(window.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
		{
			num += 30;
		}
		if (string.Equals(window.ClassName, "CabinetWClass", StringComparison.OrdinalIgnoreCase))
		{
			num += 30;
		}
		if (window.IsForeground)
		{
			num += 60;
		}
		if (!string.IsNullOrWhiteSpace(folderName) && window.Title.IndexOf(folderName, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			num += 36;
		}
		if (!string.IsNullOrWhiteSpace(folder) && window.Title.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			num += 40;
		}
		return num;
	}

	private string CurrentToolLabel()
	{
		if (!HasLoadedStage)
		{
			return _i18n.T("desktop.tools");
		}
		if (Current.Steps.Count > 0 && _stepIndex >= 0 && _stepIndex < Current.Steps.Count)
		{
			string tool = Current.Steps[_stepIndex].Tool;
			if (!string.IsNullOrWhiteSpace(tool))
			{
				return _tools.FormatLabel(tool);
			}
		}
		string text = Current.Tools.FirstOrDefault();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return _tools.FormatLabel(text);
		}
		return _i18n.T("desktop.tools");
	}

	private void RefreshTrackedWindows()
	{
		if (!_interactiveGuideEnabled)
		{
			TaskbarTrackText.Text = TextOr("common.interactive_mode_disabled_short", "交互式指引已关闭");
		}
		else if (_stages == null || _stages.Stages.Count == 0 || CourseDeskView.Visibility != 0)
		{
			TaskbarTrackText.Text = _i18n.T("common.tracked_none");
		}
		else
		{
			StartTrackingScanIfIdle();
		}
	}

	private void StartTrackingScanIfIdle()
	{
		if (_interactiveGuideEnabled)
		{
			StageDefinition[] stages = _stages.Stages.ToArray();
			if (CurrentStepNeedsProbe())
			{
				ShowProbeStatus();
			}
			GuideTrackingRequest request = new GuideTrackingRequest(stages, Current, _stageIndex, _stepIndex, Volatile.Read(in _guideRevision));
			_guideTracking.RequestScan(request, CurrentStepUsesExternalGuide(), OnOverlayScanCompleted, OnRecognitionCompleted);
		}
	}

	private void OnOverlayScanCompleted(GuideOverlayScanResult result)
	{
		if (((DispatcherObject)this).Dispatcher.HasShutdownStarted)
		{
			return;
		}
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			if (_interactiveGuideEnabled && result.Revision == Volatile.Read(in _guideRevision) && CourseDeskView.Visibility == Visibility.Visible)
			{
				ApplyOverlayTrackingScanResult(result);
				if (CurrentStepNeedsProbe())
				{
					GuideOverlayMatch? overlay = result.Overlay;
					if ((object)overlay == null || !overlay.Scene.RenderPlan.IsPreciseExternal)
					{
						return;
					}
				}
				HideProbeStatus();
			}
		}, (DispatcherPriority)4, Array.Empty<object>());
	}

	private void OnRecognitionCompleted(GuideRecognitionMatch recognition, long revision)
	{
		if (((DispatcherObject)this).Dispatcher.HasShutdownStarted)
		{
			return;
		}
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			if (_interactiveGuideEnabled && revision == Volatile.Read(in _guideRevision) && CourseDeskView.Visibility == Visibility.Visible)
			{
				TrySyncDetectedScene(recognition.Scene);
			}
		}, (DispatcherPriority)4, Array.Empty<object>());
	}

	private bool CurrentStepNeedsProbe()
	{
		if (_stages == null || _stages.Stages.Count == 0 || Current.Steps.Count == 0 || _stepIndex >= Current.Steps.Count)
		{
			return false;
		}
		if (_latestExternalScene != null && _latestExternalScene.StageIndex == _stageIndex && (_latestExternalScene.IsDeviation || _latestExternalScene.StepIndex == _stepIndex) && IsRenderableExternalScene(_latestExternalScene) && _latestExternalScene.RenderPlan.IsPreciseExternal)
		{
			return false;
		}
		return UsesProcessGuide(Current.Steps[_stepIndex]);
	}

	private bool CurrentStepUsesExternalGuide()
	{
		if (_stages == null || _stages.Stages.Count == 0 || Current.Steps.Count == 0 || _stepIndex >= Current.Steps.Count)
		{
			return false;
		}
		return UsesProcessGuide(Current.Steps[_stepIndex]);
	}

	private void ApplyOverlayTrackingScanResult(GuideOverlayScanResult result)
	{
		List<string> list = (from s in result.Windows.Select(FormatTrackedWindowLine)
			where s.Length > 0
			select s).ToList();
		TrackedWindowsText.Text = ((list.Count == 0) ? _i18n.T("common.tracked_none") : string.Join(Environment.NewLine, list));
		TaskbarTrackText.Text = ((list.Count == 0) ? _i18n.T("common.tracked_none") : list[0]);
		string obj = _latestExternalScene?.Deviation?.Id;
		GuideSceneMatch guideSceneMatch = result.Overlay?.Scene;
		_latestExternalScene = ((guideSceneMatch != null && SceneBelongsToCurrentGuide(guideSceneMatch) && IsRenderableExternalScene(guideSceneMatch)) ? guideSceneMatch : null);
		if (obj != null && _latestExternalScene?.Deviation == null)
		{
			RefreshStepFallback();
		}
		GuideSceneMatch latestExternalScene = _latestExternalScene;
		if (latestExternalScene == null)
		{
			if (CurrentStepNeedsProbe())
			{
				ShowProbeStatus();
			}
			ShowUnanchoredExternalGuide();
			return;
		}
		WindowInfo targetWindow = latestExternalScene.RenderPlan.TargetWindow;
		string value = latestExternalScene.Stage?.Id ?? Current.Id;
		string text = $"Scene: {value}/{latestExternalScene.Step.Id} score={latestExternalScene.Score}" + ((targetWindow == null) ? "" : (" process=" + targetWindow.ProcessName + " title=" + targetWindow.Title));
		list.Add(text);
		TrackedWindowsText.Text = string.Join(Environment.NewLine, list);
		TaskbarTrackText.Text = text;
		if (!TryShowPendingExplorerGuide() && SceneBelongsToCurrentGuide(latestExternalScene) && IsRenderableExternalScene(latestExternalScene))
		{
			ShowExternalScene(latestExternalScene);
		}
	}

	private string FormatTrackedWindowLine(TrackedWindowSnapshot snapshot)
	{
		if (snapshot.Waiting)
		{
			return _i18n.T("common.tracked_waiting", new Dictionary<string, string>
			{
				["id"] = snapshot.Id,
				["pid"] = snapshot.Pid?.ToString() ?? "?"
			});
		}
		return _i18n.T("common.tracked_line", new Dictionary<string, string>
		{
			["id"] = snapshot.Id,
			["pid"] = snapshot.Pid?.ToString() ?? "?",
			["hwnd"] = ((IntPtr)(snapshot.Hwnd?)).ToString() ?? "?",
			["title"] = snapshot.Title,
			["w"] = snapshot.Width.ToString(),
			["h"] = snapshot.Height.ToString()
		});
	}

	private void ShowExternalScene(GuideSceneMatch scene)
	{
		if (!_interactiveGuideEnabled || !SceneBelongsToCurrentGuide(scene) || !IsRenderableExternalScene(scene))
		{
			_overlay.ClearGuide();
			return;
		}
		UpdateProbeStatusForPlan(scene.RenderPlan);
		if (scene.Deviation != null && !string.Equals(_activeDeviationFallbackId, scene.Deviation.Id, StringComparison.OrdinalIgnoreCase))
		{
			_activeDeviationFallbackId = scene.Deviation.Id;
			StepArticleText.Text = StepText(scene.Step);
			StepImage.Source = TryLoadStepImage(scene.Step.Image);
			StepMediaEmptyText.Visibility = ((StepImage.Source != null) ? Visibility.Collapsed : Visibility.Visible);
			StepImageFrame.Visibility = (string.IsNullOrWhiteSpace(scene.Step.Image) ? Visibility.Collapsed : Visibility.Visible);
		}
		string text = ((scene.Deviation != null) ? _i18n.T(scene.Deviation.TextKey) : StepText(scene.Step));
		string title = _i18n.T("common.coach_title");
		ShowGuide(scene.RenderPlan, text, title);
	}

	private void UpdateProbeStatusForPlan(GuideRenderPlan plan)
	{
		if (plan.IsPreciseExternal)
		{
			HideProbeStatus();
		}
		else
		{
			ShowProbeStatus();
		}
	}

	private GuideSpec HostGuide(string key, string placement = "auto")
	{
		return new GuideSpec
		{
			Target = new GuideTargetSpec
			{
				Scope = "host",
				Key = key
			},
			Overlay = new GuideOverlaySpec
			{
				Placement = placement,
				Padding = 6.0
			}
		};
	}

	private void ShowGuide(GuideRenderPlan plan, string text, string title)
	{
		if (plan.Layer == GuideLayerKind.Host)
		{
			_lastActivatedGuideTarget = null;
			_overlay.ClearGuide();
			_hostGuideOverlay.Show(plan, text, title);
			return;
		}
		_hostGuideOverlay.Clear();
		if (plan.Layer == GuideLayerKind.Screen)
		{
			if (!GuideSceneMatcher.IsDisplayableExternalPlan(plan) || !TargetWindowStillVisible(plan))
			{
				_overlay.ClearGuide();
				return;
			}
			ActivateExternalGuideTargetOnce(plan);
			_overlay.ShowGuide(plan.BoundsScreen, text, title, plan.Overlay);
		}
		else
		{
			_overlay.ClearGuide();
		}
	}

	private void ActivateExternalGuideTargetOnce(GuideRenderPlan plan)
	{
		WindowInfo targetWindow = plan.TargetWindow;
		if (targetWindow != null)
		{
			string text = Current.Id + ":" + _stepIndex + ":" + (IntPtr)targetWindow.Hwnd;
			if (!string.Equals(_lastActivatedGuideTarget, text, StringComparison.Ordinal))
			{
				_lastActivatedGuideTarget = text;
				_tracker.TryActivate(targetWindow);
			}
		}
	}

	private bool ApplyGuideBranch(StageStep step, string trigger)
	{
		int? num = _guide.ResolveBranchIndex(Current, step, trigger);
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			_guideUnlocked = true;
			_stepIndex = valueOrDefault;
			AdvanceGuideRevision();
			RefreshCoach();
			return true;
		}
		return false;
	}

	private bool TrySyncDetectedScene(GuideSceneMatch scene)
	{
		if (!_interactiveGuideEnabled)
		{
			return false;
		}
		if (scene.StageIndex < 0 || scene.StageIndex >= _stages.Stages.Count)
		{
			return false;
		}
		if (!scene.IsExternal)
		{
			return false;
		}
		if (scene.IsDeviation)
		{
			return false;
		}
		if (!scene.IsStepTrigger || !GuideSceneMatcher.IsReliableExternalRecognition(scene))
		{
			return false;
		}
		if (scene.StageIndex == _stageIndex && scene.StepIndex < _stepIndex)
		{
			return false;
		}
		CourseProgressData courseProgressData = CurrentProgress();
		if (scene.StageIndex < MinimumActiveStageIndexFromClearedProgress(courseProgressData))
		{
			return false;
		}
		int stepIndex = ((!scene.IsDeviation) ? scene.StepIndex : ((_stageIndex == scene.StageIndex) ? _stepIndex : 0));
		GuideProgressPosition guideProgressPosition = GuideProgressionPolicy.SynchronizeToRecognition(scene.StageIndex, stepIndex, _stages.Stages[scene.StageIndex].Steps.Count);
		if (_stageIndex == guideProgressPosition.StageIndex && _stepIndex == guideProgressPosition.StepIndex && _guideUnlocked)
		{
			return false;
		}
		int stageIndex = _stageIndex;
		_stageIndex = guideProgressPosition.StageIndex;
		_stepIndex = guideProgressPosition.StepIndex;
		_guideUnlocked = true;
		AdvanceGuideRevision();
		courseProgressData.StageIndex = _stageIndex;
		courseProgressData.StageId = Current.Id;
		if (_stageIndex > stageIndex)
		{
			for (int i = stageIndex; i < _stageIndex; i++)
			{
				if (!courseProgressData.ClearedStages.Contains(_stages.Stages[i].Id))
				{
					courseProgressData.ClearedStages.Add(_stages.Stages[i].Id);
				}
			}
		}
		if (string.Equals(Current.CompletionMode, "external_submit", StringComparison.OrdinalIgnoreCase) && Current.Steps.Count > 0 && _stepIndex == Current.Steps.Count - 1 && !scene.IsDeviation && scene.IsStepTrigger && !courseProgressData.ClearedStages.Contains(Current.Id))
		{
			courseProgressData.ClearedStages.Add(Current.Id);
		}
		_progress.MirrorLegacy(courseProgressData);
		_progress.Save();
		RenderCurrentGuideState();
		return true;
	}

	private void RenderCurrentGuideState()
	{
		ProgressText.Text = _i18n.T("common.progress", new Dictionary<string, string>
		{
			["current"] = (_stageIndex + 1).ToString(),
			["total"] = _stages.Stages.Count.ToString()
		});
		StageTitleText.Text = _i18n.T(Current.TitleKey);
		StageGoalText.Text = _i18n.T(Current.GoalKey);
		TaskbarStageText.Text = StageTitleText.Text;
		RebuildDesktopIcons();
		RefreshCoach();
	}

	private bool SceneBelongsToCurrentGuide(GuideSceneMatch scene)
	{
		if (scene.StageIndex == _stageIndex)
		{
			if (!scene.IsDeviation)
			{
				return scene.StepIndex == _stepIndex;
			}
			return true;
		}
		return false;
	}

	private bool IsRenderableExternalScene(GuideSceneMatch scene)
	{
		if (GuideSceneMatcher.IsDisplayableExternalPlan(scene.RenderPlan))
		{
			return TargetWindowStillVisible(scene.RenderPlan);
		}
		return false;
	}

	private static bool IsReusableExternalScene(GuideSceneMatch scene)
	{
		return GuideSceneMatcher.IsDisplayableExternalPlan(scene.RenderPlan);
	}

	private bool TargetWindowStillVisible(GuideRenderPlan plan)
	{
		WindowInfo target = plan.TargetWindow;
		if (target == null)
		{
			return false;
		}
		try
		{
			return _windowSnapshots.GetSnapshot().Any((WindowInfo w) => w.Hwnd == target.Hwnd && w.Pid == target.Pid);
		}
		catch
		{
			return false;
		}
	}

	private string? GuideValue(string key)
	{
		return key.Trim().ToLowerInvariant() switch
		{
			"flag" => FlagInput.Text, 
			"feedback" => FeedbackText.Text, 
			"stage" => HasLoadedStage ? Current.Id : "", 
			_ => null, 
		};
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.2.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/MoeCTF.RE.Tutorial;V0.1.0.0;component/mainwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.2.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.2.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			WindowShellBorder = (Border)target;
			break;
		case 2:
			TitleText = (TextBlock)target;
			break;
		case 3:
			SubtitleText = (TextBlock)target;
			break;
		case 4:
			BackToHubButton = (Button)target;
			BackToHubButton.Click += BackToHubButton_OnClick;
			break;
		case 5:
			InteractiveModeToggle = (ToggleButton)target;
			InteractiveModeToggle.Click += InteractiveModeToggle_OnClick;
			break;
		case 6:
			((Button)target).Click += MinimizeButton_OnClick;
			break;
		case 7:
			((Button)target).Click += MaximizeButton_OnClick;
			break;
		case 8:
			((Button)target).Click += CloseButton_OnClick;
			break;
		case 9:
			CourseHubView = (Grid)target;
			break;
		case 10:
			HubEyebrowText = (TextBlock)target;
			break;
		case 11:
			HubTitleText = (TextBlock)target;
			break;
		case 12:
			HubIntroText = (TextBlock)target;
			break;
		case 13:
			CheckUpdateButton = (Button)target;
			CheckUpdateButton.Click += CheckUpdateButton_OnClick;
			break;
		case 14:
			SettingsButton = (Button)target;
			SettingsButton.Click += SettingsButton_OnClick;
			break;
		case 15:
			UpdateStatusText = (TextBlock)target;
			break;
		case 16:
			CourseListHeaderText = (TextBlock)target;
			break;
		case 17:
			CourseListPanel = (StackPanel)target;
			break;
		case 18:
			ActiveCourseCard = (Border)target;
			break;
		case 19:
			ActiveCourseIconHost = (ContentControl)target;
			break;
		case 20:
			ActiveCourseTitleText = (TextBlock)target;
			break;
		case 21:
			ActiveCourseSubtitleText = (TextBlock)target;
			break;
		case 22:
			ActiveCourseCompatibilityText = (TextBlock)target;
			break;
		case 23:
			ActiveCourseSummaryText = (TextBlock)target;
			break;
		case 24:
			ActiveCourseTrackTag = (R2STag)target;
			break;
		case 25:
			ActiveCourseVersionTag = (R2STag)target;
			break;
		case 26:
			CourseRouteCard = (Border)target;
			break;
		case 27:
			CourseRouteText = (TextBlock)target;
			break;
		case 28:
			CourseStageMetricLabelText = (TextBlock)target;
			break;
		case 29:
			CourseStageCountText = (TextBlock)target;
			break;
		case 30:
			CourseMinutesMetricLabelText = (TextBlock)target;
			break;
		case 31:
			CourseMinutesText = (TextBlock)target;
			break;
		case 32:
			CourseSourceMetricLabelText = (TextBlock)target;
			break;
		case 33:
			CourseSourceText = (TextBlock)target;
			break;
		case 34:
			CourseUpdateCard = (Border)target;
			break;
		case 35:
			CourseUpdateText = (TextBlock)target;
			break;
		case 36:
			RequirementCard = (Border)target;
			RequirementCard.PreviewDragEnter += RequirementsPanel_OnDragOver;
			RequirementCard.PreviewDragOver += RequirementsPanel_OnDragOver;
			RequirementCard.PreviewDrop += RequirementsPanel_OnDrop;
			break;
		case 37:
			RequirementGroupsPanel = (WrapPanel)target;
			break;
		case 38:
			RequirementStatusText = (TextBlock)target;
			break;
		case 39:
			EnterCourseButton = (Button)target;
			EnterCourseButton.Click += EnterCourseButton_OnClick;
			break;
		case 40:
			CourseDeskView = (Grid)target;
			break;
		case 41:
			DesktopCanvas = (Canvas)target;
			break;
		case 42:
			DragHintText = (TextBlock)target;
			break;
		case 43:
			ProgressText = (TextBlock)target;
			break;
		case 44:
			StageTitleText = (TextBlock)target;
			break;
		case 45:
			StageGoalText = (TextBlock)target;
			break;
		case 46:
			StepImageFrame = (Border)target;
			break;
		case 47:
			StepImage = (Image)target;
			break;
		case 48:
			StepMediaEmptyText = (TextBlock)target;
			break;
		case 49:
			StepArticleText = (TextBlock)target;
			break;
		case 50:
			TaskBoard = (Border)target;
			break;
		case 51:
			TaskHeader = (TextBlock)target;
			break;
		case 52:
			ResetButton = (Button)target;
			ResetButton.Click += ResetButton_OnClick;
			break;
		case 53:
			FlagSubmissionPanel = (Border)target;
			break;
		case 54:
			FlagInput = (TextBox)target;
			break;
		case 55:
			SubmitButton = (Button)target;
			SubmitButton.Click += SubmitButton_OnClick;
			break;
		case 56:
			ExternalCompletionPanel = (Border)target;
			break;
		case 57:
			ExternalCompletionText = (TextBlock)target;
			break;
		case 58:
			FeedbackText = (TextBlock)target;
			break;
		case 59:
			TrackedWindowsText = (TextBlock)target;
			break;
		case 60:
			CoachCard = (Border)target;
			break;
		case 61:
			CoachHeader = (TextBlock)target;
			break;
		case 62:
			CoachText = (TextBlock)target;
			break;
		case 63:
			ActionButton = (Button)target;
			ActionButton.Click += ActionButton_OnClick;
			break;
		case 64:
			NextStepButton = (Button)target;
			NextStepButton.Click += NextStepButton_OnClick;
			break;
		case 65:
			StartBadge = (TextBlock)target;
			break;
		case 66:
			TaskbarStageText = (TextBlock)target;
			break;
		case 67:
			TaskbarTrackText = (TextBlock)target;
			break;
		case 68:
			ProbeStatusBadge = (Border)target;
			break;
		case 69:
			TaskbarClockText = (TextBlock)target;
			break;
		case 70:
			InAppGuideLayer = (Canvas)target;
			break;
		case 71:
			InAppGuideHighlight = (Border)target;
			break;
		case 72:
			InAppGuideArrow = (Polygon)target;
			break;
		case 73:
			InAppGuideBubble = (Border)target;
			break;
		case 74:
			InAppGuideTitle = (TextBlock)target;
			break;
		case 75:
			InAppGuideText = (TextBlock)target;
			break;
		case 76:
			CourseCompletionOverlay = (Grid)target;
			break;
		case 77:
			CourseCompletionTitleText = (TextBlock)target;
			break;
		case 78:
			CourseCompletionBodyText = (TextBlock)target;
			break;
		case 79:
			CourseCompletionContactText = (TextBlock)target;
			break;
		case 80:
			CourseCompletionBackButton = (Button)target;
			CourseCompletionBackButton.Click += CourseCompletionBackButton_OnClick;
			break;
		case 81:
			SettingsModalOverlay = (Grid)target;
			break;
		case 82:
			SettingsTitleText = (TextBlock)target;
			break;
		case 83:
			SettingsSubtitleText = (TextBlock)target;
			break;
		case 84:
			((Button)target).Click += CloseSettingsButton_OnClick;
			break;
		case 85:
			ToolSettingsPanel = (StackPanel)target;
			break;
		case 86:
			AboutTextBlock = (TextBlock)target;
			break;
		case 87:
			((Button)target).Click += CloseSettingsButton_OnClick;
			break;
		case 88:
			CourseEntryNoticeOverlay = (Grid)target;
			break;
		case 89:
			CourseEntryNoticeTitleText = (TextBlock)target;
			break;
		case 90:
			CourseEntryNoticeBodyText = (TextBlock)target;
			break;
		case 91:
			CourseEntryNoticeFallbackText = (TextBlock)target;
			break;
		case 92:
			CancelCourseEntryButton = (Button)target;
			CancelCourseEntryButton.Click += CancelCourseEntryButton_OnClick;
			break;
		case 93:
			ConfirmCourseEntryButton = (Button)target;
			ConfirmCourseEntryButton.Click += ConfirmCourseEntryButton_OnClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
