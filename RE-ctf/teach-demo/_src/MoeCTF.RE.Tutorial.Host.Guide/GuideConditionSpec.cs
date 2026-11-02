using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideConditionSpec
{
	[JsonPropertyName("kind")]
	public string Kind { get; set; } = "always";

	[JsonPropertyName("not")]
	public bool Not { get; set; }

	[JsonPropertyName("process")]
	public string? Process { get; set; }

	[JsonPropertyName("key")]
	public string? Key { get; set; }

	[JsonPropertyName("value")]
	public string? Value { get; set; }

	[JsonPropertyName("valueKey")]
	public string? ValueKey { get; set; }

	[JsonPropertyName("locator")]
	public GuideLocatorSpec? Locator { get; set; }

	[JsonPropertyName("all")]
	public List<GuideConditionSpec> All { get; set; } = new List<GuideConditionSpec>();

	[JsonPropertyName("any")]
	public List<GuideConditionSpec> Any { get; set; } = new List<GuideConditionSpec>();
}
