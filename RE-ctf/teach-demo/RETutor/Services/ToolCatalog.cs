using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RETutor.Services;

public sealed class ToolSpec
{
    public string Id;
    public string DisplayName;
    public string Glyph;
    public string[] WindowTitleHints = Array.Empty<string>();
    public string[] ProcessNameHints = Array.Empty<string>();
    public string[] ExecutableNames = Array.Empty<string>();
}

/// <summary>
/// 对应原程序 Services\ToolAdapter.cs（ToolSpec 里没有路径也没有参数，只有身份线索与白名单文件名）。
/// </summary>
public sealed class ToolCatalog
{
    public static readonly ToolSpec[] Specs =
    {
        new() { Id="die",   DisplayName="Detect It Easy", Glyph="DIE",
                WindowTitleHints=new[]{"Detect It Easy","DIE","die"},
                ProcessNameHints=new[]{"die","diec","diel"},
                ExecutableNames=new[]{"die.exe","DIE.exe"} },
        new() { Id="ida",   DisplayName="IDA", Glyph="IDA",
                WindowTitleHints=new[]{"IDA","Hex-Rays","idapro","Please confirm","Load a new file"},
                ProcessNameHints=new[]{"ida","ida64","idat"},
                ExecutableNames=new[]{"ida.exe","ida64.exe","idat.exe","idafree.exe"} },
        new() { Id="ghidra",DisplayName="Ghidra", Glyph="GHD",
                WindowTitleHints=new[]{"Ghidra","CodeBrowser","Decompiler","Listing"},
                ProcessNameHints=new[]{"ghidra","java","javaw"},
                ExecutableNames=new[]{"ghidraRun.bat",@"support\launch.bat"} },
        new() { Id="dnspy", DisplayName="dnSpy", Glyph="NET",
                WindowTitleHints=new[]{"dnSpy"},
                ProcessNameHints=new[]{"dnspy","dnSpy"},
                ExecutableNames=new[]{"dnSpy.exe","dnspy.exe"} },
        new() { Id="x64dbg",DisplayName="x64dbg", Glyph="DBG",
                WindowTitleHints=new[]{"x64dbg","x96dbg","x32dbg"},
                ProcessNameHints=new[]{"x64dbg","x96dbg","x32dbg"},
                ExecutableNames=new[]{@"release\x64\x64dbg.exe",@"x64\x64dbg.exe","x64dbg.exe","x96dbg.exe"} },
    };

    public static ToolSpec Get(string id) =>
        Specs.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    public static ToolSpec ByAdapter(string adapter) => Get(adapter);

    /// <summary>搜索 assets/tools/&lt;id&gt;/ 下的白名单可执行文件（原程序 PathService.FindBundledToolExecutable）。</summary>
    public static string FindBundledExecutable(string contentRoot, string toolId)
    {
        var spec = Get(toolId);
        if (spec == null) return null;
        var dir = Path.Combine(contentRoot, "assets", "tools", toolId);
        if (!Directory.Exists(dir)) return null;
        foreach (var name in spec.ExecutableNames)
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }
        return Directory.EnumerateFiles(dir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
    }
}
