using System.Collections.Generic;
using System.Text.Json.Serialization;
using MoeCTF.RE.Tutorial.Host.Guide;

namespace MoeCTF.RE.Tutorial.Host.Stages;

public sealed class StageStep
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("textKey")]
	public string TextKey { get; set; } = "";

	[JsonPropertyName("bodyKey")]
	public string? BodyKey { get; set; }

	[JsonPropertyName("image")]
	public string? Image { get; set; }

	[JsonPropertyName("action")]
	public string Action { get; set; } = "none";

	[JsonPropertyName("tool")]
	public string? Tool { get; set; }

	[JsonPropertyName("file")]
	public string? File { get; set; }

	[JsonPropertyName("guide")]
	public GuideSpec? Guide { get; set; }

	[JsonPropertyName("trigger")]
	public GuideConditionSpec? Trigger { get; set; }

	[JsonPropertyName("branches")]
	public List<GuideBranchSpec> Branches { get; set; } = new List<GuideBranchSpec>();

	[JsonPropertyName("highlightScope")]
	public string? HighlightScope { get; set; }

	[JsonPropertyName("highlightHost")]
	public string? HighlightHost { get; set; }

	[JsonPropertyName("highlightProcess")]
	public string? HighlightProcess { get; set; }

	[JsonPropertyName("highlightNames")]
	public List<string> HighlightNames { get; set; } = new List<string>();

	[JsonPropertyName("highlightControlType")]
	public string? HighlightControlType { get; set; }
}
