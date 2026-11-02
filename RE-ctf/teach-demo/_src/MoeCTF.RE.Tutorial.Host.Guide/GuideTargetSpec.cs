using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideTargetSpec
{
	[JsonPropertyName("scope")]
	public string Scope { get; set; } = "host";

	[JsonPropertyName("key")]
	public string? Key { get; set; }

	[JsonPropertyName("process")]
	public string? Process { get; set; }

	[JsonPropertyName("adapter")]
	public string? Adapter { get; set; }

	[JsonPropertyName("titleContains")]
	public List<string> TitleContains { get; set; } = new List<string>();

	[JsonPropertyName("processNameContains")]
	public List<string> ProcessNameContains { get; set; } = new List<string>();

	[JsonPropertyName("exeNameContains")]
	public List<string> ExeNameContains { get; set; } = new List<string>();

	[JsonPropertyName("exePathContains")]
	public List<string> ExePathContains { get; set; } = new List<string>();

	[JsonPropertyName("windowClassNameContains")]
	public List<string> WindowClassNameContains { get; set; } = new List<string>();

	[JsonPropertyName("titlePidFromProcess")]
	public string? TitlePidFromProcess { get; set; }
}
