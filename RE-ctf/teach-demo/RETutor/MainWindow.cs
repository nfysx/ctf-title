using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RETutor.Guide;
using RETutor.Models;
using RETutor.Services;

namespace RETutor;

/// <summary>
/// 仿制版外壳。视觉对齐原程序：左侧课程栏 + 右侧课程页（hero / 指标 / 更新记录 / 开始课程条）
/// + 底部状态栏；进入课程后切到 Desk（关卡列表 + 步骤 + 资源卡片）。
/// </summary>
public sealed class MainWindow : Window
{
    // ---- 主题 token（取自原程序 ret2shelltokens.baml）----
    private static readonly Brush Layer = Rgb(0x1A, 0x1A, 0x1A);
    private static readonly Brush LayerDeep = Rgb(0x10, 0x11, 0x12);
    private static readonly Brush Raised = Rgb(0x26, 0x26, 0x26);
    private static readonly Brush Ink = Rgb(0xFF, 0xFD, 0xFD);
    private static readonly Brush Muted = Rgb(0xA6, 0xA6, 0xA6);
    private static readonly Brush Faint = Rgb(0x66, 0x66, 0x66);
    private static readonly Brush Primary = Rgb(0x1E, 0x9D, 0xE0);
    private static readonly Brush Success = Rgb(0x2D, 0xBE, 0x76);
    private static readonly Brush Warning = Rgb(0xE8, 0x79, 0x00);
    private static readonly Brush SoftBorder = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

    // ---- 服务 ----
    private readonly ContentStore _content;
    private readonly ResourceRegistry _registry;
    private readonly string _registryPath = ResourceRegistry.DefaultPath;
    private readonly ProgressStore _progress;
    private readonly WindowTracker _tracker = new();
    private readonly UiaLocator _uia = new();
    private readonly OverlayWindow _overlay = new();
    private readonly GuideEngine _engine;
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, FrameworkElement> _hotspots = new(StringComparer.OrdinalIgnoreCase);

    // ---- 状态 ----
    private LoadedCourse _course;
    private int _stageIndex;
    private int _stepIndex;
    private bool _interactive = true;
    private int _stable;
    private string _lastTriggerKey = "";
    private string _stageListCourseId;
    private bool _fillingStages;
    private readonly UpdateService _updates = new();
    private CourseUpdateManifest _pendingCourseUpdate;
    private Button _updateLink;
    private bool _updateBusy;
    private bool _inDesk;

    // ---- 壳 ----
    private ContentControl _host;
    private TextBlock _statusLeft, _statusHint, _clockText;
    private Border _autoPill;
    private Grid _rootGrid;
    private Border _settingsOverlay;
    private StackPanel _toolRows;

    // ---- Hub ----
    private StackPanel _courseListPanel;
    private WrapPanel _hubCards;
    private TextBlock _heroTitle, _heroSubtitle, _heroCompat, _heroSummary, _heroEyebrow, _hubHint;
    private Button _startButton;
    private TextBlock _metricStages, _metricMinutes, _metricSource;

    // ---- Desk ----
    private ListBox _stageList;
    private TextBlock _stageTitle, _stageGoal, _stepTitle, _stepBody, _hint;
    private TextBlock _finishTitle, _finishBody;
    private Image _stepImage;
    private Button _prevBtn, _nextBtn;
    private Border _finishPanel;
    private WrapPanel _deskCards;

    public MainWindow()
    {
        Width = 1280; Height = 820; MinWidth = 1080; MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        Background = Layer;
        Foreground = Ink;
        FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });

        // 内容根优先级：显式环境变量 > 内嵌内容包（按指纹刷新）> 目录探测
        var envRoot = Environment.GetEnvironmentVariable("RETUTOR_CONTENT");
        string contentRoot = null;
        if (!string.IsNullOrWhiteSpace(envRoot) && File.Exists(Path.Combine(envRoot, "courses", "index.json")))
            contentRoot = envRoot;
        contentRoot ??= ContentInstaller.EnsureInstalled();
        contentRoot ??= ContentStore.ResolveRoot();
        if (string.IsNullOrEmpty(contentRoot) || !File.Exists(Path.Combine(contentRoot, "courses", "index.json")))
            contentRoot = ContentStore.ResolveRoot() ?? contentRoot ?? AppContext.BaseDirectory;
        _content = new ContentStore(contentRoot);
        _content.Load();
        UiStrings.Load(_content.Root);
        Title = S("app.title", "RETutor — MoeCTF 逆向工程指北（仿制版）");

        _registry = ResourceRegistry.Load(_registryPath);
        _progress = new ProgressStore(ProgressStore.DefaultPath);
        _engine = new GuideEngine(_tracker, _uia, key => _hotspots.TryGetValue(key, out var el) ? el : null);

        BuildShell();
        LoadCourses();
        ShowHub();

        _ticker.Tick += Timer_Tick;
        _ticker.Start();
        _clock.Tick += (_, _) => { if (_clockText != null) _clockText.Text = DateTime.Now.ToString("HH:mm:ss"); };
        _clock.Start();
        _clockText.Text = DateTime.Now.ToString("HH:mm:ss");

        Closed += (_, _) => { _ticker.Stop(); _clock.Stop(); _overlay.Close(); };
    }

    private static Brush Rgb(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
    private static string S(string key, string fallback) => UiStrings.T(key, fallback);
    private string C(string contentKey, string fallback) =>
        _course != null && _course.I18n.TryGetValue(contentKey, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

    // ================= 外壳 =================
    private void BuildShell()
    {
        var root = new Grid();
        _rootGrid = root;
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

        root.Children.Add(BuildTitleBar());

        _host = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        Grid.SetRow(_host, 1);
        root.Children.Add(_host);

        root.Children.Add(BuildStatusBar());
        root.Children.Add(BuildSettingsOverlay());
        Content = root;
    }

    private FrameworkElement BuildTitleBar()
    {
        var bar = new Border { Background = LayerDeep, BorderBrush = SoftBorder, BorderThickness = new Thickness(0, 0, 0, 1) };
        bar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new Border
        {
            Width = 26, Height = 26, CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x1E, 0x9D, 0xE0)),
            Child = new TextBlock { Text = "A", FontWeight = FontWeights.Bold, Foreground = Primary, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        left.Children.Add(new TextBlock { Text = S("app.title", "MoeCTF逆向工程指北"), FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        left.Children.Add(new TextBlock { Text = S("app.breadcrumb", "/  MoeCTF 2026 / Reverse"), FontSize = 12, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        _autoPill = new Border
        {
            CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 5, 12, 5),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x1E, 0x9D, 0xE0)),
            BorderBrush = Primary, BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "●  " + S("label.auto_guide", "交互指引"), FontSize = 12, Foreground = Primary }
        };
        _autoPill.MouseLeftButtonDown += (_, e) => { e.Handled = true; };   // 别让标题栏 DragMove 吃掉这次点击
        _autoPill.MouseLeftButtonUp += (_, e) => { e.Handled = true; ToggleInteractive(); };
        right.Children.Add(_autoPill);

        right.Children.Add(WindowButton("—", () => WindowState = WindowState.Minimized));
        right.Children.Add(WindowButton("▢", ToggleMaximize));
        right.Children.Add(WindowButton("✕", Close, danger: true));
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        bar.Child = grid;
        return bar;
    }

    private Button WindowButton(string glyph, Action onClick, bool danger = false)
    {
        var b = new Button
        {
            Content = glyph, Width = 40, Height = 30, Margin = new Thickness(2, 0, 0, 0),
            Background = Brushes.Transparent, Foreground = Muted, BorderThickness = new Thickness(0),
            FontSize = 12
        };
        b.Click += (_, _) => onClick();
        b.MouseEnter += (_, _) => { b.Background = danger ? new SolidColorBrush(Color.FromArgb(0x33, 0xE4, 0x5B, 0x5F)) : Raised; b.Foreground = Ink; };
        b.MouseLeave += (_, _) => { b.Background = Brushes.Transparent; b.Foreground = Muted; };
        return b;
    }

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void ToggleInteractive()
    {
        _interactive = !_interactive;
        _autoPill.Background = _interactive ? new SolidColorBrush(Color.FromArgb(0x33, 0x1E, 0x9D, 0xE0)) : Brushes.Transparent;
        ((TextBlock)_autoPill.Child).Foreground = _interactive ? Primary : Muted;
        ((TextBlock)_autoPill.Child).Text = (_interactive ? "●  " : "○  ") + S("label.auto_guide", "交互指引");
        if (!_interactive) _overlay.ClearGuide();
        SetStatus(S("status.auto_guide", "交互指引"), _interactive ? S("status.auto_guide_on", "已开启：引导会跟着工具窗口移动") : S("status.auto_guide_off", "交互指引已关闭。"));
    }

    private FrameworkElement BuildStatusBar()
    {
        var bar = new Border { Background = LayerDeep, BorderBrush = SoftBorder, BorderThickness = new Thickness(0, 1, 0, 0) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock { Text = S("status.pill", "课程进行中"), FontSize = 11.5, Foreground = Muted, FontWeight = FontWeights.SemiBold });
        _statusLeft = new TextBlock { Text = "", FontSize = 11.5, Foreground = Ink, Margin = new Thickness(10, 0, 10, 0) };
        _statusHint = new TextBlock { Text = S("status.tracked_none", "还没有识别到当前步骤需要的工具窗口。先打开对应工具，指引会在窗口出现后跟上。"), FontSize = 11.5, Foreground = Faint };
        left.Children.Add(_statusLeft);
        left.Children.Add(_statusHint);
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        _clockText = new TextBlock { Text = "--:--:--", FontSize = 11.5, Foreground = Muted, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_clockText, 1);
        grid.Children.Add(_clockText);

        bar.Child = grid;
        Grid.SetRow(bar, 2);
        return bar;
    }

    private void SetStatus(string left, string hint)
    {
        if (_statusLeft != null) _statusLeft.Text = left ?? "";
        if (_statusHint != null && hint != null) _statusHint.Text = hint;
    }

    // ================= Hub =================
    private FrameworkElement BuildHub()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // 左栏
        var side = new Border { Background = LayerDeep, BorderBrush = SoftBorder, BorderThickness = new Thickness(0, 0, 1, 0) };
        var sideGrid = new Grid { Margin = new Thickness(22, 20, 22, 16) };
        sideGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sideGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var sideTop = new StackPanel();
        _heroEyebrow = new TextBlock { Text = S("hub.eyebrow", "Reverse"), FontSize = 11.5, Foreground = Primary, FontWeight = FontWeights.Bold };
        sideTop.Children.Add(_heroEyebrow);
        sideTop.Children.Add(new TextBlock
        {
            Text = S("hub.title", "MoeCTF逆向工程指北"), FontSize = 26, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 6, 0, 10)
        });
        sideTop.Children.Add(new TextBlock
        {
            Text = S("hub.intro", "第一次做 Reverse，不知道工具怎么开、该看哪里？选一门课，跟着提示自己动手，一步步把 flag 找出来。比赛期间还会更新更多课程。"),
            FontSize = 13, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 21
        });
        sideTop.Children.Add(new TextBlock { Text = S("hub.course_list", "课程"), FontSize = 12, Foreground = Faint, Margin = new Thickness(0, 18, 0, 8) });
        _courseListPanel = new StackPanel();
        sideTop.Children.Add(_courseListPanel);
        Grid.SetRow(sideTop, 0);
        sideGrid.Children.Add(sideTop);

        var sideBottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        _updateLink = LinkButton(S("hub.check_update", "检查更新"), CheckForUpdates);
        sideBottom.Children.Add(_updateLink);
        sideBottom.Children.Add(LinkButton(S("settings.open", "设置"), ShowSettings));
        sideBottom.Children.Add(new TextBlock
        {
            Text = S("hub.update_text", "比赛期间还会发布新课程。检查更新后就能看到，已经完成的进度不会丢。"),
            FontSize = 11.5, Foreground = Faint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0)
        });
        Grid.SetRow(sideBottom, 2);
        sideGrid.Children.Add(sideBottom);

        side.Child = sideGrid;
        Grid.SetColumn(side, 0);
        grid.Children.Add(side);

        // 右栏
        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(34, 26, 34, 0)
        };
        var content = new StackPanel();

        var hero = new Grid();
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = new Border
        {
            Width = 58, Height = 58, CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x9D, 0xE0)),
            BorderBrush = Primary, BorderThickness = new Thickness(1.5),
            Child = new TextBlock
            {
                Text = ">_", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Primary,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(icon, 0);
        var iconWrap = new Border { Child = icon, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 20, 0) };
        hero.Children.Add(iconWrap);

        var heroText = new StackPanel();
        _heroTitle = new TextBlock { Text = "", FontSize = 30, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
        _heroSubtitle = new TextBlock { Text = "", FontSize = 13.5, Foreground = Muted, Margin = new Thickness(0, 8, 0, 6), TextWrapping = TextWrapping.Wrap };
        _heroCompat = new TextBlock { Text = "", FontSize = 12, Foreground = Success, Margin = new Thickness(0, 0, 0, 14) };
        _heroSummary = new TextBlock { Text = "", FontSize = 14, TextWrapping = TextWrapping.Wrap, LineHeight = 23 };
        heroText.Children.Add(_heroTitle);
        heroText.Children.Add(_heroSubtitle);
        heroText.Children.Add(_heroCompat);
        heroText.Children.Add(_heroSummary);
        Grid.SetColumn(heroText, 1);
        hero.Children.Add(heroText);
        content.Children.Add(hero);

        content.Children.Add(new Border { Height = 1, Background = SoftBorder, Margin = new Thickness(0, 22, 0, 20) });

        content.Children.Add(new TextBlock { Text = S("hub.route_header", "课程内容"), FontSize = 15, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock
        {
            Text = S("hub.route_text", "先认识工具，再跟着线索读懂程序，最后亲手找到 flag。"),
            FontSize = 13, Foreground = Muted, Margin = new Thickness(0, 8, 0, 16), TextWrapping = TextWrapping.Wrap
        });

        var metricsRow = new Grid();
        metricsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metricsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metricsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _metricStages = MetricCard(metricsRow, 0, S("hub.stage_count", "关卡"), "3");
        _metricMinutes = MetricCard(metricsRow, 1, S("hub.minutes", "预计分钟"), "35");
        _metricSource = MetricCard(metricsRow, 2, S("hub.source", "来源"), S("hub.source_builtin", "随程序提供"));

        content.Children.Add(metricsRow);

        scroll.Content = content;
        Grid.SetRow(scroll, 0);
        right.Children.Add(scroll);

        // 底部：开始课程条（附件 + 工具卡片 + 开始按钮）
        var bottom = new Border
        {
            Background = LayerDeep, BorderBrush = SoftBorder, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(34, 16, 34, 14)
        };
        var bottomGrid = new Grid();
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var bottomLeft = new StackPanel();
        bottomLeft.Children.Add(new TextBlock { Text = S("hub.enter_course", "开始课程"), FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        _hubCards = new WrapPanel { Orientation = Orientation.Horizontal };
        bottomLeft.Children.Add(_hubCards);
        _hubHint = new TextBlock
        {
            Text = S("hub.requirements_missing", "把缺少的文件或工具拖到这里，也可以点击对应项选择。"),
            FontSize = 11.5, Foreground = Faint, Margin = new Thickness(0, 10, 0, 0)
        };
        bottomLeft.Children.Add(_hubHint);
        Grid.SetColumn(bottomLeft, 0);
        bottomGrid.Children.Add(bottomLeft);

        _startButton = new Button
        {
            Content = S("hub.enter_course", "开始课程"),
            MinWidth = 150, Height = 42, Margin = new Thickness(24, 28, 0, 0),
            Background = Primary, Foreground = Brushes.White, BorderThickness = new Thickness(0),
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Top
        };
        _startButton.Click += (_, _) => ShowDesk();
        Grid.SetColumn(_startButton, 1);
        bottomGrid.Children.Add(_startButton);

        bottom.Child = bottomGrid;
        Grid.SetRow(bottom, 1);
        right.Children.Add(bottom);

        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private TextBlock MetricCard(Grid parent, int column, string label, string value)
    {
        var card = new Border
        {
            Background = Raised, CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 14, 0)
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = label, FontSize = 11.5, Foreground = Muted });
        var valueBlock = new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        stack.Children.Add(valueBlock);
        card.Child = stack;
        Grid.SetColumn(card, column);
        parent.Children.Add(card);
        return valueBlock;
    }

    private Button LinkButton(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text, HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent, Foreground = Ink, BorderThickness = new Thickness(0),
            Padding = new Thickness(0), Margin = new Thickness(0, 6, 0, 6), FontSize = 13
        };
        b.Click += (_, _) => onClick();
        b.MouseEnter += (_, _) => b.Foreground = Primary;
        b.MouseLeave += (_, _) => b.Foreground = Ink;
        return b;
    }

    // ================= Desk =================
    private FrameworkElement BuildDesk()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var side = new Border { Background = LayerDeep, BorderBrush = SoftBorder, BorderThickness = new Thickness(0, 0, 1, 0) };
        var sideStack = new StackPanel { Margin = new Thickness(18, 16, 18, 12) };
        var back = LinkButton(S("hub.back", "← 返回课程"), ShowHub);
        sideStack.Children.Add(back);
        sideStack.Children.Add(new TextBlock { Text = S("label.stages", "关卡"), FontSize = 12, Foreground = Faint, Margin = new Thickness(0, 14, 0, 8) });
        _stageList = new ListBox
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = Ink, FontSize = 13, Padding = new Thickness(0)
        };
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        itemStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 0, 4)));
        _stageList.ItemContainerStyle = itemStyle;
        _stageList.SelectionChanged += (_, _) =>
        {
            if (_fillingStages) return;                       // 填充列表时不要反过来触发 LoadStage
            if (_stageList.SelectedIndex >= 0) LoadStage(_stageList.SelectedIndex);
        };
        sideStack.Children.Add(_stageList);
        side.Child = new ScrollViewer { Content = sideStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(side, 0);
        grid.Children.Add(side);

        var right = new StackPanel { Margin = new Thickness(28, 22, 28, 12) };
        _stageTitle = new TextBlock { FontSize = 24, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
        _stageGoal = new TextBlock { FontSize = 13, Foreground = Muted, Margin = new Thickness(0, 8, 0, 16), TextWrapping = TextWrapping.Wrap };
        _stepTitle = new TextBlock { FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8), TextWrapping = TextWrapping.Wrap };
        _stepBody = new TextBlock { FontSize = 13.5, TextWrapping = TextWrapping.Wrap, LineHeight = 23 };
        _stepImage = new Image { Margin = new Thickness(0, 12, 0, 0), MaxHeight = 300, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Height = 360 };
        var stepStack = new StackPanel();
        stepStack.Children.Add(_stepTitle);
        stepStack.Children.Add(_stepBody);
        stepStack.Children.Add(_stepImage);
        scroll.Content = stepStack;

        right.Children.Add(_stageTitle);
        right.Children.Add(_stageGoal);
        right.Children.Add(scroll);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        _prevBtn = MakeButton(S("btn.prev", "上一步"), (_, _) => MoveStep(-1));
        _nextBtn = MakeButton(S("btn.next", "下一步"), (_, _) => MoveStep(1));
        buttons.Children.Add(_prevBtn);
        buttons.Children.Add(_nextBtn);
        right.Children.Add(buttons);

        right.Children.Add(new TextBlock { Text = S("res.title", "本课程需要"), FontSize = 12, Foreground = Faint, Margin = new Thickness(0, 16, 0, 8) });
        _deskCards = new WrapPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(_deskCards);

        _hint = new TextBlock { FontSize = 12, Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(0x7D, 0xD3, 0xFF)) };
        right.Children.Add(_hint);

        _finishTitle = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Success };
        _finishBody = new TextBlock { FontSize = 13.5, TextWrapping = TextWrapping.Wrap, LineHeight = 22, Margin = new Thickness(0, 8, 0, 0) };
        var finishStack = new StackPanel();
        finishStack.Children.Add(_finishTitle);
        finishStack.Children.Add(_finishBody);
        _finishPanel = new Border
        {
            Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(14), CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x2D, 0xBE, 0x76)),
            BorderBrush = Success, BorderThickness = new Thickness(1),
            Visibility = Visibility.Collapsed, Child = finishStack
        };
        right.Children.Add(_finishPanel);

        _hotspots["coach"] = _hint;
        _hotspots["next"] = _nextBtn;

        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private static Button MakeButton(string text, RoutedEventHandler onClick)
    {
        var b = new Button
        {
            Content = text, MinWidth = 104, Height = 34, Margin = new Thickness(0, 0, 10, 0),
            Background = Raised, Foreground = Ink, BorderThickness = new Thickness(0), FontSize = 13
        };
        b.Click += onClick;
        return b;
    }

    // ================= 视图切换 =================
    private FrameworkElement _hubView;
    private FrameworkElement _deskView;

    private void ShowHub()
    {
        if (_hubView == null) _hubView = BuildHub();
        _inDesk = false;
        _host.Content = _hubView;
        _overlay.ClearGuide();
        RefreshHub();
        SetStatus(_course?.Manifest.Metadata.Title ?? "", S("status.hub", "选好课程，点右下角「开始课程」。"));
    }

    private void ShowDesk()
    {
        if (_course == null) return;
        if (_course.Stages.Count == 0) { SetStatus(S("hub.empty_title", "还没有课程"), S("hub.empty_summary", "新课程发布后会出现在这里。")); return; }
        if (_deskView == null) _deskView = BuildDesk();
        _inDesk = true;
        _host.Content = _deskView;
        LoadStage(_stageIndex);
        SetStatus(_course.Manifest.Metadata.Title, S("status.desk", "跟着提示操作；识别到你的进度会自动前进。"));
    }

    // ================= 课程 =================
    private void LoadCourses()
    {
        if (_content.Courses.Count == 0)
        {
            SetStatus(S("hub.empty_title", "还没有课程"), S("status.no_content", "没有找到课程。把内容包放到 RETutorContent/ 或设置 RETUTOR_CONTENT。当前：") + _content.Root);
            return;
        }
        _course = _content.Courses[0];
    }

    private void RefreshHub()
    {
        if (_hubView == null) return;
        _courseListPanel.Children.Clear();
        foreach (var c in _content.Courses)
            _courseListPanel.Children.Add(MakeCourseTile(c));

        if (_course == null) return;
        var m = _course.Manifest;
        _heroEyebrow.Text = S("hub.eyebrow", string.IsNullOrWhiteSpace(m.Metadata.Track) ? "Reverse" : m.Metadata.Track);
        _heroTitle.Text = m.Metadata.Title;
        _heroSubtitle.Text = m.Metadata.Subtitle;
        var compat = m.Compatibility;
        _heroCompat.Text = compat?.MinHostVersion != null && compat?.MaxHostVersionExclusive != null
            ? S("hub.compat", "兼容程序版本 ") + ">= " + compat.MinHostVersion + ", < " + compat.MaxHostVersionExclusive
            : S("hub.compat_none", "未声明版本区间");
        _heroSummary.Text = m.Metadata.Summary;
        _metricStages.Text = _course.Stages.Count.ToString();
        _metricMinutes.Text = m.Metadata.EstimatedMinutes > 0 ? m.Metadata.EstimatedMinutes.ToString() : "-";
        _metricSource.Text = S("hub.source_builtin", "随程序提供");

        RefreshCards(_hubCards);
        UpdateStartButton();
    }

    private FrameworkElement MakeCourseTile(LoadedCourse c)
    {
        bool active = _course != null && string.Equals(_course.Manifest.Id, c.Manifest.Id, StringComparison.OrdinalIgnoreCase);
        var card = new Border
        {
            Background = active ? Raised : new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            BorderBrush = active ? Primary : SoftBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = new Border
        {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x9D, 0xE0)),
            Child = new TextBlock { Text = ">_", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Primary, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        text.Children.Add(new TextBlock { Text = c.Manifest.Metadata.Title, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock
        {
            Text = c.Manifest.Metadata.Track + " / " + c.Manifest.Metadata.Difficulty + " / " + c.Stages.Count + S("hub.stage_count", "关卡"),
            FontSize = 11.5, Foreground = Muted, Margin = new Thickness(0, 4, 0, 2)
        });
        text.Children.Add(new TextBlock
        {
            Text = S("hub.compat", "兼容程序版本 ") + ">= " + (c.Manifest.Compatibility?.MinHostVersion ?? "-"),
            FontSize = 11, Foreground = Faint
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        card.Child = grid;
        card.MouseLeftButtonUp += (_, _) =>
        {
            _course = c;
            _stageIndex = 0; _stepIndex = 0;
            RefreshHub();
            SetStatus(c.Manifest.Metadata.Title, S("hub.requirements_ready", "都准备好了，开始吧。"));
        };
        return card;
    }

    private void UpdateStartButton()
    {
        if (_startButton == null) return;
        bool ready = _course != null && _course.Stages.Count > 0;
        _startButton.IsEnabled = ready;
        _startButton.Opacity = ready ? 1 : 0.5;
    }

    // ================= 资源卡片 =================
    private void RefreshCards(WrapPanel panel)
    {
        if (panel == null || _course == null) return;
        panel.Children.Clear();
        var stage = _inDesk ? Current : null;
        foreach (var item in EnumerateRequirements())
            panel.Children.Add(MakeResourceCard(item.id, item.label, item.kind, stage));
    }

    private List<(string id, string label, string kind, string declaredSha)> EnumerateRequirements()
    {
        var items = new List<(string, string, string, string)>();
        var req = _course?.Manifest.Requirements;
        void Walk(RequirementNode n)
        {
            if (n == null) return;
            if (string.Equals(n.Kind, "tool", StringComparison.OrdinalIgnoreCase))
                items.Add((n.ToolId ?? n.Id, string.IsNullOrWhiteSpace(n.Label) ? (ToolCatalog.Get(n.ToolId)?.DisplayName ?? n.ToolId) : n.Label, "tool", null));
            else if (string.Equals(n.Kind, "target", StringComparison.OrdinalIgnoreCase))
                items.Add((n.TargetId ?? n.Id, string.IsNullOrWhiteSpace(n.Label) ? (n.TargetId ?? n.Id) : n.Label, "target", n.Match?.Sha256));
            foreach (var c in n.AllOf ?? new List<RequirementNode>()) Walk(c);
            foreach (var c in n.AnyOf ?? new List<RequirementNode>()) Walk(c);
        }
        if (req != null)
        {
            foreach (var n in req.AllOf ?? new List<RequirementNode>()) Walk(n);
            foreach (var n in req.AnyOf ?? new List<RequirementNode>()) Walk(n);
        }
        if (items.Count == 0 && Current != null)
        {
            foreach (var t in Current.Tools) items.Add((t, ToolCatalog.Get(t)?.DisplayName ?? t, "tool", null));
            foreach (var t in Current.Targets) items.Add((t, t, "target", null));
        }
        return items;
    }

    private FrameworkElement MakeResourceCard(string id, string label, string kind, StageDefinition stage)
    {
        bool isTool = kind == "tool";
        string path = isTool ? ResolveToolPath(id) : ResolveTargetPath(id);
        bool ready = !string.IsNullOrEmpty(path) && File.Exists(path);

        string statusText;
        Brush statusBrush;
        if (!ready) { statusText = S("res.missing", "未配置"); statusBrush = Warning; }
        else if (isTool) { statusText = S("res.ready", "已就绪"); statusBrush = Success; }
        else
        {
            var rec = _registry.GetTarget(id);
            var real = rec != null && string.Equals(rec.Path, path, StringComparison.OrdinalIgnoreCase) ? rec.Sha256 : ResourceRegistry.Sha256Of(path);
            var declared = DeclaredSha(id);
            // 以本地文件为准：记录里存的就是本地文件的哈希，显示前 8 位便于核对
            statusText = S("res.ready", "已就绪") + (real.Length >= 8 ? "  " + real.Substring(0, 8) : "");
            statusBrush = Success;
            if (!string.IsNullOrEmpty(declared) && !string.IsNullOrEmpty(real) &&
                !string.Equals(declared, real, StringComparison.OrdinalIgnoreCase))
                statusText += S("res.local_badge", "（本地）");
        }

        bool usedHere = stage != null &&
            ((isTool && stage.Tools.Any(t => string.Equals(t, id, StringComparison.OrdinalIgnoreCase))) ||
             (!isTool && stage.Targets.Any(t => string.Equals(t, id, StringComparison.OrdinalIgnoreCase))));

        var card = new Border
        {
            Width = 176, Height = 102, Margin = new Thickness(0, 0, 12, 10),
            CornerRadius = new CornerRadius(10),
            Background = Raised, BorderBrush = usedHere ? Primary : SoftBorder,
            BorderThickness = new Thickness(usedHere ? 1.5 : 1),
            Cursor = Cursors.Hand, AllowDrop = true,
            ToolTip = ready ? BuildReadyTooltip(path, isTool, id) : S("res.pick_tip", "点击选择文件，或把文件拖到这张卡片上")
        };
        var stack = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        stack.Children.Add(new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x1E, 0x9D, 0xE0)), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = isTool ? (ToolCatalog.Get(id)?.Glyph ?? "EXE") : "FILE",
                FontSize = isTool ? 10.5 : 9, FontWeight = FontWeights.Bold, Foreground = Primary,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        });
        stack.Children.Add(new TextBlock
        {
            Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = usedHere ? Primary : Ink
        });
        stack.Children.Add(new TextBlock { Text = statusText, FontSize = 10.5, Foreground = statusBrush, TextTrimming = TextTrimming.CharacterEllipsis });
        card.Child = stack;

        card.MouseEnter += (_, _) => card.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        card.MouseLeave += (_, _) => card.Background = Raised;
        card.MouseLeftButtonUp += (_, _) => PickOrOpen(isTool, id, label);
        card.Drop += (_, e) => HandleDrop(e, isTool, id);
        card.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        return card;
    }

    private string BuildReadyTooltip(string path, bool isTool, string id)
    {
        var tip = path;
        if (!isTool)
        {
            var real = ResourceRegistry.Sha256Of(path);
            var declared = DeclaredSha(id);
            tip += "\nsha256(本地) = " + real;
            if (!string.IsNullOrEmpty(declared))
                tip += "\nsha256(课程声明) = " + declared + "\n（不一致时以本地文件为准）";
        }
        return tip;
    }

    private string DeclaredSha(string targetId)
    {
        foreach (var item in EnumerateRequirements())
            if (item.kind == "target" && string.Equals(item.id, targetId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(item.declaredSha))
                return item.declaredSha;
        return null;
    }

    private string ResolveToolPath(string toolId)
    {
        var configured = _registry.GetTool(toolId);
        if (configured != null) return configured;
        return ToolCatalog.FindBundledExecutable(_content.Root, toolId);
    }

    private string ResolveTargetPath(string targetId)
    {
        var rec = _registry.GetTarget(targetId);
        if (rec != null && !string.IsNullOrWhiteSpace(rec.Path) && File.Exists(rec.Path)) return rec.Path;
        if (string.IsNullOrWhiteSpace(targetId) || _course == null) return null;
        var courseId = _course.Manifest.Id;
        var candidates = new[]
        {
            Path.Combine(_content.Root, "assets", "targets", targetId),
            Path.Combine(_content.Root, "targets", targetId),
            Path.Combine(_content.Root, "courses", courseId, "assets", "targets", targetId),
            Path.Combine(_content.Root, "courses", courseId, "assets", targetId),
        };
        foreach (var c in candidates)
            try { if (File.Exists(c)) return c; } catch { }
        return null;
    }

    private void PickResource(bool isTool, string id, string label)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = (isTool ? S("res.pick_tool", "选择工具可执行文件：") : S("res.pick_target", "选择题目附件：")) + label,
            CheckFileExists = true,
            Filter = isTool ? "可执行文件|*.exe;*.bat;*.cmd|所有文件|*.*" : "所有文件|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        if (isTool) RegisterTool(id, dlg.FileName); else RegisterTarget(id, dlg.FileName);
    }

    private void HandleDrop(DragEventArgs e, bool isTool, string id)
    {
        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var file = files?.FirstOrDefault();
            if (string.IsNullOrEmpty(file)) return;
            if (isTool) RegisterTool(id, file); else RegisterTarget(id, file);
        }
        catch (Exception ex) { SetStatus("", S("hint.tool_failed", "启动失败：") + ex.Message); }
    }

    private void RegisterTool(string id, string path)
    {
        _registry.SetTool(id, path);
        _registry.Save(_registryPath);
        _hint?.SetCurrentValue(TextBlock.TextProperty, S("res.tool_saved", "已记住工具路径：") + path);
        RefreshAllCards();
        SetStatus(_course?.Manifest.Metadata.Title ?? "", S("res.tool_saved", "已记住工具路径：") + path);
    }

    private void RegisterTarget(string id, string path)
    {
        var sha = ResourceRegistry.Sha256Of(path);
        long size = 0;
        try { size = new FileInfo(path).Length; } catch { }

        // 尽量规范化：复制到 courses/<id>/assets/targets/ 下（内容根可写时）
        var destination = path;
        try
        {
            var dir = Path.Combine(_content.Root, "courses", _course.Manifest.Id, "assets", "targets");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, id);
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                File.Copy(path, dest, overwrite: true);
            destination = dest;
        }
        catch { }

        var declared = DeclaredSha(id);
        _registry.SetTarget(id, new TargetRecord
        {
            Path = destination,
            Sha256 = ResourceRegistry.Sha256Of(destination),
            Size = size,
            DeclaredSha256 = declared ?? ""
        });
        _registry.Save(_registryPath);

        var msg = S("res.target_saved", "已记下题目附件：") + destination + "  ·  sha256 " + sha.Substring(0, Math.Min(16, sha.Length));
        if (!string.IsNullOrEmpty(declared) && !string.Equals(declared, sha, StringComparison.OrdinalIgnoreCase))
            msg += "  ·  " + S("res.hash_warn", "与课程声明的哈希不同，已按本地文件为准");
        _hint?.SetCurrentValue(TextBlock.TextProperty, msg);
        RefreshAllCards();
        SetStatus(_course?.Manifest.Metadata.Title ?? "", msg);
    }

    private void RefreshAllCards()
    {
        RefreshCards(_hubCards);
        RefreshCards(_deskCards);
    }

    private void LaunchTool(string toolId)
    {
        var exe = ResolveToolPath(toolId);
        if (string.IsNullOrEmpty(exe))
        {
            SetStatus("", S("hint.tool_missing", "没有找到 ") + toolId + S("res.pick_tip2", "：点卡片选择可执行文件，或拖到卡片上。"));
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? _content.Root,
                UseShellExecute = exe.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            });
            SetStatus("", S("hint.tool_started", "已启动 ") + toolId + "：" + exe);
        }
        catch (Exception ex) { SetStatus("", S("hint.tool_failed", "启动失败：") + ex.Message); }
    }

    private void OpenTargetFolder(string targetId)
    {
        var path = ResolveTargetPath(targetId);
        if (string.IsNullOrEmpty(path))
        {
            SetStatus("", S("hint.target_missing", "没有找到题目附件 ") + targetId + S("res.pick_tip2", "：点卡片选择可执行文件，或拖到卡片上。"));
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + path + "\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex) { SetStatus("", S("hint.tool_failed", "启动失败：") + ex.Message); }
    }

    // ================= 设置浮层 =================
    private FrameworkElement BuildSettingsOverlay()
    {
        _settingsOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xB3, 0x00, 0x00, 0x00)),
            Visibility = Visibility.Collapsed
        };
        Grid.SetRowSpan(_settingsOverlay, 3);
        Panel.SetZIndex(_settingsOverlay, 100);

        var card = new Border
        {
            Width = 760, MaxHeight = 680,
            Background = Rgb(0x1A, 0x1A, 0x1A),
            BorderBrush = SoftBorder, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var cardGrid = new Grid { Margin = new Thickness(28, 24, 28, 22) };
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 头部
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headText = new StackPanel();
        headText.Children.Add(new TextBlock { Text = S("settings.title", "设置"), FontSize = 24, FontWeight = FontWeights.Bold });
        headText.Children.Add(new TextBlock
        {
            Text = S("settings.subtitle", "这里会记住你电脑上的工具位置，换课程时不用重新选择。"),
            FontSize = 12.5, Foreground = Muted, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(headText, 0);
        head.Children.Add(headText);

        var closeBtn = new Button
        {
            Content = "✕", Width = 40, Height = 40, FontSize = 13,
            Background = Raises(), Foreground = Muted, BorderThickness = new Thickness(0)
        };
        closeBtn.Click += (_, _) => HideSettings();
        Grid.SetColumn(closeBtn, 1);
        head.Children.Add(closeBtn);
        Grid.SetRow(head, 0);
        cardGrid.Children.Add(head);

        // 滚动内容
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 20, 0, 0) };
        var stack = new StackPanel();

        // 工具路径
        var toolsSection = new Border { Background = Raised, CornerRadius = new CornerRadius(10), Padding = new Thickness(18, 16, 18, 10) };
        var toolsStack = new StackPanel();
        toolsStack.Children.Add(new TextBlock { Text = S("settings.tools_header", "工具路径"), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        _toolRows = new StackPanel();
        toolsStack.Children.Add(_toolRows);
        toolsSection.Child = toolsStack;
        stack.Children.Add(toolsSection);

        // 关于
        var aboutSection = new Border { Background = Raised, CornerRadius = new CornerRadius(10), Padding = new Thickness(18, 16, 18, 16), Margin = new Thickness(0, 14, 0, 0) };
        var aboutStack = new StackPanel();
        aboutStack.Children.Add(new TextBlock { Text = S("about.header", "关于"), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        aboutStack.Children.Add(AboutLine(S("about.title", "MoeCTF逆向工程指北") + " " + S("about.version", "0.1.0（仿制版）")));
        aboutStack.Children.Add(AboutLine(S("about.author", "制作：@nkanf（原版）；仿制：RETutor")));
        aboutStack.Children.Add(new Border { Height = 10 });
        aboutStack.Children.Add(AboutLine(S("about.design", "界面设计参考 ret2shell/ret2shell，并按 WPF 重新实现。")));
        aboutStack.Children.Add(AboutLine(S("about.font", "字体：Microsoft YaHei UI、Segoe UI（原版使用 JetBrains Mono / OFL）。")));
        aboutSection.Child = aboutStack;
        stack.Children.Add(aboutSection);

        scroll.Content = stack;
        Grid.SetRow(scroll, 1);
        cardGrid.Children.Add(scroll);

        // 底部
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var doneBtn = new Button
        {
            Content = S("settings.done", "完成"), MinWidth = 110, Height = 38,
            Background = Primary, Foreground = Brushes.White, BorderThickness = new Thickness(0),
            FontSize = 13.5, FontWeight = FontWeights.SemiBold
        };
        doneBtn.Click += (_, _) => HideSettings();
        footer.Children.Add(doneBtn);
        Grid.SetRow(footer, 2);
        cardGrid.Children.Add(footer);

        card.Child = cardGrid;
        _settingsOverlay.Child = new Grid { Children = { card } };
        _settingsOverlay.MouseLeftButtonDown += (_, e) => { if (ReferenceEquals(e.OriginalSource, _settingsOverlay)) HideSettings(); };
        return _settingsOverlay;
    }

    private static Brush Raises() => new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

    private TextBlock AboutLine(string text) => new()
    {
        Text = text, FontSize = 12.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2)
    };

    private void SetUpdateHint(string text)
    {
        if (_hubHint != null) _hubHint.Text = text;
        SetStatus(S("hub.check_update", "检查更新"), text);
    }

    private async void CheckForUpdates()
    {
        if (_updateBusy) return;
        _updateBusy = true;
        try
        {
            // 已经有待安装的课程更新 → 这次点击就是"安装"
            if (_pendingCourseUpdate != null) { await InstallCourseUpdateAsync(_pendingCourseUpdate); return; }

            var channel = _updates.Channel;
            if (!channel.HasAnyEndpoint)
            {
                SetUpdateHint(S("update.no_channel", "未配置更新源（只使用内嵌内容包）。可在 exe 同级放 update-config.json 指定自己的渠道。"));
                return;
            }

            SetUpdateHint(S("update.checking", "正在检查更新…"));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            var hostUrl = channel.AppUpdateUrl;
            var courseTask = _course != null
                ? _updates.CheckCourseAsync(_course, channel.CourseUpdateUrl, cts.Token)
                : Task.FromResult(new CheckResult { Error = "no-course" });
            var hostTask = _updates.CheckHostAsync(hostUrl, cts.Token);
            var courseRes = await courseTask;
            var hostRes = await hostTask;

            var lines = new List<string>();

            if (_course == null)
                lines.Add(S("update.no_course", "没有课程可检查"));
            else if (!courseRes.Reachable)
                lines.Add(S("update.source_unreachable", "更新源不可达") + "：" + Short(courseRes.Error));
            else if (courseRes.Available)
            {
                _pendingCourseUpdate = courseRes.Course;
                lines.Add(S("update.course_available", "课程有新版本 ") + courseRes.RemoteVersion +
                          S("update.local_suffix", "（本地 ") + courseRes.LocalVersion + "）");
                if (_updateLink != null) _updateLink.Content = S("update.install", "安装课程更新");
            }
            else
                lines.Add(S("update.course_latest", "课程已是最新 ") + courseRes.LocalVersion);

            if (hostRes.Reachable && !string.IsNullOrEmpty(hostRes.Channel) &&
                !string.Equals(hostRes.Channel, channel.Channel, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(S("update.channel_mismatch", "程序更新渠道不符（期望 ") + channel.Channel +
                          S("update.channel_got", "，清单是 ") + hostRes.Channel + S("update.channel_ignored", "），已忽略"));
            }
            else if (hostRes.Reachable && hostRes.Available)
            {
                lines.Add(S("update.host_available", "程序有新版本 ") + hostRes.RemoteVersion +
                          S("update.local_suffix", "（本地 ") + hostRes.LocalVersion + "）");
                lines.Add(S("update.host_downloading", "正在下载新程序…"));
                SetUpdateHint(string.Join("　·　", lines));
                var (ok, path, error) = await _updates.DownloadHostAsync(hostRes.Host, cts.Token);
                lines.RemoveAt(lines.Count - 1);
                lines.Add(ok
                    ? S("update.host_downloaded", "已下载并校验：") + path + S("update.host_manual", "（退出后手动替换）")
                    : S("update.host_download_failed", "程序更新下载失败：") + Short(error));
            }
            else if (hostRes.Reachable)
                lines.Add(S("update.host_latest", "程序已是最新 ") + VersionUtil.HostVersion);
            else if (!string.IsNullOrEmpty(hostRes.Error) && hostRes.Error != "no-manifest-url")
                lines.Add(S("update.host_unreachable", "程序更新源不可达") + "：" + Short(hostRes.Error));

            SetUpdateHint(string.Join("　·　", lines));
        }
        catch (Exception ex)
        {
            SetUpdateHint(S("update.failed", "更新失败：") + Short(ex.Message));
        }
        finally { _updateBusy = false; }
    }

    private async Task InstallCourseUpdateAsync(CourseUpdateManifest manifest)
    {
        var courseId = _course.Manifest.Id;
        var localVersion = _course.Manifest.Version;
        var total = manifest.Files?.Count ?? 0;
        try
        {
            SetUpdateHint(S("update.installing", "正在下载并校验 ") + "0/" + total + S("update.files", " 个文件…"));
            var progress = new Progress<string>(msg => SetUpdateHint(msg));
            var error = await Task.Run(async () =>
            {
                return await _updates.InstallCourseAsync(manifest, _content.Root, (done, count, path) =>
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        SetUpdateHint(S("update.installing", "正在下载并校验 ") + done + "/" + count + " · " + path)));
                }, CancellationToken.None);
            });

            if (error != null)
            {
                SetUpdateHint(S("update.failed", "更新失败：") + Short(error));
                return;
            }

            _pendingCourseUpdate = null;
            if (_updateLink != null) _updateLink.Content = S("hub.check_update", "检查更新");

            // 重新装载内容
            _content.Load();
            var reloaded = _content.Courses.FirstOrDefault(c => string.Equals(c.Manifest.Id, courseId, StringComparison.OrdinalIgnoreCase))
                           ?? _content.Courses.FirstOrDefault();
            _course = reloaded;
            _stageIndex = 0; _stepIndex = 0; _stageListCourseId = null;
            ShowHub();
            SetUpdateHint(S("update.installed", "课程更新完成：") + localVersion + " → " + manifest.CourseVersion +
                          S("update.progress_reset", "（课程版本变化，该课进度已重置）"));
        }
        catch (Exception ex)
        {
            SetUpdateHint(S("update.failed", "更新失败：") + Short(ex.Message));
        }
    }

    private static string Short(string s) => string.IsNullOrEmpty(s) ? "-" : (s.Length > 120 ? s.Substring(0, 120) + "…" : s);

    private void ShowSettings()
    {
        if (_settingsOverlay == null) return;
        RefreshToolRows();
        _settingsOverlay.Visibility = Visibility.Visible;
    }

    private void HideSettings()
    {
        if (_settingsOverlay != null) _settingsOverlay.Visibility = Visibility.Collapsed;
        RefreshAllCards();
    }

    private void RefreshToolRows()
    {
        if (_toolRows == null) return;
        _toolRows.Children.Clear();
        foreach (var spec in ToolCatalog.Specs)
        {
            var configured = _registry.GetTool(spec.Id);
            var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new Border
            {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x9D, 0xE0)),
                Child = new TextBlock
                {
                    Text = spec.Glyph, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Primary,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(icon, 0);
            row.Children.Add(icon);

            var text = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = spec.DisplayName, FontSize = 13.5, FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock
            {
                Text = configured ?? S("settings.tool_missing", "未配置"),
                FontSize = 11, Foreground = configured != null ? Success : Warning,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 430
            });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var pick = LinkButton(S("settings.choose", "选择"), () => { PickToolFor(spec.Id); RefreshToolRows(); });
            pick.Margin = new Thickness(0, 0, 16, 0);
            var clear = LinkButton(S("settings.clear", "清除"), () =>
            {
                _registry.Tools.Remove(spec.Id);
                _registry.Save(_registryPath);
                RefreshToolRows();
                RefreshAllCards();
            });
            clear.Foreground = Muted;
            actions.Children.Add(pick);
            actions.Children.Add(clear);
            Grid.SetColumn(actions, 2);
            row.Children.Add(actions);

            _toolRows.Children.Add(row);
        }
    }

    private void PickToolFor(string toolId)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = S("settings.choose_tool", "选择工具可执行文件"),
            CheckFileExists = true,
            Filter = "可执行文件|*.exe;*.bat;*.cmd|所有文件|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        _registry.SetTool(toolId, dlg.FileName);
        _registry.Save(_registryPath);
        SetStatus(_course?.Manifest.Metadata.Title ?? "", S("res.tool_saved", "已记住工具路径：") + dlg.FileName);
    }

    // ================= Desk 逻辑 =================
    private StageDefinition Current => (_course != null && _stageIndex >= 0 && _stageIndex < _course.Stages.Count) ? _course.Stages[_stageIndex] : null;

    private void LoadStage(int index)
    {
        if (_course == null || _course.Stages.Count == 0) return;
        _stageIndex = Math.Clamp(index, 0, _course.Stages.Count - 1);
        if (_stageList != null && _stageList.SelectedIndex != _stageIndex) _stageList.SelectedIndex = _stageIndex;
        _stepIndex = 0;
        _stable = 0; _lastTriggerKey = "";
        if (_finishPanel != null) _finishPanel.Visibility = Visibility.Collapsed;
        if (_nextBtn != null) _nextBtn.Content = S("btn.next", "下一步");
        SaveStep();
        Refresh();
    }

    private void MoveStep(int delta)
    {
        var stage = Current;
        if (stage == null) return;
        if (_stepIndex >= stage.Steps.Count)
        {
            if (delta > 0) GoNextStageOrFinish();
            else { _stepIndex = Math.Max(0, stage.Steps.Count - 1); SaveStep(); Refresh(); }
            return;
        }
        int next = _stepIndex + delta;
        if (next >= stage.Steps.Count)
        {
            _stepIndex = stage.Steps.Count;
            _stable = 0; _lastTriggerKey = "";
            ClearCurrentStage();

            // 非最后一段：引导走完直接进下一关，不显示"本关引导已走完"这一页
            if (delta > 0 && _stageIndex + 1 < _course.Stages.Count)
            {
                SaveStep();
                LoadStage(_stageIndex + 1);
                SetStatus(_course.Manifest.Metadata.Title,
                    S("stage.auto_next", "本段已完成，已自动进入下一段。"));
                return;
            }

            SaveStep();
            Refresh();
            return;
        }
        _stepIndex = Math.Clamp(next, 0, stage.Steps.Count - 1);
        _stable = 0; _lastTriggerKey = "";
        SaveStep();
        Refresh();
    }

    private void SaveStep()
    {
        var p = _progress.ForCourse(_course.Manifest.Id, _course.Manifest.Version);
        p.StageIndex = _stageIndex;
        p.StepIndex = _stepIndex;
        _progress.Save();
    }

    private void ClearCurrentStage()
    {
        var stage = Current;
        if (stage == null) return;
        var p = _progress.ForCourse(_course.Manifest.Id, _course.Manifest.Version);
        if (!p.ClearedStages.Contains(stage.Id)) p.ClearedStages.Add(stage.Id);
        _progress.Save();
    }

    private void GoNextStageOrFinish()
    {
        if (_stageIndex + 1 < _course.Stages.Count) { LoadStage(_stageIndex + 1); return; }
        _finishTitle.Text = S("finish.panel_title", C("course_completion.title", "全部关卡已完成"));
        _finishBody.Text = S("finish.panel_body", C("common.external_submit", "把你在工具里得到的 flag 复制下来，粘贴到比赛平台的提交页面（本程序不收集、不判题）。")) + "\n" +
                           S("finish.panel_tip", "如果平台判错：回到最后一步，重新确认 s2 的取值、strncmp 的长度和终止符处理，再对一遍拼接顺序。");
        _finishPanel.Visibility = Visibility.Visible;
        _stageTitle.Text = S("finish.stage_title", "课程已完成 · ") + _course.Manifest.Metadata.Title;
        _stageGoal.Text = S("finish.stage_goal", "没有站内判题：flag 由你带到比赛平台提交。");
        _stepTitle.Text = S("finish.step_title", "你已经走完全部关卡");
        _stepBody.Text = S("finish.step_body", C("course_completion.body", "接下来把 flag 复制到比赛平台提交 —— 本程序不收集答案，也不判题。"));
        _stepImage.Source = null;
        _hint.Text = S("finish.hint", "记得先自己确认一遍：字符串在哪定义、在哪被比较、比较长度是多少。");
        _overlay.ClearGuide();
        SetStatus(_course.Manifest.Metadata.Title, S("finish.status", "课程已完成：本程序不收集、不判题，请到比赛平台提交 flag。"));
    }

    private void Refresh()
    {
        var stage = Current;
        if (stage == null || _course == null) return;
        _stageTitle.Text = ContentStore.T(_course.I18n, stage.TitleKey);
        _stageGoal.Text = ContentStore.T(_course.I18n, stage.GoalKey);

        if (_stageList != null)
        {
            var key = _course.Manifest.Id + "|" + _course.Manifest.Version + "|" + _course.Stages.Count;
            if (!string.Equals(_stageListCourseId, key, StringComparison.Ordinal))
            {
                _stageListCourseId = key;
                _fillingStages = true;
                try
                {
                    _stageList.Items.Clear();
                    foreach (var s in _course.Stages)
                        _stageList.Items.Add(s.Order + " · " + ContentStore.T(_course.I18n, s.TitleKey));
                }
                finally { _fillingStages = false; }
            }
            if (_stageList.SelectedIndex != _stageIndex) _stageList.SelectedIndex = _stageIndex;
        }

        RefreshCards(_deskCards);

        var step = (_stepIndex >= 0 && _stepIndex < stage.Steps.Count) ? stage.Steps[_stepIndex] : null;
        if (step == null)
        {
            bool isLast = _stageIndex + 1 >= _course.Stages.Count;
            _stepTitle.Text = S("stage.done_title", "本关引导已走完");
            _stepBody.Text = S("stage.done_body", C("common.external_submit", "把你在工具里得到的 flag 复制下来，粘贴到比赛平台的提交页面（本程序不收集、不判题）。"));
            _stepImage.Source = null;
            _overlay.ClearGuide();
            _nextBtn.Content = isLast ? S("btn.finish_course", "完成课程") : S("btn.next_stage", "进入下一关");
            _hint.Text = isLast
                ? S("stage.done_hint_last", "最后一关：点“完成课程”查看收尾提示。")
                : S("stage.done_hint_next", "确认无误后点“进入下一关”。");
            return;
        }
        _nextBtn.Content = S("btn.next", "下一步");
        _stepTitle.Text = ContentStore.T(_course.I18n, step.TextKey);
        _stepBody.Text = ContentStore.T(_course.I18n, step.BodyKey ?? "");
        _stepImage.Source = LoadImage(step.Image);
        _hint.Text = S("step.counter", "步骤 ") + (_stepIndex + 1) + "/" + stage.Steps.Count + " · " + step.Id;
        UpdateGuide();
    }

    private ImageSource LoadImage(string rel)
    {
        if (string.IsNullOrWhiteSpace(rel)) return null;
        var candidates = new[]
        {
            Path.Combine(_content.Root, rel.Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(_course.AssetsDir, Path.GetFileName(rel)),
        };
        foreach (var c in candidates)
        {
            try
            {
                if (!File.Exists(c)) continue;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(c);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { }
        }
        return null;
    }

    private void UpdateGuide()
    {
        if (!_inDesk || !_interactive) { _overlay.ClearGuide(); return; }
        var stage = Current;
        var step = (stage != null && _stepIndex >= 0 && _stepIndex < stage.Steps.Count) ? stage.Steps[_stepIndex] : null;
        if (step?.Guide == null) { _overlay.ClearGuide(); return; }

        var plan = _engine.Resolve(step.Guide);
        if (!plan.IsDisplayable)
        {
            _overlay.ClearGuide();
            SetStatus(_course?.Manifest.Metadata.Title ?? "", S("status.locating", "定位：") + plan.Evidence);
            return;
        }
        var title = _hotspots.ContainsKey(step.Guide.Target?.Key ?? "") ? S("guide.host_title", "本程序提示") : (plan.Target?.ProcessName ?? "COACH");
        _overlay.ShowGuide(plan.BoundsScreen, title, ContentStore.T(_course.I18n, step.TextKey), plan.Overlay);
        SetStatus(_course?.Manifest.Metadata.Title ?? "",
            S("status.locating", "定位：") + plan.Evidence + " ｜ layer=" + plan.Layer + " ｜ attachment=" + plan.Attachment + " ｜ score=" + (int)plan.Score);
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        if (!_inDesk || !_interactive || _course == null) return;
        var stage = Current;
        var step = (stage != null && _stepIndex >= 0 && _stepIndex < stage.Steps.Count) ? stage.Steps[_stepIndex] : null;
        if (step == null) return;

        var windows = _engine.Windows();
        UpdateGuide();

        if (step.Trigger == null) return;
        bool matched = _engine.Evaluate(step.Trigger, windows, out var evidence);
        string key = stage.Id + ":" + step.Id + ":" + evidence;
        if (!matched) { _stable = 0; _lastTriggerKey = ""; return; }
        if (key == _lastTriggerKey) _stable++; else { _stable = 1; _lastTriggerKey = key; }
        if (_stable >= 2)
        {
            _stable = 0;
            SetStatus(_course.Manifest.Metadata.Title, S("status.recognized", "已识别到这一步的操作：") + evidence + S("status.recognized_tail", " → 自动前进"));
            MoveStep(1);
        }
    }

    // 卡片点击分派：工具→启动（未配置则选文件），附件→定位（未配置则选文件）
    private void PickOrOpen(bool isTool, string id, string label)
    {
        if (isTool)
        {
            if (string.IsNullOrEmpty(ResolveToolPath(id))) PickResource(true, id, label);
            else LaunchTool(id);
        }
        else
        {
            if (string.IsNullOrEmpty(ResolveTargetPath(id))) PickResource(false, id, label);
            else OpenTargetFolder(id);
        }
    }
}
