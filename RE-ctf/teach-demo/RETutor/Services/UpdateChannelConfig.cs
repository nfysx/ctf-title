using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RETutor.Services;

/// <summary>
/// 仿制版自己的更新渠道：与原版完全分离。
/// 读取顺序：环境变量 RETUTOR_UPDATE_CONFIG 指向的文件 → exe 同级 update-config.json → %LOCALAPPDATA%\RETutor\update-config.json。
/// 三者都没有 = 未配置更新源，只使用内嵌内容包（不访问任何原版地址）。
/// </summary>
public sealed class UpdateChannelConfig
{
    [JsonPropertyName("channel")] public string Channel { get; set; } = "retutor";
    [JsonPropertyName("courseUpdateUrl")] public string CourseUpdateUrl { get; set; }
    [JsonPropertyName("appUpdateUrl")] public string AppUpdateUrl { get; set; }
    [JsonPropertyName("notes")] public string Notes { get; set; }

    [JsonIgnore] public string SourcePath { get; private set; }

    public bool HasAnyEndpoint =>
        !string.IsNullOrWhiteSpace(CourseUpdateUrl) || !string.IsNullOrWhiteSpace(AppUpdateUrl);

    public static UpdateChannelConfig Load()
    {
        var candidates = new System.Collections.Generic.List<string>();
        var env = Environment.GetEnvironmentVariable("RETUTOR_UPDATE_CONFIG");
        if (!string.IsNullOrWhiteSpace(env)) candidates.Add(env);
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "update-config.json"));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RETutor", "update-config.json"));

        foreach (var path in candidates)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var config = JsonSerializer.Deserialize<UpdateChannelConfig>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (config == null) continue;
                config.SourcePath = path;
                if (string.IsNullOrWhiteSpace(config.Channel)) config.Channel = "retutor";
                return config;
            }
            catch { }
        }
        return new UpdateChannelConfig { SourcePath = null };
    }
}
