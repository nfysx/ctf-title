using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class NativeWindowChromeService : IDisposable
{
	private enum DwmWindowCornerPreference
	{
		Default,
		DoNotRound,
		Round
	}

	private struct NativePoint
	{
		public int X;

		public int Y;
	}

	private struct MinMaxInfo
	{
		public NativePoint Reserved;

		public NativePoint MaxSize;

		public NativePoint MaxPosition;

		public NativePoint MinTrackSize;

		public NativePoint MaxTrackSize;
	}

	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct MonitorInfo
	{
		public int Size;

		public NativeRect Monitor;

		public NativeRect WorkArea;

		public uint Flags;
	}

	private const int DwmwaWindowCornerPreference = 33;

	private const int WmSize = 5;

	private const int WmSettingChange = 26;

	private const int WmGetMinMaxInfo = 36;

	private const int WmDisplayChange = 126;

	private const int WmDwmCompositionChanged = 798;

	private const int WmDpiChanged = 736;

	private const uint MonitorDefaultToNearest = 2u;

	private readonly Window _window;

	private readonly double _radiusDip;

	private HwndSource? _source;

	private nint _windowHandle;

	private bool? _dwmCornersAvailable;

	private bool _fallbackRegionApplied;

	private bool _cornerUpdatePending;

	private bool _disposeRequested;

	private bool _disposed;

	private NativeWindowChromeService(Window window, double radiusDip)
	{
		_window = window;
		_radiusDip = radiusDip;
		_window.SourceInitialized += OnSourceInitialized;
		_window.SizeChanged += OnWindowSizeChanged;
		_window.StateChanged += OnWindowStateChanged;
		_window.Closed += OnWindowClosed;
		nint handle = new WindowInteropHelper(_window).Handle;
		if (handle != IntPtr.Zero)
		{
			InitializeNativeHandle(handle);
		}
	}

	public static NativeWindowChromeService Attach(Window window, double radiusDip = 8.0)
	{
		ArgumentNullException.ThrowIfNull(window, "window");
		((DispatcherObject)window).Dispatcher.VerifyAccess();
		if (double.IsNaN(radiusDip) || double.IsInfinity(radiusDip) || radiusDip < 0.0)
		{
			throw new ArgumentOutOfRangeException("radiusDip", radiusDip, "The corner radius must be a finite, non-negative DIP value.");
		}
		return new NativeWindowChromeService(window, radiusDip);
	}

	public void Dispose()
	{
		if (_disposed || _disposeRequested)
		{
			return;
		}
		if (!((DispatcherObject)_window).Dispatcher.CheckAccess())
		{
			if (!((DispatcherObject)_window).Dispatcher.HasShutdownStarted && !((DispatcherObject)_window).Dispatcher.HasShutdownFinished)
			{
				_disposeRequested = true;
				((DispatcherObject)_window).Dispatcher.BeginInvoke((DispatcherPriority)10, (Delegate)(Action)delegate
				{
					_disposeRequested = false;
					DisposeCore();
				});
			}
		}
		else
		{
			DisposeCore();
		}
	}

	private void OnSourceInitialized(object? sender, EventArgs e)
	{
		nint handle = new WindowInteropHelper(_window).Handle;
		if (handle != IntPtr.Zero)
		{
			InitializeNativeHandle(handle);
		}
	}

	private void InitializeNativeHandle(nint handle)
	{
		if (!_disposed && handle != _windowHandle)
		{
			RemoveHook();
			_windowHandle = handle;
			_source = HwndSource.FromHwnd(handle);
			_source?.AddHook(WindowMessageHook);
			UpdateNativeCorners();
		}
	}

	private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
	{
		ScheduleCornerUpdate();
	}

	private void OnWindowStateChanged(object? sender, EventArgs e)
	{
		ScheduleCornerUpdate();
	}

	private void OnWindowClosed(object? sender, EventArgs e)
	{
		DisposeCore();
	}

	private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
	{
		switch (message)
		{
		case 36:
			ConstrainMaximizedWindowToWorkArea(hwnd, lParam);
			break;
		case 5:
		case 26:
		case 126:
		case 736:
			ScheduleCornerUpdate();
			break;
		case 798:
			_dwmCornersAvailable = null;
			ScheduleCornerUpdate();
			break;
		}
		return IntPtr.Zero;
	}

	private void ScheduleCornerUpdate()
	{
		if (!_disposed && !_cornerUpdatePending && _windowHandle != IntPtr.Zero)
		{
			_cornerUpdatePending = true;
			((DispatcherObject)_window).Dispatcher.BeginInvoke((DispatcherPriority)7, (Delegate)(Action)delegate
			{
				_cornerUpdatePending = false;
				UpdateNativeCorners();
			});
		}
	}

	private void UpdateNativeCorners()
	{
		if (!_disposed && _windowHandle != IntPtr.Zero && IsWindow(_windowHandle))
		{
			bool flag = _window.WindowState == WindowState.Maximized || IsZoomed(_windowHandle);
			bool flag2 = _window.WindowState == WindowState.Minimized || IsIconic(_windowHandle);
			if (_radiusDip <= 0.0 || flag || flag2)
			{
				TrySetDwmCornerPreference(DwmWindowCornerPreference.DoNotRound);
				ClearFallbackRegion();
			}
			else if (TrySetDwmCornerPreference(DwmWindowCornerPreference.Round))
			{
				ClearFallbackRegion();
			}
			else
			{
				ApplyFallbackRegion();
			}
		}
	}

	private bool TrySetDwmCornerPreference(DwmWindowCornerPreference preference)
	{
		if (_dwmCornersAvailable == false || _windowHandle == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			int attributeValue = (int)preference;
			int num = DwmSetWindowAttribute(_windowHandle, 33, ref attributeValue, 4);
			_dwmCornersAvailable = num >= 0;
		}
		catch (DllNotFoundException)
		{
			_dwmCornersAvailable = false;
		}
		catch (EntryPointNotFoundException)
		{
			_dwmCornersAvailable = false;
		}
		return _dwmCornersAvailable == true;
	}

	private void ApplyFallbackRegion()
	{
		if (!GetWindowRect(_windowHandle, out var rectangle))
		{
			return;
		}
		int num = rectangle.Right - rectangle.Left;
		int num2 = rectangle.Bottom - rectangle.Top;
		if (num <= 0 || num2 <= 0)
		{
			return;
		}
		double windowDpi = GetWindowDpi();
		int num3;
		checked
		{
			num3 = Math.Max(1, unchecked((int)Math.Ceiling(_radiusDip * windowDpi / 96.0))) * 2;
		}
		nint num4 = CreateRoundRectRgn(0, 0, num + 1, num2 + 1, num3, num3);
		if (num4 != IntPtr.Zero)
		{
			if (SetWindowRgn(_windowHandle, num4, redraw: true) == 0)
			{
				DeleteObject(num4);
			}
			else
			{
				_fallbackRegionApplied = true;
			}
		}
	}

	private double GetWindowDpi()
	{
		try
		{
			uint dpiForWindow = GetDpiForWindow(_windowHandle);
			if (dpiForWindow != 0)
			{
				return dpiForWindow;
			}
		}
		catch (EntryPointNotFoundException)
		{
		}
		DpiScale dpi = VisualTreeHelper.GetDpi(_window);
		if (!(dpi.PixelsPerInchX > 0.0))
		{
			return 96.0;
		}
		return dpi.PixelsPerInchX;
	}

	private void ClearFallbackRegion()
	{
		if (_fallbackRegionApplied && _windowHandle != IntPtr.Zero && SetWindowRgn(_windowHandle, IntPtr.Zero, redraw: true) != 0)
		{
			_fallbackRegionApplied = false;
		}
	}

	private static void ConstrainMaximizedWindowToWorkArea(nint hwnd, nint minMaxInfoPointer)
	{
		if (minMaxInfoPointer == IntPtr.Zero)
		{
			return;
		}
		nint num = MonitorFromWindow(hwnd, 2u);
		if (num != IntPtr.Zero)
		{
			MonitorInfo monitorInfo = default(MonitorInfo);
			monitorInfo.Size = Marshal.SizeOf<MonitorInfo>();
			MonitorInfo monitorInfo2 = monitorInfo;
			if (GetMonitorInfo(num, ref monitorInfo2))
			{
				MinMaxInfo structure = Marshal.PtrToStructure<MinMaxInfo>(minMaxInfoPointer);
				structure.MaxPosition.X = monitorInfo2.WorkArea.Left - monitorInfo2.Monitor.Left;
				structure.MaxPosition.Y = monitorInfo2.WorkArea.Top - monitorInfo2.Monitor.Top;
				structure.MaxSize.X = monitorInfo2.WorkArea.Right - monitorInfo2.WorkArea.Left;
				structure.MaxSize.Y = monitorInfo2.WorkArea.Bottom - monitorInfo2.WorkArea.Top;
				Marshal.StructureToPtr(structure, minMaxInfoPointer, fDeleteOld: false);
			}
		}
	}

	private void DisposeCore()
	{
		if (!_disposed)
		{
			_disposed = true;
			_window.SourceInitialized -= OnSourceInitialized;
			_window.SizeChanged -= OnWindowSizeChanged;
			_window.StateChanged -= OnWindowStateChanged;
			_window.Closed -= OnWindowClosed;
			if (_windowHandle != IntPtr.Zero && IsWindow(_windowHandle))
			{
				ClearFallbackRegionForDispose();
				RestoreDefaultDwmPreference();
			}
			RemoveHook();
			_windowHandle = IntPtr.Zero;
			GC.SuppressFinalize(this);
		}
	}

	private void ClearFallbackRegionForDispose()
	{
		if (_fallbackRegionApplied && SetWindowRgn(_windowHandle, IntPtr.Zero, redraw: true) != 0)
		{
			_fallbackRegionApplied = false;
		}
	}

	private void RestoreDefaultDwmPreference()
	{
		if (_dwmCornersAvailable != true)
		{
			return;
		}
		int attributeValue = 0;
		try
		{
			DwmSetWindowAttribute(_windowHandle, 33, ref attributeValue, 4);
		}
		catch (DllNotFoundException)
		{
		}
		catch (EntryPointNotFoundException)
		{
		}
	}

	private void RemoveHook()
	{
		if (_source != null)
		{
			_source.RemoveHook(WindowMessageHook);
			_source = null;
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int attributeValue, int attributeSize);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DeleteObject(nint graphicsObject);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(nint hwnd, out NativeRect rectangle);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(nint hwnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindow(nint hwnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsZoomed(nint hwnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsIconic(nint hwnd);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);
}
