using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class AppControlMatch
{
	public required Rect Bounds { get; init; }

	public int Score { get; init; }

	public string AdapterId { get; init; } = "";

	public string State { get; init; } = "";

	public string Role { get; init; } = "";

	public bool UsedFallbackRegion { get; init; }

	public ControlMatch? Control { get; init; }

	public string Reason { get; init; } = "";
}
