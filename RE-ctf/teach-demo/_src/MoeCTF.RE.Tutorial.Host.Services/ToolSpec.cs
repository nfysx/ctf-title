using System;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ToolSpec
{
	public string Id { get; init; } = "";

	public string DisplayName { get; init; } = "";

	public string Glyph { get; init; } = "";

	public string[] WindowTitleHints { get; init; } = Array.Empty<string>();

	public string[] ProcessNameHints { get; init; } = Array.Empty<string>();
}
