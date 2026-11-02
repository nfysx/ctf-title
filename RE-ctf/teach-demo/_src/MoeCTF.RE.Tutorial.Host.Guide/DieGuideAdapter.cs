using System.Collections.Generic;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal sealed class DieGuideAdapter : GuideAppAdapterBase
{
	protected override GuideAppSignature AppSignature { get; } = new GuideAppSignature
	{
		AdapterId = "die",
		ProcessNameHints = new string[3] { "die", "diec", "diel" },
		ExeNameHints = new string[3] { "die", "diec", "diel" },
		TitleHints = new string[2] { "Detect It Easy", "DIE" },
		ClassNameHints = new string[1] { "Qt" }
	};

	protected override IReadOnlyDictionary<string, AppRoleLocator> Roles { get; } = new Dictionary<string, AppRoleLocator>
	{
		["default"] = new AppRoleLocator
		{
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.08, 0.12, 0.84, 0.76)
		},
		["analysis_summary"] = new AppRoleLocator
		{
			Names = new string[5] { "ELF64", "AMD64", "EXEC", "C/C++", "Binary" },
			FallbackScore = 46,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.12, 0.18, 0.76, 0.58)
		},
		["file_type"] = new AppRoleLocator
		{
			Names = new string[6] { "文件类型", "File type", "File Type", "PE64", "PE32", "Binary" },
			AutomationId = "GuiMainWindow.centralwidget.widgetFormats.groupBoxFileType.comboBoxFileType",
			ControlType = "combobox",
			FallbackScore = 44,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.12, 0.2, 0.72, 0.22)
		},
		["packer_indicator"] = new AppRoleLocator
		{
			Names = new string[8] { "链接程序", "附加", "打包工具", "编译器", "Packer", "Compiler", "GNU linker", "Binary" },
			ControlType = "treeitem",
			FallbackScore = 42,
			FallbackRegion = (Rect w) => GuideAppAdapterBase.Region(w, 0.12, 0.42, 0.72, 0.25)
		}
	};
}
