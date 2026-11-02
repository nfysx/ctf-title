using System;
using System.Collections.Generic;
using System.Linq;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal sealed class AppStateLocator
{
	public IReadOnlyList<string> TitleHints { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> ContentHints { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> RejectTitleHints { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> RejectContentHints { get; init; } = Array.Empty<string>();

	public int MinContentMatches { get; init; }

	public int MaxNodes { get; init; } = 4000;

	public int TimeoutMs { get; init; } = 1200;

	public int MinScore { get; init; } = 28;

	public bool StopAfterFirstMatch { get; init; }

	public ControlTextEvidence FindTitleEvidence(WindowInfo window)
	{
		return FindTitleEvidence(window, TitleHints);
	}

	public ControlTextEvidence FindRejectTitleEvidence(WindowInfo window)
	{
		return FindTitleEvidence(window, RejectTitleHints);
	}

	private static ControlTextEvidence FindTitleEvidence(WindowInfo window, IReadOnlyList<string> hints)
	{
		string[] array = hints.Where((string h) => !string.IsNullOrWhiteSpace(h) && window.Title.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		ControlTextEvidence controlTextEvidence = new ControlTextEvidence();
		controlTextEvidence.Score = array.Sum((string h) => 20 + Math.Min(h.Length, 24));
		controlTextEvidence.MatchedHints = array;
		controlTextEvidence.SampleNames = ((array.Length == 0) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[1] { window.Title }));
		return controlTextEvidence;
	}
}
