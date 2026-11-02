using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideBranchSpec
{
	[JsonPropertyName("trigger")]
	public string Trigger { get; set; } = "next";

	[JsonPropertyName("when")]
	public GuideConditionSpec When { get; set; } = new GuideConditionSpec();

	[JsonPropertyName("next")]
	public string? Next { get; set; }
}
