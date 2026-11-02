using System;
using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class CourseDefinition
{
	private static readonly Version HostAssemblyVersion = typeof(CourseDefinition).Assembly.GetName().Version ?? new Version(0, 1, 0);

	public static string CurrentHostVersion { get; } = $"{HostAssemblyVersion.Major}.{HostAssemblyVersion.Minor}.{Math.Max(0, HostAssemblyVersion.Build)}";

	public string Id { get; init; } = "";

	public string Title { get; init; } = "";

	public string Subtitle { get; init; } = "";

	public string Summary { get; init; } = "";

	public string Version { get; init; } = "";

	public string FlowFingerprint { get; init; } = "";

	public string Track { get; init; } = "";

	public string Difficulty { get; init; } = "";

	public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();

	public string Status { get; init; } = "installed";

	public int StageCount { get; init; }

	public int EstimatedMinutes { get; init; }

	public bool IsBuiltIn { get; init; }

	public string ContentRoot { get; init; } = "";

	public string StagesDir { get; init; } = "";

	public string I18nDir { get; init; } = "";

	public string AssetsDir { get; init; } = "";

	public string? UpdateManifestUrl { get; init; }

	public string? AppUpdateManifestUrl { get; init; }

	public string MinHostVersion { get; init; } = "";

	public string MaxHostVersionExclusive { get; init; } = "";

	public string HostCompatibilityDisplay
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(MinHostVersion) && !string.IsNullOrWhiteSpace(MaxHostVersionExclusive))
			{
				return ">= " + MinHostVersion + ", < " + MaxHostVersionExclusive;
			}
			return "未声明";
		}
	}

	public bool IsCompatibilityRangeValid
	{
		get
		{
			SemanticVersion minimum;
			SemanticVersion maximum;
			return TryGetCompatibilityRange(out minimum, out maximum);
		}
	}

	public bool IsHostCompatible
	{
		get
		{
			if (!TryGetCompatibilityRange(out SemanticVersion minimum, out SemanticVersion maximum) || !SemanticVersion.TryParse(CurrentHostVersion, out SemanticVersion version))
			{
				return false;
			}
			if (version.CompareTo(minimum) >= 0)
			{
				return version.CompareTo(maximum) < 0;
			}
			return false;
		}
	}

	public IReadOnlyList<CourseRequirementGroup> RequirementGroups { get; init; } = Array.Empty<CourseRequirementGroup>();

	public IReadOnlyList<CourseChangelogEntry> Changelog { get; init; } = Array.Empty<CourseChangelogEntry>();

	public void Apply(PathService paths)
	{
		paths.UseContentPaths(ContentRoot, StagesDir, I18nDir, AssetsDir);
	}

	private bool TryGetCompatibilityRange(out SemanticVersion minimum, out SemanticVersion maximum)
	{
		minimum = null;
		maximum = null;
		if (SemanticVersion.TryParse(MinHostVersion, out minimum) && SemanticVersion.TryParse(MaxHostVersionExclusive, out maximum))
		{
			return minimum.CompareTo(maximum) < 0;
		}
		return false;
	}
}
