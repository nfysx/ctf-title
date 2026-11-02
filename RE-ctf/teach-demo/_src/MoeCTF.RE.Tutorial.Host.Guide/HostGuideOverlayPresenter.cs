using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class HostGuideOverlayPresenter
{
	private readonly Window _host;

	private readonly Canvas _layer;

	private readonly Border _highlight;

	private readonly Polygon _arrow;

	private readonly Border _bubble;

	private readonly TextBlock _title;

	private readonly TextBlock _text;

	public HostGuideOverlayPresenter(Window host, Canvas layer, Border highlight, Polygon arrow, Border bubble, TextBlock title, TextBlock text)
	{
		_host = host;
		_layer = layer;
		_highlight = highlight;
		_arrow = arrow;
		_bubble = bubble;
		_title = title;
		_text = text;
	}

	public void Show(GuideRenderPlan plan, string text, string title)
	{
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		_layer.Visibility = Visibility.Visible;
		_layer.Width = Math.Max(1.0, _host.ActualWidth);
		_layer.Height = Math.Max(1.0, _host.ActualHeight);
		_title.Text = title;
		_text.Text = text;
		_bubble.MaxWidth = Math.Max(260.0, Math.Min(plan.Overlay.MaxWidth ?? 420.0, _layer.Width - 24.0));
		_bubble.Visibility = Visibility.Visible;
		Rect? boundsScreen = plan.BoundsScreen;
		if (boundsScreen.HasValue)
		{
			Rect valueOrDefault = boundsScreen.GetValueOrDefault();
			if (((Rect)(ref valueOrDefault)).Width > 1.0 && ((Rect)(ref valueOrDefault)).Height > 1.0)
			{
				Rect val = ScreenPixelsToHost(valueOrDefault);
				double num = Math.Max(0.0, plan.Overlay.Padding);
				Rect target = default(Rect);
				((Rect)(ref target))._002Ector(Math.Max(0.0, ((Rect)(ref val)).Left - num), Math.Max(0.0, ((Rect)(ref val)).Top - num), Math.Max(18.0, ((Rect)(ref val)).Width + num * 2.0), Math.Max(18.0, ((Rect)(ref val)).Height + num * 2.0));
				_highlight.Visibility = Visibility.Visible;
				Canvas.SetLeft(_highlight, ((Rect)(ref target)).Left);
				Canvas.SetTop(_highlight, ((Rect)(ref target)).Top);
				_highlight.Width = ((Rect)(ref target)).Width;
				_highlight.Height = ((Rect)(ref target)).Height;
				Pulse(_highlight);
				PositionBubble(target, plan.Overlay);
				return;
			}
		}
		_highlight.Visibility = Visibility.Collapsed;
		_arrow.Visibility = Visibility.Collapsed;
		Canvas.SetLeft(_bubble, Math.Max(12.0, _layer.Width - _bubble.MaxWidth - 12.0));
		Canvas.SetTop(_bubble, 24.0);
	}

	public void Clear()
	{
		_highlight.Visibility = Visibility.Collapsed;
		_arrow.Visibility = Visibility.Collapsed;
		_bubble.Visibility = Visibility.Collapsed;
		_layer.Visibility = Visibility.Collapsed;
	}

	private void PositionBubble(Rect target, GuideOverlaySpec overlay)
	{
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		string text = (overlay.Placement ?? "auto").Trim().ToLowerInvariant();
		double num = Math.Max(6.0, overlay.Gap);
		double maxWidth = _bubble.MaxWidth;
		double num2 = Math.Min(maxWidth, Math.Max(260.0, maxWidth));
		if (text == "auto")
		{
			double top = ((Rect)(ref target)).Top;
			double num3 = _layer.Height - ((Rect)(ref target)).Bottom;
			text = ((_layer.Width - ((Rect)(ref target)).Right >= num2 + num) ? "right" : ((num3 >= 116.0 + num) ? "bottom" : ((top >= 116.0 + num) ? "top" : "left")));
		}
		double left = ((Rect)(ref target)).Left;
		double num4 = ((Rect)(ref target)).Bottom + num;
		switch (text)
		{
		case "top":
			num4 = ((Rect)(ref target)).Top - 116.0 - num;
			left = AlignX(target, num2, overlay.Alignment);
			SetArrow(((Rect)(ref target)).Left + ((Rect)(ref target)).Width / 2.0 - 8.0, ((Rect)(ref target)).Top - 18.0, "down");
			break;
		case "bottom":
			num4 = ((Rect)(ref target)).Bottom + num;
			left = AlignX(target, num2, overlay.Alignment);
			SetArrow(((Rect)(ref target)).Left + ((Rect)(ref target)).Width / 2.0 - 8.0, ((Rect)(ref target)).Bottom + 2.0, "up");
			break;
		case "left":
			left = ((Rect)(ref target)).Left - num2 - num;
			num4 = AlignY(target, 116.0, overlay.Alignment);
			SetArrow(((Rect)(ref target)).Left - 18.0, ((Rect)(ref target)).Top + ((Rect)(ref target)).Height / 2.0 - 8.0, "right");
			break;
		case "right":
			left = ((Rect)(ref target)).Right + num;
			num4 = AlignY(target, 116.0, overlay.Alignment);
			SetArrow(((Rect)(ref target)).Right + 2.0, ((Rect)(ref target)).Top + ((Rect)(ref target)).Height / 2.0 - 8.0, "left");
			break;
		case "inside":
			left = ((Rect)(ref target)).Left + num;
			num4 = ((Rect)(ref target)).Top + num;
			_arrow.Visibility = Visibility.Collapsed;
			break;
		default:
			left = AlignX(target, num2, overlay.Alignment);
			num4 = ((Rect)(ref target)).Bottom + num;
			SetArrow(((Rect)(ref target)).Left + ((Rect)(ref target)).Width / 2.0 - 8.0, ((Rect)(ref target)).Bottom + 2.0, "up");
			break;
		}
		left += overlay.OffsetX;
		num4 += overlay.OffsetY;
		left = Math.Clamp(left, 12.0, Math.Max(12.0, _layer.Width - num2 - 12.0));
		num4 = Math.Clamp(num4, 12.0, Math.Max(12.0, _layer.Height - 116.0 - 12.0));
		Canvas.SetLeft(_bubble, left);
		Canvas.SetTop(_bubble, num4);
	}

	private double AlignX(Rect target, double width, string? alignment)
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
		_arrow.Visibility = Visibility.Visible;
		_arrow.RenderTransformOrigin = new Point(0.5, 0.5);
		Polygon arrow = _arrow;
		arrow.RenderTransform = new RotateTransform(direction switch
		{
			"up" => -90, 
			"down" => 90, 
			"right" => 0, 
			"left" => 180, 
			_ => 0, 
		});
		Canvas.SetLeft(_arrow, x);
		Canvas.SetTop(_arrow, y);
	}

	private Rect ScreenPixelsToHost(Rect screenPx)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		Matrix val = (Matrix)(((_003F?)(PresentationSource.FromVisual(_host) as HwndSource)?.CompositionTarget?.TransformFromDevice) ?? Matrix.Identity);
		Point val2 = ((Matrix)(ref val)).Transform(new Point(((Rect)(ref screenPx)).Left, ((Rect)(ref screenPx)).Top));
		Point val3 = ((Matrix)(ref val)).Transform(new Point(((Rect)(ref screenPx)).Right, ((Rect)(ref screenPx)).Bottom));
		Point val4 = _host.PointToScreen(new Point(0.0, 0.0));
		val4 = ((Matrix)(ref val)).Transform(val4);
		return new Rect(((Point)(ref val2)).X - ((Point)(ref val4)).X, ((Point)(ref val2)).Y - ((Point)(ref val4)).Y, Math.Max(0.0, ((Point)(ref val3)).X - ((Point)(ref val2)).X), Math.Max(0.0, ((Point)(ref val3)).Y - ((Point)(ref val2)).Y));
	}

	private static void Pulse(FrameworkElement el)
	{
		DoubleAnimation animation = new DoubleAnimation
		{
			From = 0.62,
			To = 1.0,
			Duration = TimeSpan.FromMilliseconds(720.0),
			AutoReverse = true,
			RepeatBehavior = RepeatBehavior.Forever
		};
		el.BeginAnimation(UIElement.OpacityProperty, animation);
	}
}
