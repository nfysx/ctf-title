using System;
using System.Collections.Generic;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideRenderPlan
{
	public GuideLayerKind Layer { get; init; }

	public Rect? BoundsScreen { get; init; }

	public NormalizedRect? BoundsInWindow { get; init; }

	public WindowInfo? TargetWindow { get; init; }

	public GuideOverlaySpec Overlay { get; init; } = new GuideOverlaySpec();

	public GuideAttachmentState Attachment { get; init; }

	public int Score { get; init; }

	public string CoordinateSpace { get; init; } = "screen";

	public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

	public string? FailureReason { get; init; }

	public bool IsExternalDisplayable
	{
		get
		{
			bool flag = Layer == GuideLayerKind.Screen && TargetWindow != null && BoundsScreen.HasValue && FailureReason == null;
			if (flag)
			{
				GuideAttachmentState attachment = Attachment;
				bool flag2 = ((attachment == GuideAttachmentState.Window || (uint)(attachment - 4) <= 2u) ? true : false);
				flag = flag2;
			}
			return flag;
		}
	}

	public bool IsPreciseExternal
	{
		get
		{
			bool flag = IsExternalDisplayable;
			if (flag)
			{
				GuideAttachmentState attachment = Attachment;
				bool flag2 = (uint)(attachment - 5) <= 1u;
				flag = flag2;
			}
			return flag;
		}
	}
}
