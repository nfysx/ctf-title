using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RETutor.Services;

namespace RETutor.Services;

public sealed class WindowInfo
{
    public IntPtr Hwnd { get; init; }
    public int Pid { get; init; }
    public string Title { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string ProcessName { get; init; } = "";
    public string ExePath { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double ClientX { get; init; }
    public double ClientY { get; init; }
    public double ClientWidth { get; init; }
    public double ClientHeight { get; init; }
    public bool IsForeground { get; init; }
    public IntPtr OwnerHwnd { get; init; }
    public IntPtr RootOwnerHwnd { get; init; }
    public IntPtr LastActivePopup { get; init; }

    public bool HitContains(string needle) =>
        !string.IsNullOrEmpty(needle) &&
        (Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
         ClassName.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
         ProcessName.Contains(needle, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// 对应原程序 Services\WindowTracker.cs：只做"看"，EnumWindows + DWM 扩展边框 + 客户区双矩形 + 进程树。
/// </summary>
public sealed class WindowTracker
{
    private Dictionary<int, string> _exeCache = new();
    private Dictionary<int, string> _nameCache = new();

    public List<WindowInfo> EnumerateVisibleWindows()
    {
        var list = new List<WindowInfo>();
        var fg = NativeMethods.GetForegroundWindow();
        NativeMethods.EnumWindows((hwnd, l) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            var sb = new StringBuilder(512);
            NativeMethods.GetWindowTextW(hwnd, sb, sb.Capacity);
            var title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;
            var info = Describe(hwnd, title, fg);
            if (info == null) return true;
            if (info.Width < 50 || info.Height < 50) return true;
            list.Add(info);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public WindowInfo Describe(IntPtr hwnd, string title = null, IntPtr? foreground = null)
    {
        try
        {
            if (title == null)
            {
                var sb = new StringBuilder(512);
                NativeMethods.GetWindowTextW(hwnd, sb, sb.Capacity);
                title = sb.ToString();
            }
            var cls = new StringBuilder(256);
            NativeMethods.GetClassNameW(hwnd, cls, cls.Capacity);
            NativeMethods.GetWindowRect(hwnd, out var wr);
            var bounds = wr;
            // DWM 扩展边框 = 视觉边界；失败才退回 GetWindowRect
            if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var dwm, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) == 0)
                bounds = dwm;

            int x = bounds.Left, y = bounds.Top;
            int w = bounds.Right - bounds.Left, h = bounds.Bottom - bounds.Top;
            NativeMethods.GetClientRect(hwnd, out var cr);
            var pt = new NativeMethods.POINT { X = 0, Y = 0 };
            NativeMethods.ClientToScreen(hwnd, ref pt);

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            return new WindowInfo
            {
                Hwnd = hwnd,
                Pid = (int)pid,
                Title = title ?? "",
                ClassName = cls.ToString(),
                ProcessName = ProcessNameOf((int)pid),
                ExePath = ExePathOf((int)pid),
                X = x, Y = y, Width = Math.Max(1, w), Height = Math.Max(1, h),
                ClientX = pt.X, ClientY = pt.Y,
                ClientWidth = Math.Max(1, cr.Right - cr.Left),
                ClientHeight = Math.Max(1, cr.Bottom - cr.Top),
                IsForeground = hwnd == (foreground ?? IntPtr.Zero),
                OwnerHwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER),
                RootOwnerHwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOTOWNER),
                LastActivePopup = NativeMethods.GetLastActivePopup(hwnd)
            };
        }
        catch { return null; }
    }

    public string ProcessNameOf(int pid)
    {
        if (_nameCache.TryGetValue(pid, out var cached)) return cached;
        string name = "";
        try { using var p = Process.GetProcessById(pid); name = p.ProcessName; } catch { }
        _nameCache[pid] = name;
        return name;
    }

    public string ExePathOf(int pid)
    {
        if (_exeCache.TryGetValue(pid, out var cached)) return cached;
        string path = "";
        try { using var p = Process.GetProcessById(pid); path = p.MainModule?.FileName ?? ""; } catch { }
        _exeCache[pid] = path;
        return path;
    }

    /// <summary>进程树 BFS（Ghidra 的 javaw → java 需要整棵树）。</summary>
    public HashSet<int> CollectProcessTreeIds(int rootPid)
    {
        var result = new HashSet<int> { rootPid };
        var parentOf = new Dictionary<int, int>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                int parent = ParentPid(p.Id);
                if (parent > 0) parentOf[p.Id] = parent;
            }
            catch { }
            finally { p.Dispose(); }
        }
        var queue = new Queue<int>(result);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (var kv in parentOf.Where(kv => kv.Value == cur).ToList())
                if (result.Add(kv.Key)) queue.Enqueue(kv.Key);
        }
        return result;
    }

    private static int ParentPid(int pid)
    {
        IntPtr h = NativeMethods.OpenProcess(0x1000 /*PROCESS_QUERY_LIMITED_INFORMATION*/, false, (uint)pid);
        if (h == IntPtr.Zero) return 0;
        try
        {
            var pbi = new NativeMethods.PROCESS_BASIC_INFORMATION();
            int len;
            int status = NativeMethods.NtQueryInformationProcess(h, 0, ref pbi, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.PROCESS_BASIC_INFORMATION>(), out len);
            if (status != 0) return 0;
            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
        finally { NativeMethods.CloseHandle(h); }
    }

    public HashSet<int> TreeOf(int pid) => CollectProcessTreeIds(pid);
}
