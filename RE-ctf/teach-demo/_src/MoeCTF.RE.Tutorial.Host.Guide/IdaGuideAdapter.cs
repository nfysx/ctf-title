using System.Collections.Generic;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal sealed class IdaGuideAdapter : GuideAppAdapterBase
{
	protected override GuideAppSignature AppSignature { get; } = new GuideAppSignature
	{
		AdapterId = "ida",
		ProcessNameHints = new string[3] { "ida", "ida64", "idat" },
		ExeNameHints = new string[3] { "ida", "ida64", "idat" },
		TitleHints = new string[4] { "IDA", "Hex-Rays", "Load a new file", "Please confirm" },
		ClassNameHints = new string[2] { "Qt", "QWidget" }
	};

	protected override IReadOnlyDictionary<string, AppStateLocator> States { get; } = new Dictionary<string, AppStateLocator>
	{
		["quick_start"] = new AppStateLocator
		{
			ContentHints = new string[3] { "Quick start", "Load a new file", "Recent files" },
			RejectTitleHints = new string[6] { "Open", "Select file", "打开", "DWARF info found", "Please confirm", "Load a new file" },
			RejectContentHints = new string[5] { "File name", "文件名", "All Files", "DWARF debug information", "Import file names/line numbers" },
			MinContentMatches = 2,
			MinScore = 48
		},
		["open_file_modal"] = new AppStateLocator
		{
			TitleHints = new string[3] { "Open", "Select file", "打开" },
			ContentHints = new string[7] { "File name", "文件名", "All Files", "All Files (*)", "打开(O)", "Cancel", "取消" },
			RejectTitleHints = new string[3] { "DWARF info found", "Please confirm", "Load a new file" },
			RejectContentHints = new string[5] { "Load DWARF debug information?", "Import file names/line numbers", "Global names", "Functions", "Function prototypes are definitive" },
			MinContentMatches = 2,
			MinScore = 48
		},
		["load_file"] = new AppStateLocator
		{
			TitleHints = new string[2] { "Load a new file", "Load file" },
			ContentHints = new string[5] { "Load a new file", "Load file", "Processor type", "Portable executable", "OK" },
			MinScore = 28
		},
		["database_exists_modal"] = new AppStateLocator
		{
			TitleHints = new string[1] { "Please confirm" },
			ContentHints = new string[4] { "Please confirm", "already exists", "Load existing", "Overwrite" },
			MinScore = 28
		},
		["database_repair_modal"] = new AppStateLocator
		{
			TitleHints = new string[1] { "Please confirm" },
			ContentHints = new string[4] { "Please confirm", "not closed", "Repair", "Reload file" },
			MinScore = 28
		},
		["problem_modal"] = new AppStateLocator
		{
			TitleHints = new string[1] { "IDA has encountered a problem" },
			ContentHints = new string[3] { "IDA has encountered a problem", "restart IDA", "mini dump" },
			MinScore = 28
		},
		["debug_info_modal"] = new AppStateLocator
		{
			TitleHints = new string[2] { "DWARF info found", "Load DWARF debug information?" },
			ContentHints = new string[4] { "Load DWARF debug information?", "Global names", "Functions", "Import file names/line numbers" },
			MinContentMatches = 2,
			MinScore = 52
		},
		["analysis_modal"] = new AppStateLocator
		{
			TitleHints = new string[2] { "Autoanalysis", "Analysis" },
			ContentHints = new string[4] { "Autoanalysis", "Please wait", "Kernel options", "Analysis" },
			MinScore = 28
		},
		["main"] = new AppStateLocator
		{
			ContentHints = new string[4] { "Navigator", "Functions", "IDA View-A", "AU:" },
			MinScore = 28
		},
		["strings"] = new AppStateLocator
		{
			TitleHints = new string[1] { "Strings" },
			ContentHints = new string[1] { "Strings" },
			MinScore = 24
		},
		["xrefs"] = new AppStateLocator
		{
			TitleHints = new string[2] { "xrefs", "references" },
			ContentHints = new string[2] { "xrefs", "references" },
			MinScore = 24
		},
		["disassembly"] = new AppStateLocator
		{
			TitleHints = new string[1] { "IDA View" },
			ContentHints = new string[3] { "IDAView host for IDA View", "IDA View-A", "Function name" },
			MinScore = 24
		},
		["pseudocode"] = new AppStateLocator
		{
			TitleHints = new string[1] { "Pseudocode" },
			ContentHints = new string[4] { "Pseudocode", "Decompiler", "F5", "Hex-Rays" },
			MinScore = 28
		}
	};

	protected override IReadOnlyDictionary<string, AppRoleLocator> Roles { get; } = new Dictionary<string, AppRoleLocator>
	{
		["default"] = new AppRoleLocator
		{
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.06, 0.12, 0.88, 0.78)
		},
		["quick_start_open"] = new AppRoleLocator
		{
			Names = new string[4] { "Open", "Go", "Load a new file", "Quick start" },
			FallbackFirst = true,
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.32, 0.28, 0.36, 0.34)
		},
		["quick_start_new"] = new AppRoleLocator
		{
			Names = new string[4] { "New", "Go", "Load a new file", "Quick start" },
			FallbackFirst = true,
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.32, 0.28, 0.36, 0.18)
		},
		["confirm_button"] = new AppRoleLocator
		{
			Names = new string[6] { "OK", "Yes", "确定", "是", "Continue", "Load" },
			ControlType = "button",
			FallbackFirst = true,
			FallbackScore = 32,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.62, 0.84, 0.22, 0.1)
		},
		["file_picker_dialog"] = new AppRoleLocator
		{
			Names = new string[4] { "File name", "文件名", "Open", "打开" },
			ContentHints = new string[6] { "File name", "Open", "Cancel", "文件名", "打开", "取消" },
			FallbackScore = 36,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.04, 0.12, 0.92, 0.78)
		},
		["decline_button"] = new AppRoleLocator
		{
			Names = new string[4] { "No", "否", "Cancel", "取消" },
			ControlType = "button",
			FallbackFirst = true,
			FallbackScore = 32,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.48, 0.84, 0.22, 0.1)
		},
		["ok_button"] = new AppRoleLocator
		{
			Names = new string[2] { "OK", "确定" },
			ControlType = "button",
			FallbackFirst = true,
			FallbackScore = 32,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.62, 0.78, 0.24, 0.14)
		},
		["load_existing_button"] = new AppRoleLocator
		{
			Names = new string[1] { "Load existing" },
			ControlType = "button",
			FallbackFirst = true,
			FallbackScore = 32,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.54, 0.76, 0.2, 0.12)
		},
		["reload_file_button"] = new AppRoleLocator
		{
			Names = new string[1] { "Reload file" },
			ControlType = "button",
			FallbackFirst = true,
			FallbackScore = 32,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.58, 0.76, 0.18, 0.12)
		},
		["loader_choice"] = new AppRoleLocator
		{
			Names = new string[3] { "Portable executable", "PE", "Binary file" },
			ControlType = "listitem",
			FallbackFirst = true,
			FallbackScore = 42,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.03, 0.14, 0.94, 0.12)
		},
		["processor_list"] = new AppRoleLocator
		{
			Names = new string[3] { "Processor type", "MetaPC", "metapc" },
			ClassName = "chooser_dirtree_widget_t",
			ControlType = "treeitem",
			FallbackFirst = true,
			FallbackScore = 42,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.03, 0.34, 0.94, 0.2)
		},
		["strings_view"] = new AppRoleLocator
		{
			Names = new string[1] { "Strings" },
			ContentHints = new string[3] { "Strings", "Address", "Length" },
			ControlType = "tabitem",
			FallbackFirst = true,
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.05, 0.12, 0.45, 0.74)
		},
		["functions_view"] = new AppRoleLocator
		{
			Names = new string[4] { "Functions", "Function name", "WinMain", "mainCRTStartup" },
			ContentHints = new string[2] { "Functions", "Function name" },
			FallbackFirst = true,
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.02, 0.14, 0.24, 0.76)
		},
		["disassembly"] = new AppRoleLocator
		{
			Names = new string[3] { "IDAView host for IDA View", "IDA View-A", "IDA View" },
			ContentHints = new string[2] { "IDAView host for IDA View", "IDA View-A" },
			ClassName = "IDAViewHost",
			ControlType = "custom",
			FallbackFirst = true,
			FallbackScore = 48,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.24, 0.16, 0.58, 0.7)
		},
		["pseudocode"] = new AppRoleLocator
		{
			Names = new string[3] { "Pseudocode", "Decompiler", "Hex-Rays" },
			ContentHints = new string[3] { "Pseudocode", "int", "return" },
			FallbackFirst = true,
			FallbackScore = 48,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.36, 0.14, 0.6, 0.76)
		},
		["xrefs_list"] = new AppRoleLocator
		{
			FallbackFirst = true,
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.06, 0.18, 0.88, 0.6)
		},
		["menu_view"] = new AppRoleLocator
		{
			Names = new string[1] { "View" },
			ControlType = "menuitem",
			FallbackFirst = true,
			FallbackScore = 36,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.14, 0.03, 0.12, 0.1)
		},
		["menu_search"] = new AppRoleLocator
		{
			Names = new string[4] { "Search", "Strings", "Text", "Sequence" },
			ControlType = "menuitem",
			FallbackFirst = true,
			FallbackScore = 36,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.18, 0.03, 0.34, 0.1)
		}
	};
}
