using System;
using System.Collections.Generic;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideAppSignature
{
	public required string AdapterId { get; init; }

	public IReadOnlyList<string> ProcessNameHints { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> ExeNameHints { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> TitleHints { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> ClassNameHints { get; init; } = Array.Empty<string>();

	public int Score(WindowInfo window, ICollection<string>? reasons = null)
	{
		int num = 0;
		num += ScoreHints("process", window.ProcessName, ProcessNameHints, 46, reasons);
		num += ScoreHints("exe", window.ExeName, ExeNameHints, 46, reasons);
		num += ScoreHints("title", window.Title, TitleHints, 38, reasons);
		if (num <= 0)
		{
			return 0;
		}
		int num2 = num;
		num2 += ScoreHints("class", window.ClassName, ClassNameHints, 18, reasons);
		if (num2 > 0)
		{
			num2 += 8;
			reasons?.Add("adapter:" + AdapterId);
		}
		return num2;
	}

	private static int ScoreHints(string kind, string value, IEnumerable<string> hints, int weight, ICollection<string>? reasons)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return 0;
		}
		int num = 0;
		foreach (string hint in hints)
		{
			if (!string.IsNullOrWhiteSpace(hint) && value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				num += weight + Math.Min(hint.Length, 18);
				reasons?.Add(kind + ":" + hint);
			}
		}
		return num;
	}
}
