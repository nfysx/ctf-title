using System;
using System.Collections.Generic;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ControlMatch
{
	public required Rect Bounds { get; init; }

	public int Score { get; init; }

	public int VisitedNodes { get; init; }

	public string Name { get; init; } = "";

	public string AutomationId { get; init; } = "";

	public string ClassName { get; init; } = "";

	public string ControlType { get; init; } = "";

	public string Provider { get; init; } = "uia";

	public IReadOnlyList<string> MatchedHints { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
