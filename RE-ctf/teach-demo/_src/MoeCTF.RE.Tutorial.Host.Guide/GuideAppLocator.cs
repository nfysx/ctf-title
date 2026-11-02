using System;
using System.Collections.Generic;
using System.Linq;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideAppLocator
{
	private readonly IReadOnlyList<IGuideAppAdapter> _adapters = new IGuideAppAdapter[6]
	{
		new DieGuideAdapter(),
		new IdaGuideAdapter(),
		new GhidraGuideAdapter(),
		new DnSpyGuideAdapter(),
		new X64DbgGuideAdapterStable(),
		new Win32TargetGuideAdapter()
	};

	public IReadOnlyList<GuideAppSignature> Signatures => _adapters.Select((IGuideAppAdapter a) => a.Signature).ToArray();

	public GuideAppSignature? GetSignature(string? adapterId)
	{
		string id = Normalize(adapterId);
		if (string.IsNullOrWhiteSpace(id))
		{
			return null;
		}
		return _adapters.FirstOrDefault((IGuideAppAdapter a) => string.Equals(a.Signature.AdapterId, id, StringComparison.OrdinalIgnoreCase))?.Signature;
	}

	public int ScoreWindow(WindowInfo window, string? adapterId, ICollection<string>? reasons = null)
	{
		string text = Normalize(adapterId);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return GetSignature(text)?.Score(window, reasons) ?? 0;
		}
		int num = 0;
		foreach (IGuideAppAdapter adapter in _adapters)
		{
			List<string> list = new List<string>();
			int num2 = adapter.Signature.Score(window, list);
			if (num2 <= num)
			{
				continue;
			}
			num = num2;
			if (reasons == null)
			{
				continue;
			}
			reasons.Clear();
			foreach (string item in list)
			{
				reasons.Add(item);
			}
		}
		return num;
	}

	public AppControlMatch? Locate(WindowInfo window, GuideLocatorSpec locator, ControlLocator controlLocator)
	{
		string text = Normalize(locator.Adapter);
		string value = Normalize(locator.Role);
		if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		foreach (IGuideAppAdapter adapter in _adapters)
		{
			if (adapter.CanHandle(text, window))
			{
				AppControlMatch appControlMatch = adapter.Locate(window, locator, controlLocator);
				if (appControlMatch != null)
				{
					return appControlMatch;
				}
			}
		}
		return null;
	}

	private static string Normalize(string? value)
	{
		return (value ?? string.Empty).Trim().ToLowerInvariant();
	}
}
