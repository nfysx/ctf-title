using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RETutor.Services;

public sealed class CourseProgress
{
    [JsonPropertyName("stageIndex")] public int StageIndex { get; set; }
    [JsonPropertyName("stepIndex")] public int StepIndex { get; set; }
    [JsonPropertyName("clearedStages")] public List<string> ClearedStages { get; set; } = new();
    [JsonPropertyName("courseVersion")] public string CourseVersion { get; set; }
}

public sealed class ProgressFile
{
    [JsonPropertyName("activeCourseId")] public string ActiveCourseId { get; set; }
    [JsonPropertyName("courses")] public Dictionary<string, CourseProgress> Courses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProgressStore
{
    private readonly string _path;
    public ProgressFile Data { get; private set; } = new();

    public ProgressStore(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(path))
                Data = JsonSerializer.Deserialize<ProgressFile>(File.ReadAllText(path)) ?? new ProgressFile();
        }
        catch { Data = new ProgressFile(); }
    }

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RETutor", "progress.json");

    public CourseProgress ForCourse(string courseId, string version)
    {
        if (!Data.Courses.TryGetValue(courseId, out var p))
        {
            p = new CourseProgress { CourseVersion = version };
            Data.Courses[courseId] = p;
        }
        else if (!string.Equals(p.CourseVersion, version, StringComparison.Ordinal))
        {
            // 课程版本变化 → 该课进度归零（原程序的失效契约）
            p = new CourseProgress { CourseVersion = version };
            Data.Courses[courseId] = p;
        }
        return p;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
