using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ControlLocator
{
	private delegate bool EnumChildProc(nint hWnd, nint lParam);

	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private sealed record ChildWindowInfo(nint Hwnd, string Name, string ClassName, Rect Bounds)
	{
		[CompilerGenerated]
		private bool PrintMembers(StringBuilder builder)
		{
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			RuntimeHelpers.EnsureSufficientExecutionStack();
			builder.Append("Hwnd = ");
			builder.Append(((object)Hwnd/*cast due to .constrained prefix*/).ToString());
			builder.Append(", Name = ");
			builder.Append((object?)Name);
			builder.Append(", ClassName = ");
			builder.Append((object?)ClassName);
			builder.Append(", Bounds = ");
			Rect bounds = Bounds;
			builder.Append(((object)(Rect)(ref bounds)/*cast due to .constrained prefix*/).ToString());
			return true;
		}
	}

	private sealed record UiaTextPiece(string Text, string Source);

	private sealed record TextHintMatch(string Hint, string Source);

	private sealed record UiaNodeSnapshot(int Depth, string Name, string AutomationId, string ClassName, string ControlType, bool IsOffscreen, Rect Bounds, IReadOnlyList<UiaTextPiece> Text)
	{
		[CompilerGenerated]
		private bool PrintMembers(StringBuilder builder)
		{
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			RuntimeHelpers.EnsureSufficientExecutionStack();
			builder.Append("Depth = ");
			builder.Append(Depth.ToString());
			builder.Append(", Name = ");
			builder.Append((object?)Name);
			builder.Append(", AutomationId = ");
			builder.Append((object?)AutomationId);
			builder.Append(", ClassName = ");
			builder.Append((object?)ClassName);
			builder.Append(", ControlType = ");
			builder.Append((object?)ControlType);
			builder.Append(", IsOffscreen = ");
			builder.Append(IsOffscreen.ToString());
			builder.Append(", Bounds = ");
			Rect bounds = Bounds;
			builder.Append(((object)(Rect)(ref bounds)/*cast due to .constrained prefix*/).ToString());
			builder.Append(", Text = ");
			builder.Append(Text);
			return true;
		}
	}

	private sealed record UiaWindowSnapshot(IReadOnlyList<UiaNodeSnapshot> Nodes, bool TimedOut, long ElapsedMilliseconds);

	private sealed record CacheEntry<T>(T Value, long Timestamp);

	private sealed class SemaphoreLease
	{
		private readonly SemaphoreSlim _semaphore;

		private int _released;

		public SemaphoreLease(SemaphoreSlim semaphore)
		{
			_semaphore = semaphore;
		}

		public void Release()
		{
			if (Interlocked.Exchange(ref _released, 1) == 0)
			{
				_semaphore.Release();
			}
		}
	}

	[CompilerGenerated]
	private sealed class _003CFindExactTextPropertyMatches_003Ed__48 : IEnumerable<AutomationElement>, IEnumerable, IEnumerator<AutomationElement>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private AutomationElement _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private string hint;

		public string _003C_003E3__hint;

		private AutomationElement root;

		public AutomationElement _003C_003E3__root;

		private AutomationElementCollection _003Cmatches_003E5__2;

		private int _003Ci_003E5__3;

		AutomationElement IEnumerator<AutomationElement>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CFindExactTextPropertyMatches_003Ed__48(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003Cmatches_003E5__2 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003Cmatches_003E5__2 = null;
				try
				{
					OrCondition condition = new OrCondition(new PropertyCondition(AutomationElement.NameProperty, hint, PropertyConditionFlags.IgnoreCase), new PropertyCondition(ValuePattern.ValueProperty, hint, PropertyConditionFlags.IgnoreCase));
					_003Cmatches_003E5__2 = root.FindAll(TreeScope.Descendants, condition);
				}
				catch
				{
				}
				if (_003Cmatches_003E5__2 == null)
				{
					return false;
				}
				_003Ci_003E5__3 = 0;
				break;
			case 1:
				_003C_003E1__state = -1;
				_003Ci_003E5__3++;
				break;
			}
			if (_003Ci_003E5__3 < _003Cmatches_003E5__2.Count)
			{
				_003C_003E2__current = _003Cmatches_003E5__2[_003Ci_003E5__3];
				_003C_003E1__state = 1;
				return true;
			}
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<AutomationElement> IEnumerable<AutomationElement>.GetEnumerator()
		{
			_003CFindExactTextPropertyMatches_003Ed__48 _003CFindExactTextPropertyMatches_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CFindExactTextPropertyMatches_003Ed__ = this;
			}
			else
			{
				_003CFindExactTextPropertyMatches_003Ed__ = new _003CFindExactTextPropertyMatches_003Ed__48(0);
			}
			_003CFindExactTextPropertyMatches_003Ed__.root = _003C_003E3__root;
			_003CFindExactTextPropertyMatches_003Ed__.hint = _003C_003E3__hint;
			return _003CFindExactTextPropertyMatches_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<AutomationElement>)this).GetEnumerator();
		}
	}

	private const long CacheTtlMilliseconds = 350L;

	private const long SnapshotCacheTtlMilliseconds = 450L;

	private const long TimeoutBackoffMilliseconds = 5000L;

	private const int MaxTraversalNodes = 3000;

	private const int MaxQueryTimeoutMs = 1200;

	private static readonly SemaphoreSlim UiaGate = new SemaphoreSlim(Math.Clamp(Environment.ProcessorCount / 2, 2, 6));

	private readonly WindowTracker _windows = new WindowTracker();

	private readonly object _cacheLock = new object();

	private readonly Dictionary<string, CacheEntry<ControlMatch?>> _controlCache = new Dictionary<string, CacheEntry<ControlMatch>>();

	private readonly Dictionary<string, CacheEntry<ControlTextEvidence>> _textEvidenceCache = new Dictionary<string, CacheEntry<ControlTextEvidence>>();

	private readonly Dictionary<string, CacheEntry<ControlMatch?>> _textControlCache = new Dictionary<string, CacheEntry<ControlMatch>>();

	private readonly Dictionary<long, CacheEntry<UiaWindowSnapshot>> _snapshotCache = new Dictionary<long, CacheEntry<UiaWindowSnapshot>>();

	private readonly ConcurrentDictionary<long, object> _snapshotGates = new ConcurrentDictionary<long, object>();

	private readonly ConcurrentDictionary<long, long> _snapshotRevisions = new ConcurrentDictionary<long, long>();

	private readonly Dictionary<string, long> _uiaTimeoutBackoff = new Dictionary<string, long>();

	[DllImport("user32.dll")]
	private static extern bool EnumChildWindows(nint hWndParent, EnumChildProc lpEnumFunc, nint lParam);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out NativeRect lpRect);

	public void InvalidateWindow(nint hwnd)
	{
		long key2 = ((IntPtr)hwnd).ToInt64();
		_snapshotRevisions.AddOrUpdate(key2, 1L, (long _, long revision) => revision + 1);
		string token = "|" + key2 + "|";
		lock (_cacheLock)
		{
			_snapshotCache.Remove(key2);
			RemoveKeys(_controlCache, token);
			RemoveKeys(_textEvidenceCache, token);
			RemoveKeys(_textControlCache, token);
			string[] array = _uiaTimeoutBackoff.Keys.Where((string key) => key.Contains(token, StringComparison.Ordinal) || key.Equals(WindowBackoffKey(hwnd), StringComparison.Ordinal)).ToArray();
			foreach (string key3 in array)
			{
				_uiaTimeoutBackoff.Remove(key3);
			}
		}
	}

	private static void RemoveKeys<T>(Dictionary<string, CacheEntry<T>> cache, string token)
	{
		string[] array = cache.Keys.Where((string key) => key.Contains(token, StringComparison.Ordinal)).ToArray();
		foreach (string key2 in array)
		{
			cache.Remove(key2);
		}
	}

	public Rect? FindChildControlBounds(ManagedProcess mp, IReadOnlyList<string> nameHints, string? controlTypeHint)
	{
		return FindChildControlBounds(mp, new ControlLocatorOptions
		{
			NameContains = nameHints,
			ControlType = controlTypeHint
		});
	}

	public Rect? FindChildControlBounds(ManagedProcess mp, ControlLocatorOptions options)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		return FindChildControlMatch(mp, options)?.Bounds;
	}

	public ControlMatch? FindChildControlMatch(ManagedProcess mp, ControlLocatorOptions options)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		IReadOnlyList<WindowInfo> readOnlyList = _windows.FindWindowsForManaged(mp);
		if (readOnlyList.Count == 0)
		{
			return null;
		}
		using (IEnumerator<WindowInfo> enumerator = readOnlyList.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				WindowInfo current = enumerator.Current;
				try
				{
					ControlMatch controlMatch = FindChildControlMatch(current.Hwnd, options);
					if (controlMatch != null)
					{
						return controlMatch;
					}
					return WindowFallback(current.Bounds, 3, "managed window fallback");
				}
				catch
				{
					return WindowFallback(current.Bounds, 3, "managed window fallback after UIA failure");
				}
			}
		}
		return null;
	}

	public Rect? FindChildControlBounds(nint hwnd, ControlLocatorOptions options)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		return FindChildControlMatch(hwnd, options)?.Bounds;
	}

	public ControlMatch? FindChildControlMatch(nint hwnd, ControlLocatorOptions options)
	{
		string text = "control|" + ((IntPtr)hwnd).ToInt64() + "|" + ControlOptionsKey(options);
		if (TryGetCached<ControlMatch>(_controlCache, text, out var value))
		{
			return value;
		}
		string text2 = NormalizeBackend(options.Backend);
		ControlMatch controlMatch;
		if (text2 == "win32")
		{
			controlMatch = FindWin32ChildControlMatch(hwnd, options);
			SetCached(_controlCache, text, controlMatch);
			return controlMatch;
		}
		if (IsUiaBackedOff(text) || IsUiaBackedOff(WindowBackoffKey(hwnd)))
		{
			controlMatch = ((text2 == "uia") ? null : FindWin32ChildControlMatch(hwnd, options));
			SetCached(_controlCache, text, controlMatch);
			return controlMatch;
		}
		UiaWindowSnapshot uiaSnapshot = GetUiaSnapshot(hwnd);
		ControlMatch controlMatch2 = (((object)uiaSnapshot == null) ? null : FindSnapshotControlMatch(uiaSnapshot, options));
		if ((object)uiaSnapshot == null)
		{
			SetUiaBackoff(text);
			SetUiaBackoff(WindowBackoffKey(hwnd));
		}
		controlMatch = ((controlMatch2 != null || text2 == "uia") ? controlMatch2 : FindWin32ChildControlMatch(hwnd, options));
		SetCached(_controlCache, text, controlMatch);
		return controlMatch;
	}

	private ControlMatch? FindChildControlMatchCore(nint hwnd, ControlLocatorOptions options)
	{
		AutomationElement automationElement = AutomationElement.FromHandle(hwnd);
		if ((object)automationElement == null)
		{
			return null;
		}
		return FindBestMatch(automationElement, options);
	}

	public ControlTextEvidence FindTextEvidence(nint hwnd, IReadOnlyList<string> hints, int maxNodes = 3000, int timeoutMs = 1200, bool stopAfterFirstMatch = false)
	{
		string text = $"text-evidence|{((IntPtr)hwnd).ToInt64()}|{ListKey(hints)}|{maxNodes}|{timeoutMs}|{stopAfterFirstMatch}";
		if (TryGetCached(_textEvidenceCache, text, out var value))
		{
			return value;
		}
		if (IsUiaBackedOff(text) || IsUiaBackedOff(WindowBackoffKey(hwnd)))
		{
			ControlTextEvidence controlTextEvidence = FindWin32TextEvidence(hwnd, hints, maxNodes, Math.Min(timeoutMs, 400), stopAfterFirstMatch);
			ControlTextEvidence controlTextEvidence2 = ((controlTextEvidence.MatchedHints.Count > 0) ? controlTextEvidence : new ControlTextEvidence
			{
				TimedOut = true,
				Provider = "uia-backoff"
			});
			SetCached(_textEvidenceCache, text, controlTextEvidence2);
			return controlTextEvidence2;
		}
		UiaWindowSnapshot uiaSnapshot = GetUiaSnapshot(hwnd);
		ControlTextEvidence controlTextEvidence3 = (((object)uiaSnapshot == null) ? new ControlTextEvidence
		{
			TimedOut = true,
			Provider = "uia"
		} : FindSnapshotTextEvidence(uiaSnapshot, hints, maxNodes, stopAfterFirstMatch));
		if ((object)uiaSnapshot == null)
		{
			SetUiaBackoff(text);
			SetUiaBackoff(WindowBackoffKey(hwnd));
		}
		if (controlTextEvidence3.MatchedHints.Count > 0 || hints.Count == 0)
		{
			SetCached(_textEvidenceCache, text, controlTextEvidence3);
			return controlTextEvidence3;
		}
		ControlTextEvidence controlTextEvidence4 = FindWin32TextEvidence(hwnd, hints, maxNodes, timeoutMs, stopAfterFirstMatch);
		ControlTextEvidence controlTextEvidence5 = ((controlTextEvidence4.MatchedHints.Count > 0) ? controlTextEvidence4 : controlTextEvidence3);
		SetCached(_textEvidenceCache, text, controlTextEvidence5);
		return controlTextEvidence5;
	}

	public ControlMatch? FindTextControlMatch(nint hwnd, IReadOnlyList<string> hints, int maxNodes = 3000, int timeoutMs = 1200, bool requireAllHints = false, string backend = "auto", Rect? withinBounds = null)
	{
		string text = $"text-control|{((IntPtr)hwnd).ToInt64()}|{ListKey(hints)}|{maxNodes}|{timeoutMs}|{requireAllHints}|{NormalizeBackend(backend)}|{RectKey(withinBounds)}";
		if (TryGetCached<ControlMatch>(_textControlCache, text, out var value))
		{
			return value;
		}
		string text2 = NormalizeBackend(backend);
		ControlMatch controlMatch;
		if (text2 == "win32")
		{
			controlMatch = FindWin32TextControlMatch(hwnd, hints, maxNodes, timeoutMs, requireAllHints, withinBounds);
			SetCached(_textControlCache, text, controlMatch);
			return controlMatch;
		}
		if (IsUiaBackedOff(text) || IsUiaBackedOff(WindowBackoffKey(hwnd)))
		{
			controlMatch = ((text2 == "uia") ? null : FindWin32TextControlMatch(hwnd, hints, maxNodes, Math.Min(timeoutMs, 400), requireAllHints, withinBounds));
			SetCached(_textControlCache, text, controlMatch);
			return controlMatch;
		}
		UiaWindowSnapshot uiaSnapshot = GetUiaSnapshot(hwnd);
		ControlMatch controlMatch2 = (((object)uiaSnapshot == null) ? null : FindSnapshotTextControlMatch(uiaSnapshot, hints, maxNodes, requireAllHints, withinBounds));
		if ((object)uiaSnapshot == null)
		{
			SetUiaBackoff(text);
			SetUiaBackoff(WindowBackoffKey(hwnd));
		}
		controlMatch = ((controlMatch2 != null || text2 == "uia") ? controlMatch2 : FindWin32TextControlMatch(hwnd, hints, maxNodes, timeoutMs, requireAllHints, withinBounds));
		SetCached(_textControlCache, text, controlMatch);
		return controlMatch;
	}

	private UiaWindowSnapshot? GetUiaSnapshot(nint hwnd)
	{
		long num = ((IntPtr)hwnd).ToInt64();
		if (TryGetSnapshotCached(num, out UiaWindowSnapshot snapshot))
		{
			return snapshot;
		}
		lock (_snapshotGates.GetOrAdd(num, (long _) => new object()))
		{
			if (TryGetSnapshotCached(num, out snapshot))
			{
				return snapshot;
			}
			long orAdd = _snapshotRevisions.GetOrAdd(num, 0L);
			bool timedOut = false;
			UiaWindowSnapshot uiaWindowSnapshot = RunUiaQuery(() => BuildUiaSnapshot(hwnd), 1200, null, out timedOut);
			if ((object)uiaWindowSnapshot == null)
			{
				return null;
			}
			if (_snapshotRevisions.GetOrAdd(num, 0L) != orAdd)
			{
				return null;
			}
			SetSnapshotCached(num, uiaWindowSnapshot);
			return uiaWindowSnapshot;
		}
	}

	private static UiaWindowSnapshot? BuildUiaSnapshot(nint hwnd)
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		AutomationElement automationElement = AutomationElement.FromHandle(hwnd);
		if ((object)automationElement == null)
		{
			return null;
		}
		List<UiaNodeSnapshot> list = new List<UiaNodeSnapshot>();
		Queue<(AutomationElement, int)> queue = new Queue<(AutomationElement, int)>();
		TreeWalker controlViewWalker = TreeWalker.ControlViewWalker;
		Stopwatch stopwatch = Stopwatch.StartNew();
		queue.Enqueue((automationElement, 0));
		bool timedOut = false;
		Rect bounds = default(Rect);
		while (queue.Count > 0 && list.Count < 3000)
		{
			if (stopwatch.ElapsedMilliseconds > 1200)
			{
				timedOut = true;
				break;
			}
			var (automationElement2, num) = queue.Dequeue();
			try
			{
				AutomationElement.AutomationElementInformation current = automationElement2.Current;
				Rect boundingRectangle = current.BoundingRectangle;
				((Rect)(ref bounds))._002Ector(((Rect)(ref boundingRectangle)).X, ((Rect)(ref boundingRectangle)).Y, ((Rect)(ref boundingRectangle)).Width, ((Rect)(ref boundingRectangle)).Height);
				list.Add(new UiaNodeSnapshot(num, current.Name ?? string.Empty, current.AutomationId ?? string.Empty, current.ClassName ?? string.Empty, current.ControlType?.ProgrammaticName ?? string.Empty, current.IsOffscreen, bounds, ReadSnapshotTextPieces(automationElement2, current)));
				AutomationElement automationElement3 = controlViewWalker.GetFirstChild(automationElement2);
				while ((object)automationElement3 != null)
				{
					queue.Enqueue((automationElement3, num + 1));
					automationElement3 = controlViewWalker.GetNextSibling(automationElement3);
				}
			}
			catch
			{
			}
		}
		return new UiaWindowSnapshot(list, timedOut, stopwatch.ElapsedMilliseconds);
	}

	private static IReadOnlyList<UiaTextPiece> ReadSnapshotTextPieces(AutomationElement node, AutomationElement.AutomationElementInformation cur)
	{
		List<UiaTextPiece> list = new List<UiaTextPiece>();
		AddTextPiece(list, "name", cur.Name);
		TryAddPatternText(list, "value", node, ValuePattern.Pattern, (object pattern) => ((ValuePattern)pattern).Current.Value);
		TryAddLegacyText(list, node);
		if (ShouldProbeTextPattern(cur))
		{
			TryAddPatternText(list, "text", node, TextPattern.Pattern, (object pattern) => ((TextPattern)pattern).DocumentRange.GetText(1200));
		}
		return list.Where((UiaTextPiece piece) => !string.IsNullOrWhiteSpace(piece.Text)).DistinctBy<UiaTextPiece, string>((UiaTextPiece piece) => piece.Source + "\0" + piece.Text, StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static ControlTextEvidence FindSnapshotTextEvidence(UiaWindowSnapshot snapshot, IReadOnlyList<string> hints, int maxNodes, bool stopAfterFirstMatch)
	{
		string[] array = NormalizeHints(hints);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<string> list = new List<string>();
		int num = 0;
		foreach (UiaNodeSnapshot item in snapshot.Nodes.Take(Math.Clamp(maxNodes, 50, 3000)))
		{
			num++;
			if (item.IsOffscreen)
			{
				continue;
			}
			foreach (TextHintMatch match in MatchTextHints(item.Text, array))
			{
				hashSet.Add(match.Hint);
				if (list.Count < 12)
				{
					list.Add(SampleText(item.Text.First((UiaTextPiece piece) => piece.Source.Equals(match.Source, StringComparison.OrdinalIgnoreCase)).Text));
				}
			}
			if ((stopAfterFirstMatch && hashSet.Count > 0) || hashSet.Count == array.Length)
			{
				break;
			}
		}
		return new ControlTextEvidence
		{
			Score = hashSet.Sum((string hint) => 18 + Math.Min(hint.Length, 24)),
			VisitedNodes = num,
			TimedOut = snapshot.TimedOut,
			Provider = "uia-snapshot",
			MatchedHints = hashSet.ToArray(),
			SampleNames = list.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray()
		};
	}

	private static ControlMatch? FindSnapshotTextControlMatch(UiaWindowSnapshot snapshot, IReadOnlyList<string> hints, int maxNodes, bool requireAllHints, Rect? withinBounds)
	{
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		string[] array = NormalizeHints(hints);
		ControlMatch controlMatch = null;
		int num = 0;
		foreach (UiaNodeSnapshot item in snapshot.Nodes.Take(Math.Clamp(maxNodes, 50, 3000)))
		{
			num++;
			if (item.IsOffscreen)
			{
				continue;
			}
			IReadOnlyList<TextHintMatch> readOnlyList = MatchTextHints(item.Text, array);
			string[] array2 = readOnlyList.Select((TextHintMatch match) => match.Hint).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
			if (array2.Length != 0 && (!requireAllHints || array2.Length >= array.Length) && IsUsableContentRect(item.Bounds, withinBounds))
			{
				int score = ScoreContentTarget(array2) + (IsPreciseControlTypeName(item.ControlType) ? 4 : 0);
				ControlMatch controlMatch2 = new ControlMatch
				{
					Bounds = item.Bounds,
					Score = score,
					VisitedNodes = num,
					Name = item.Name,
					AutomationId = item.AutomationId,
					ClassName = item.ClassName,
					ControlType = item.ControlType,
					Provider = "uia-snapshot",
					MatchedHints = array2,
					Reasons = BuildContentTargetReasons(readOnlyList)
				};
				if (controlMatch == null || controlMatch2.Score > controlMatch.Score)
				{
					controlMatch = controlMatch2;
				}
			}
		}
		return controlMatch;
	}

	private static ControlMatch? FindSnapshotControlMatch(UiaWindowSnapshot snapshot, ControlLocatorOptions options)
	{
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		string[] array = NormalizeHints(options.NameContains);
		ControlMatch controlMatch = null;
		int num = 0;
		foreach (UiaNodeSnapshot item in snapshot.Nodes.Take(Math.Clamp(options.MaxNodes, 50, 3000)))
		{
			num++;
			if (item.IsOffscreen || (!string.IsNullOrWhiteSpace(options.NameEquals) && !item.Name.Equals(options.NameEquals, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrWhiteSpace(options.AutomationId) && !item.AutomationId.Equals(options.AutomationId, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrWhiteSpace(options.ClassName) && !item.ClassName.Contains(options.ClassName, StringComparison.OrdinalIgnoreCase)) || !SnapshotControlTypeMatches(item.ControlType, options.ControlType) || !IsUsableContentRect(item.Bounds, null))
			{
				continue;
			}
			IReadOnlyList<TextHintMatch> source = MatchTextHints(item.Text, array);
			string[] array2 = source.Select((TextHintMatch match) => match.Hint).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
			bool flag = options.AllowTypeOnlyMatch && (!string.IsNullOrWhiteSpace(options.AutomationId) || !string.IsNullOrWhiteSpace(options.ClassName));
			if ((array.Length != 0 && array2.Length == 0 && !flag) || (options.RequireAllNameContains && array2.Length < array.Length))
			{
				continue;
			}
			List<string> list = new List<string>();
			int num2 = 0;
			if (!string.IsNullOrWhiteSpace(options.NameEquals))
			{
				num2 += 80;
				list.Add("name-equals");
			}
			if (!string.IsNullOrWhiteSpace(options.AutomationId))
			{
				num2 += 70;
				list.Add("automation-id");
			}
			if (!string.IsNullOrWhiteSpace(options.ClassName))
			{
				num2 += 30;
				list.Add("class-name");
			}
			if (flag)
			{
				num2 += 20;
				list.Add("identity-only");
			}
			foreach (TextHintMatch item2 in source.DistinctBy<TextHintMatch, string>((TextHintMatch value) => value.Source + "\0" + value.Hint, StringComparer.OrdinalIgnoreCase))
			{
				num2 += 10 + Math.Min(item2.Hint.Length, 24);
				list.Add(item2.Source + ":" + item2.Hint);
			}
			if (!string.IsNullOrWhiteSpace(options.ControlType))
			{
				num2 += 18;
				list.Add("control-type");
			}
			if (IsPreciseControlTypeName(item.ControlType))
			{
				num2 += 4;
				list.Add("precise-control");
			}
			if (num2 >= Math.Clamp(options.MinScore, 1, 200))
			{
				ControlMatch controlMatch2 = new ControlMatch
				{
					Bounds = item.Bounds,
					Score = num2,
					VisitedNodes = num,
					Name = item.Name,
					AutomationId = item.AutomationId,
					ClassName = item.ClassName,
					ControlType = item.ControlType,
					Provider = "uia-snapshot",
					MatchedHints = array2,
					Reasons = list
				};
				if (controlMatch == null || controlMatch2.Score > controlMatch.Score)
				{
					controlMatch = controlMatch2;
				}
			}
		}
		return controlMatch;
	}

	private static bool SnapshotControlTypeMatches(string actual, string? expected)
	{
		if (string.IsNullOrWhiteSpace(expected))
		{
			return true;
		}
		string text;
		if (!actual.StartsWith("ControlType.", StringComparison.OrdinalIgnoreCase))
		{
			text = actual;
		}
		else
		{
			int length = "ControlType.".Length;
			text = actual.Substring(length, actual.Length - length);
		}
		return text.Equals(expected, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsPreciseControlTypeName(string controlType)
	{
		if (!controlType.EndsWith("Button", StringComparison.OrdinalIgnoreCase) && !controlType.EndsWith("MenuItem", StringComparison.OrdinalIgnoreCase) && !controlType.EndsWith("TabItem", StringComparison.OrdinalIgnoreCase) && !controlType.EndsWith("ListItem", StringComparison.OrdinalIgnoreCase) && !controlType.EndsWith("TreeItem", StringComparison.OrdinalIgnoreCase) && !controlType.EndsWith("Edit", StringComparison.OrdinalIgnoreCase))
		{
			return controlType.EndsWith("Document", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private ControlTextEvidence FindTextEvidenceCore(nint hwnd, IReadOnlyList<string> hints, int maxNodes, int timeoutMs, bool stopAfterFirstMatch)
	{
		AutomationElement automationElement = AutomationElement.FromHandle(hwnd);
		if ((object)automationElement == null || hints.Count == 0)
		{
			return new ControlTextEvidence();
		}
		string[] array = (from h in hints
			where !string.IsNullOrWhiteSpace(h)
			select h.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		if (array.Length == 0)
		{
			return new ControlTextEvidence();
		}
		ControlTextEvidence controlTextEvidence = FindFastTextEvidence(automationElement, array, stopAfterFirstMatch);
		if (controlTextEvidence != null && ((stopAfterFirstMatch && controlTextEvidence.MatchedHints.Count > 0) || controlTextEvidence.MatchedHints.Count == array.Length))
		{
			return controlTextEvidence;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<string> list = new List<string>();
		TreeWalker controlViewWalker = TreeWalker.ControlViewWalker;
		Queue<AutomationElement> queue = new Queue<AutomationElement>();
		queue.Enqueue(automationElement);
		int num = 0;
		int num2 = Math.Clamp(maxNodes, 50, 3000);
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num3 = Math.Clamp(timeoutMs, 100, 1200);
		bool timedOut = false;
		while (queue.Count > 0 && num < num2)
		{
			if (stopwatch.ElapsedMilliseconds > num3)
			{
				timedOut = true;
				break;
			}
			AutomationElement automationElement2 = queue.Dequeue();
			num++;
			try
			{
				AutomationElement.AutomationElementInformation current = automationElement2.Current;
				foreach (UiaTextPiece item in ReadUiaTextPieces(automationElement2, current, array, !stopAfterFirstMatch))
				{
					string[] array2 = array;
					foreach (string text in array2)
					{
						if (item.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							hashSet.Add(text);
							if (list.Count < 12)
							{
								list.Add(SampleText(item.Text));
							}
						}
					}
					if ((stopAfterFirstMatch && hashSet.Count > 0) || hashSet.Count == array.Length)
					{
						break;
					}
				}
				if ((stopAfterFirstMatch && hashSet.Count > 0) || hashSet.Count == array.Length)
				{
					break;
				}
				AutomationElement automationElement3 = controlViewWalker.GetFirstChild(automationElement2);
				while ((object)automationElement3 != null)
				{
					queue.Enqueue(automationElement3);
					automationElement3 = controlViewWalker.GetNextSibling(automationElement3);
				}
				continue;
			}
			catch
			{
				continue;
			}
		}
		return new ControlTextEvidence
		{
			Score = hashSet.Sum((string h) => 18 + Math.Min(h.Length, 24)),
			VisitedNodes = num,
			TimedOut = timedOut,
			MatchedHints = hashSet.ToArray(),
			SampleNames = list.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray()
		};
	}

	private ControlMatch? FindTextControlMatchCore(nint hwnd, IReadOnlyList<string> hints, int maxNodes, int timeoutMs, bool requireAllHints, Rect? withinBounds)
	{
		AutomationElement automationElement = AutomationElement.FromHandle(hwnd);
		if ((object)automationElement == null)
		{
			return null;
		}
		string[] array = NormalizeHints(hints);
		if (array.Length == 0)
		{
			return null;
		}
		ControlMatch controlMatch = FindFastTextControlMatch(automationElement, array, requireAllHints, withinBounds);
		if (controlMatch != null)
		{
			return controlMatch;
		}
		TreeWalker controlViewWalker = TreeWalker.ControlViewWalker;
		Queue<(AutomationElement, int)> queue = new Queue<(AutomationElement, int)>();
		queue.Enqueue((automationElement, 0));
		int num = 0;
		int num2 = Math.Clamp(maxNodes, 50, 3000);
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num3 = Math.Clamp(timeoutMs, 100, 1200);
		ControlMatch controlMatch2 = null;
		while (queue.Count > 0 && num < num2 && stopwatch.ElapsedMilliseconds <= num3)
		{
			(AutomationElement, int) tuple = queue.Dequeue();
			AutomationElement item = tuple.Item1;
			int item2 = tuple.Item2;
			num++;
			try
			{
				AutomationElement.AutomationElementInformation current = item.Current;
				if (item2 <= 0)
				{
					goto IL_00ff;
				}
				ControlMatch controlMatch3 = ScoreTextElementCandidate(item, current, array, requireAllHints, withinBounds, num);
				if (controlMatch3 == null || (controlMatch2 != null && controlMatch3.Score <= controlMatch2.Score))
				{
					goto IL_00ff;
				}
				controlMatch2 = controlMatch3;
				if (!requireAllHints || controlMatch3.MatchedHints.Count != array.Length || controlMatch3.Score < 64)
				{
					goto IL_00ff;
				}
				goto end_IL_00a1;
				IL_00ff:
				AutomationElement automationElement2 = controlViewWalker.GetFirstChild(item);
				while ((object)automationElement2 != null)
				{
					queue.Enqueue((automationElement2, item2 + 1));
					automationElement2 = controlViewWalker.GetNextSibling(automationElement2);
				}
				continue;
				end_IL_00a1:;
			}
			catch
			{
				continue;
			}
			break;
		}
		return controlMatch2;
	}

	private static ControlMatch? FindWin32ChildControlMatch(nint hwnd, ControlLocatorOptions options)
	{
		if (!string.IsNullOrWhiteSpace(options.AutomationId))
		{
			return null;
		}
		int maxNodes = Math.Clamp(options.MaxNodes, 50, 3000);
		int budget = Math.Clamp(options.TimeoutMs, 100, 1200);
		Stopwatch timer = Stopwatch.StartNew();
		int visited = 0;
		ControlMatch best = null;
		EnumChildWindows(hwnd, delegate(nint child, nint _)
		{
			if (visited >= maxNodes || timer.ElapsedMilliseconds > budget)
			{
				return false;
			}
			visited++;
			try
			{
				if (!IsWindowVisible(child))
				{
					return true;
				}
				ChildWindowInfo childWindowInfo = ReadChildWindow(child);
				if ((object)childWindowInfo == null)
				{
					return true;
				}
				ControlMatch controlMatch = ScoreWin32Candidate(childWindowInfo, options, visited);
				if (controlMatch != null && (best == null || controlMatch.Score > best.Score))
				{
					best = controlMatch;
					if (IsStrongMatch(controlMatch))
					{
						return false;
					}
				}
			}
			catch
			{
			}
			return true;
		}, IntPtr.Zero);
		int num = Math.Clamp(options.MinScore, 1, 200);
		if (best == null || best.Score < num)
		{
			return null;
		}
		return best;
	}

	private static ControlTextEvidence FindWin32TextEvidence(nint hwnd, IReadOnlyList<string> hints, int maxNodes, int timeoutMs, bool stopAfterFirstMatch)
	{
		string[] wanted = (from h in hints
			where !string.IsNullOrWhiteSpace(h)
			select h.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		if (wanted.Length == 0)
		{
			return new ControlTextEvidence
			{
				Provider = "win32"
			};
		}
		int limit = Math.Clamp(maxNodes, 50, 3000);
		int budget = Math.Clamp(timeoutMs, 100, 1200);
		Stopwatch timer = Stopwatch.StartNew();
		int visited = 0;
		bool timedOut = false;
		HashSet<string> matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<string> samples = new List<string>();
		EnumChildWindows(hwnd, delegate(nint child, nint _)
		{
			if (visited >= limit)
			{
				return false;
			}
			if (timer.ElapsedMilliseconds > budget)
			{
				timedOut = true;
				return false;
			}
			visited++;
			try
			{
				if (!IsWindowVisible(child))
				{
					return true;
				}
				string text = ReadWindowText(child);
				if (string.IsNullOrWhiteSpace(text))
				{
					return true;
				}
				string[] array = wanted;
				foreach (string text2 in array)
				{
					if (text.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						matched.Add(text2);
						if (samples.Count < 12)
						{
							samples.Add(text);
						}
					}
				}
				if ((stopAfterFirstMatch && matched.Count > 0) || matched.Count == wanted.Length)
				{
					return false;
				}
			}
			catch
			{
			}
			return true;
		}, IntPtr.Zero);
		return new ControlTextEvidence
		{
			Score = matched.Sum((string h) => 16 + Math.Min(h.Length, 24)),
			VisitedNodes = visited,
			TimedOut = timedOut,
			Provider = "win32",
			MatchedHints = matched.ToArray(),
			SampleNames = samples.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray()
		};
	}

	private static ControlMatch? FindWin32TextControlMatch(nint hwnd, IReadOnlyList<string> hints, int maxNodes, int timeoutMs, bool requireAllHints, Rect? withinBounds)
	{
		string[] wanted = NormalizeHints(hints);
		if (wanted.Length == 0)
		{
			return null;
		}
		int limit = Math.Clamp(maxNodes, 50, 3000);
		int budget = Math.Clamp(timeoutMs, 100, 1200);
		Stopwatch timer = Stopwatch.StartNew();
		int visited = 0;
		ControlMatch best = null;
		EnumChildWindows(hwnd, delegate(nint child, nint _)
		{
			if (visited >= limit || timer.ElapsedMilliseconds > budget)
			{
				return false;
			}
			visited++;
			try
			{
				if (!IsWindowVisible(child))
				{
					return true;
				}
				ChildWindowInfo childWindowInfo = ReadChildWindow(child);
				if ((object)childWindowInfo == null)
				{
					return true;
				}
				ControlMatch controlMatch = ScoreWin32TextCandidate(childWindowInfo, wanted, requireAllHints, withinBounds, visited);
				if (controlMatch != null && (best == null || controlMatch.Score > best.Score))
				{
					best = controlMatch;
					if (requireAllHints && controlMatch.MatchedHints.Count == wanted.Length && controlMatch.Score >= 64)
					{
						return false;
					}
				}
			}
			catch
			{
			}
			return true;
		}, IntPtr.Zero);
		return best;
	}

	private static ControlMatch? ScoreWin32Candidate(ChildWindowInfo candidate, ControlLocatorOptions options, int visited)
	{
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		if (!string.IsNullOrWhiteSpace(options.NameEquals) && !string.Equals(candidate.Name, options.NameEquals, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		bool flag = !string.IsNullOrWhiteSpace(options.ClassName);
		if (flag && candidate.ClassName.IndexOf(options.ClassName, StringComparison.OrdinalIgnoreCase) < 0)
		{
			return null;
		}
		string text = NormalizeControlType(options.ControlType);
		bool flag2 = string.IsNullOrWhiteSpace(text) || Win32ControlTypeMatches(candidate.ClassName, text);
		if (!flag2 && (!flag || candidate.ClassName.IndexOf(options.ClassName, StringComparison.OrdinalIgnoreCase) < 0))
		{
			return null;
		}
		string[] array = (from h in options.NameContains
			where !string.IsNullOrWhiteSpace(h)
			select h.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		string[] array2 = array.Where((string h) => candidate.Name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
		if (array.Length != 0 && array2.Length == 0 && !options.AllowTypeOnlyMatch)
		{
			return null;
		}
		if (options.RequireAllNameContains && array2.Length < array.Length)
		{
			return null;
		}
		int num = 0;
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(options.NameEquals))
		{
			num += 80;
			list.Add("name-equals");
		}
		if (flag)
		{
			num += 44;
			list.Add("class-name");
		}
		string[] array3 = array2;
		foreach (string text2 in array3)
		{
			num += 10 + Math.Min(text2.Length, 24);
			list.Add("name:" + text2);
		}
		if (options.RequireAllNameContains && array.Length != 0)
		{
			num += 20;
			list.Add("all-name-hints");
		}
		if (!string.IsNullOrWhiteSpace(text) && flag2)
		{
			num += 16;
			list.Add("control-type");
		}
		if (IsPreciseWin32Class(candidate.ClassName))
		{
			num += 4;
			list.Add("precise-control");
		}
		list.Add("hwnd:" + ((IntPtr)candidate.Hwnd).ToInt64().ToString("X"));
		return new ControlMatch
		{
			Bounds = candidate.Bounds,
			Score = num,
			VisitedNodes = visited,
			Name = candidate.Name,
			ClassName = candidate.ClassName,
			ControlType = Win32ControlTypeName(candidate.ClassName),
			Provider = "win32",
			MatchedHints = array2,
			Reasons = list
		};
	}

	private static ControlTextEvidence? FindFastTextEvidence(AutomationElement root, IReadOnlyList<string> wanted, bool stopAfterFirstMatch)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<string> list = new List<string>();
		int num = 0;
		foreach (string item in wanted.Where((string h) => h.Length <= 128).Take(8))
		{
			using (IEnumerator<AutomationElement> enumerator2 = FindExactTextPropertyMatches(root, item).GetEnumerator())
			{
				if (enumerator2.MoveNext())
				{
					_ = enumerator2.Current;
					num++;
					hashSet.Add(item);
					if (list.Count < 12)
					{
						list.Add(item);
					}
				}
			}
			if ((stopAfterFirstMatch && hashSet.Count > 0) || hashSet.Count == wanted.Count)
			{
				break;
			}
		}
		if (hashSet.Count == 0)
		{
			return null;
		}
		return new ControlTextEvidence
		{
			Score = hashSet.Sum((string h) => 18 + Math.Min(h.Length, 24)),
			VisitedNodes = num,
			MatchedHints = hashSet.ToArray(),
			SampleNames = list.ToArray()
		};
	}

	private static ControlMatch? FindFastTextControlMatch(AutomationElement root, IReadOnlyList<string> wanted, bool requireAllHints, Rect? withinBounds)
	{
		ControlMatch controlMatch = null;
		int num = 0;
		foreach (string item in wanted.Where((string h) => h.Length <= 128).Take(8))
		{
			foreach (AutomationElement item2 in FindExactTextPropertyMatches(root, item))
			{
				num++;
				ControlMatch controlMatch2;
				try
				{
					controlMatch2 = ScoreTextElementCandidate(item2, item2.Current, wanted, requireAllHints, withinBounds, num);
				}
				catch
				{
					continue;
				}
				if (controlMatch2 != null && (controlMatch == null || controlMatch2.Score > controlMatch.Score))
				{
					controlMatch = controlMatch2;
					if (!requireAllHints || controlMatch2.MatchedHints.Count == wanted.Count)
					{
						return controlMatch;
					}
				}
			}
		}
		return controlMatch;
	}

	[IteratorStateMachine(typeof(_003CFindExactTextPropertyMatches_003Ed__48))]
	private static IEnumerable<AutomationElement> FindExactTextPropertyMatches(AutomationElement root, string hint)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CFindExactTextPropertyMatches_003Ed__48(-2)
		{
			_003C_003E3__root = root,
			_003C_003E3__hint = hint
		};
	}

	private static ControlMatch? ScoreTextElementCandidate(AutomationElement node, AutomationElement.AutomationElementInformation cur, IReadOnlyList<string> wanted, bool requireAllHints, Rect? withinBounds, int visited)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		if (cur.IsOffscreen)
		{
			return null;
		}
		IReadOnlyList<TextHintMatch> readOnlyList = MatchTextHints(ReadUiaTextPieces(node, cur, wanted, requireAllHints), wanted);
		if (readOnlyList.Count == 0 || (requireAllHints && readOnlyList.Select((TextHintMatch m) => m.Hint).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count() < wanted.Count))
		{
			return null;
		}
		Rect boundingRectangle = cur.BoundingRectangle;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref boundingRectangle)).X, ((Rect)(ref boundingRectangle)).Y, ((Rect)(ref boundingRectangle)).Width, ((Rect)(ref boundingRectangle)).Height);
		if (!IsUsableContentRect(val, withinBounds))
		{
			return null;
		}
		string[] matchedHints = readOnlyList.Select((TextHintMatch m) => m.Hint).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		List<string> list = BuildContentTargetReasons(readOnlyList);
		if (IsPreciseControlType(cur.ControlType))
		{
			list.Add("precise-control");
		}
		return new ControlMatch
		{
			Bounds = val,
			Score = ScoreContentTarget(matchedHints) + (IsPreciseControlType(cur.ControlType) ? 4 : 0),
			VisitedNodes = visited,
			Name = (cur.Name ?? string.Empty),
			AutomationId = (cur.AutomationId ?? string.Empty),
			ClassName = (cur.ClassName ?? string.Empty),
			ControlType = (cur.ControlType?.ProgrammaticName ?? string.Empty),
			Provider = "uia",
			MatchedHints = matchedHints,
			Reasons = list
		};
	}

	private static ControlMatch? ScoreWin32TextCandidate(ChildWindowInfo candidate, IReadOnlyList<string> wanted, bool requireAllHints, Rect? withinBounds, int visited)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		string[] array = MatchTextHints(candidate.Name, wanted);
		if (array.Length == 0 || (requireAllHints && array.Length < wanted.Count))
		{
			return null;
		}
		if (!IsUsableContentRect(candidate.Bounds, withinBounds))
		{
			return null;
		}
		List<string> list = BuildContentTargetReasons(array);
		if (IsPreciseWin32Class(candidate.ClassName))
		{
			list.Add("precise-control");
		}
		list.Add("hwnd:" + ((IntPtr)candidate.Hwnd).ToInt64().ToString("X"));
		return new ControlMatch
		{
			Bounds = candidate.Bounds,
			Score = ScoreContentTarget(array) + (IsPreciseWin32Class(candidate.ClassName) ? 4 : 0),
			VisitedNodes = visited,
			Name = candidate.Name,
			ClassName = candidate.ClassName,
			ControlType = Win32ControlTypeName(candidate.ClassName),
			Provider = "win32",
			MatchedHints = array,
			Reasons = list
		};
	}

	private static string[] NormalizeHints(IReadOnlyList<string> hints)
	{
		return (from h in hints
			where !string.IsNullOrWhiteSpace(h)
			select h.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static string[] MatchTextHints(string value, IReadOnlyList<string> wanted)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return wanted.Where((string h) => value.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
		}
		return Array.Empty<string>();
	}

	private static IReadOnlyList<TextHintMatch> MatchTextHints(IReadOnlyList<UiaTextPiece> pieces, IReadOnlyList<string> wanted)
	{
		if (pieces.Count == 0 || wanted.Count == 0)
		{
			return Array.Empty<TextHintMatch>();
		}
		List<TextHintMatch> list = new List<TextHintMatch>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (UiaTextPiece piece in pieces)
		{
			foreach (string item2 in wanted)
			{
				if (piece.Text.IndexOf(item2, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					string item = piece.Source + "\0" + item2;
					if (hashSet.Add(item))
					{
						list.Add(new TextHintMatch(item2, piece.Source));
					}
				}
			}
		}
		return list;
	}

	private static List<string> BuildContentTargetReasons(IEnumerable<string> matchedHints)
	{
		List<string> list = new List<string>();
		list.Add("content-target");
		list.AddRange(matchedHints.Select((string h) => "name:" + h));
		return list;
	}

	private static List<string> BuildContentTargetReasons(IEnumerable<TextHintMatch> matchedHints)
	{
		List<string> list = new List<string>();
		list.Add("content-target");
		list.AddRange(matchedHints.Select((TextHintMatch h) => h.Source + ":" + h.Hint).Distinct<string>(StringComparer.OrdinalIgnoreCase));
		return list;
	}

	private static int ScoreContentTarget(IEnumerable<string> matchedHints)
	{
		return 42 + matchedHints.Sum((string h) => 18 + Math.Min(h.Length, 24));
	}

	private static bool IsUsableContentRect(Rect rect, Rect? withinBounds)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (((Rect)(ref rect)).IsEmpty || ((Rect)(ref rect)).Width <= 3.0 || ((Rect)(ref rect)).Height <= 3.0 || ((Rect)(ref rect)).Width >= 3500.0 || ((Rect)(ref rect)).Height >= 2200.0)
		{
			return false;
		}
		if (withinBounds.HasValue)
		{
			Rect valueOrDefault = withinBounds.GetValueOrDefault();
			double num = ((Rect)(ref rect)).Left + ((Rect)(ref rect)).Width / 2.0;
			double num2 = ((Rect)(ref rect)).Top + ((Rect)(ref rect)).Height / 2.0;
			if (num >= ((Rect)(ref valueOrDefault)).Left && num <= ((Rect)(ref valueOrDefault)).Right && num2 >= ((Rect)(ref valueOrDefault)).Top && num2 <= ((Rect)(ref valueOrDefault)).Bottom)
			{
				return true;
			}
			double num3 = Math.Max(((Rect)(ref rect)).Left, ((Rect)(ref valueOrDefault)).Left);
			double num4 = Math.Max(((Rect)(ref rect)).Top, ((Rect)(ref valueOrDefault)).Top);
			double num5 = Math.Min(((Rect)(ref rect)).Right, ((Rect)(ref valueOrDefault)).Right);
			double num6 = Math.Min(((Rect)(ref rect)).Bottom, ((Rect)(ref valueOrDefault)).Bottom);
			if (num5 <= num3 || num6 <= num4)
			{
				return false;
			}
			return (num5 - num3) * (num6 - num4) / Math.Max(1.0, ((Rect)(ref rect)).Width * ((Rect)(ref rect)).Height) >= 0.25;
		}
		return true;
	}

	private static T RunUiaQuery<T>(Func<T> query, int timeoutMs, T timeoutValue, out bool timedOut)
	{
		timedOut = false;
		int millisecondsTimeout = Math.Clamp(timeoutMs, 100, 1200);
		if (!UiaGate.Wait(0))
		{
			timedOut = true;
			return timeoutValue;
		}
		SemaphoreLease lease = new SemaphoreLease(UiaGate);
		Task<T> task = Task.Run(delegate
		{
			try
			{
				return query();
			}
			finally
			{
				lease.Release();
			}
		});
		try
		{
			if (task.Wait(millisecondsTimeout))
			{
				return task.Result;
			}
			timedOut = true;
			lease.Release();
			return timeoutValue;
		}
		catch
		{
			return timeoutValue;
		}
	}

	public Rect? FindChildControlBounds(Process process, IReadOnlyList<string> nameHints, string? controlTypeHint)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		IReadOnlyList<WindowInfo> readOnlyList = _windows.FindWindowsForProcess(process);
		if (readOnlyList.Count == 0)
		{
			return null;
		}
		try
		{
			ControlMatch controlMatch = FindChildControlMatch(readOnlyList[0].Hwnd, new ControlLocatorOptions
			{
				NameContains = nameHints,
				ControlType = controlTypeHint
			});
			if (controlMatch != null)
			{
				return controlMatch.Bounds;
			}
		}
		catch
		{
		}
		return readOnlyList[0].Bounds;
	}

	private static ControlMatch? FindBestMatch(AutomationElement root, ControlLocatorOptions options)
	{
		ControlType type = ParseControlType(options.ControlType);
		ControlMatch controlMatch = FindFastPropertyMatch(root, options, type);
		if (controlMatch != null)
		{
			return controlMatch;
		}
		TreeWalker controlViewWalker = TreeWalker.ControlViewWalker;
		Queue<AutomationElement> queue = new Queue<AutomationElement>();
		queue.Enqueue(root);
		ControlMatch controlMatch2 = null;
		int num = 0;
		int num2 = Math.Clamp(options.MaxNodes, 50, 3000);
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num3 = Math.Clamp(options.TimeoutMs, 100, 1200);
		while (queue.Count > 0 && num < num2 && stopwatch.ElapsedMilliseconds <= num3)
		{
			AutomationElement automationElement = queue.Dequeue();
			num++;
			try
			{
				AutomationElement.AutomationElementInformation current = automationElement.Current;
				ControlMatch controlMatch3 = ScoreCandidate(automationElement, current, options, type, num);
				if (controlMatch3 == null || (controlMatch2 != null && controlMatch3.Score <= controlMatch2.Score))
				{
					goto IL_00c3;
				}
				controlMatch2 = controlMatch3;
				if (!IsStrongMatch(controlMatch3))
				{
					goto IL_00c3;
				}
				goto end_IL_0084;
				IL_00c3:
				AutomationElement automationElement2 = controlViewWalker.GetFirstChild(automationElement);
				while ((object)automationElement2 != null)
				{
					queue.Enqueue(automationElement2);
					automationElement2 = controlViewWalker.GetNextSibling(automationElement2);
				}
				continue;
				end_IL_0084:;
			}
			catch
			{
				continue;
			}
			break;
		}
		int num4 = Math.Clamp(options.MinScore, 1, 200);
		if (controlMatch2 == null || controlMatch2.Score < num4)
		{
			return null;
		}
		return controlMatch2;
	}

	private static ControlMatch? FindFastPropertyMatch(AutomationElement root, ControlLocatorOptions options, ControlType? type)
	{
		System.Windows.Automation.Condition condition = null;
		if (!string.IsNullOrWhiteSpace(options.AutomationId))
		{
			condition = new PropertyCondition(AutomationElement.AutomationIdProperty, options.AutomationId, PropertyConditionFlags.IgnoreCase);
		}
		else if (!string.IsNullOrWhiteSpace(options.ClassName))
		{
			condition = new PropertyCondition(AutomationElement.ClassNameProperty, options.ClassName, PropertyConditionFlags.IgnoreCase);
		}
		if (condition == null)
		{
			return null;
		}
		if (condition != null)
		{
			System.Windows.Automation.Condition condition2 = condition;
			System.Windows.Automation.Condition condition3 = ((type == null) ? condition2 : new AndCondition(condition2, new PropertyCondition(AutomationElement.ControlTypeProperty, type)));
			ControlMatch controlMatch = null;
			try
			{
				AutomationElementCollection automationElementCollection = root.FindAll(TreeScope.Descendants, condition3);
				for (int i = 0; i < automationElementCollection.Count; i++)
				{
					ControlMatch controlMatch2 = ScoreCandidate(automationElementCollection[i], automationElementCollection[i].Current, options, type, i + 1);
					if (controlMatch2 != null && (controlMatch == null || controlMatch2.Score > controlMatch.Score))
					{
						controlMatch = controlMatch2;
					}
				}
			}
			catch
			{
				return null;
			}
			int num = Math.Clamp(options.MinScore, 1, 200);
			if (controlMatch == null || controlMatch.Score < num)
			{
				return null;
			}
			return controlMatch;
		}
		return null;
	}

	private static bool IsStrongMatch(ControlMatch match)
	{
		IReadOnlyList<string> reasons = match.Reasons;
		bool flag = reasons.Any((string r) => r.StartsWith("name:", StringComparison.OrdinalIgnoreCase) || r == "name-equals");
		bool flag2 = reasons.Contains("automation-id") || reasons.Contains("class-name");
		bool flag3 = reasons.Contains("control-type");
		bool flag4 = reasons.Contains("identity-only");
		if (!(match.Score >= 64 && flag) || !(flag2 || flag3))
		{
			return match.Score >= 64 && flag4 && flag2;
		}
		return true;
	}

	private static ControlMatch? ScoreCandidate(AutomationElement node, AutomationElement.AutomationElementInformation cur, ControlLocatorOptions options, ControlType? type, int visited)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_032c: Unknown result type (might be due to invalid IL or missing references)
		string name = cur.Name ?? string.Empty;
		if (cur.IsOffscreen)
		{
			return null;
		}
		if (!MatchesHardFilters(cur, options, type, name))
		{
			return null;
		}
		Rect boundingRectangle = cur.BoundingRectangle;
		if (((Rect)(ref boundingRectangle)).IsEmpty || ((Rect)(ref boundingRectangle)).Width <= 4.0 || ((Rect)(ref boundingRectangle)).Height <= 4.0 || ((Rect)(ref boundingRectangle)).Width >= 3500.0 || ((Rect)(ref boundingRectangle)).Height >= 2200.0)
		{
			return null;
		}
		string[] array = (from h in options.NameContains
			where !string.IsNullOrWhiteSpace(h)
			select h.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		bool flag = options.AllowTypeOnlyMatch && (!string.IsNullOrWhiteSpace(options.AutomationId) || !string.IsNullOrWhiteSpace(options.ClassName));
		TextHintMatch[] source = ((array.Length == 0 || flag) ? Array.Empty<TextHintMatch>() : MatchTextHints(ReadUiaTextPieces(node, cur, array, options.RequireAllNameContains), array).ToArray());
		string[] array2 = source.Select((TextHintMatch m) => m.Hint).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		if (array.Length != 0 && array2.Length == 0 && !options.AllowTypeOnlyMatch)
		{
			return null;
		}
		if (options.RequireAllNameContains && array2.Length < array.Length)
		{
			return null;
		}
		int num = 0;
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(options.NameEquals))
		{
			num += 80;
			list.Add("name-equals");
		}
		if (!string.IsNullOrWhiteSpace(options.AutomationId))
		{
			num += 70;
			list.Add("automation-id");
		}
		if (!string.IsNullOrWhiteSpace(options.ClassName))
		{
			num += 30;
			list.Add("class-name");
		}
		if (flag)
		{
			num += 20;
			list.Add("identity-only");
		}
		foreach (TextHintMatch item in source.DistinctBy<TextHintMatch, string>((TextHintMatch m) => m.Source + "\0" + m.Hint, StringComparer.OrdinalIgnoreCase))
		{
			num += 10 + Math.Min(item.Hint.Length, 24);
			list.Add(item.Source + ":" + item.Hint);
		}
		if (options.RequireAllNameContains && array.Length != 0 && array2.Length == array.Length)
		{
			num += 20;
			list.Add("all-name-hints");
		}
		if (type != null && cur.ControlType == type)
		{
			num += 18;
			list.Add("control-type");
		}
		if (IsPreciseControlType(cur.ControlType))
		{
			num += 4;
			list.Add("precise-control");
		}
		return new ControlMatch
		{
			Bounds = new Rect(((Rect)(ref boundingRectangle)).X, ((Rect)(ref boundingRectangle)).Y, ((Rect)(ref boundingRectangle)).Width, ((Rect)(ref boundingRectangle)).Height),
			Score = num,
			VisitedNodes = visited,
			Name = name,
			AutomationId = (cur.AutomationId ?? string.Empty),
			ClassName = (cur.ClassName ?? string.Empty),
			ControlType = (cur.ControlType?.ProgrammaticName ?? string.Empty),
			MatchedHints = array2,
			Reasons = list
		};
	}

	private static bool MatchesHardFilters(AutomationElement.AutomationElementInformation cur, ControlLocatorOptions options, ControlType? type, string name)
	{
		if (!string.IsNullOrWhiteSpace(options.NameEquals) && !string.Equals(name, options.NameEquals, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(options.AutomationId) && !string.Equals(cur.AutomationId ?? string.Empty, options.AutomationId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(options.ClassName) && (cur.ClassName ?? string.Empty).IndexOf(options.ClassName, StringComparison.OrdinalIgnoreCase) < 0)
		{
			return false;
		}
		if (type != null && cur.ControlType != type)
		{
			return false;
		}
		return true;
	}

	private static IReadOnlyList<UiaTextPiece> ReadUiaTextPieces(AutomationElement node, AutomationElement.AutomationElementInformation cur, IReadOnlyList<string> wanted, bool requireAllHints)
	{
		List<UiaTextPiece> list = new List<UiaTextPiece>();
		AddTextPiece(list, "name", cur.Name);
		if (TextPiecesSatisfy(list, wanted, requireAllHints))
		{
			return list;
		}
		TryAddPatternText(list, "value", node, ValuePattern.Pattern, (object pattern) => ((ValuePattern)pattern).Current.Value);
		TryAddLegacyText(list, node);
		if (!TextPiecesSatisfy(list, wanted, requireAllHints) && ShouldProbeTextPattern(cur))
		{
			TryAddPatternText(list, "text", node, TextPattern.Pattern, (object pattern) => ((TextPattern)pattern).DocumentRange.GetText(800));
		}
		return list.Where((UiaTextPiece p) => !string.IsNullOrWhiteSpace(p.Text)).DistinctBy<UiaTextPiece, string>((UiaTextPiece p) => p.Source + "\0" + p.Text, StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static bool TextPiecesSatisfy(IReadOnlyList<UiaTextPiece> pieces, IReadOnlyList<string> wanted, bool requireAllHints)
	{
		if (wanted.Count == 0)
		{
			return true;
		}
		int num = (from m in MatchTextHints(pieces, wanted)
			select m.Hint).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count();
		if (!requireAllHints)
		{
			return num > 0;
		}
		return num == wanted.Count;
	}

	private static bool ShouldProbeTextPattern(AutomationElement.AutomationElementInformation cur)
	{
		if (cur.ControlType == ControlType.Document || cur.ControlType == ControlType.Edit || cur.ControlType == ControlType.Text)
		{
			return true;
		}
		string text = cur.ClassName ?? string.Empty;
		string text2 = cur.AutomationId ?? string.Empty;
		if (text.IndexOf("text", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("edit", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("code", StringComparison.OrdinalIgnoreCase) < 0 && text2.IndexOf("text", StringComparison.OrdinalIgnoreCase) < 0 && text2.IndexOf("edit", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return text2.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private static void TryAddPatternText(List<UiaTextPiece> pieces, string source, AutomationElement node, AutomationPattern pattern, Func<object, string?> read)
	{
		try
		{
			if (node.TryGetCurrentPattern(pattern, out var patternObject) && patternObject != null)
			{
				AddTextPiece(pieces, source, read(patternObject));
			}
		}
		catch
		{
		}
	}

	private static void TryAddLegacyText(List<UiaTextPiece> pieces, AutomationElement node)
	{
		try
		{
			AutomationPattern automationPattern = AutomationPattern.LookupById(10018);
			if (automationPattern != null && node.TryGetCurrentPattern(automationPattern, out var patternObject) && patternObject != null)
			{
				object obj = patternObject.GetType().GetProperty("Current")?.GetValue(patternObject);
				if (obj != null)
				{
					AddReflectedPropertyText(pieces, "legacy-name", obj, "Name");
					AddReflectedPropertyText(pieces, "legacy-value", obj, "Value");
					AddReflectedPropertyText(pieces, "legacy-description", obj, "Description");
				}
			}
		}
		catch
		{
		}
	}

	private static void AddReflectedPropertyText(List<UiaTextPiece> pieces, string source, object current, string property)
	{
		try
		{
			AddTextPiece(pieces, source, current.GetType().GetProperty(property)?.GetValue(current) as string);
		}
		catch
		{
		}
	}

	private static void AddTextPiece(List<UiaTextPiece> pieces, string source, string? text)
	{
		if (!string.IsNullOrWhiteSpace(text))
		{
			pieces.Add(new UiaTextPiece(NormalizeText(text), source));
		}
	}

	private static string NormalizeText(string text)
	{
		return text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
	}

	private static string SampleText(string text)
	{
		string text2 = NormalizeText(text).Replace('\n', ' ');
		if (text2.Length > 180)
		{
			return text2.Substring(0, 180) + "...";
		}
		return text2;
	}

	private static bool IsPreciseControlType(ControlType? type)
	{
		if (type != ControlType.Button && type != ControlType.Edit && type != ControlType.Text && type != ControlType.TreeItem && type != ControlType.ListItem && type != ControlType.MenuItem && type != ControlType.TabItem && type != ControlType.DataItem && type != ControlType.Document && type != ControlType.Pane && type != ControlType.ComboBox && type != ControlType.Group && type != ControlType.CheckBox && type != ControlType.Custom && type != ControlType.Table && type != ControlType.ToolBar && type != ControlType.List && type != ControlType.Tab && type != ControlType.Header)
		{
			return type == ControlType.StatusBar;
		}
		return true;
	}

	private static ControlMatch WindowFallback(Rect bounds, int score, string reason)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		ControlMatch controlMatch = new ControlMatch();
		controlMatch.Bounds = bounds;
		controlMatch.Score = score;
		controlMatch.Provider = "fallback";
		controlMatch.Reasons = new string[1] { reason };
		return controlMatch;
	}

	private static ChildWindowInfo? ReadChildWindow(nint hwnd)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (!GetWindowRect(hwnd, out var lpRect))
		{
			return null;
		}
		int num = lpRect.Right - lpRect.Left;
		int num2 = lpRect.Bottom - lpRect.Top;
		if (num <= 4 || num2 <= 4 || num >= 3500 || num2 >= 2200)
		{
			return null;
		}
		return new ChildWindowInfo(hwnd, ReadWindowText(hwnd), ReadClassName(hwnd), new Rect((double)lpRect.Left, (double)lpRect.Top, (double)num, (double)num2));
	}

	private static string ReadWindowText(nint hwnd)
	{
		StringBuilder stringBuilder = new StringBuilder(1024);
		GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	private static string ReadClassName(nint hwnd)
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		GetClassName(hwnd, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	private bool TryGetCached<T>(Dictionary<string, CacheEntry<T>> cache, string key, out T value)
	{
		lock (_cacheLock)
		{
			if (cache.TryGetValue(key, out CacheEntry<T> value2) && Stopwatch.GetElapsedTime(value2.Timestamp).TotalMilliseconds <= 350.0)
			{
				value = value2.Value;
				return true;
			}
		}
		value = default(T);
		return false;
	}

	private void SetCached<T>(Dictionary<string, CacheEntry<T>> cache, string key, T value)
	{
		lock (_cacheLock)
		{
			if (cache.Count > 512)
			{
				cache.Clear();
			}
			cache[key] = new CacheEntry<T>(value, Stopwatch.GetTimestamp());
		}
	}

	private bool TryGetSnapshotCached(long hwnd, out UiaWindowSnapshot snapshot)
	{
		lock (_cacheLock)
		{
			if (_snapshotCache.TryGetValue(hwnd, out CacheEntry<UiaWindowSnapshot> value) && Stopwatch.GetElapsedTime(value.Timestamp).TotalMilliseconds <= 450.0)
			{
				snapshot = value.Value;
				return true;
			}
		}
		snapshot = null;
		return false;
	}

	private void SetSnapshotCached(long hwnd, UiaWindowSnapshot snapshot)
	{
		lock (_cacheLock)
		{
			if (_snapshotCache.Count > 64)
			{
				_snapshotCache.Clear();
			}
			_snapshotCache[hwnd] = new CacheEntry<UiaWindowSnapshot>(snapshot, Stopwatch.GetTimestamp());
		}
	}

	private bool IsUiaBackedOff(string key)
	{
		lock (_cacheLock)
		{
			if (!_uiaTimeoutBackoff.TryGetValue(key, out var value))
			{
				return false;
			}
			if (Stopwatch.GetElapsedTime(value).TotalMilliseconds <= 5000.0)
			{
				return true;
			}
			_uiaTimeoutBackoff.Remove(key);
			return false;
		}
	}

	private void SetUiaBackoff(string key)
	{
		lock (_cacheLock)
		{
			if (_uiaTimeoutBackoff.Count > 512)
			{
				_uiaTimeoutBackoff.Clear();
			}
			_uiaTimeoutBackoff[key] = Stopwatch.GetTimestamp();
		}
	}

	private static string ControlOptionsKey(ControlLocatorOptions options)
	{
		return string.Join("|", ListKey(options.NameContains), options.RequireAllNameContains.ToString(), options.NameEquals ?? "", options.AutomationId ?? "", options.ClassName ?? "", options.ControlType ?? "", NormalizeBackend(options.Backend), options.MaxNodes.ToString(), options.TimeoutMs.ToString(), options.MinScore.ToString(), options.AllowTypeOnlyMatch.ToString());
	}

	private static string ListKey(IReadOnlyList<string> items)
	{
		return string.Join("\u001f", items.Select((string i) => (i ?? string.Empty).Trim()));
	}

	private static string RectKey(Rect? rect)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (rect.HasValue)
		{
			Rect valueOrDefault = rect.GetValueOrDefault();
			return $"{((Rect)(ref valueOrDefault)).Left:0.###},{((Rect)(ref valueOrDefault)).Top:0.###},{((Rect)(ref valueOrDefault)).Width:0.###},{((Rect)(ref valueOrDefault)).Height:0.###}";
		}
		return "";
	}

	private static string WindowBackoffKey(nint hwnd)
	{
		return "window|" + ((IntPtr)hwnd).ToInt64();
	}

	private static string NormalizeControlType(string? hint)
	{
		return (hint ?? string.Empty).Trim().ToLowerInvariant();
	}

	private static string NormalizeBackend(string? backend)
	{
		string text = (backend ?? "auto").Trim().ToLowerInvariant();
		if ((!(text == "uia") && !(text == "win32")) || 1 == 0)
		{
			return "auto";
		}
		return text;
	}

	private static bool Win32ControlTypeMatches(string className, string expected)
	{
		return Win32ControlTypeName(className).Equals(expected, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsPreciseWin32Class(string className)
	{
		return !string.IsNullOrWhiteSpace(Win32ControlTypeName(className));
	}

	private static string Win32ControlTypeName(string className)
	{
		string text = className.Trim().ToLowerInvariant();
		if (text.Contains("button"))
		{
			return "button";
		}
		bool flag;
		switch (text)
		{
		case "edit":
		case "richedit20w":
		case "richedit50w":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag || text.Contains("edit"))
		{
			return "edit";
		}
		if (text == "static" || text.Contains("label"))
		{
			return "text";
		}
		if (text.Contains("combobox"))
		{
			return "combobox";
		}
		if (text.Contains("systreeview") || text.Contains("treeview"))
		{
			return "treeitem";
		}
		if (text.Contains("syslistview") || text.Contains("listview"))
		{
			return "listitem";
		}
		if (text.Contains("toolbar"))
		{
			return "toolbar";
		}
		if (text.Contains("systabcontrol") || text.Contains("tabcontrol"))
		{
			return "tab";
		}
		if (text.Contains("statusbar"))
		{
			return "statusbar";
		}
		if (text.Contains("scrollbar"))
		{
			return "scrollbar";
		}
		return string.Empty;
	}

	private static ControlType? ParseControlType(string? hint)
	{
		if (string.IsNullOrWhiteSpace(hint))
		{
			return null;
		}
		return hint.Trim().ToLowerInvariant() switch
		{
			"button" => ControlType.Button, 
			"edit" => ControlType.Edit, 
			"text" => ControlType.Text, 
			"treeitem" => ControlType.TreeItem, 
			"listitem" => ControlType.ListItem, 
			"menuitem" => ControlType.MenuItem, 
			"tabitem" => ControlType.TabItem, 
			"pane" => ControlType.Pane, 
			"window" => ControlType.Window, 
			"document" => ControlType.Document, 
			"dataitem" => ControlType.DataItem, 
			"combobox" => ControlType.ComboBox, 
			"group" => ControlType.Group, 
			"checkbox" => ControlType.CheckBox, 
			"custom" => ControlType.Custom, 
			"table" => ControlType.Table, 
			"toolbar" => ControlType.ToolBar, 
			"list" => ControlType.List, 
			"tab" => ControlType.Tab, 
			"header" => ControlType.Header, 
			"statusbar" => ControlType.StatusBar, 
			"titlebar" => ControlType.TitleBar, 
			_ => null, 
		};
	}
}
