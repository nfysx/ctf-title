using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public interface IGuideAppAdapter
{
	GuideAppSignature Signature { get; }

	bool CanHandle(string adapterId, WindowInfo window);

	AppControlMatch? Locate(WindowInfo window, GuideLocatorSpec locator, ControlLocator controlLocator);
}
