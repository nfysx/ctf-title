using System;
using System.Collections.Generic;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideRuntimeContext
{
	public required Window HostWindow { get; init; }

	public required IReadOnlyDictionary<string, FrameworkElement> HostHotspots { get; init; }

	public required IReadOnlyDictionary<string, ManagedProcess> Processes { get; init; }

	public required WindowTracker WindowTracker { get; init; }

	public WindowSnapshotCache? WindowSnapshots { get; init; }

	public required ControlLocator ControlLocator { get; init; }

	public GuideAppLocator AppLocator { get; init; } = new GuideAppLocator();

	public Func<string, string?> ValueProvider { get; init; } = (string _) => (string?)null;

	public Func<string, string?> TextProvider { get; init; } = (string _) => (string?)null;

	public IReadOnlyList<WindowInfo> VisibleWindows()
	{
		return WindowSnapshots?.GetSnapshot() ?? WindowTracker.EnumerateVisibleWindows();
	}
}
