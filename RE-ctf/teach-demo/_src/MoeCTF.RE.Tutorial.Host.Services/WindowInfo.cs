using System.IO;
using System.Windows;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class WindowInfo
{
	public nint Hwnd { get; init; }

	public int Pid { get; init; }

	public int ThreadId { get; init; }

	public nint OwnerHwnd { get; init; }

	public nint RootOwnerHwnd { get; init; }

	public nint LastActivePopupHwnd { get; init; }

	public bool IsForeground { get; init; }

	public string Title { get; init; } = "";

	public string ProcessName { get; init; } = "";

	public string ClassName { get; init; } = "";

	public string ExePath { get; init; } = "";

	public Rect Bounds { get; init; }

	public Rect ClientBounds { get; init; }

	public string ExeName
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(ExePath))
			{
				return Path.GetFileNameWithoutExtension(ExePath);
			}
			return string.Empty;
		}
	}
}
