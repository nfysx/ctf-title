using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class CourseProgressData
{
	public string? CourseVersion { get; set; }

	public string? FlowFingerprint { get; set; }

	public string? StageId { get; set; }

	public int StageIndex { get; set; }

	public List<string> ClearedStages { get; set; } = new List<string>();
}
