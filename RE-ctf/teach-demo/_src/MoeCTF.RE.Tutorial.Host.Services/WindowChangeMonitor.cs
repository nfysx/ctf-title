using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class WindowChangeMonitor : IDisposable
{
	private delegate void WinEventDelegate(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint eventThread, uint eventTime);

	private const uint WineventOutOfContext = 0u;

	private const uint WineventSkipOwnProcess = 2u;

	private const uint EventObjectShow = 32770u;

	private const uint EventObjectFocus = 32773u;

	private const uint EventObjectStateChange = 32778u;

	private const uint EventObjectNameChange = 32780u;

	private const uint EventObjectValueChange = 32782u;

	private const uint GaRoot = 2u;

	private readonly Action<nint> _changed;

	private readonly WinEventDelegate _callback;

	private readonly List<nint> _hooks = new List<nint>();

	[DllImport("user32.dll")]
	private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint eventHook, WinEventDelegate callback, uint processId, uint threadId, uint flags);

	[DllImport("user32.dll")]
	private static extern bool UnhookWinEvent(nint hook);

	[DllImport("user32.dll")]
	private static extern nint GetAncestor(nint hwnd, uint flags);

	public WindowChangeMonitor(Action<nint> changed)
	{
		_changed = changed;
		_callback = OnWinEvent;
		AddHook(32770u, 32773u);
		AddHook(32778u, 32778u);
		AddHook(32780u, 32780u);
		AddHook(32782u, 32782u);
	}

	private void AddHook(uint minimum, uint maximum)
	{
		nint num = SetWinEventHook(minimum, maximum, IntPtr.Zero, _callback, 0u, 0u, 2u);
		if (num != IntPtr.Zero)
		{
			_hooks.Add(num);
		}
	}

	private void OnWinEvent(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint eventThread, uint eventTime)
	{
		if (hwnd != IntPtr.Zero)
		{
			nint ancestor = GetAncestor(hwnd, 2u);
			_changed((ancestor == IntPtr.Zero) ? hwnd : ancestor);
		}
	}

	public void Dispose()
	{
		foreach (nint hook in _hooks)
		{
			UnhookWinEvent(hook);
		}
		_hooks.Clear();
		GC.KeepAlive(_callback);
	}
}
