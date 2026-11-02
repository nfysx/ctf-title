using System;
using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ProgressData
{
	public string ActiveCourseId { get; set; } = "freshman-re";

	public Dictionary<string, CourseProgressData> Courses { get; set; } = new Dictionary<string, CourseProgressData>(StringComparer.OrdinalIgnoreCase);

	public int StageIndex { get; set; }

	public List<string> ClearedStages { get; set; } = new List<string>();
}
