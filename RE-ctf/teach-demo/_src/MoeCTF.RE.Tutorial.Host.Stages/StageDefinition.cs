using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Stages;

public sealed class StageDefinition
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("order")]
	public int Order { get; set; }

	[JsonPropertyName("titleKey")]
	public string TitleKey { get; set; } = "";

	[JsonPropertyName("goalKey")]
	public string GoalKey { get; set; } = "";

	[JsonPropertyName("answerKey")]
	public string AnswerKey { get; set; } = "";

	[JsonPropertyName("completionMode")]
	public string CompletionMode { get; set; } = "answer";

	[JsonPropertyName("fakeAnswerKeys")]
	public List<string> FakeAnswerKeys { get; set; } = new List<string>();

	[JsonPropertyName("targets")]
	public List<string> Targets { get; set; } = new List<string>();

	[JsonPropertyName("tools")]
	public List<string> Tools { get; set; } = new List<string>();

	[JsonPropertyName("guideMode")]
	public string GuideMode { get; set; } = "forced";

	[JsonPropertyName("freeExploreTextKey")]
	public string? FreeExploreTextKey { get; set; }

	[JsonPropertyName("deviations")]
	public List<GuideDeviationSpec> Deviations { get; set; } = new List<GuideDeviationSpec>();

	[JsonPropertyName("steps")]
	public List<StageStep> Steps { get; set; } = new List<StageStep>();
}
