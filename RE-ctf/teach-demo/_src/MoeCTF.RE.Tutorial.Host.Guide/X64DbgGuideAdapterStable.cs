using System.Collections.Generic;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal sealed class X64DbgGuideAdapterStable : GuideAppAdapterBase
{
	protected override GuideAppSignature AppSignature { get; } = new GuideAppSignature
	{
		AdapterId = "x64dbg",
		ProcessNameHints = new string[3] { "x64dbg", "x96dbg", "x32dbg" },
		ExeNameHints = new string[3] { "x64dbg", "x96dbg", "x32dbg" },
		TitleHints = new string[3] { "x64dbg", "x96dbg", "x32dbg" },
		ClassNameHints = new string[1] { "Qt" }
	};

	protected override IReadOnlyDictionary<string, AppStateLocator> States { get; } = new Dictionary<string, AppStateLocator>
	{
		["system_break"] = new AppStateLocator
		{
			TitleHints = new string[1] { "ntdll.dll" },
			ContentHints = new string[3] { "已到达系统断点", "System Breakpoint", "system breakpoint" },
			MinScore = 24,
			StopAfterFirstMatch = true
		},
		["module_break"] = new AppStateLocator
		{
			TitleHints = new string[8] { "gdi32full.dll", "kernel32.dll", "KernelBase.dll", "user32.dll", "win32u.dll", "ucrtbase.dll", "combase.dll", ".dll" },
			ContentHints = new string[3] { "模块", "Module", ".dll" },
			RejectTitleHints = new string[1] { "ntdll.dll" },
			RejectContentHints = new string[3] { "已到达系统断点", "System Breakpoint", "system breakpoint" },
			MinScore = 20,
			StopAfterFirstMatch = true
		},
		["cpu"] = new AppStateLocator
		{
			TitleHints = new string[1] { "x64dbg" },
			ContentHints = new string[4] { "CPU", "反汇编", "寄存器", "内存窗口" },
			MinScore = 20,
			StopAfterFirstMatch = true
		},
		["memory_map"] = new AppStateLocator
		{
			ContentHints = new string[3] { "内存布局", "Memory Map", "Memory map" },
			MinScore = 20,
			StopAfterFirstMatch = true
		},
		["command"] = new AppStateLocator
		{
			ContentHints = new string[4] { "命令栏", "命令:", "Command", "Expression" },
			MinScore = 20,
			StopAfterFirstMatch = true
		},
		["breakpoint_hit"] = new AppStateLocator
		{
			ContentHints = new string[4] { "INT3 断点于", "INT3 breakpoint", "breakpoint at", "breakpoint hit" },
			RejectTitleHints = new string[9] { "ntdll.dll", "gdi32full.dll", "kernel32.dll", "KernelBase.dll", "user32.dll", "win32u.dll", "ucrtbase.dll", "combase.dll", ".dll" },
			RejectContentHints = new string[6] { "已到达系统断点", "System Breakpoint", "system breakpoint", "断点已设置", "Breakpoint set", "breakpoint set" },
			MinScore = 20,
			StopAfterFirstMatch = true
		},
		["dump"] = new AppStateLocator
		{
			ContentHints = new string[4] { "内存窗口", "内存 1", "Dump", "Memory" },
			MinScore = 20,
			StopAfterFirstMatch = true
		}
	};

	protected override IReadOnlyDictionary<string, AppRoleLocator> Roles { get; } = new Dictionary<string, AppRoleLocator>
	{
		["default"] = new AppRoleLocator
		{
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.05, 0.12, 0.9, 0.8)
		},
		["memory_map"] = new AppRoleLocator
		{
			Names = new string[3] { "内存布局", "Memory Map", "Memory map" },
			ControlType = "tabitem",
			FallbackFirst = true,
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.31, 0.109, 0.098, 0.037)
		},
		["command_bar"] = new AppRoleLocator
		{
			Names = new string[4] { "命令栏", "命令:", "Command", "Expression" },
			ContentHints = new string[2] { "命令栏", "命令:" },
			AutomationId = "MainWindow.cmdBar",
			ControlType = "toolbar",
			AllowIdentityOnlyMatch = true,
			FallbackFirst = true,
			FallbackScore = 42,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.006, 0.916, 0.987, 0.045)
		},
		["run_button"] = new AppRoleLocator
		{
			Names = new string[4] { "运行(R)", "运行", "Run", "Debug" },
			ControlType = "button",
			FallbackFirst = true,
			TimeoutMs = 800,
			FallbackScore = 40,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.12, 0.06, 0.035, 0.049)
		},
		["disassembly"] = new AppRoleLocator
		{
			Names = new string[2] { "反汇编", "Disassembly" },
			ClassName = "CPUDisassembly",
			ControlType = "table",
			AllowIdentityOnlyMatch = true,
			FallbackFirst = true,
			FallbackScore = 46,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.111, 0.149, 0.602, 0.467)
		},
		["dump_pane"] = new AppRoleLocator
		{
			Names = new string[4] { "内存 1", "内存窗口", "Dump", "Memory" },
			ContentHints = new string[2] { "内存窗口", "内存 1" },
			ClassName = "CPUDump",
			ControlType = "table",
			AllowIdentityOnlyMatch = true,
			FallbackFirst = true,
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.011, 0.725, 0.59, 0.185)
		},
		["registers"] = new AppRoleLocator
		{
			Names = new string[5] { "寄存器", "Registers", "RAX", "RCX", "RDX" },
			ContentHints = new string[4] { "寄存器", "RAX", "RCX", "RDX" },
			ClassName = "CPURegistersView",
			ControlType = "list",
			AllowIdentityOnlyMatch = true,
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.714, 0.149, 0.278, 0.336)
		}
	};
}
