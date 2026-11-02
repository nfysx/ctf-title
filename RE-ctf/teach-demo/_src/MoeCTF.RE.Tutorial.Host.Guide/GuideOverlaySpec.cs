using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideOverlaySpec
{
	[JsonPropertyName("placement")]
	public string Placement { get; set; } = "auto";

	[JsonPropertyName("alignment")]
	public string Alignment { get; set; } = "center";

	[JsonPropertyName("padding")]
	public double Padding { get; set; } = 6.0;

	[JsonPropertyName("gap")]
	public double Gap { get; set; } = 14.0;

	[JsonPropertyName("offsetX")]
	public double OffsetX { get; set; }

	[JsonPropertyName("offsetY")]
	public double OffsetY { get; set; }

	[JsonPropertyName("maxWidth")]
	public double? MaxWidth { get; set; }
}
