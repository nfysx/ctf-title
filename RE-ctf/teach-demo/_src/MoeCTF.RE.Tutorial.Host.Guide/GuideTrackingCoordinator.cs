using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;
using MoeCTF.RE.Tutorial.Host.Stages;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideTrackingCoordinator : IDisposable
{
	private sealed class RecognitionStabilizer
	{
		private sealed record RecognitionKey(long Revision, int Stage, int Step, int Pid, nint Hwnd);

		private const int RequiredObservations = 2;

		private static readonly TimeSpan ObservationLifetime = TimeSpan.FromMilliseconds(1400.0);

		private readonly object _gate = new object();

		private RecognitionKey? _key;

		private int _count;

		private long _lastSeen;

		private bool _delivered;

		public GuideRecognitionMatch? Observe(GuideRecognitionMatch? match, long revision)
		{
			lock (_gate)
			{
				if ((object)match == null)
				{
					if (_lastSeen != 0L && Stopwatch.GetElapsedTime(_lastSeen) > ObservationLifetime)
					{
						ResetCore();
					}
					return null;
				}
				WindowInfo targetWindow = match.Scene.RenderPlan.TargetWindow;
				RecognitionKey recognitionKey = new RecognitionKey(revision, match.Scene.StageIndex, match.Scene.StepIndex, targetWindow?.Pid ?? 0, targetWindow?.Hwnd ?? IntPtr.Zero);
				if (_key != recognitionKey || (_lastSeen != 0L && Stopwatch.GetElapsedTime(_lastSeen) > ObservationLifetime))
				{
					_key = recognitionKey;
					_count = 1;
					_delivered = false;
				}
				else
				{
					_count++;
				}
				_lastSeen = Stopwatch.GetTimestamp();
				if (_delivered || _count < 2)
				{
					return null;
				}
				_delivered = true;
				return match;
			}
		}

		public void Reset()
		{
			lock (_gate)
			{
				ResetCore();
			}
		}

		private void ResetCore()
		{
			_key = null;
			_count = 0;
			_lastSeen = 0L;
			_delivered = false;
		}
	}

	private static readonly TimeSpan ProcessRefreshInterval = TimeSpan.FromMilliseconds(300.0);

	private static readonly TimeSpan RecognitionInterval = TimeSpan.FromMilliseconds(240.0);

	private readonly GuideRuntime _runtime;

	private readonly ProcessManager _processes;

	private readonly TargetProcessObserver _targetObserver;

	private readonly WindowTracker _tracker;

	private readonly WindowSnapshotCache _windowSnapshots;

	private readonly ToolAdapter _tools;

	private readonly RequirementActivationService _requirements;

	private readonly PathService _paths;

	private readonly WindowChangeMonitor _windowChanges;

	private readonly object _processGate;

	private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

	private readonly RecognitionStabilizer _stabilizer = new RecognitionStabilizer();

	private long _lastProcessRefreshTimestamp;

	private long _lastRecognitionTimestamp;

	private int _overlayInFlight;

	private int _recognitionInFlight;

	public GuideTrackingCoordinator(GuideRuntime runtime, ProcessManager processes, TargetProcessObserver targetObserver, WindowTracker tracker, WindowSnapshotCache windowSnapshots, ControlLocator controlLocator, ToolAdapter tools, RequirementActivationService requirements, PathService paths, object processGate)
	{
		GuideTrackingCoordinator guideTrackingCoordinator = this;
		_runtime = runtime;
		_processes = processes;
		_targetObserver = targetObserver;
		_tracker = tracker;
		_windowSnapshots = windowSnapshots;
		_tools = tools;
		_requirements = requirements;
		_paths = paths;
		_processGate = processGate;
		_windowChanges = new WindowChangeMonitor(delegate(nint hwnd)
		{
			guideTrackingCoordinator._windowSnapshots.Invalidate();
			controlLocator.InvalidateWindow(hwnd);
		});
	}

	public void RequestScan(GuideTrackingRequest request, bool locateOverlay, Action<GuideOverlayScanResult> overlayCompleted, Action<GuideRecognitionMatch, long> recognitionCompleted)
	{
		if (!_lifetime.IsCancellationRequested)
		{
			if (locateOverlay)
			{
				StartOverlay(request, overlayCompleted);
			}
			StartRecognition(request, recognitionCompleted);
		}
	}

	public void ResetRecognition()
	{
		_stabilizer.Reset();
		_windowSnapshots.Invalidate();
	}

	private void StartOverlay(GuideTrackingRequest request, Action<GuideOverlayScanResult> completed)
	{
		if (Interlocked.CompareExchange(ref _overlayInFlight, 1, 0) != 0)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				RefreshProcessesIfDue(request.Stages);
				IReadOnlyList<TrackedWindowSnapshot> windows = BuildTrackedWindows();
				GuideOverlayMatch overlay = _runtime.LocateExternalOverlay(request.CurrentStage, request.StageIndex, request.StepIndex, 30, _lifetime.Token);
				completed(new GuideOverlayScanResult(windows, overlay, request.Revision));
			}
			catch (OperationCanceledException)
			{
			}
			catch
			{
				completed(new GuideOverlayScanResult(Array.Empty<TrackedWindowSnapshot>(), null, request.Revision));
			}
			finally
			{
				Volatile.Write(ref _overlayInFlight, 0);
			}
		}, CancellationToken.None);
	}

	private void StartRecognition(GuideTrackingRequest request, Action<GuideRecognitionMatch, long> completed)
	{
		if (ElapsedMilliseconds(_lastRecognitionTimestamp) < RecognitionInterval.TotalMilliseconds || Interlocked.CompareExchange(ref _recognitionInFlight, 1, 0) != 0)
		{
			return;
		}
		_lastRecognitionTimestamp = Stopwatch.GetTimestamp();
		Task.Run(delegate
		{
			try
			{
				RefreshProcessesIfDue(request.Stages);
				GuideRecognitionMatch match = _runtime.RecognizeCourse(request.Stages, 140, _lifetime.Token);
				GuideRecognitionMatch guideRecognitionMatch = _stabilizer.Observe(match, request.Revision);
				if ((object)guideRecognitionMatch != null)
				{
					completed(guideRecognitionMatch, request.Revision);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch
			{
				_stabilizer.Observe(null, request.Revision);
			}
			finally
			{
				Volatile.Write(ref _recognitionInFlight, 0);
			}
		}, CancellationToken.None);
	}

	private void RefreshProcessesIfDue(IReadOnlyList<StageDefinition> stages)
	{
		if (ElapsedMilliseconds(_lastProcessRefreshTimestamp) < ProcessRefreshInterval.TotalMilliseconds)
		{
			return;
		}
		lock (_processGate)
		{
			if (!(ElapsedMilliseconds(_lastProcessRefreshTimestamp) < ProcessRefreshInterval.TotalMilliseconds))
			{
				_processes.PruneExited();
				IEnumerable<string> toolIds = stages.SelectMany((StageDefinition stage) => stage.Tools).Distinct<string>(StringComparer.OrdinalIgnoreCase);
				IEnumerable<string> targetFiles = stages.SelectMany((StageDefinition stage) => stage.Targets).Distinct<string>(StringComparer.OrdinalIgnoreCase);
				_targetObserver.ObserveCurrentTools(_processes, toolIds, _tools.Get, _requirements.FindToolExecutable);
				_targetObserver.ObserveCurrentTargets(_processes, targetFiles, _paths.FindTarget);
				_lastProcessRefreshTimestamp = Stopwatch.GetTimestamp();
				_windowSnapshots.Invalidate();
			}
		}
	}

	private IReadOnlyList<TrackedWindowSnapshot> BuildTrackedWindows()
	{
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		List<TrackedWindowSnapshot> list = new List<TrackedWindowSnapshot>();
		ManagedProcess[] array = _processes.Processes.Values.ToArray();
		foreach (ManagedProcess managedProcess in array)
		{
			IReadOnlyList<WindowInfo> readOnlyList;
			try
			{
				readOnlyList = _tracker.FindWindowsForManaged(managedProcess);
			}
			catch
			{
				readOnlyList = Array.Empty<WindowInfo>();
			}
			if (readOnlyList.Count == 0)
			{
				bool flag = false;
				try
				{
					flag = managedProcess.Process.HasExited;
				}
				catch
				{
					flag = true;
				}
				if (flag)
				{
					lock (_processGate)
					{
						_processes.Forget(managedProcess.Id);
					}
					continue;
				}
				int? pid = null;
				try
				{
					pid = managedProcess.Process.Id;
				}
				catch
				{
				}
				list.Add(new TrackedWindowSnapshot(managedProcess.Id, pid, null, "", 0, 0, Waiting: true));
			}
			else
			{
				WindowInfo windowInfo = readOnlyList[0];
				string id = managedProcess.Id;
				int? pid2 = windowInfo.Pid;
				nint? hwnd = windowInfo.Hwnd;
				string title = windowInfo.Title;
				Rect bounds = windowInfo.Bounds;
				int width = (int)((Rect)(ref bounds)).Width;
				bounds = windowInfo.Bounds;
				list.Add(new TrackedWindowSnapshot(id, pid2, hwnd, title, width, (int)((Rect)(ref bounds)).Height, Waiting: false));
			}
		}
		return list;
	}

	private static double ElapsedMilliseconds(long timestamp)
	{
		if (timestamp != 0L)
		{
			return Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
		}
		return double.PositiveInfinity;
	}

	public void Dispose()
	{
		_lifetime.Cancel();
		_windowChanges.Dispose();
		_lifetime.Dispose();
	}
}
