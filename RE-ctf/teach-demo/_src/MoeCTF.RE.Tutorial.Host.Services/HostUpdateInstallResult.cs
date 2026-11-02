namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class HostUpdateInstallResult
{
	public bool Success { get; init; }

	public string Message { get; init; } = "";

	public string? DownloadedExecutablePath { get; init; }

	public string? RequestPath { get; init; }

	public static HostUpdateInstallResult Failed(string message)
	{
		return new HostUpdateInstallResult
		{
			Success = false,
			Message = message
		};
	}
}
