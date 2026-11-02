using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RETutor.Models;
using RETutor.Services;

namespace RETutor.Guide;

public sealed class GuidePlan
{
    public string Layer { get; set; } = "none";      // none | host | screen
    public Rect BoundsScreen { get; set; }           // 屏幕物理像素
    public double Score { get; set; }
    public string Evidence { get; set; } = "";
    public string Attachment { get; set; } = "";
    public GuideOverlaySpec Overlay { get; set; } = new();
    public WindowInfo Target { get; set; }
    public bool IsDisplayable => Layer != "none" && BoundsScreen.Width > 1 && BoundsScreen.Height > 1;
}

/// <summary>
/// 对应原程序 Guide\GuideSceneMatcher + GuideRuntime 的核心子集：
/// 打分选窗口 → 定位控件/内容 → 合成渲染计划 → 求值 trigger 条件。
/// 简化点（见报告）：不做 Adapter 的 state/role 知识库、不做歧义打分带、不做识别稳定器。
/// </summary>
public sealed class GuideEngine
{
    private readonly WindowTracker _tracker;
    private readonly UiaLocator _uia;
    private readonly Func<string, FrameworkElement> _hotspot;

    public GuideEngine(WindowTracker tracker, UiaLocator uia, Func<string, FrameworkElement> hotspot)
    {
        _tracker = tracker; _uia = uia; _hotspot = hotspot;
    }

    public List<WindowInfo> Windows() => _tracker.EnumerateVisibleWindows();

    public int ScoreWindow(WindowInfo w, GuideTargetSpec target, string state, List<string> evidence)
    {
        int score = 0;
        var tool = ResolveToolKey(target);
        if (tool != null)
        {
            var spec = ToolCatalog.Get(tool);
            bool procHit = spec != null && spec.ProcessNameHints.Any(h => Contains(w.ProcessName, h));
            bool titleHit = spec != null && spec.WindowTitleHints.Any(h => Contains(w.Title, h));
            if (procHit) { score += 50; evidence?.Add("managed-process:" + tool); }
            if (titleHit) { score += 30; evidence?.Add("managed-title:" + tool); }
            if (!procHit && !titleHit) return 0;
        }

        int identityDeclared = 0, identityHit = 0;
        foreach (var hint in target.TitleContains ?? new List<string>())
        { identityDeclared++; if (Contains(w.Title, hint)) { identityHit++; score += 32 + Math.Min(hint.Length, 18); evidence?.Add("title:" + hint); } }
        foreach (var hint in target.ProcessNameContains ?? new List<string>())
        { identityDeclared++; if (Contains(w.ProcessName, hint)) { identityHit++; score += 34 + Math.Min(hint.Length, 18); evidence?.Add("process:" + hint); } }
        foreach (var hint in target.ExeNameContains ?? new List<string>())
        { identityDeclared++; if (Contains(System.IO.Path.GetFileName(w.ExePath), hint)) { identityHit++; score += 34 + Math.Min(hint.Length, 18); evidence?.Add("exe-name:" + hint); } }
        foreach (var hint in target.ExePathContains ?? new List<string>())
        { identityDeclared++; if (Contains(w.ExePath, hint)) { identityHit++; score += 24 + Math.Min(hint.Length, 18); evidence?.Add("exe-path:" + hint); } }
        foreach (var hint in target.WindowClassNameContains ?? new List<string>())
        { identityDeclared++; if (Contains(w.ClassName, hint)) { identityHit++; score += 24 + Math.Min(hint.Length, 18); evidence?.Add("window-class:" + hint); } }
        if (identityDeclared > 0 && identityHit == 0) return 0;

        if (w.IsForeground) { score += 30; evidence?.Add("foreground-window"); }
        if (w.LastActivePopup == w.Hwnd) { score += 12; evidence?.Add("last-active-popup"); }
        if (!string.IsNullOrEmpty(state) && state.Contains("modal") &&
            (w.ClassName.Contains("#32770") || w.Title.Contains("Please confirm") || w.Title.Contains("Load a new file")))
        { score += 140; evidence?.Add("modal-window"); }
        return score;
    }

    private static string ResolveToolKey(GuideTargetSpec t)
    {
        if (!string.IsNullOrEmpty(t.Adapter)) return t.Adapter;
        if (!string.IsNullOrEmpty(t.Process) && t.Process.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
            return t.Process.Substring(5);
        return null;
    }

    private static bool Contains(string haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) && !string.IsNullOrEmpty(needle) &&
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static GuideTargetSpec MergeTarget(GuideTargetSpec t, GuideLocatorSpec l) => new()
    {
        Scope = l.Scope ?? t?.Scope ?? "host",
        Key = l.Key ?? t?.Key,
        Process = l.Process ?? t?.Process,
        Adapter = l.Adapter ?? t?.Adapter,
        TitleContains = l.TitleContains != null && l.TitleContains.Count > 0 ? l.TitleContains : (t?.TitleContains ?? new List<string>()),
        ProcessNameContains = l.ProcessNameContains != null && l.ProcessNameContains.Count > 0 ? l.ProcessNameContains : (t?.ProcessNameContains ?? new List<string>()),
        ExeNameContains = l.ExeNameContains != null && l.ExeNameContains.Count > 0 ? l.ExeNameContains : (t?.ExeNameContains ?? new List<string>()),
        ExePathContains = l.ExePathContains != null && l.ExePathContains.Count > 0 ? l.ExePathContains : (t?.ExePathContains ?? new List<string>()),
        WindowClassNameContains = l.WindowClassNameContains != null && l.WindowClassNameContains.Count > 0 ? l.WindowClassNameContains : (t?.WindowClassNameContains ?? new List<string>()),
        TitlePidFromProcess = l.TitlePidFromProcess ?? t?.TitlePidFromProcess,
    };

    public GuidePlan Resolve(GuideSpec spec)
    {
        if (spec == null) return new GuidePlan { Evidence = "no guide" };
        var windows = Windows();
        GuidePlan best = null;
        foreach (var loc in spec.Locators ?? new List<GuideLocatorSpec>())
        {
            var plan = ResolveLocator(loc, spec.Target, spec.Overlay ?? new GuideOverlaySpec(), windows);
            if (plan.Layer == "none") continue;
            if (best == null || plan.Score > best.Score) best = plan;
        }
        if (best == null)
        {
            if (spec.Fallback != null && string.Equals(spec.Fallback.Scope, "host", StringComparison.OrdinalIgnoreCase))
                return ResolveHost(spec.Fallback.Key ?? "coach", spec.Overlay ?? new GuideOverlaySpec());
            return new GuidePlan { Evidence = "guide target not attached" };
        }
        return best;
    }

    public GuidePlan ResolveLocator(GuideLocatorSpec loc, GuideTargetSpec target, GuideOverlaySpec overlay, List<WindowInfo> windows)
    {
        var merged = MergeTarget(target, loc);
        var scope = NormalizeScope(merged.Scope ?? "host");
        var plan = new GuidePlan { Overlay = overlay };
        var evidence = new List<string>();

        if (scope == "none") { plan.Layer = "none"; plan.Score = 1; plan.Evidence = "scope:none"; return plan; }
        if (scope == "host") return ResolveHost(merged.Key ?? "coach", overlay);
        if (scope == "screen" && loc.Rect != null)
        {
            plan.Layer = "screen"; plan.Score = 50; plan.Evidence = "screen:rect";
            plan.BoundsScreen = new Rect(loc.Rect.X, loc.Rect.Y, loc.Rect.Width, loc.Rect.Height);
            return plan;
        }

        var scored = new List<(WindowInfo w, int score)>();
        foreach (var w in windows)
        {
            int s = ScoreWindow(w, merged, loc.State, evidence);
            if (s > 0) scored.Add((w, s));
        }
        scored.Sort((a, b) =>
        {
            int c = b.score.CompareTo(a.score);
            if (c != 0) return c;
            return (b.w.Width * b.w.Height).CompareTo(a.w.Width * a.w.Height);
        });
        if (scored.Count == 0)
        {
            plan.Layer = "none"; plan.Evidence = "process window not found"; plan.Score = 10;
            return plan;
        }
        var win = scored[0].w;
        int windowScore = scored[0].score;
        plan.Target = win;

        if (string.Equals(loc.Strategy, "window", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(loc.Strategy, "process_window", StringComparison.OrdinalIgnoreCase))
        {
            plan.Layer = "screen"; plan.Attachment = "window";
            plan.BoundsScreen = new Rect(win.X, win.Y, win.Width, win.Height);
            plan.Score = windowScore + 24; plan.Evidence = string.Join(",", evidence) + ",locator:window";
            return ApplyAdjust(plan, loc);
        }

        if (loc.Rect != null)
        {
            plan.Layer = "screen"; plan.Attachment = "region";
            plan.BoundsScreen = ResolveRect(loc.Rect, win);
            plan.Score = windowScore + 36; plan.Evidence = string.Join(",", evidence) + ",locator:rect";
            return ApplyAdjust(plan, loc);
        }

        bool hasContent = (loc.ContentContains != null && loc.ContentContains.Count > 0) ||
                          (loc.DocumentContains != null && loc.DocumentContains.Count > 0);
        int maxNodes = Math.Clamp(loc.MaxNodes ?? 2000, 50, 3000);
        int timeout = Math.Clamp(loc.TimeoutMs ?? 1200, 100, 1200);

        if (hasContent)
        {
            bool ok = _uia.ContainsAllText(win.Hwnd, loc.ContentContains, maxNodes, timeout, out int matched);
            if (!ok)
            {
                plan.Layer = "none";
                plan.Evidence = "content:not-found (" + matched + "/" + (loc.ContentContains?.Count ?? 0) + ")";
                plan.Score = windowScore;
                return plan;
            }
            evidence.Add("content:" + matched);

            if (loc.RejectContentContains != null && loc.RejectContentContains.Count > 0)
            {
                var snap = _uia.GetSnapshot(win.Hwnd, maxNodes, timeout);
                foreach (var bad in loc.RejectContentContains)
                {
                    if (snap.Nodes.Any(n => n.Text.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        plan.Layer = "none"; plan.Evidence = "reject-content:" + bad; plan.Score = windowScore;
                        return plan;
                    }
                }
            }

            var query = new LocatorQuery
            {
                NameEquals = loc.NameEquals,
                NameContains = loc.NameContains ?? new List<string>(),
                AutomationId = loc.AutomationId,
                ClassName = loc.ClassName,
                ControlType = loc.ControlType,
                RequireAll = loc.RequireAllNameContains,
                MaxNodes = maxNodes,
                TimeoutMs = timeout,
                MinScore = 14,
            };
            var hit = _uia.FindControl(win.Hwnd, query);
            if (hit.node != null && hit.node.Bounds.Width > 3 && hit.node.Bounds.Height > 3)
            {
                plan.Layer = "screen"; plan.Attachment = "control";
                plan.BoundsScreen = hit.node.Bounds;
                plan.Score = windowScore + 30 + hit.score;
                plan.Evidence = string.Join(",", evidence) + ",control:" + hit.evidence;
                return ApplyAdjust(plan, loc);
            }
            plan.Layer = "screen"; plan.Attachment = "verified-region";
            plan.BoundsScreen = new Rect(win.X, win.Y, win.Width, win.Height);
            plan.Score = windowScore + 36; plan.Evidence = string.Join(",", evidence) + ",content:distributed-evidence";
            return ApplyAdjust(plan, loc);
        }

        if ((loc.NameContains != null && loc.NameContains.Count > 0) ||
            !string.IsNullOrEmpty(loc.NameEquals) || !string.IsNullOrEmpty(loc.AutomationId))
        {
            var query = new LocatorQuery
            {
                NameEquals = loc.NameEquals,
                NameContains = loc.NameContains ?? new List<string>(),
                AutomationId = loc.AutomationId,
                ClassName = loc.ClassName,
                ControlType = loc.ControlType,
                RequireAll = loc.RequireAllNameContains,
                MaxNodes = maxNodes,
                TimeoutMs = timeout,
                MinScore = 14,
            };
            var hit = _uia.FindControl(win.Hwnd, query);
            if (hit.node != null && hit.node.Bounds.Width > 3 && hit.node.Bounds.Height > 3)
            {
                plan.Layer = "screen"; plan.Attachment = "control";
                plan.BoundsScreen = hit.node.Bounds;
                plan.Score = windowScore + 30 + hit.score;
                plan.Evidence = string.Join(",", evidence) + ",control:" + hit.evidence;
                return ApplyAdjust(plan, loc);
            }
            if (!loc.FallbackToWindow)
            {
                plan.Layer = "none"; plan.Evidence = "control:not-found"; plan.Score = windowScore;
                return plan;
            }
        }

        plan.Layer = "screen"; plan.Attachment = "window";
        plan.BoundsScreen = new Rect(win.X, win.Y, win.Width, win.Height);
        plan.Score = windowScore + 24; plan.Evidence = string.Join(",", evidence) + ",control:window-fallback";
        return ApplyAdjust(plan, loc);
    }

    private GuidePlan ResolveHost(string key, GuideOverlaySpec overlay)
    {
        var plan = new GuidePlan { Overlay = overlay, Attachment = "host", Score = 70, Evidence = "host:" + key };
        var el = _hotspot?.Invoke(key);
        if (el != null && el.IsVisible && el.ActualWidth >= 1 && el.ActualHeight >= 1)
        {
            try
            {
                var p1 = el.PointToScreen(new Point(0, 0));
                var p2 = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
                plan.Layer = "host";
                plan.BoundsScreen = new Rect(p1.X, p1.Y, Math.Max(1, p2.X - p1.X), Math.Max(1, p2.Y - p1.Y));
                return plan;
            }
            catch { }
        }
        plan.Layer = "none"; plan.Evidence = "host:not-visible";
        return plan;
    }

    private static Rect ResolveRect(GuideRectSpec rect, WindowInfo win)
    {
        var space = (rect.Space ?? "screen").ToLowerInvariant();
        if (space == "screen") return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        double bx = win.ClientWidth > 1 ? win.ClientX : win.X;
        double by = win.ClientHeight > 1 ? win.ClientY : win.Y;
        double bw = win.ClientWidth > 1 ? win.ClientWidth : win.Width;
        double bh = win.ClientHeight > 1 ? win.ClientHeight : win.Height;
        return new Rect(bx + rect.X * bw, by + rect.Y * bh, rect.Width * bw, rect.Height * bh);
    }

    private static GuidePlan ApplyAdjust(GuidePlan plan, GuideLocatorSpec loc)
    {
        if (loc.Adjust == null || plan.BoundsScreen.Width <= 0) return plan;
        var r = plan.BoundsScreen;
        double x = r.X + loc.Adjust.OffsetX, y = r.Y + loc.Adjust.OffsetY;
        double w = loc.Adjust.Width ?? r.Width, h = loc.Adjust.Height ?? r.Height;
        double inflate = loc.Adjust.Inflate;
        plan.BoundsScreen = new Rect(x - inflate, y - inflate, w + inflate * 2, h + inflate * 2);
        return plan;
    }

    private static string NormalizeScope(string scope)
    {
        switch ((scope ?? "host").ToLowerInvariant())
        {
            case "child":
            case "external":
            case "target": return "process";
            case "desktop": return "host";
            default: return (scope ?? "host").ToLowerInvariant();
        }
    }

    public bool Evaluate(GuideConditionSpec cond, List<WindowInfo> windows, out string evidence)
    {
        evidence = "";
        if (cond == null) { evidence = "condition:null"; return true; }
        bool core;
        switch ((cond.Kind ?? "always").ToLowerInvariant())
        {
            case "always": core = true; evidence = "always"; break;
            case "all":
                core = (cond.All ?? new List<GuideConditionSpec>()).All(c => Evaluate(c, windows, out _));
                evidence = "all(" + (cond.All?.Count ?? 0) + ")";
                break;
            case "any":
                core = (cond.Any ?? new List<GuideConditionSpec>()).Any(c => Evaluate(c, windows, out _));
                evidence = "any(" + (cond.Any?.Count ?? 0) + ")";
                break;
            case "process_running":
                core = windows.Any(w => MatchProcessKey(w, cond.Process));
                evidence = "process_running:" + cond.Process;
                break;
            case "window_visible":
                core = windows.Any(w => MatchProcessKey(w, cond.Process));
                evidence = "window_visible:" + cond.Process;
                break;
            case "host_visible":
                var el = _hotspot?.Invoke(cond.Key ?? "coach");
                core = el != null && el.IsVisible;
                evidence = "host_visible:" + cond.Key;
                break;
            case "locator_found":
                var p = cond.Locator == null ? null : ResolveLocator(cond.Locator, new GuideTargetSpec(), new GuideOverlaySpec(), windows);
                core = p != null && p.Layer != "none";
                evidence = p?.Evidence ?? "locator:null";
                break;
            case "value_equals": core = false; evidence = "value_equals"; break;
            case "value_contains": core = false; evidence = "value_contains"; break;
            default: core = false; evidence = "unknown-kind:" + cond.Kind; break;
        }
        if (cond.Not) { core = !core; evidence = "not(" + evidence + ")"; }
        return core;
    }

    public bool MatchProcessKey(WindowInfo w, string key)
    {
        if (string.IsNullOrEmpty(key)) return true;
        if (key.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
        {
            var spec = ToolCatalog.Get(key.Substring(5));
            return spec != null && (spec.ProcessNameHints.Any(h => Contains(w.ProcessName, h)) ||
                                    spec.WindowTitleHints.Any(h => Contains(w.Title, h)));
        }
        if (key.StartsWith("target:", StringComparison.OrdinalIgnoreCase))
        {
            var name = key.Substring(7);
            return Contains(w.Title, name) || Contains(System.IO.Path.GetFileName(w.ExePath), name);
        }
        return Contains(w.ProcessName, key) || Contains(w.Title, key);
    }
}
