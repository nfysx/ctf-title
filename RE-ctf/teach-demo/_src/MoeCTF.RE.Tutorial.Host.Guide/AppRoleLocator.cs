using System;
using System.Collections.Generic;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal sealed class AppRoleLocator
{
	public IReadOnlyList<string> Names { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> ContentHints { get; init; } = Array.Empty<string>();

	public string? AutomationId { get; init; }

	public string? ClassName { get; init; }

	public string? ControlType { get; init; }

	public bool RequireAllNames { get; init; }

	public bool AllowIdentityOnlyMatch { get; init; }

	public bool FallbackFirst { get; init; }

	public int MaxNodes { get; init; } = 3000;

	public int TimeoutMs { get; init; } = 1200;

	public int FallbackScore { get; init; } = 34;

	public Func<Rect, Rect> FallbackRegion { get; init; } = (Rect w) => w;

	public static AppRoleLocator Window()
	{
		return new AppRoleLocator
		{
			FallbackScore = 20
		};
	}

	public ControlLocatorOptions ToOptions(GuideLocatorSpec locator)
	{
		IReadOnlyList<string> readOnlyList2;
		if (Names.Count <= 0)
		{
			IReadOnlyList<string> readOnlyList = ((locator.NameContains.Count > 0) ? locator.NameContains : locator.ContentContains);
			readOnlyList2 = readOnlyList;
		}
		else
		{
			readOnlyList2 = Names;
		}
		IReadOnlyList<string> nameContains = readOnlyList2;
		return new ControlLocatorOptions
		{
			NameContains = nameContains,
			RequireAllNameContains = RequireAllNames,
			NameEquals = locator.NameEquals,
			AutomationId = FirstNonEmpty(locator.AutomationId, AutomationId),
			ClassName = FirstNonEmpty(locator.ClassName, ClassName),
			ControlType = FirstNonEmpty(locator.ControlType, ControlType),
			Backend = ControlBackend(locator.Strategy),
			MaxNodes = (locator.MaxNodes ?? MaxNodes),
			TimeoutMs = (locator.TimeoutMs ?? TimeoutMs),
			MinScore = 14,
			AllowTypeOnlyMatch = AllowIdentityOnlyMatch
		};
	}

	private static string? FirstNonEmpty(string? first, string? second)
	{
		if (string.IsNullOrWhiteSpace(first))
		{
			if (string.IsNullOrWhiteSpace(second))
			{
				return null;
			}
			return second;
		}
		return first;
	}

	private static string ControlBackend(string? strategy)
	{
		if (!((strategy ?? string.Empty).Trim().ToLowerInvariant() == "win32"))
		{
			return "auto";
		}
		return "win32";
	}
}
