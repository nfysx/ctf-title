using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RETutor.Models;

namespace RETutor.Services;

public sealed class UpdateFileEntry
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    [JsonPropertyName("size")] public long? Size { get; set; }
}

public sealed class CourseUpdateManifest
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = "1";
    [JsonPropertyName("courseId")] public string CourseId { get; set; }
    [JsonPropertyName("courseVersion")] public string CourseVersion { get; set; } = "";
    [JsonPropertyName("minHostVersion")] public string MinHostVersion { get; set; }
    [JsonPropertyName("maxHostVersionExclusive")] public string MaxHostVersionExclusive { get; set; }
    [JsonPropertyName("notes")] public string Notes { get; set; }
    [JsonPropertyName("prune")] public bool Prune { get; set; }
    [JsonPropertyName("delete")] public List<string> Delete { get; set; } = new();
    [JsonPropertyName("files")] public List<UpdateFileEntry> Files { get; set; } = new();
    [JsonPropertyName("hostUpdateManifestUrl")] public string HostUpdateManifestUrl { get; set; }
}

public sealed class HostUpdateManifest
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = "1";
    [JsonPropertyName("appVersion")] public string AppVersion { get; set; } = "";
    [JsonPropertyName("channel")] public string Channel { get; set; } = "stable";
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; }
    [JsonPropertyName("releaseNotesUrl")] public string ReleaseNotesUrl { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; }
    [JsonPropertyName("size")] public long? Size { get; set; }
    [JsonPropertyName("publishedAt")] public string PublishedAt { get; set; }
}

public sealed class CheckResult
{
    public bool Reachable;
    public bool Available;
    public string LocalVersion = "";
    public string RemoteVersion = "";
    public string Error;
    public string Channel;
    public CourseUpdateManifest Course;
    public HostUpdateManifest Host;
}

/// <summary>
/// 真·在线更新（对应原程序 ContentUpdater / HostUpdateInstaller 的核心子集）：
/// 拉清单 → 比版本 → 逐文件下载 + SHA-256 校验 → 落到内容根；程序更新只下载并校验，不自我替换。
/// </summary>
public sealed class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    public UpdateChannelConfig Channel { get; } = UpdateChannelConfig.Load();

    /// <summary>raw.githubusercontent 常被墙/限流：自动再试一次 jsDelivr 镜像（与原程序的多源策略同思路）。</summary>
    private static IEnumerable<string> Mirrors(string url)
    {
        yield return url;
        if (url.IndexOf("raw.githubusercontent.com/", StringComparison.OrdinalIgnoreCase) < 0) yield break;
        Uri uri;
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) yield break;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length >= 6 && parts[2] == "refs")
        {
            var owner = parts[0];
            var repo = parts[1];
            var kind = parts[3];            // heads / tags
            var branch = parts[4];
            var path = string.Join("/", parts.Skip(5));
            yield return "https://cdn.jsdelivr.net/gh/" + owner + "/" + repo + "@" + branch + "/" + path;
            if (kind == "heads")
                yield return "https://cdn.statically.io/gh/" + owner + "/" + repo + "/" + branch + "/" + path;
        }
    }

    private static async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        Exception last = null;
        foreach (var candidate in Mirrors(url))
        {
            try { return await Http.GetStringAsync(candidate, ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { last = new TimeoutException(candidate); }
            catch (Exception ex) { last = ex; }
        }
        throw last ?? new InvalidOperationException("no mirror reachable");
    }

    private static async Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
    {
        Exception last = null;
        foreach (var candidate in Mirrors(url))
        {
            try { return await Http.GetByteArrayAsync(candidate, ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { last = new TimeoutException(candidate); }
            catch (Exception ex) { last = ex; }
        }
        throw last ?? new InvalidOperationException("no mirror reachable");
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "RETutor/0.1.0 (+WPF)");
        return client;
    }

    public async Task<CheckResult> CheckCourseAsync(LoadedCourse course, string url, CancellationToken ct)
    {
        var result = new CheckResult { LocalVersion = course.Manifest.Version };
        if (string.IsNullOrWhiteSpace(url)) { result.Error = "no-manifest-url"; return result; }
        try
        {
            var json = await GetStringAsync(url, ct);
            var manifest = JsonSerializer.Deserialize<CourseUpdateManifest>(json, ContentStore.Json);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.CourseVersion))
            {
                result.Error = "bad-manifest";
                return result;
            }
            result.Reachable = true;
            result.Course = manifest;
            result.RemoteVersion = manifest.CourseVersion;
            result.Available = VersionUtil.Compare(manifest.CourseVersion, course.Manifest.Version) > 0;
        }
        catch (Exception ex) { result.Error = ex.GetType().Name + ": " + ex.Message; }
        return result;
    }

    public async Task<CheckResult> CheckHostAsync(string url, CancellationToken ct)
    {
        var result = new CheckResult { LocalVersion = VersionUtil.HostVersion };
        if (string.IsNullOrWhiteSpace(url)) { result.Error = "no-manifest-url"; return result; }
        try
        {
            var json = await GetStringAsync(url, ct);
            var manifest = JsonSerializer.Deserialize<HostUpdateManifest>(json, ContentStore.Json);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.AppVersion)) { result.Error = "bad-manifest"; return result; }
            result.Reachable = true;
            result.Host = manifest;
            result.RemoteVersion = manifest.AppVersion;
            result.Available = VersionUtil.Compare(manifest.AppVersion, VersionUtil.HostVersion) > 0;
            result.Channel = manifest.Channel;
        }
        catch (Exception ex) { result.Error = ex.GetType().Name + ": " + ex.Message; }
        return result;
    }

    /// <summary>下载 + 校验 + 落盘；任何一步失败都不会改动内容根（先全部落到 staging）。</summary>
    public async Task<string> InstallCourseAsync(CourseUpdateManifest manifest, string contentRoot, Action<int, int, string> progress, CancellationToken ct)
    {
        if (manifest?.Files == null || manifest.Files.Count == 0) return "manifest has no files";
        var rootFull = Path.GetFullPath(contentRoot);
        var staging = Path.Combine(rootFull, ".update-staging");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);

        int done = 0, total = manifest.Files.Count;
        var staged = new List<(string rel, string stagedPath)>();
        foreach (var file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            var rel = (file.Path ?? "").Replace('/', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(Path.Combine(rootFull, rel));
            if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("unsafe path: " + file.Path);

            var bytes = await GetBytesAsync(file.Url, ct);
            var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.IsNullOrEmpty(file.Sha256) && !string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("sha256 mismatch: " + file.Path);

            var stagedPath = Path.Combine(staging, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
            await File.WriteAllBytesAsync(stagedPath, bytes, ct);
            staged.Add((rel, stagedPath));
            done++;
            progress?.Invoke(done, total, file.Path);
        }

        foreach (var (rel, stagedPath) in staged)
        {
            var target = Path.Combine(rootFull, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(stagedPath, target, overwrite: true);
        }

        foreach (var rel in manifest.Delete ?? new List<string>())
        {
            var target = Path.GetFullPath(Path.Combine(rootFull, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) && File.Exists(target)) File.Delete(target);
        }

        Directory.Delete(staging, true);
        return null;   // 成功
    }

    /// <summary>程序更新：只下载到临时目录并校验，不自动替换正在运行的 exe。</summary>
    public async Task<(bool ok, string path, string error)> DownloadHostAsync(HostUpdateManifest manifest, CancellationToken ct)
    {
        try
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.DownloadUrl)) return (false, null, "no-download-url");
            var dir = Path.Combine(Path.GetTempPath(), "RETutor-update");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "RETutor-" + manifest.AppVersion + ".exe");
            var bytes = await GetBytesAsync(manifest.DownloadUrl, ct);
            var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.IsNullOrEmpty(manifest.Sha256) && !string.Equals(actual, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                return (false, null, "sha256 mismatch");
            await File.WriteAllBytesAsync(path, bytes, ct);
            return (true, path, null);
        }
        catch (Exception ex) { return (false, null, ex.Message); }
    }
}

public static class VersionUtil
{
    public static string HostVersion
    {
        get
        {
            var v = typeof(VersionUtil).Assembly.GetName().Version;
            return v == null ? "0.1.0" : $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
        }
    }

    /// <summary>宽松 semver 比较：按数字段比，缺段当 0；含后缀的一律视为更低（够用即可）。</summary>
    public static int Compare(string a, string b)
    {
        var pa = Split(a); var pb = Split(b);
        int n = Math.Max(pa.Count, pb.Count);
        for (int i = 0; i < n; i++)
        {
            int x = i < pa.Count ? pa[i] : 0;
            int y = i < pb.Count ? pb[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }

    private static List<int> Split(string s)
    {
        var list = new List<int>();
        if (string.IsNullOrWhiteSpace(s)) return list;
        var core = s.Split('-', '+')[0];
        foreach (var part in core.Split('.'))
            list.Add(int.TryParse(part, out var v) ? v : 0);
        return list;
    }
}
