using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class RequirementActivationState
{
	[JsonPropertyName("tools")]
	public Dictionary<string, string> Tools { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	[JsonPropertyName("courseTargets")]
	public Dictionary<string, Dictionary<string, string>> CourseTargets { get; set; } = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

	[JsonPropertyName("courseChoices")]
	public Dictionary<string, Dictionary<string, string>> CourseChoices { get; set; } = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
}
