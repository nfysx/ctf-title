namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ContentUpdateResult
{
	public bool Success { get; init; }

	public bool PendingActivation { get; init; }

	public bool RequiresHostUpdate { get; init; }

	public string? RequiredHostVersion { get; init; }

	public string? HostUpdateManifestUrl { get; init; }

	public string Message { get; init; } = "";

	public ContentUpdateManifest? Manifest { get; init; }
}
