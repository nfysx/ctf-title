using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ContentUpdateFile
{
	[JsonPropertyName("path")]
	public string Path { get; set; } = "";

	[JsonPropertyName("url")]
	public string? Url { get; set; }

	[JsonPropertyName("sha256")]
	public string? Sha256 { get; set; }
}
