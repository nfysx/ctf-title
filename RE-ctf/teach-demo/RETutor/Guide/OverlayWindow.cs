using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RETutor.Models;
using RETutor.Services;

namespace RETutor.Guide;

/// <summary>
/// 对应原程序 Coach\CoachOverlayWindow：覆盖虚拟屏幕、置顶、点击穿透的 HUD。
/// 三件套缺一不可：AllowsTransparency + ShowActivated=false + IsHitTestVisible=false + WS_EX_TRANSPARENT|NOACTIVATE|TOOLWINDOW。
/// 简化点：host 层也复用本窗口（原程序画在主窗口 Canvas 里），几何算法一致。
/// </summary>
public sealed class OverlayWindow : Window
{
    private readonly Canvas _root = new();
    private readonly Border _highlight = new();
    private readonly Border _bubble = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _text = new();
    private readonly Polygon _arrow = new();

    public OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _highlight.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0xC2, 0xFF));
        _highlight.BorderThickness = new Thickness(2);
        _highlight.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x4C, 0xC2, 0xFF));
        _highlight.CornerRadius = new CornerRadius(6);
        _highlight.Visibility = Visibility.Collapsed;
        _highlight.IsHitTestVisible = false;

        _bubble.Background = new SolidColorBrush(Color.FromArgb(0xB3, 0x12, 0x18, 0x24));
        _bubble.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0xC2, 0xFF));
        _bubble.BorderThickness = new Thickness(1.5);
        _bubble.CornerRadius = new CornerRadius(14);
        _bubble.Padding = new Thickness(14);
        _bubble.MaxWidth = 420;
        _bubble.IsHitTestVisible = false;

        _title.Foreground = new SolidColorBrush(Color.FromRgb(0x7D, 0xD3, 0xFF));
        _title.FontSize = 12;
        _title.FontWeight = FontWeights.SemiBold;
        _title.Text = "COACH";

        _text.Foreground = Brushes.White;
        _text.FontSize = 14;
        _text.TextWrapping = TextWrapping.Wrap;

        var stack = new StackPanel();
        stack.Children.Add(_title);
        stack.Children.Add(_text);
        _bubble.Child = stack;

        _arrow.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0xC2, 0xFF));
        _arrow.Points = new PointCollection { new Point(0, 0), new Point(12, 6), new Point(0, 12) };
        _arrow.Visibility = Visibility.Collapsed;

        _root.Children.Add(_highlight);
        _root.Children.Add(_arrow);
        _root.Children.Add(_bubble);
        Content = _root;

        CoverVirtualScreen();
        SourceInitialized += (_, _) => ApplyClickThrough();
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SystemParameters.VirtualScreenWidth) or nameof(SystemParameters.VirtualScreenHeight))
                CoverVirtualScreen();
        };
    }

    private void CoverVirtualScreen()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = Math.Max(1, SystemParameters.VirtualScreenWidth);
        Height = Math.Max(1, SystemParameters.VirtualScreenHeight);
    }

    private void ApplyClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            ex | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW |
            NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_LAYERED);
    }

    /// <summary>物理像素 → 本窗口 DIP。</summary>
    private Matrix DeviceToDip()
    {
        var source = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
        return source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
    }

    public void ShowGuide(Rect boundsScreen, string title, string text, GuideOverlaySpec spec)
    {
        spec ??= new GuideOverlaySpec();
        CoverVirtualScreen();
        _title.Text = string.IsNullOrWhiteSpace(title) ? "COACH" : title;
        _text.Text = text ?? "";

        var m = DeviceToDip();
        var p1 = m.Transform(new Point(boundsScreen.X, boundsScreen.Y));
        var p2 = m.Transform(new Point(boundsScreen.Right, boundsScreen.Bottom));
        double left = p1.X - Left, top = p1.Y - Top;
        double w = Math.Max(1, p2.X - p1.X), h = Math.Max(1, p2.Y - p1.Y);

        var work = WorkAreaDip(boundsScreen);

        _highlight.Visibility = Visibility.Visible;
        Canvas.SetLeft(_highlight, left);
        Canvas.SetTop(_highlight, top);
        _highlight.Width = w;
        _highlight.Height = h;
        Pulse();

        _bubble.MaxWidth = Math.Max(260, Math.Min(spec.MaxWidth ?? 420, work.Width - 24));
        _bubble.Measure(new Size(_bubble.MaxWidth, double.PositiveInfinity));
        var bubbleSize = new Size(_bubble.MaxWidth, Math.Max(60, _bubble.DesiredSize.Height));
        _bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        bubbleSize = new Size(Math.Min(_bubble.MaxWidth, _bubble.DesiredSize.Width), _bubble.DesiredSize.Height);
        var placed = PositionBubble(new Rect(left, top, w, h), spec, bubbleSize, work);
        _bubble.Width = bubbleSize.Width;
        Canvas.SetLeft(_bubble, placed.X);
        Canvas.SetTop(_bubble, placed.Y);

        PlaceArrow(placed, new Rect(left, top, w, h), bubbleSize, spec);

        if (!IsVisible) Show();
        ApplyClickThrough();
    }

    private Rect WorkAreaDip(Rect boundsScreenPx)
    {
        var rc = new NativeMethods.RECT
        {
            Left = (int)boundsScreenPx.X,
            Top = (int)boundsScreenPx.Y,
            Right = (int)boundsScreenPx.Right,
            Bottom = (int)boundsScreenPx.Bottom
        };
        var hMon = NativeMethods.MonitorFromRect(ref rc, 2 /*MONITOR_DEFAULTTONEAREST*/);
        var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (hMon != IntPtr.Zero && NativeMethods.GetMonitorInfoW(hMon, ref info))
        {
            var m = DeviceToDip();
            var a = m.Transform(new Point(info.rcWork.Left, info.rcWork.Top));
            var b = m.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom));
            return new Rect(a.X - Left, a.Y - Top, b.X - a.X, b.Y - a.Y);
        }
        return new Rect(0, 0, Width, Height);
    }

    private static Rect PositionBubble(Rect target, GuideOverlaySpec spec, Size bubble, Rect work)
    {
        double gap = spec.Gap, pad = spec.Padding;
        double bx, by;
        var placement = (spec.Placement ?? "auto").ToLowerInvariant();
        if (placement == "auto")
        {
            if (work.Right - target.Right >= bubble.Width + gap) placement = "right";
            else if (target.Bottom + gap + bubble.Height <= work.Bottom - 12) placement = "bottom";
            else if (target.Top - gap - bubble.Height >= work.Top + 12) placement = "top";
            else if (target.Left - gap - bubble.Width >= work.Left + 12) placement = "left";
            else placement = "inside";
        }
        switch (placement)
        {
            case "right": bx = target.Right + gap + pad; by = AlignY(target, bubble, spec.Alignment); break;
            case "left": bx = target.Left - gap - pad - bubble.Width; by = AlignY(target, bubble, spec.Alignment); break;
            case "top": bx = AlignX(target, bubble, spec.Alignment); by = target.Top - gap - pad - bubble.Height; break;
            case "inside": bx = AlignX(target, bubble, spec.Alignment); by = AlignY(target, bubble, spec.Alignment); break;
            default: bx = AlignX(target, bubble, spec.Alignment); by = target.Bottom + gap + pad; break;
        }
        bx += spec.OffsetX; by += spec.OffsetY;
        bx = Math.Clamp(bx, work.Left + 12, Math.Max(work.Left + 12, work.Right - bubble.Width - 12));
        by = Math.Clamp(by, work.Top + 12, Math.Max(work.Top + 12, work.Bottom - bubble.Height - 12));
        return new Rect(bx, by, bubble.Width, bubble.Height);
    }

    private static double AlignX(Rect target, Size bubble, string alignment) => (alignment ?? "center").ToLowerInvariant() switch
    {
        "start" => target.Left,
        "end" => target.Right - bubble.Width,
        _ => target.Left + (target.Width - bubble.Width) / 2,
    };

    private static double AlignY(Rect target, Size bubble, string alignment) => (alignment ?? "center").ToLowerInvariant() switch
    {
        "start" => target.Top,
        "end" => target.Bottom - bubble.Height,
        _ => target.Top + (target.Height - bubble.Height) / 2,
    };

    private void PlaceArrow(Rect bubble, Rect target, Size size, GuideOverlaySpec spec)
    {
        _arrow.Visibility = Visibility.Collapsed;
    }

    private void Pulse()
    {
        var anim = new DoubleAnimation(0.55, 1.0, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        _highlight.BeginAnimation(OpacityProperty, anim);
    }

    public void ClearGuide()
    {
        _highlight.Visibility = Visibility.Collapsed;
        _arrow.Visibility = Visibility.Collapsed;
        _bubble.Visibility = Visibility.Collapsed;
        if (IsVisible) Hide();
    }
}
