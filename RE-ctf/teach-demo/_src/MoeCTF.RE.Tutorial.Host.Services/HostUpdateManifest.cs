using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class HostUpdateManifest
{
	[JsonPropertyName("schemaVersion")]
	public string SchemaVersion { get; set; } = "1";

	[JsonPropertyName("appVersion")]
	public string AppVersion { get; set; } = "";

	[JsonPropertyName("channel")]
	public string Channel { get; set; } = "stable";

	[JsonPropertyName("mandatory")]
	public bool Mandatory { get; set; }

	[JsonPropertyName("downloadUrl")]
	public string? DownloadUrl { get; set; }

	[JsonPropertyName("releaseNotesUrl")]
	public string? ReleaseNotesUrl { get; set; }

	[JsonPropertyName("sha256")]
	public string? Sha256 { get; set; }

	[JsonPropertyName("size")]
	public long? Size { get; set; }

	[JsonPropertyName("publishedAt")]
	public string? PublishedAt { get; set; }

	[JsonPropertyName("notes")]
	public string? Notes { get; set; }
}
