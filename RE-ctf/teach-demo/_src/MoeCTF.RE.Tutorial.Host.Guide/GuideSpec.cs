using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideSpec
{
	[JsonPropertyName("target")]
	public GuideTargetSpec Target { get; set; } = new GuideTargetSpec();

	[JsonPropertyName("locators")]
	public List<GuideLocatorSpec> Locators { get; set; } = new List<GuideLocatorSpec>();

	[JsonPropertyName("overlay")]
	public GuideOverlaySpec Overlay { get; set; } = new GuideOverlaySpec();

	[JsonPropertyName("fallback")]
	public GuideTargetSpec? Fallback { get; set; }
}
