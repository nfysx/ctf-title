using System;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
	public static NormalizedRect FromScreen(Rect bounds, Rect container)
	{
		if (((Rect)(ref container)).Width <= 1.0 || ((Rect)(ref container)).Height <= 1.0)
		{
			return new NormalizedRect(0.0, 0.0, 1.0, 1.0);
		}
		return new NormalizedRect((((Rect)(ref bounds)).Left - ((Rect)(ref container)).Left) / ((Rect)(ref container)).Width, (((Rect)(ref bounds)).Top - ((Rect)(ref container)).Top) / ((Rect)(ref container)).Height, ((Rect)(ref bounds)).Width / ((Rect)(ref container)).Width, ((Rect)(ref bounds)).Height / ((Rect)(ref container)).Height);
	}

	public Rect ToScreen(Rect container)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		return new Rect(((Rect)(ref container)).Left + ((Rect)(ref container)).Width * X, ((Rect)(ref container)).Top + ((Rect)(ref container)).Height * Y, Math.Max(1.0, ((Rect)(ref container)).Width * Width), Math.Max(1.0, ((Rect)(ref container)).Height * Height));
	}

	public NormalizedRect Clamp()
	{
		double num = Math.Clamp(X, 0.0, 1.0);
		double num2 = Math.Clamp(Y, 0.0, 1.0);
		double num3 = Math.Clamp(X + Width, num, 1.0);
		double num4 = Math.Clamp(Y + Height, num2, 1.0);
		return new NormalizedRect(num, num2, Math.Max(0.001, num3 - num), Math.Max(0.001, num4 - num2));
	}
}
