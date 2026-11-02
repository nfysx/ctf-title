using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class WindowSnapshotCache
{
	private const double DefaultTtlMilliseconds = 120.0;

	private readonly WindowTracker _tracker;

	private readonly object _gate = new object();

	private IReadOnlyList<WindowInfo> _snapshot = Array.Empty<WindowInfo>();

	private long _timestamp;

	public WindowSnapshotCache(WindowTracker tracker)
	{
		_tracker = tracker;
	}

	public IReadOnlyList<WindowInfo> GetSnapshot(double maxAgeMilliseconds = 120.0)
	{
		lock (_gate)
		{
			if (_snapshot.Count > 0 && Stopwatch.GetElapsedTime(_timestamp).TotalMilliseconds <= maxAgeMilliseconds)
			{
				return _snapshot;
			}
			_snapshot = _tracker.EnumerateVisibleWindows().ToArray();
			_timestamp = Stopwatch.GetTimestamp();
			return _snapshot;
		}
	}

	public void Invalidate()
	{
		lock (_gate)
		{
			_timestamp = 0L;
		}
	}
}
