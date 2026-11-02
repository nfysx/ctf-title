using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ContentUpdateManifest
{
	[JsonPropertyName("contentVersion")]
	public string ContentVersion { get; set; } = "";

	[JsonPropertyName("courseId")]
	public string? CourseId { get; set; }

	[JsonPropertyName("courseVersion")]
	public string? CourseVersion { get; set; }

	[JsonPropertyName("schemaVersion")]
	public string SchemaVersion { get; set; } = "1";

	[JsonPropertyName("minHostVersion")]
	public string MinHostVersion { get; set; } = "";

	[JsonPropertyName("maxHostVersionExclusive")]
	public string? MaxHostVersionExclusive { get; set; }

	[JsonPropertyName("hostUpdateManifestUrl")]
	public string? HostUpdateManifestUrl { get; set; }

	[JsonPropertyName("notes")]
	public string? Notes { get; set; }

	[JsonPropertyName("publishedAt")]
	public string? PublishedAt { get; set; }

	[JsonPropertyName("prune")]
	public bool Prune { get; set; }

	[JsonPropertyName("delete")]
	public List<string> Delete { get; set; } = new List<string>();

	[JsonPropertyName("files")]
	public List<ContentUpdateFile> Files { get; set; } = new List<ContentUpdateFile>();
}
