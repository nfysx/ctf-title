using System;
using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class CourseRequirementGroup
{
	public string Id { get; init; } = "";

	public string Title { get; init; } = "";

	public string Description { get; init; } = "";

	public string Mode { get; init; } = "allOf";

	public IReadOnlyList<CourseRequirement> Items { get; init; } = Array.Empty<CourseRequirement>();

	public string ModeNormalized
	{
		get
		{
			if (!string.Equals(Mode, "anyOf", StringComparison.OrdinalIgnoreCase))
			{
				return "allOf";
			}
			return "anyOf";
		}
	}
}
