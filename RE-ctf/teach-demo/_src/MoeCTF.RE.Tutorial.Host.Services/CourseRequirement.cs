using System;
using System.Collections.Generic;
using System.IO;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class CourseRequirement
{
	public string Id { get; init; } = "";

	public string Kind { get; init; } = "target";

	public string Label { get; init; } = "";

	public string Description { get; init; } = "";

	public string? ToolId { get; init; }

	public string? FileName { get; init; }

	public string? AssetPath { get; init; }

	public IReadOnlyList<string> MatchNames { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> MatchPathContains { get; init; } = Array.Empty<string>();

	public string? MatchSha256 { get; init; }

	public long? MatchSize { get; init; }

	public string KindNormalized
	{
		get
		{
			if (!string.Equals(Kind, "tool", StringComparison.OrdinalIgnoreCase))
			{
				return "target";
			}
			return "tool";
		}
	}

	public string ToolIdOrDefault()
	{
		if (!string.IsNullOrWhiteSpace(ToolId))
		{
			return ToolId;
		}
		return Id;
	}

	public string ToolExecutableName()
	{
		return ToolIdOrDefault().ToLowerInvariant() switch
		{
			"die" => "die.exe", 
			"ida" => "ida.exe", 
			"dnspy" => "dnSpy.exe", 
			"ghidra" => "ghidraRun.bat", 
			"x64dbg" => "x64dbg.exe", 
			_ => ToolIdOrDefault() + ".exe", 
		};
	}

	public string FileNameOrDefault(string? fallback = null)
	{
		if (!string.IsNullOrWhiteSpace(FileName))
		{
			return FileName;
		}
		if (!string.IsNullOrWhiteSpace(AssetPath))
		{
			return Path.GetFileName(AssetPath.Replace('/', Path.DirectorySeparatorChar));
		}
		return fallback ?? Id;
	}

	public string AssetPathOrDefault(string? fallbackFileName = null)
	{
		if (!string.IsNullOrWhiteSpace(AssetPath))
		{
			return AssetPath.Replace('\\', '/').TrimStart('/');
		}
		return "assets/targets/" + FileNameOrDefault(fallbackFileName).Replace('\\', '/');
	}
}
