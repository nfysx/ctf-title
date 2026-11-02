namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class HostUpdateResult
{
	public bool Success { get; init; }

	public bool UpdateAvailable { get; init; }

	public string Message { get; init; } = "";

	public string? DownloadUrl { get; init; }

	public HostUpdateManifest? Manifest { get; init; }
}
