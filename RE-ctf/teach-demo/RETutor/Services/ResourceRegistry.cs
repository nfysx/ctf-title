using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RETutor.Services;

public sealed class TargetRecord
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("declaredSha256")] public string DeclaredSha256 { get; set; } = "";
}

/// <summary>
/// 用户自选的资源路径（工具 exe / 题目附件）落盘：%LOCALAPPDATA%\RETutor\resources.json
/// 卡片点击选文件、拖拽放入都会写这里；下次启动直接复用。
/// </summary>
public sealed class ResourceRegistry
{
    [JsonPropertyName("tools")] public Dictionary<string, string> Tools { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("targets")] public Dictionary<string, TargetRecord> Targets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RETutor", "resources.json");

    public static ResourceRegistry Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<ResourceRegistry>(File.ReadAllText(path), Options);
                if (loaded != null)
                {
                    loaded.Tools = new Dictionary<string, string>(loaded.Tools ?? new(), StringComparer.OrdinalIgnoreCase);
                    loaded.Targets = new Dictionary<string, TargetRecord>(loaded.Targets ?? new(), StringComparer.OrdinalIgnoreCase);
                    return loaded;
                }
            }
        }
        catch { }
        return new ResourceRegistry();
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
        catch { }
    }

    public string GetTool(string id) =>
        id != null && Tools.TryGetValue(id, out var v) && !string.IsNullOrWhiteSpace(v) && File.Exists(v) ? v : null;

    public void SetTool(string id, string path) => Tools[id] = path;

    public TargetRecord GetTarget(string id) =>
        id != null && Targets.TryGetValue(id, out var v) ? v : null;

    public void SetTarget(string id, TargetRecord rec) => Targets[id] = rec;

    public static string Sha256Of(string path)
    {
        try
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }
        catch { return ""; }
    }
}
