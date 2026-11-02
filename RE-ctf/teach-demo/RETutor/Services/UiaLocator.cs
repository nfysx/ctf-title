using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

namespace RETutor.Services;

public sealed class UiaNode
{
    public string Name = "";
    public string AutomationId = "";
    public string ClassName = "";
    public string ControlType = "";
    public string Text = "";
    public Rect Bounds;
    public bool Offscreen;
    public int Depth;
}

public sealed class UiaSnapshot
{
    public List<UiaNode> Nodes = new();
    public bool TimedOut;
    public DateTime CreatedUtc = DateTime.MinValue;
    public bool IsFresh => (DateTime.UtcNow - CreatedUtc).TotalMilliseconds < 450;
}

public sealed class LocatorQuery
{
    public string NameEquals;
    public List<string> NameContains = new();
    public string AutomationId;
    public string ClassName;
    public string ControlType;
    public bool RequireAll;
    public int MaxNodes = 2000;
    public int TimeoutMs = 1200;
    public int MinScore = 14;
}

/// <summary>
/// 对应原程序 Services\ControlLocator.cs 的核心思路：
/// 一次 BFS 建 UIA 快照（节点/时间双上限），多个定位器复用；文本按 Name → ValuePattern → LegacyIAccessible → TextPattern 逐级取。
/// 简化点：不做 Win32 子窗回退后端与 450ms 结果缓存（见报告"可简化"一节）。
/// </summary>
public sealed class UiaLocator
{
    // 全局并发闸：UIA 是跨进程 COM，对端 UI 线程卡死时并发会把自己拖垮
    private static readonly SemaphoreSlim Gate = new(Math.Clamp(Environment.ProcessorCount / 2, 2, 6));
    private readonly Dictionary<IntPtr, UiaSnapshot> _snapshots = new();
    private readonly object _snapLock = new();

    public UiaSnapshot GetSnapshot(IntPtr hwnd, int maxNodes = 2000, int timeoutMs = 1200)
    {
        lock (_snapLock)
        {
            if (_snapshots.TryGetValue(hwnd, out var cached))
            {
                if ((DateTime.UtcNow - cached.CreatedUtc).TotalMilliseconds < 450) return cached;
            }
        }
        if (!Gate.Wait(0)) return new UiaSnapshot { TimedOut = true }; // 抢不到闸直接判超时，绝不排队
        try
        {
            var snap = new UiaSnapshot();
            var task = Task.Run(() => Build(hwnd, maxNodes, snap));
            if (!task.Wait(timeoutMs)) { snap.TimedOut = true; }
            snap.CreatedUtc = DateTime.UtcNow;
            lock (_snapLock) { _snapshots[hwnd] = snap; }
            return snap;
        }
        finally { Gate.Release(); }
    }

    private static void Build(IntPtr hwnd, int maxNodes, UiaSnapshot snap)
    {
        AutomationElement root;
        try { root = AutomationElement.FromHandle(hwnd); }
        catch { return; }
        if (root == null) return;

        var queue = new Queue<(AutomationElement el, int depth)>();
        queue.Enqueue((root, 0));
        var walker = TreeWalker.ControlViewWalker; // ControlView：RawView 会把 Qt/Java 内部 panel 全吐出来
        var sw = Stopwatch.StartNew();
        while (queue.Count > 0 && snap.Nodes.Count < maxNodes && sw.ElapsedMilliseconds < 1200)
        {
            var (el, depth) = queue.Dequeue();
            try
            {
                var cur = el.Current;
                var node = new UiaNode
                {
                    Name = cur.Name ?? "",
                    AutomationId = cur.AutomationId ?? "",
                    ClassName = cur.ClassName ?? "",
                    ControlType = cur.ControlType?.ProgrammaticName ?? "",
                    Bounds = cur.BoundingRectangle,
                    Offscreen = cur.IsOffscreen,
                    Depth = depth,
                };
                node.Text = ReadText(el, node);
                snap.Nodes.Add(node);
            }
            catch { }
            try
            {
                var child = walker.GetFirstChild(el);
                while (child != null && snap.Nodes.Count < maxNodes)
                {
                    queue.Enqueue((child, depth + 1));
                    child = walker.GetNextSibling(child);
                }
            }
            catch { }
        }
    }

    /// <summary>逐级升级取文本：Name → Value → LegacyIAccessible → TextPattern。</summary>
    private static string ReadText(AutomationElement el, UiaNode node)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(node.Name)) parts.Add(node.Name);
        try
        {
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vp))
            {
                var v = ((ValuePattern)vp).Current.Value;
                if (!string.IsNullOrWhiteSpace(v)) parts.Add(v);
            }
        }
        catch { }
        try
        {
            // LegacyIAccessible = MSAA 通道，Qt/自绘控件的唯一文本来源
            var legacy = AutomationPattern.LookupById(10018);
            if (legacy != null && el.TryGetCurrentPattern(legacy, out var lp))
            {
                var t = lp.GetType();
                foreach (var prop in new[] { "CurrentName", "CurrentValue", "CurrentDescription" })
                {
                    var pi = t.GetProperty(prop);
                    var val = pi?.GetValue(lp) as string;
                    if (!string.IsNullOrWhiteSpace(val)) parts.Add(val);
                }
            }
        }
        catch { }
        bool wantsText = node.ControlType.Contains("Document") || node.ControlType.Contains("Edit") || node.ControlType.Contains("Text")
                         || node.ClassName.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0
                         || node.ClassName.IndexOf("edit", StringComparison.OrdinalIgnoreCase) >= 0
                         || node.AutomationId.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0;
        if (wantsText)
        {
            try
            {
                if (el.TryGetCurrentPattern(TextPattern.Pattern, out var tp))
                {
                    var text = ((TextPattern)tp).DocumentRange.GetText(800);
                    if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
                }
            }
            catch { }
        }
        return string.Join("\u001f", parts);
    }

    private static int Score(UiaNode node, LocatorQuery q, out string evidence)
    {
        int score = 0;
        var ev = new List<string>();
        if (!string.IsNullOrEmpty(q.NameEquals) && string.Equals(node.Name, q.NameEquals, StringComparison.OrdinalIgnoreCase))
        { score += 80; ev.Add("name-equals"); }
        if (!string.IsNullOrEmpty(q.AutomationId) && string.Equals(node.AutomationId, q.AutomationId, StringComparison.OrdinalIgnoreCase))
        { score += 70; ev.Add("automation-id"); }
        if (!string.IsNullOrEmpty(q.ClassName) && node.ClassName.IndexOf(q.ClassName, StringComparison.OrdinalIgnoreCase) >= 0)
        { score += 30; ev.Add("class-name"); }
        if (!string.IsNullOrEmpty(q.ControlType) && node.ControlType.IndexOf(q.ControlType, StringComparison.OrdinalIgnoreCase) >= 0)
        { score += 18; ev.Add("control-type"); }
        int hit = 0;
        foreach (var hint in q.NameContains)
        {
            if (string.IsNullOrEmpty(hint)) continue;
            if (node.Text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0 || node.Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
            { score += 10 + Math.Min(hint.Length, 24); hit++; ev.Add("name:" + hint); }
        }
        if (q.RequireAll && q.NameContains.Count > 0 && hit == q.NameContains.Count) { score += 20; ev.Add("all-hints"); }
        evidence = string.Join(",", ev);
        return score;
    }

    public (UiaNode node, int score, string evidence) FindControl(IntPtr hwnd, LocatorQuery q)
    {
        var snap = GetSnapshot(hwnd, q.MaxNodes, q.TimeoutMs);
        UiaNode best = null; int bestScore = 0; string bestEv = "";
        foreach (var node in snap.Nodes)
        {
            if (node.Offscreen) continue;
            if (node.Bounds.Width <= 3 || node.Bounds.Height <= 3) continue;
            if (q.RequireAll && q.NameContains.Count > 0)
            {
                bool all = q.NameContains.All(h => node.Text.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!all) continue;
            }
            int s = Score(node, q, out var ev);
            if (s > bestScore) { bestScore = s; best = node; bestEv = ev; }
        }
        return bestScore >= q.MinScore ? (best, bestScore, bestEv) : (null, bestScore, bestEv);
    }

    public bool ContainsAllText(IntPtr hwnd, List<string> hints, int maxNodes, int timeoutMs, out int matched)
    {
        matched = 0;
        if (hints == null || hints.Count == 0) return true;
        var snap = GetSnapshot(hwnd, maxNodes, timeoutMs);
        foreach (var hint in hints)
        {
            if (snap.Nodes.Any(n => n.Text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)) matched++;
        }
        return matched == hints.Count;
    }

    public void InvalidateWindow(IntPtr hwnd)
    {
        lock (_snapLock) { _snapshots.Remove(hwnd); }
    }
}
