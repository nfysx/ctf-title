using System.Text.Json.Serialization;
using MoeCTF.RE.Tutorial.Host.Guide;

namespace MoeCTF.RE.Tutorial.Host.Stages;

public sealed class GuideDeviationSpec
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("textKey")]
	public string TextKey { get; set; } = "";

	[JsonPropertyName("bodyKey")]
	public string? BodyKey { get; set; }

	[JsonPropertyName("image")]
	public string? Image { get; set; }

	[JsonPropertyName("priority")]
	public int Priority { get; set; }

	[JsonPropertyName("when")]
	public GuideConditionSpec When { get; set; } = new GuideConditionSpec();

	[JsonPropertyName("guide")]
	public GuideSpec? Guide { get; set; }
}
