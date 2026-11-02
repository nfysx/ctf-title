using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideRectAdjustSpec
{
	[JsonPropertyName("offsetX")]
	public double OffsetX { get; set; }

	[JsonPropertyName("offsetY")]
	public double OffsetY { get; set; }

	[JsonPropertyName("inflate")]
	public double Inflate { get; set; }

	[JsonPropertyName("width")]
	public double? Width { get; set; }

	[JsonPropertyName("height")]
	public double? Height { get; set; }
}
