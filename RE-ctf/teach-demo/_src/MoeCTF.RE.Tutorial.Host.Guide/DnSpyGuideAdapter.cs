using System.Collections.Generic;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal sealed class DnSpyGuideAdapter : GuideAppAdapterBase
{
	protected override GuideAppSignature AppSignature { get; } = new GuideAppSignature
	{
		AdapterId = "dnspy",
		ProcessNameHints = new string[1] { "dnspy" },
		ExeNameHints = new string[1] { "dnspy" },
		TitleHints = new string[1] { "dnSpy" },
		ClassNameHints = new string[1] { "HwndWrapper" }
	};

	protected override IReadOnlyDictionary<string, AppRoleLocator> Roles { get; } = new Dictionary<string, AppRoleLocator>
	{
		["default"] = new AppRoleLocator
		{
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.06, 0.12, 0.88, 0.78)
		},
		["assembly_tree"] = new AppRoleLocator
		{
			Names = new string[5] { "程序集资源管理器", "Assembly Explorer", "3c68a27e81e2", "MainForm", "Unlock" },
			ControlType = "text",
			FallbackScore = 46,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.02, 0.12, 0.34, 0.78)
		},
		["code_view"] = new AppRoleLocator
		{
			Names = new string[7] { "MainForm", "Unlock", "flag{th3_3x3_1s_n0t_c_3x3}", "public", "private", "class", "namespace" },
			ControlType = "text",
			FallbackScore = 46,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.34, 0.12, 0.62, 0.78)
		}
	};
}
