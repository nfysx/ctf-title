using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed record GuideOverlayScanResult(IReadOnlyList<TrackedWindowSnapshot> Windows, GuideOverlayMatch? Overlay, long Revision);
