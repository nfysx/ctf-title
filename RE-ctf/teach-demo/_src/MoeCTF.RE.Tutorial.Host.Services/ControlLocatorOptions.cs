using System;
using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ControlLocatorOptions
{
	public IReadOnlyList<string> NameContains { get; init; } = Array.Empty<string>();

	public bool RequireAllNameContains { get; init; }

	public string? NameEquals { get; init; }

	public string? AutomationId { get; init; }

	public string? ClassName { get; init; }

	public string? ControlType { get; init; }

	public string Backend { get; init; } = "auto";

	public int MaxNodes { get; init; } = 2000;

	public int TimeoutMs { get; init; } = 1200;

	public int MinScore { get; init; } = 8;

	public bool AllowTypeOnlyMatch { get; init; }
}
