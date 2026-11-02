using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace RETutor.Services;

/// <summary>
/// 单文件分发：内容包内嵌为资源，首次运行解到 exe 同级 RETutorContent\。
/// 用 .retutor-content-version 记录"包指纹"：包一变（内嵌文案更新）就重新解，老目录自动刷新。
/// </summary>
public static class ContentInstaller
{
    private const string ResourceName = "RETutor.Content.zip";
    public const string FolderName = "RETutorContent";
    private const string VersionFile = ".retutor-content-version";

    public static string EnsureInstalled()
    {
        var target = Path.Combine(AppContext.BaseDirectory, FolderName);
        var marker = Path.Combine(target, "courses", "index.json");
        var stampPath = Path.Combine(target, VersionFile);

        byte[] payload = null;
        string stamp = null;
        try
        {
            using var probe = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (probe == null) return File.Exists(marker) ? target : null;
            using var ms = new MemoryStream();
            probe.CopyTo(ms);
            payload = ms.ToArray();
            stamp = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        }
        catch { return File.Exists(marker) ? target : null; }

        var installed = File.Exists(stampPath) ? File.ReadAllText(stampPath).Trim() : null;
        if (File.Exists(marker) && string.Equals(installed, stamp, StringComparison.OrdinalIgnoreCase))
            return target;   // 已是最新内容

        try
        {
            Directory.CreateDirectory(target);
            using var zip = new ZipArchive(new MemoryStream(payload), ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var rel = entry.FullName.Replace('\\', '/');
                if (rel.StartsWith("/") || rel.Contains("..")) continue;
                var full = Path.GetFullPath(Path.Combine(target, rel.Replace('/', Path.DirectorySeparatorChar)));
                if (!full.StartsWith(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                entry.ExtractToFile(full, overwrite: true);
            }
            File.WriteAllText(stampPath, stamp);
            return File.Exists(marker) ? target : null;
        }
        catch { return File.Exists(marker) ? target : null; }
    }
}
