using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ProgressStore
{
	private readonly string _path;

	public ProgressData Data { get; private set; } = new ProgressData();

	public ProgressStore(string path)
	{
		_path = path;
		Load();
	}

	public CourseProgressData ForCourse(string courseId, string? courseVersion = null, string? flowFingerprint = null)
	{
		if (!Data.Courses.TryGetValue(courseId, out CourseProgressData value) || !IdentityMatches(value, courseVersion, flowFingerprint))
		{
			value = new CourseProgressData
			{
				CourseVersion = courseVersion,
				FlowFingerprint = flowFingerprint
			};
			Data.Courses[courseId] = value;
		}
		return value;
	}

	public void SetActiveCourse(string courseId, string? courseVersion = null, string? flowFingerprint = null)
	{
		Data.ActiveCourseId = (string.IsNullOrWhiteSpace(courseId) ? "freshman-re" : courseId);
		MirrorLegacy(ForCourse(Data.ActiveCourseId, courseVersion, flowFingerprint));
	}

	public void ResetCourse(string courseId, string? courseVersion = null, string? flowFingerprint = null)
	{
		Data.Courses[courseId] = new CourseProgressData
		{
			CourseVersion = courseVersion,
			FlowFingerprint = flowFingerprint
		};
		if (string.Equals(Data.ActiveCourseId, courseId, StringComparison.OrdinalIgnoreCase))
		{
			MirrorLegacy(Data.Courses[courseId]);
		}
		Save();
	}

	public void MirrorLegacy(CourseProgressData progress)
	{
		Data.StageIndex = progress.StageIndex;
		Data.ClearedStages = progress.ClearedStages.ToList();
	}

	public void Load()
	{
		try
		{
			if (File.Exists(_path))
			{
				Data = JsonSerializer.Deserialize<ProgressData>(File.ReadAllText(_path)) ?? new ProgressData();
			}
		}
		catch
		{
			Data = new ProgressData();
		}
		if (string.IsNullOrWhiteSpace(Data.ActiveCourseId))
		{
			Data.ActiveCourseId = "freshman-re";
		}
		if (Data.Courses.TryGetValue(Data.ActiveCourseId, out CourseProgressData value))
		{
			MirrorLegacy(value);
		}
		else
		{
			MirrorLegacy(new CourseProgressData());
		}
	}

	public void Save()
	{
		string directoryName = Path.GetDirectoryName(_path);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		File.WriteAllText(_path, JsonSerializer.Serialize(Data, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}

	private static bool IdentityMatches(CourseProgressData progress, string? courseVersion, string? flowFingerprint)
	{
		if (string.IsNullOrWhiteSpace(courseVersion) && string.IsNullOrWhiteSpace(flowFingerprint))
		{
			return true;
		}
		if (string.Equals(progress.CourseVersion, courseVersion, StringComparison.Ordinal))
		{
			return string.Equals(progress.FlowFingerprint, flowFingerprint, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
