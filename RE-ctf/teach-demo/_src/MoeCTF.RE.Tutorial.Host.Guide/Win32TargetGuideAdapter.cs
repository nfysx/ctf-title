using System.Collections.Generic;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal sealed class Win32TargetGuideAdapter : GuideAppAdapterBase
{
	protected override GuideAppSignature AppSignature { get; } = new GuideAppSignature
	{
		AdapterId = "win32"
	};

	protected override IReadOnlyDictionary<string, AppRoleLocator> Roles { get; } = new Dictionary<string, AppRoleLocator>
	{
		["default"] = new AppRoleLocator
		{
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.08, 0.18, 0.84, 0.68)
		},
		["button_generate"] = new AppRoleLocator
		{
			Names = new string[1] { "Generate" },
			ControlType = "button",
			FallbackScore = 38,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.05, 0.16, 0.3, 0.28)
		},
		["input"] = new AppRoleLocator
		{
			ControlType = "edit",
			FallbackScore = 36,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.05, 0.18, 0.7, 0.22)
		},
		["status"] = new AppRoleLocator
		{
			ControlType = "text",
			FallbackScore = 34,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.05, 0.52, 0.85, 0.24)
		}
	};
}
