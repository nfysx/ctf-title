using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace RETutor.Services;

/// <summary>
/// 界面文案，两级来源：
///   1) exe 内嵌的 UiStrings.json（编译时就烧进程序，换台机器不丢文案）
///   2) 外部 ui.zh-CN.json（exe 同级 或 {内容根}/assets/i18n/）—— 覆盖内嵌值，改了不用重编译
/// 再往下才是代码里的 S("键","默认值")。
/// </summary>
public static class UiStrings
{
    private const string EmbeddedName = "RETutor.UiStrings.json";
    private static readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);

    public static string SourcePath { get; private set; } = "(代码内置默认值)";
    public static int OverrideCount { get; private set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static void Load(string contentRoot)
    {
        Values.Clear();
        OverrideCount = 0;
        SourcePath = "(代码内置默认值)";

        // 1) 内嵌
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd(), Options);
                if (map != null)
                {
                    foreach (var kv in map)
                        if (!string.IsNullOrEmpty(kv.Key)) Values[kv.Key] = kv.Value ?? "";
                    SourcePath = "(exe 内嵌 UiStrings.json)";
                    OverrideCount = Values.Count;
                }
            }
        }
        catch { }

        // 2) 外部覆盖
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(contentRoot))
            candidates.Add(Path.Combine(contentRoot, "assets", "i18n", "ui.zh-CN.json"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "ui.zh-CN.json"));

        foreach (var path in candidates)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), Options);
                if (map == null) continue;
                foreach (var kv in map)
                    if (!string.IsNullOrEmpty(kv.Key)) Values[kv.Key] = kv.Value ?? "";
                SourcePath = path;
                OverrideCount = Values.Count;
                break;
            }
            catch
            {
                SourcePath = path + "（解析失败，已忽略）";
            }
        }
    }

    public static string T(string key, string fallback) =>
        key != null && Values.TryGetValue(key, out var v) && v != null ? v : fallback;
}
