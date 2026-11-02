using System;
using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ControlTextEvidence
{
	public int Score { get; init; }

	public int VisitedNodes { get; init; }

	public bool TimedOut { get; init; }

	public string Provider { get; init; } = "uia";

	public IReadOnlyList<string> MatchedHints { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> SampleNames { get; init; } = Array.Empty<string>();
}
