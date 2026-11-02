using System;
using System.Diagnostics;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ManagedProcess
{
	public string Id { get; init; } = "";

	public string Kind { get; init; } = "";

	public Process Process { get; init; }

	public string? Path { get; init; }

	public string? ToolId { get; init; }

	public string[] TitleHints { get; init; } = Array.Empty<string>();

	public string[] ProcessHints { get; init; } = Array.Empty<string>();
}
