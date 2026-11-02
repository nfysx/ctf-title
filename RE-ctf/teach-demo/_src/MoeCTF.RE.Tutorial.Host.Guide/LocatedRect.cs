using System;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class LocatedRect
{
	public required Rect BoundsScreen { get; init; }

	public NormalizedRect? BoundsInWindow { get; init; }

	public string CoordinateSpace { get; init; } = "screen";

	public string Source { get; init; } = "";

	public static LocatedRect FromWindow(Rect bounds, WindowInfo window, string source)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		return FromWindow(bounds, window, source, WindowReferenceBounds(window));
	}

	public static LocatedRect FromWindow(Rect bounds, WindowInfo window, string source, Rect referenceBounds)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		return new LocatedRect
		{
			BoundsScreen = bounds,
			BoundsInWindow = NormalizedRect.FromScreen(bounds, referenceBounds).Clamp(),
			CoordinateSpace = (ReferenceUsesClient(window, referenceBounds) ? "client-normalized" : "window-normalized"),
			Source = source
		};
	}

	public static LocatedRect FromScreen(Rect bounds, string source)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return new LocatedRect
		{
			BoundsScreen = bounds,
			CoordinateSpace = "screen",
			Source = source
		};
	}

	public static Rect WindowReferenceBounds(WindowInfo window)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		Rect clientBounds = window.ClientBounds;
		if (((Rect)(ref clientBounds)).Width > 1.0)
		{
			clientBounds = window.ClientBounds;
			if (((Rect)(ref clientBounds)).Height > 1.0)
			{
				return window.ClientBounds;
			}
		}
		return window.Bounds;
	}

	private static bool ReferenceUsesClient(WindowInfo window, Rect referenceBounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		Rect clientBounds = window.ClientBounds;
		if (((Rect)(ref clientBounds)).Width > 1.0)
		{
			clientBounds = window.ClientBounds;
			if (((Rect)(ref clientBounds)).Height > 1.0)
			{
				double left = ((Rect)(ref referenceBounds)).Left;
				clientBounds = window.ClientBounds;
				if (Math.Abs(left - ((Rect)(ref clientBounds)).Left) < 0.5)
				{
					double top = ((Rect)(ref referenceBounds)).Top;
					clientBounds = window.ClientBounds;
					if (Math.Abs(top - ((Rect)(ref clientBounds)).Top) < 0.5)
					{
						double width = ((Rect)(ref referenceBounds)).Width;
						clientBounds = window.ClientBounds;
						if (Math.Abs(width - ((Rect)(ref clientBounds)).Width) < 0.5)
						{
							double height = ((Rect)(ref referenceBounds)).Height;
							clientBounds = window.ClientBounds;
							return Math.Abs(height - ((Rect)(ref clientBounds)).Height) < 0.5;
						}
					}
				}
			}
		}
		return false;
	}
}
