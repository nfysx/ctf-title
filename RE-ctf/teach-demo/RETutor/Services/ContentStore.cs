using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RETutor.Models;

namespace RETutor.Services;

public sealed class LoadedCourse
{
    public CourseManifest Manifest { get; init; }
    public string CourseRoot { get; init; } = "";
    public List<StageDefinition> Stages { get; init; } = new();
    public Dictionary<string, string> I18n { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string AssetsDir { get; init; } = "";
}

/// <summary>
/// 对应原程序 Http：PathService + CourseCatalog + StageRepository + I18nService。
/// 目标：与原始内容包 100% 兼容（同一份 courses/、stages/、assets/i18n/ 直接可读）。
/// </summary>
public sealed class ContentStore
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string Root { get; }
    public Dictionary<string, string> GlobalI18n { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<LoadedCourse> Courses { get; } = new();
    public string GlobalAssetsDir => Path.Combine(Root, "assets");

    public ContentStore(string root) { Root = root; }

    public static string ResolveRoot()
    {
        var env = Environment.GetEnvironmentVariable("RETUTOR_CONTENT");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(Path.Combine(env, "courses", "index.json"))) return env;

        // 只认仿制版自己的目录，绝不读取原版的 MoeCTF.RE.Tutorial
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var own = Path.Combine(dir.FullName, ContentInstaller.FolderName);
            if (File.Exists(Path.Combine(own, "courses", "index.json"))) return own;
        }
        return Path.Combine(AppContext.BaseDirectory, "RETutorContent");
    }

    public void Load()
    {
        Courses.Clear();
        GlobalI18n.Clear();
        var global = Path.Combine(Root, "assets", "i18n", "zh-CN.json");
        if (File.Exists(global)) MergeI18n(global, GlobalI18n);

        var indexPath = Path.Combine(Root, "courses", "index.json");
        if (!File.Exists(indexPath)) return;
        var index = JsonSerializer.Deserialize<CourseIndex>(File.ReadAllText(indexPath), Json) ?? new CourseIndex();
        foreach (var rel in index.Courses)
        {
            var courseJson = Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(courseJson)) continue;
            var manifest = JsonSerializer.Deserialize<CourseManifest>(File.ReadAllText(courseJson), Json);
            if (manifest == null) continue;
            var courseRoot = Path.GetDirectoryName(courseJson) ?? Root;

            var i18n = new Dictionary<string, string>(GlobalI18n, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(manifest.Content?.I18nDir))
            {
                var p = Path.Combine(Root, manifest.Content.I18nDir.Replace('/', Path.DirectorySeparatorChar), "zh-CN.json");
                if (File.Exists(p)) MergeI18n(p, i18n);
            }

            var stages = new List<StageDefinition>();
            var stageIndexPath = Path.Combine(Root, (manifest.Content?.StageIndex ?? "").Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(stageIndexPath))
            {
                var sIdx = JsonSerializer.Deserialize<StageIndexFile>(File.ReadAllText(stageIndexPath), Json) ?? new StageIndexFile();
                var stagesDir = Path.GetDirectoryName(stageIndexPath);
                foreach (var name in sIdx.Stages)
                {
                    var f = Path.Combine(stagesDir, name + ".json");
                    if (!File.Exists(f)) f = Path.Combine(stagesDir, name);
                    if (!File.Exists(f)) continue;
                    var st = JsonSerializer.Deserialize<StageDefinition>(File.ReadAllText(f), Json);
                    if (st != null) stages.Add(st);
                }
            }
            stages.Sort((a, b) => a.Order.CompareTo(b.Order));

            Courses.Add(new LoadedCourse
            {
                Manifest = manifest,
                CourseRoot = courseRoot,
                Stages = stages,
                I18n = i18n,
                AssetsDir = string.IsNullOrWhiteSpace(manifest.Content?.AssetsDir)
                    ? courseRoot
                    : Path.Combine(Root, manifest.Content.AssetsDir.Replace('/', Path.DirectorySeparatorChar))
            });
        }
    }

    private static void MergeI18n(string path, Dictionary<string, string> target)
    {
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), Json);
            if (map == null) return;
            foreach (var kv in map) target[kv.Key] = kv.Value;
        }
        catch { }
    }

    public static string T(Dictionary<string, string> table, string key, params (string k, object v)[] args)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (table == null || !table.TryGetValue(key, out var text) || text == null) return key;
        foreach (var (k, v) in args)
            text = Regex.Replace(text, "\\{" + Regex.Escape(k) + "\\}", v?.ToString() ?? "");
        return text;
    }
}
