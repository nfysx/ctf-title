using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MoeCTF.RE.Tutorial.Host.Guide;

namespace MoeCTF.RE.Tutorial.Host.Coach;

public class CoachOverlayWindow : Window, IComponentConnector
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct MONITORINFO
	{
		public int cbSize;

		public RECT rcMonitor;

		public RECT rcWork;

		public uint dwFlags;
	}

	private const int GwlExstyle = -20;

	private const int WsExTransparent = 32;

	private const int WsExToolwindow = 128;

	private const int WsExNoactivate = 134217728;

	private const int WsExLayered = 524288;

	private const int MonitorDefaultToNearest = 2;

	internal Canvas RootCanvas;

	internal Border Highlight;

	internal Polygon Arrow;

	internal Border Bubble;

	internal TextBlock BubbleTitle;

	internal TextBlock BubbleText;

	private bool _contentLoaded;

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromRect(ref RECT lprc, int dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

	public CoachOverlayWindow()
	{
		InitializeComponent();
		base.ShowActivated = false;
		base.Focusable = false;
		base.IsHitTestVisible = false;
		CoverVirtualScreen();
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		ApplyClickThrough();
	}

	private void CoverVirtualScreen()
	{
		base.Left = SystemParameters.VirtualScreenLeft;
		base.Top = SystemParameters.VirtualScreenTop;
		base.Width = SystemParameters.VirtualScreenWidth;
		base.Height = SystemParameters.VirtualScreenHeight;
	}

	private void ApplyClickThrough()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		if (handle != IntPtr.Zero)
		{
			int windowLong = GetWindowLong(handle, -20);
			SetWindowLong(handle, -20, windowLong | 0x20 | 0x80 | 0x8000000 | 0x80000);
		}
	}

	public void ShowGuide(Rect? targetBoundsScreenPx, string text, string? title = null, GuideOverlaySpec? overlay = null)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		if (overlay == null)
		{
			overlay = new GuideOverlaySpec();
		}
		CoverVirtualScreen();
		ApplyClickThrough();
		BubbleTitle.Text = (string.IsNullOrWhiteSpace(title) ? "COACH" : title);
		BubbleText.Text = text;
		Bubble.Visibility = Visibility.Visible;
		if (targetBoundsScreenPx.HasValue)
		{
			Rect valueOrDefault = targetBoundsScreenPx.GetValueOrDefault();
			if (((Rect)(ref valueOrDefault)).Width > 1.0 && ((Rect)(ref valueOrDefault)).Height > 1.0)
			{
				Rect val = ScreenPixelsToOverlay(valueOrDefault);
				Rect visibleArea = MonitorWorkAreaToOverlay(valueOrDefault);
				double num = Math.Max(0.0, overlay.Padding);
				double num2 = Math.Max(0.0, ((Rect)(ref val)).Left - num);
				double num3 = Math.Max(0.0, ((Rect)(ref val)).Top - num);
				double num4 = Math.Max(18.0, ((Rect)(ref val)).Width + num * 2.0);
				double num5 = Math.Max(18.0, ((Rect)(ref val)).Height + num * 2.0);
				Rect target = default(Rect);
				((Rect)(ref target))._002Ector(num2, num3, num4, num5);
				bool animate = Highlight.Visibility == Visibility.Visible && !double.IsNaN(Canvas.GetLeft(Highlight)) && !double.IsNaN(Canvas.GetTop(Highlight));
				Highlight.Visibility = Visibility.Visible;
				SetAnimated(Highlight, Canvas.LeftProperty, ((Rect)(ref target)).Left, animate);
				SetAnimated(Highlight, Canvas.TopProperty, ((Rect)(ref target)).Top, animate);
				SetAnimated(Highlight, FrameworkElement.WidthProperty, num4, animate);
				SetAnimated(Highlight, FrameworkElement.HeightProperty, num5, animate);
				Pulse(Highlight);
				Bubble.MaxWidth = Math.Max(260.0, Math.Min(overlay.MaxWidth ?? 420.0, ((Rect)(ref visibleArea)).Width - 24.0));
				PositionBubble(target, overlay, visibleArea);
				goto IL_0287;
			}
		}
		Highlight.Visibility = Visibility.Collapsed;
		Arrow.Visibility = Visibility.Collapsed;
		Canvas.SetLeft(Bubble, Math.Max(12.0, base.Width - 440.0));
		Canvas.SetTop(Bubble, 28.0);
		goto IL_0287;
		IL_0287:
		if (!base.IsVisible)
		{
			Show();
			ApplyClickThrough();
		}
	}

	public void ClearGuide()
	{
		Highlight.Visibility = Visibility.Collapsed;
		Arrow.Visibility = Visibility.Collapsed;
		Bubble.Visibility = Visibility.Collapsed;
	}

	private static void Pulse(FrameworkElement el)
	{
		DoubleAnimation animation = new DoubleAnimation
		{
			From = 0.55,
			To = 1.0,
			Duration = TimeSpan.FromMilliseconds(700.0),
			AutoReverse = true,
			RepeatBehavior = RepeatBehavior.Forever
		};
		el.BeginAnimation(UIElement.OpacityProperty, animation);
	}

	private static void SetAnimated(FrameworkElement element, DependencyProperty property, double value, bool animate)
	{
		if (!animate)
		{
			element.BeginAnimation(property, null);
			((DependencyObject)element).SetValue(property, (object)value);
			return;
		}
		if (!(((DependencyObject)element).GetValue(property) is double num) || double.IsNaN(num))
		{
			((DependencyObject)element).SetValue(property, (object)value);
			return;
		}
		if (Math.Abs(num - value) < 0.5)
		{
			element.BeginAnimation(property, null);
			((DependencyObject)element).SetValue(property, (object)value);
			return;
		}
		DoubleAnimation animation = new DoubleAnimation
		{
			To = value,
			Duration = TimeSpan.FromMilliseconds(220.0),
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		element.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
	}

	private void PositionBubble(Rect target, GuideOverlaySpec overlay, Rect visibleArea)
	{
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Unknown result type (might be due to invalid IL or missing references)
		string text = (overlay.Placement ?? "auto").Trim().ToLowerInvariant();
		double num = Math.Max(6.0, overlay.Gap);
		double maxWidth = Bubble.MaxWidth;
		if (text == "auto")
		{
			double num2 = ((Rect)(ref target)).Top - ((Rect)(ref visibleArea)).Top;
			double num3 = ((Rect)(ref visibleArea)).Bottom - ((Rect)(ref target)).Bottom;
			double num4 = ((Rect)(ref visibleArea)).Right - ((Rect)(ref target)).Right;
			double num5 = ((Rect)(ref target)).Left - ((Rect)(ref visibleArea)).Left;
			text = ((num4 >= maxWidth + num) ? "right" : ((num3 >= 116.0 + num) ? "bottom" : ((num2 >= 116.0 + num) ? "top" : ((num5 >= maxWidth + num) ? "left" : "inside"))));
		}
		double num6 = ((Rect)(ref target)).Left;
		double num7 = ((Rect)(ref target)).Bottom + num;
		switch (text)
		{
		case "top":
			num6 = AlignX(target, maxWidth, overlay.Alignment);
			num7 = ((Rect)(ref target)).Top - 116.0 - num;
			SetArrow(((Rect)(ref target)).Left + ((Rect)(ref target)).Width / 2.0 - 8.0, ((Rect)(ref target)).Top - 18.0, "down");
			break;
		case "bottom":
			num6 = AlignX(target, maxWidth, overlay.Alignment);
			num7 = ((Rect)(ref target)).Bottom + num;
			SetArrow(((Rect)(ref target)).Left + ((Rect)(ref target)).Width / 2.0 - 8.0, ((Rect)(ref target)).Bottom + 2.0, "up");
			break;
		case "left":
			num6 = ((Rect)(ref target)).Left - maxWidth - num;
			num7 = AlignY(target, 116.0, overlay.Alignment);
			SetArrow(((Rect)(ref target)).Left - 18.0, ((Rect)(ref target)).Top + ((Rect)(ref target)).Height / 2.0 - 8.0, "right");
			break;
		case "right":
			num6 = ((Rect)(ref target)).Right + num;
			num7 = AlignY(target, 116.0, overlay.Alignment);
			SetArrow(((Rect)(ref target)).Right + 2.0, ((Rect)(ref target)).Top + ((Rect)(ref target)).Height / 2.0 - 8.0, "left");
			break;
		case "inside":
			num6 = ((Rect)(ref target)).Left + num;
			num7 = ((Rect)(ref target)).Top + num;
			Arrow.Visibility = Visibility.Collapsed;
			break;
		}
		double num8 = ((Rect)(ref visibleArea)).Left + 12.0;
		double max = Math.Max(num8, ((Rect)(ref visibleArea)).Right - maxWidth - 12.0);
		double num9 = ((Rect)(ref visibleArea)).Top + 12.0;
		double max2 = Math.Max(num9, ((Rect)(ref visibleArea)).Bottom - 116.0 - 12.0);
		num6 = Math.Clamp(num6 + overlay.OffsetX, num8, max);
		num7 = Math.Clamp(num7 + overlay.OffsetY, num9, max2);
		Canvas.SetLeft(Bubble, num6);
		Canvas.SetTop(Bubble, num7);
	}

	private static double AlignX(Rect target, double width, string? alignment)
	{
		string text = (alignment ?? "center").Trim().ToLowerInvariant();
		if (!(text == "start"))
		{
			if (text == "end")
			{
				return ((Rect)(ref target)).Right - width;
			}
			return ((Rect)(ref target)).Left + ((Rect)(ref target)).Width / 2.0 - width / 2.0;
		}
		return ((Rect)(ref target)).Left;
	}

	private static double AlignY(Rect target, double height, string? alignment)
	{
		string text = (alignment ?? "center").Trim().ToLowerInvariant();
		if (!(text == "start"))
		{
			if (text == "end")
			{
				return ((Rect)(ref target)).Bottom - height;
			}
			return ((Rect)(ref target)).Top + ((Rect)(ref target)).Height / 2.0 - height / 2.0;
		}
		return ((Rect)(ref target)).Top;
	}

	private void SetArrow(double x, double y, string direction)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		Arrow.Visibility = Visibility.Visible;
		Arrow.RenderTransformOrigin = new Point(0.5, 0.5);
		Polygon arrow = Arrow;
		arrow.RenderTransform = new RotateTransform(direction switch
		{
			"up" => -90, 
			"down" => 90, 
			"right" => 0, 
			"left" => 180, 
			_ => 0, 
		});
		Canvas.SetLeft(Arrow, x);
		Canvas.SetTop(Arrow, y);
	}

	private Rect ScreenPixelsToOverlay(Rect screenPx)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		Matrix val = (Matrix)(((_003F?)(PresentationSource.FromVisual(this) as HwndSource)?.CompositionTarget?.TransformFromDevice) ?? Matrix.Identity);
		Point val2 = ((Matrix)(ref val)).Transform(new Point(((Rect)(ref screenPx)).Left, ((Rect)(ref screenPx)).Top));
		Point val3 = ((Matrix)(ref val)).Transform(new Point(((Rect)(ref screenPx)).Right, ((Rect)(ref screenPx)).Bottom));
		double num = ((Point)(ref val2)).X - base.Left;
		double num2 = ((Point)(ref val2)).Y - base.Top;
		double num3 = Math.Max(0.0, ((Point)(ref val3)).X - ((Point)(ref val2)).X);
		double num4 = Math.Max(0.0, ((Point)(ref val3)).Y - ((Point)(ref val2)).Y);
		return new Rect(num, num2, num3, num4);
	}

	private Rect MonitorWorkAreaToOverlay(Rect targetScreenPx)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		RECT rECT = default(RECT);
		rECT.Left = (int)Math.Floor(((Rect)(ref targetScreenPx)).Left);
		rECT.Top = (int)Math.Floor(((Rect)(ref targetScreenPx)).Top);
		rECT.Right = (int)Math.Ceiling(((Rect)(ref targetScreenPx)).Right);
		rECT.Bottom = (int)Math.Ceiling(((Rect)(ref targetScreenPx)).Bottom);
		RECT lprc = rECT;
		nint num = MonitorFromRect(ref lprc, 2);
		if (num == IntPtr.Zero)
		{
			return new Rect(0.0, 0.0, base.Width, base.Height);
		}
		MONITORINFO mONITORINFO = default(MONITORINFO);
		mONITORINFO.cbSize = Marshal.SizeOf<MONITORINFO>();
		MONITORINFO lpmi = mONITORINFO;
		if (!GetMonitorInfo(num, ref lpmi))
		{
			return new Rect(0.0, 0.0, base.Width, base.Height);
		}
		RECT rcWork = lpmi.rcWork;
		return ScreenPixelsToOverlay(new Rect((double)rcWork.Left, (double)rcWork.Top, (double)Math.Max(1, rcWork.Right - rcWork.Left), (double)Math.Max(1, rcWork.Bottom - rcWork.Top)));
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.2.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/MoeCTF.RE.Tutorial;V0.1.0.0;component/coach/coachoverlaywindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.2.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			RootCanvas = (Canvas)target;
			break;
		case 2:
			Highlight = (Border)target;
			break;
		case 3:
			Arrow = (Polygon)target;
			break;
		case 4:
			Bubble = (Border)target;
			break;
		case 5:
			BubbleTitle = (TextBlock)target;
			break;
		case 6:
			BubbleText = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
