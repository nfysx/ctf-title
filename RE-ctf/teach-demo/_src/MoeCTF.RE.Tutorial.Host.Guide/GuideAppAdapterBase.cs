using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal abstract class GuideAppAdapterBase : IGuideAppAdapter
{
	public GuideAppSignature Signature => AppSignature;

	protected abstract GuideAppSignature AppSignature { get; }

	protected abstract IReadOnlyDictionary<string, AppRoleLocator> Roles { get; }

	protected virtual IReadOnlyDictionary<string, AppStateLocator> States { get; } = new Dictionary<string, AppStateLocator>();

	public bool CanHandle(string adapterId, WindowInfo window)
	{
		if (!string.IsNullOrWhiteSpace(adapterId))
		{
			return string.Equals(adapterId, AppSignature.AdapterId, StringComparison.OrdinalIgnoreCase);
		}
		return AppSignature.Score(window) > 0;
	}

	public AppControlMatch? Locate(WindowInfo window, GuideLocatorSpec locator, ControlLocator controlLocator)
	{
		//IL_0484: Unknown result type (might be due to invalid IL or missing references)
		//IL_0450: Unknown result type (might be due to invalid IL or missing references)
		//IL_047b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0472: Unknown result type (might be due to invalid IL or missing references)
		if (MatchesAny(window.Title, locator.RejectTitleContains))
		{
			return null;
		}
		if (locator.RejectContentContains.Count > 0 && controlLocator.FindTextEvidence(window.Hwnd, locator.RejectContentContains, locator.MaxNodes ?? 3000, locator.TimeoutMs ?? 1200, stopAfterFirstMatch: true).MatchedHints.Count > 0)
		{
			return null;
		}
		string text = (locator.State ?? string.Empty).Trim().ToLowerInvariant();
		ControlTextEvidence controlTextEvidence = null;
		if (!string.IsNullOrWhiteSpace(text))
		{
			if (!States.TryGetValue(text, out AppStateLocator value2))
			{
				return null;
			}
			if (value2.FindRejectTitleEvidence(window).MatchedHints.Count > 0)
			{
				return null;
			}
			if (value2.RejectContentHints.Count > 0 && controlLocator.FindTextEvidence(window.Hwnd, value2.RejectContentHints, locator.MaxNodes ?? value2.MaxNodes, locator.TimeoutMs ?? value2.TimeoutMs, stopAfterFirstMatch: true).MatchedHints.Count > 0)
			{
				return null;
			}
			controlTextEvidence = value2.FindTitleEvidence(window);
			ControlTextEvidence controlTextEvidence2 = null;
			if (value2.ContentHints.Count > 0 && (controlTextEvidence.Score < value2.MinScore || value2.MinContentMatches > 0))
			{
				controlTextEvidence2 = controlLocator.FindTextEvidence(window.Hwnd, value2.ContentHints, locator.MaxNodes ?? value2.MaxNodes, locator.TimeoutMs ?? value2.TimeoutMs, value2.StopAfterFirstMatch);
			}
			if (value2.MinContentMatches > 0 && (controlTextEvidence2?.MatchedHints.Count ?? 0) < value2.MinContentMatches)
			{
				return null;
			}
			if (controlTextEvidence2 != null)
			{
				controlTextEvidence = MergeStateEvidence(controlTextEvidence, controlTextEvidence2);
			}
			if (controlTextEvidence.Score < value2.MinScore && (controlTextEvidence2?.MatchedHints.Count ?? 0) < Math.Max(1, value2.MinContentMatches))
			{
				return null;
			}
		}
		string text2 = (locator.Role ?? string.Empty).Trim().ToLowerInvariant();
		AppRoleLocator value4;
		if (string.IsNullOrWhiteSpace(text2))
		{
			value4 = (Roles.TryGetValue("default", out AppRoleLocator value3) ? value3 : AppRoleLocator.Window());
		}
		else if (!Roles.TryGetValue(text2, out value4))
		{
			return null;
		}
		string[] array = (from value in locator.DocumentContains.Concat(locator.ContentContains)
			where !string.IsNullOrWhiteSpace(value)
			select value).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		bool flag = array.Length != 0 && !GuideSpecHelpers.IsContentTarget(locator);
		IReadOnlyList<string> readOnlyList;
		if (!flag)
		{
			readOnlyList = value4.ContentHints;
		}
		else
		{
			IReadOnlyList<string> readOnlyList2 = array;
			readOnlyList = readOnlyList2;
		}
		IReadOnlyList<string> readOnlyList3 = readOnlyList;
		ControlTextEvidence controlTextEvidence3 = null;
		bool flag2 = locator.FallbackToWindow && value4.FallbackFirst && !string.IsNullOrWhiteSpace(text) && !flag;
		if (readOnlyList3.Count > 0 && !flag2)
		{
			controlTextEvidence3 = controlLocator.FindTextEvidence(window.Hwnd, readOnlyList3, locator.MaxNodes ?? value4.MaxNodes, locator.TimeoutMs ?? value4.TimeoutMs, !flag || (!locator.RequireAllNameContains && locator.DocumentContains.Count == 0));
			if (flag && controlTextEvidence3.MatchedHints.Count == 0)
			{
				return null;
			}
			if (flag && locator.RequireAllNameContains && controlTextEvidence3.MatchedHints.Count < readOnlyList3.Count)
			{
				return null;
			}
		}
		if (flag2)
		{
			return BuildFallbackMatch(window, locator, value4, text, text2, controlTextEvidence, controlTextEvidence3, "fast-region");
		}
		ControlLocatorOptions options = value4.ToOptions(locator);
		ControlMatch controlMatch = controlLocator.FindChildControlMatch(window.Hwnd, options);
		if (controlMatch != null)
		{
			int num = controlTextEvidence3?.Score ?? 0;
			return new AppControlMatch
			{
				AdapterId = AppSignature.AdapterId,
				State = text,
				Role = text2,
				Bounds = (Rect)(GuideSpecHelpers.IsControlRelativeRect(locator.Rect) ? controlMatch.Bounds : (((_003F?)ApplyAdjust(controlMatch.Bounds, locator.Adjust)) ?? controlMatch.Bounds)),
				Score = 44 + controlMatch.Score + num + (controlTextEvidence?.Score ?? 0),
				Control = controlMatch,
				Reason = $"{AppSignature.AdapterId}.{StatePrefix(text)}{text2}:{controlMatch.Provider}:{string.Join(",", controlMatch.Reasons)}{StateReason(controlTextEvidence)}{ContentReason(controlTextEvidence3)}"
			};
		}
		if (!locator.FallbackToWindow)
		{
			return null;
		}
		return BuildFallbackMatch(window, locator, value4, text, text2, controlTextEvidence, controlTextEvidence3, "fallback-region");
	}

	private AppControlMatch BuildFallbackMatch(WindowInfo window, GuideLocatorSpec locator, AppRoleLocator roleLocator, string state, string role, ControlTextEvidence? stateEvidence, ControlTextEvidence? contentEvidence, string reason)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		int num = roleLocator.FallbackScore + (contentEvidence?.Score ?? 0);
		Rect val = roleLocator.FallbackRegion(LocatedRect.WindowReferenceBounds(window));
		return new AppControlMatch
		{
			AdapterId = AppSignature.AdapterId,
			State = state,
			Role = role,
			Bounds = (GuideSpecHelpers.IsControlRelativeRect(locator.Rect) ? val : (ApplyAdjust(val, locator.Adjust) ?? val)),
			Score = num + (stateEvidence?.Score ?? 0),
			UsedFallbackRegion = true,
			Reason = $"{AppSignature.AdapterId}.{StatePrefix(state)}{role}:{reason}{StateReason(stateEvidence)}{ContentReason(contentEvidence)}"
		};
	}

	protected static Rect Region(Rect window, double x, double y, double width, double height)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		return new Rect(((Rect)(ref window)).Left + ((Rect)(ref window)).Width * x, ((Rect)(ref window)).Top + ((Rect)(ref window)).Height * y, Math.Max(18.0, ((Rect)(ref window)).Width * width), Math.Max(18.0, ((Rect)(ref window)).Height * height));
	}

	private static Rect? ApplyAdjust(Rect? rect, GuideRectAdjustSpec? adjust)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		if (rect.HasValue)
		{
			Rect valueOrDefault = rect.GetValueOrDefault();
			if (adjust != null)
			{
				double num = adjust.Width ?? ((Rect)(ref valueOrDefault)).Width;
				double num2 = adjust.Height ?? ((Rect)(ref valueOrDefault)).Height;
				Rect value = default(Rect);
				((Rect)(ref value))._002Ector(((Rect)(ref valueOrDefault)).Left + adjust.OffsetX, ((Rect)(ref valueOrDefault)).Top + adjust.OffsetY, num, num2);
				if (Math.Abs(adjust.Inflate) > 0.001)
				{
					((Rect)(ref value)).Inflate(adjust.Inflate, adjust.Inflate);
				}
				if (!(((Rect)(ref value)).Width > 1.0) || !(((Rect)(ref value)).Height > 1.0))
				{
					return null;
				}
				return value;
			}
		}
		return rect;
	}

	private static string StatePrefix(string state)
	{
		if (!string.IsNullOrWhiteSpace(state))
		{
			return state + ".";
		}
		return "";
	}

	private static string ContentReason(ControlTextEvidence? evidence)
	{
		if (evidence != null && evidence.MatchedHints.Count != 0)
		{
			return ":content:" + string.Join(",", evidence.MatchedHints);
		}
		return string.Empty;
	}

	private static string StateReason(ControlTextEvidence? evidence)
	{
		if (evidence != null && evidence.MatchedHints.Count != 0)
		{
			return ":state:" + string.Join(",", evidence.MatchedHints);
		}
		return string.Empty;
	}

	private static ControlTextEvidence MergeStateEvidence(ControlTextEvidence titleEvidence, ControlTextEvidence contentEvidence)
	{
		if (titleEvidence.MatchedHints.Count == 0)
		{
			return contentEvidence;
		}
		if (contentEvidence.MatchedHints.Count == 0)
		{
			return titleEvidence;
		}
		return new ControlTextEvidence
		{
			Score = titleEvidence.Score + contentEvidence.Score,
			VisitedNodes = Math.Max(titleEvidence.VisitedNodes, contentEvidence.VisitedNodes),
			TimedOut = (titleEvidence.TimedOut || contentEvidence.TimedOut),
			Provider = contentEvidence.Provider,
			MatchedHints = titleEvidence.MatchedHints.Concat(contentEvidence.MatchedHints).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray(),
			SampleNames = titleEvidence.SampleNames.Concat(contentEvidence.SampleNames).Distinct<string>(StringComparer.OrdinalIgnoreCase).Take(12)
				.ToArray()
		};
	}

	private static bool MatchesAny(string value, IEnumerable<string> hints)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return hints.Any((string h) => !string.IsNullOrWhiteSpace(h) && value.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
		}
		return false;
	}

	private static bool IsFastRegionStrategy(string? strategy)
	{
		switch ((strategy ?? string.Empty).Trim().ToLowerInvariant())
		{
		case "fast":
		case "fixed":
		case "region":
		case "rect":
			return true;
		default:
			return false;
		}
	}
}
