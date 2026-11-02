namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed record TrackedWindowSnapshot(string Id, int? Pid, nint? Hwnd, string Title, int Width, int Height, bool Waiting);
