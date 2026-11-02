using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideRectSpec
{
	[JsonPropertyName("x")]
	public double X { get; set; }

	[JsonPropertyName("y")]
	public double Y { get; set; }

	[JsonPropertyName("width")]
	public double Width { get; set; }

	[JsonPropertyName("height")]
	public double Height { get; set; }

	[JsonPropertyName("space")]
	public string Space { get; set; } = "screen";
}
