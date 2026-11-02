using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Services;

internal static class BundledContentInstaller
{
	private sealed record ValidatedEntry(ZipArchiveEntry Entry, string RelativePath, string DestinationPath);

	private sealed class BundledContentState
	{
		[JsonPropertyName("files")]
		public Dictionary<string, string> Files { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	}

	internal const string ResourceName = "MoeCTF.RE.Tutorial.Host.BundledContent.zip";

	private const string StateRelativePath = "config/bundled-content-state.json";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public static bool Install(string destinationRoot)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot, "destinationRoot");
		using Stream stream = typeof(BundledContentInstaller).Assembly.GetManifestResourceStream("MoeCTF.RE.Tutorial.Host.BundledContent.zip");
		if (stream == null)
		{
			return false;
		}
		string fullPath = Path.GetFullPath(destinationRoot);
		Directory.CreateDirectory(fullPath);
		if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
		{
			throw new InvalidDataException("Bundled content install root cannot be a reparse point: " + fullPath);
		}
		string text = Path.Combine(fullPath, "config/bundled-content-state.json".Replace('/', Path.DirectorySeparatorChar));
		BundledContentState bundledContentState = LoadState(text);
		BundledContentState bundledContentState2 = new BundledContentState();
		using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
		foreach (ValidatedEntry item in ValidateEntries(archive, fullPath))
		{
			string text2 = HashEntry(item.Entry);
			bundledContentState2.Files[item.RelativePath] = text2;
			EnsureSafeDirectory(fullPath, Path.GetDirectoryName(item.DestinationPath));
			if (File.Exists(item.DestinationPath))
			{
				string a = HashFile(item.DestinationPath);
				if (string.Equals(a, text2, StringComparison.OrdinalIgnoreCase) || !bundledContentState.Files.TryGetValue(item.RelativePath, out string value) || !string.Equals(a, value, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
			}
			WriteEntryAtomically(item.Entry, item.DestinationPath, fullPath);
		}
		WriteJsonAtomically(text, bundledContentState2, fullPath);
		return true;
	}

	private static IReadOnlyList<ValidatedEntry> ValidateEntries(ZipArchive archive, string root)
	{
		string value = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<ValidatedEntry> list = new List<ValidatedEntry>();
		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			string text = entry.FullName.Replace('\\', '/');
			if (!text.EndsWith("/", StringComparison.Ordinal))
			{
				string[] array = text.Split('/');
				if (string.IsNullOrWhiteSpace(text) || Path.IsPathRooted(text) || array.Any(IsUnsafePathPart))
				{
					throw new InvalidDataException("Unsafe bundled content path: " + entry.FullName);
				}
				string text2 = string.Join('/', array);
				if (string.Equals(text2, "config/bundled-content-state.json", StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("Bundled content cannot replace installer state: " + entry.FullName);
				}
				if (!hashSet.Add(text2))
				{
					throw new InvalidDataException("Duplicate bundled content path: " + entry.FullName);
				}
				string fullPath = Path.GetFullPath(Path.Combine(root, text2.Replace('/', Path.DirectorySeparatorChar)));
				if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("Bundled content path escapes the install root: " + entry.FullName);
				}
				if (!hashSet2.Add(fullPath))
				{
					throw new InvalidDataException("Bundled content paths resolve to the same file: " + entry.FullName);
				}
				list.Add(new ValidatedEntry(entry, text2, fullPath));
			}
		}
		return list;
	}

	private static bool IsUnsafePathPart(string part)
	{
		bool flag = string.IsNullOrWhiteSpace(part);
		if (!flag)
		{
			bool flag2 = ((part == "." || part == "..") ? true : false);
			flag = flag2;
		}
		if (!flag && !part.EndsWith(' ') && !part.EndsWith('.'))
		{
			return part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
		}
		return true;
	}

	private static BundledContentState LoadState(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return new BundledContentState();
			}
			BundledContentState bundledContentState = JsonSerializer.Deserialize<BundledContentState>(File.ReadAllText(path), JsonOptions);
			return (bundledContentState?.Files == null) ? new BundledContentState() : new BundledContentState
			{
				Files = new Dictionary<string, string>(bundledContentState.Files, StringComparer.OrdinalIgnoreCase)
			};
		}
		catch (JsonException)
		{
			return new BundledContentState();
		}
		catch (IOException)
		{
			return new BundledContentState();
		}
	}

	private static string HashEntry(ZipArchiveEntry entry)
	{
		using Stream source = entry.Open();
		return Convert.ToHexString(SHA256.HashData(source));
	}

	private static string HashFile(string path)
	{
		using FileStream source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		return Convert.ToHexString(SHA256.HashData(source));
	}

	private static void WriteEntryAtomically(ZipArchiveEntry entry, string destination, string root)
	{
		string text = Path.GetDirectoryName(destination) ?? throw new InvalidDataException("Bundled content has no parent directory: " + entry.FullName);
		EnsureSafeDirectory(root, text);
		string text2 = Path.Combine(text, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
		try
		{
			using (Stream stream = entry.Open())
			{
				using FileStream fileStream = new FileStream(text2, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough);
				stream.CopyTo(fileStream);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text2, destination, overwrite: true);
		}
		finally
		{
			if (File.Exists(text2))
			{
				File.Delete(text2);
			}
		}
	}

	private static void WriteJsonAtomically(string destination, BundledContentState state, string root)
	{
		string directoryName = Path.GetDirectoryName(destination);
		EnsureSafeDirectory(root, directoryName);
		string text = Path.Combine(directoryName, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
		try
		{
			byte[] array = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
			{
				fileStream.Write(array);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text, destination, overwrite: true);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}

	private static void EnsureSafeDirectory(string root, string directory)
	{
		string relativePath = Path.GetRelativePath(root, directory);
		string text = root;
		string[] array = relativePath.Split(new char[2]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar
		}, StringSplitOptions.RemoveEmptyEntries);
		foreach (string path in array)
		{
			text = Path.Combine(text, path);
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			if ((File.GetAttributes(text) & FileAttributes.ReparsePoint) != 0)
			{
				throw new InvalidDataException("Bundled content destination contains a reparse point: " + text);
			}
		}
	}
}
