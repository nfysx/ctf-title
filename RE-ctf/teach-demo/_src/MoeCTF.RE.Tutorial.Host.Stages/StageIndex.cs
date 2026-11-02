using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Stages;

public sealed class StageIndex
{
	[JsonPropertyName("stages")]
	public List<string> Stages { get; set; } = new List<string>();
}
