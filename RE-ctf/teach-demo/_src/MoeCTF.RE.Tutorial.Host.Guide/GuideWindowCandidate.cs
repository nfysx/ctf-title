using System;
using System.Collections.Generic;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideWindowCandidate
{
	public required WindowInfo Window { get; init; }

	public int Score { get; init; }

	public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}
