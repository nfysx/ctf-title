using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class WindowTracker
{
	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct POINT
	{
		public int X;

		public int Y;
	}

	private struct PROCESS_BASIC_INFORMATION
	{
		public nint Reserved1;

		public nint PebBaseAddress;

		public nint Reserved2_0;

		public nint Reserved2_1;

		public nint UniqueProcessId;

		public nint InheritedFromUniqueProcessId;
	}

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool ClientToScreen(nint hWnd, ref POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern nint GetLastActivePopup(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint hWnd, uint uCmd);

	[DllImport("user32.dll")]
	private static extern nint GetAncestor(nint hWnd, uint gaFlags);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool QueryFullProcessImageName(nint processHandle, int flags, StringBuilder exeName, ref int size);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(nint handle);

	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationProcess(nint processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

	public IReadOnlyList<WindowInfo> FindWindowsForProcess(Process process)
	{
		try
		{
			if (process.HasExited)
			{
				return Array.Empty<WindowInfo>();
			}
		}
		catch
		{
			return Array.Empty<WindowInfo>();
		}
		return FindWindowsByPids(CollectProcessTreeIds(process.Id));
	}

	public IReadOnlyList<WindowInfo> FindWindowsForManaged(ManagedProcess mp)
	{
		List<WindowInfo> list = new List<WindowInfo>();
		try
		{
			if (!mp.Process.HasExited)
			{
				list.AddRange(FindWindowsForProcess(mp.Process));
			}
		}
		catch
		{
		}
		if (list.Count == 0 && (mp.TitleHints.Length != 0 || mp.ProcessHints.Length != 0))
		{
			list.AddRange(FindWindowsByHints(mp.TitleHints, mp.ProcessHints));
		}
		return (from w in list
			group w by w.Hwnd into g
			select g.First()).OrderByDescending(delegate(WindowInfo w)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			Rect bounds = w.Bounds;
			double width = ((Rect)(ref bounds)).Width;
			bounds = w.Bounds;
			return width * ((Rect)(ref bounds)).Height;
		}).ToList();
	}

	public IReadOnlyList<WindowInfo> FindWindowsByHints(IEnumerable<string> titleHints, IEnumerable<string> processHints)
	{
		string[] titles = titleHints.Where((string s) => !string.IsNullOrWhiteSpace(s)).ToArray();
		string[] procs = processHints.Where((string s) => !string.IsNullOrWhiteSpace(s)).ToArray();
		return EnumerateVisibleWindows().Where(delegate(WindowInfo w)
		{
			if (procs.Any((string h) => w.ProcessName.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
			{
				return true;
			}
			return titles.Any((string h) => w.Title.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0) ? true : false;
		}).OrderByDescending(delegate(WindowInfo w)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			Rect bounds = w.Bounds;
			double width = ((Rect)(ref bounds)).Width;
			bounds = w.Bounds;
			return width * ((Rect)(ref bounds)).Height;
		}).ToList();
	}

	private IReadOnlyList<WindowInfo> FindWindowsByPids(HashSet<uint> pids)
	{
		return (from w in EnumerateVisibleWindows()
			where pids.Contains((uint)w.Pid)
			select w).OrderByDescending(delegate(WindowInfo w)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			Rect bounds = w.Bounds;
			double width = ((Rect)(ref bounds)).Width;
			bounds = w.Bounds;
			return width * ((Rect)(ref bounds)).Height;
		}).ToList();
	}

	public IReadOnlyList<WindowInfo> EnumerateVisibleWindows()
	{
		List<WindowInfo> list = new List<WindowInfo>();
		nint foreground = GetForegroundWindow();
		EnumWindows(delegate(nint hWnd, nint _)
		{
			//IL_0159: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			if (!IsWindowVisible(hWnd))
			{
				return true;
			}
			uint lpdwProcessId;
			uint windowThreadProcessId = GetWindowThreadProcessId(hWnd, out lpdwProcessId);
			StringBuilder stringBuilder = new StringBuilder(512);
			GetWindowText(hWnd, stringBuilder, stringBuilder.Capacity);
			if (!TryGetWindowBounds(hWnd, out var bounds))
			{
				return true;
			}
			double width = ((Rect)(ref bounds)).Width;
			double height = ((Rect)(ref bounds)).Height;
			if (width < 50.0 || height < 50.0)
			{
				return true;
			}
			if (!TryGetClientBounds(hWnd, out var bounds2))
			{
				bounds2 = bounds;
			}
			string text = stringBuilder.ToString();
			if (string.IsNullOrWhiteSpace(text))
			{
				return true;
			}
			StringBuilder stringBuilder2 = new StringBuilder(256);
			GetClassName(hWnd, stringBuilder2, stringBuilder2.Capacity);
			string processName = "";
			string exePath = "";
			try
			{
				using Process process = Process.GetProcessById((int)lpdwProcessId);
				processName = process.ProcessName;
				exePath = TryQueryExePath((uint)process.Id);
			}
			catch
			{
			}
			list.Add(new WindowInfo
			{
				Hwnd = hWnd,
				Pid = (int)lpdwProcessId,
				ThreadId = (int)windowThreadProcessId,
				OwnerHwnd = GetWindow(hWnd, 4u),
				RootOwnerHwnd = GetAncestor(hWnd, 3u),
				LastActivePopupHwnd = GetLastActivePopup(hWnd),
				IsForeground = (hWnd == foreground),
				Title = text,
				ProcessName = processName,
				ClassName = stringBuilder2.ToString(),
				ExePath = exePath,
				Bounds = bounds,
				ClientBounds = bounds2
			});
			return true;
		}, IntPtr.Zero);
		return list;
	}

	public bool TryActivate(WindowInfo window)
	{
		if (window.Hwnd == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			ShowWindow(window.Hwnd, 9);
			return SetForegroundWindow(window.Hwnd);
		}
		catch
		{
			return false;
		}
	}

	private static HashSet<uint> CollectProcessTreeIds(int rootPid)
	{
		HashSet<uint> hashSet = new HashSet<uint> { (uint)rootPid };
		Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			try
			{
				int parentProcessId = GetParentProcessId(process);
				if (parentProcessId > 0)
				{
					if (!dictionary.TryGetValue(parentProcessId, out var value))
					{
						value = (dictionary[parentProcessId] = new List<int>());
					}
					value.Add(process.Id);
				}
			}
			catch
			{
			}
			finally
			{
				try
				{
					process.Dispose();
				}
				catch
				{
				}
			}
		}
		Queue<int> queue = new Queue<int>();
		queue.Enqueue(rootPid);
		while (queue.Count > 0)
		{
			int key = queue.Dequeue();
			if (!dictionary.TryGetValue(key, out var value2))
			{
				continue;
			}
			foreach (int item in value2)
			{
				if (hashSet.Add((uint)item))
				{
					queue.Enqueue(item);
				}
			}
		}
		return hashSet;
	}

	private static int GetParentProcessId(Process process)
	{
		try
		{
			PROCESS_BASIC_INFORMATION processInformation = default(PROCESS_BASIC_INFORMATION);
			if (NtQueryInformationProcess(process.Handle, 0, ref processInformation, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out var _) != 0)
			{
				return 0;
			}
			return ((IntPtr)processInformation.InheritedFromUniqueProcessId).ToInt32();
		}
		catch
		{
			return 0;
		}
	}

	private static string TryQueryExePath(uint pid)
	{
		nint num = OpenProcess(4096u, inheritHandle: false, pid);
		if (num == IntPtr.Zero)
		{
			return string.Empty;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(4096);
			int size = stringBuilder.Capacity;
			return QueryFullProcessImageName(num, 0, stringBuilder, ref size) ? stringBuilder.ToString() : string.Empty;
		}
		catch
		{
			return string.Empty;
		}
		finally
		{
			CloseHandle(num);
		}
	}

	private static bool TryGetWindowBounds(nint hwnd, out Rect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		bounds = Rect.Empty;
		if (DwmGetWindowAttribute(hwnd, 9, out var pvAttribute, Marshal.SizeOf<RECT>()) == 0)
		{
			int num = pvAttribute.Right - pvAttribute.Left;
			int num2 = pvAttribute.Bottom - pvAttribute.Top;
			if (num > 0 && num2 > 0)
			{
				bounds = new Rect((double)pvAttribute.Left, (double)pvAttribute.Top, (double)num, (double)num2);
				return true;
			}
		}
		if (!GetWindowRect(hwnd, out var lpRect))
		{
			return false;
		}
		int num3 = lpRect.Right - lpRect.Left;
		int num4 = lpRect.Bottom - lpRect.Top;
		if (num3 <= 0 || num4 <= 0)
		{
			return false;
		}
		bounds = new Rect((double)lpRect.Left, (double)lpRect.Top, (double)num3, (double)num4);
		return true;
	}

	private static bool TryGetClientBounds(nint hwnd, out Rect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		bounds = Rect.Empty;
		if (!GetClientRect(hwnd, out var lpRect))
		{
			return false;
		}
		int num = lpRect.Right - lpRect.Left;
		int num2 = lpRect.Bottom - lpRect.Top;
		if (num <= 0 || num2 <= 0)
		{
			return false;
		}
		POINT pOINT = default(POINT);
		pOINT.X = lpRect.Left;
		pOINT.Y = lpRect.Top;
		POINT lpPoint = pOINT;
		if (!ClientToScreen(hwnd, ref lpPoint))
		{
			return false;
		}
		bounds = new Rect((double)lpPoint.X, (double)lpPoint.Y, (double)num, (double)num2);
		return true;
	}
}
