using System.Collections.Generic;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal sealed class GhidraGuideAdapter : GuideAppAdapterBase
{
	protected override GuideAppSignature AppSignature { get; } = new GuideAppSignature
	{
		AdapterId = "ghidra",
		ProcessNameHints = new string[3] { "ghidra", "java", "javaw" },
		ExeNameHints = new string[3] { "ghidra", "java", "javaw" },
		TitleHints = new string[4] { "Ghidra", "CodeBrowser", "Decompiler", "Listing" },
		ClassNameHints = new string[2] { "SunAwtFrame", "Ghidra" }
	};

	protected override IReadOnlyDictionary<string, AppRoleLocator> Roles { get; } = new Dictionary<string, AppRoleLocator>
	{
		["default"] = new AppRoleLocator
		{
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.06, 0.1, 0.88, 0.82)
		},
		["project_tree"] = new AppRoleLocator
		{
			Names = new string[3] { "Project", "Program Trees", "Symbol Tree" },
			ControlType = "treeitem",
			FallbackScore = 40,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.02, 0.12, 0.26, 0.78)
		},
		["listing"] = new AppRoleLocator
		{
			Names = new string[4] { "Listing", "Address", "Bytes", "Instruction" },
			FallbackScore = 48,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.28, 0.14, 0.42, 0.76)
		},
		["decompiler"] = new AppRoleLocator
		{
			Names = new string[3] { "Decompiler", "Pseudocode", "Function" },
			FallbackScore = 48,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.56, 0.14, 0.4, 0.76)
		},
		["symbol_tree"] = new AppRoleLocator
		{
			Names = new string[3] { "Symbol Tree", "Functions", "Labels" },
			ControlType = "treeitem",
			FallbackScore = 42,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.02, 0.12, 0.26, 0.78)
		}
	};
}
