using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideLocatorSpec
{
	[JsonPropertyName("strategy")]
	public string Strategy { get; set; } = "auto";

	[JsonPropertyName("scope")]
	public string? Scope { get; set; }

	[JsonPropertyName("key")]
	public string? Key { get; set; }

	[JsonPropertyName("process")]
	public string? Process { get; set; }

	[JsonPropertyName("adapter")]
	public string? Adapter { get; set; }

	[JsonPropertyName("state")]
	public string? State { get; set; }

	[JsonPropertyName("role")]
	public string? Role { get; set; }

	[JsonPropertyName("nameEquals")]
	public string? NameEquals { get; set; }

	[JsonPropertyName("nameContains")]
	public List<string> NameContains { get; set; } = new List<string>();

	[JsonPropertyName("contentContains")]
	public List<string> ContentContains { get; set; } = new List<string>();

	[JsonPropertyName("documentContains")]
	public List<string> DocumentContains { get; set; } = new List<string>();

	[JsonPropertyName("contentMode")]
	public string ContentMode { get; set; } = "evidence";

	[JsonPropertyName("rejectTitleContains")]
	public List<string> RejectTitleContains { get; set; } = new List<string>();

	[JsonPropertyName("rejectContentContains")]
	public List<string> RejectContentContains { get; set; } = new List<string>();

	[JsonPropertyName("requireAllNameContains")]
	public bool RequireAllNameContains { get; set; }

	[JsonPropertyName("automationId")]
	public string? AutomationId { get; set; }

	[JsonPropertyName("className")]
	public string? ClassName { get; set; }

	[JsonPropertyName("controlType")]
	public string? ControlType { get; set; }

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

	[JsonPropertyName("rect")]
	public GuideRectSpec? Rect { get; set; }

	[JsonPropertyName("adjust")]
	public GuideRectAdjustSpec? Adjust { get; set; }

	[JsonPropertyName("fallbackToWindow")]
	public bool FallbackToWindow { get; set; }

	[JsonPropertyName("maxNodes")]
	public int? MaxNodes { get; set; }

	[JsonPropertyName("timeoutMs")]
	public int? TimeoutMs { get; set; }
}
