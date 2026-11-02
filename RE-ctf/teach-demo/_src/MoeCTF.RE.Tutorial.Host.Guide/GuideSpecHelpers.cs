using System;
using System.Collections.Generic;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host.Guide;

internal static class GuideSpecHelpers
{
	public static GuideLocatorSpec TargetToLocator(GuideTargetSpec target)
	{
		return new GuideLocatorSpec
		{
			Scope = target.Scope,
			Key = target.Key,
			Process = target.Process,
			Adapter = target.Adapter,
			TitleContains = target.TitleContains,
			ProcessNameContains = target.ProcessNameContains,
			ExeNameContains = target.ExeNameContains,
			ExePathContains = target.ExePathContains,
			WindowClassNameContains = target.WindowClassNameContains,
			TitlePidFromProcess = target.TitlePidFromProcess,
			Strategy = ((target.Scope == "process") ? "window" : "auto")
		};
	}

	public static GuideTargetSpec MergeTarget(GuideLocatorSpec locator, GuideTargetSpec target, string scope)
	{
		return new GuideTargetSpec
		{
			Scope = scope,
			Key = FirstNonEmpty(locator.Key, target.Key),
			Process = FirstNonEmpty(locator.Process, target.Process),
			Adapter = FirstNonEmpty(locator.Adapter, target.Adapter),
			TitleContains = ((locator.TitleContains.Count > 0) ? locator.TitleContains : target.TitleContains),
			ProcessNameContains = ((locator.ProcessNameContains.Count > 0) ? locator.ProcessNameContains : target.ProcessNameContains),
			ExeNameContains = ((locator.ExeNameContains.Count > 0) ? locator.ExeNameContains : target.ExeNameContains),
			ExePathContains = ((locator.ExePathContains.Count > 0) ? locator.ExePathContains : target.ExePathContains),
			WindowClassNameContains = ((locator.WindowClassNameContains.Count > 0) ? locator.WindowClassNameContains : target.WindowClassNameContains),
			TitlePidFromProcess = FirstNonEmpty(locator.TitlePidFromProcess, target.TitlePidFromProcess)
		};
	}

	public static ControlLocatorOptions ToControlOptions(GuideLocatorSpec locator)
	{
		ControlLocatorOptions controlLocatorOptions = new ControlLocatorOptions();
		IReadOnlyList<string> nameContains;
		if (locator.NameContains.Count <= 0)
		{
			if (!IsContentTarget(locator))
			{
				IReadOnlyList<string> contentContains = locator.ContentContains;
				nameContains = contentContains;
			}
			else
			{
				IReadOnlyList<string> contentContains = Array.Empty<string>();
				nameContains = contentContains;
			}
		}
		else
		{
			IReadOnlyList<string> contentContains = locator.NameContains;
			nameContains = contentContains;
		}
		controlLocatorOptions.NameContains = nameContains;
		controlLocatorOptions.RequireAllNameContains = locator.RequireAllNameContains;
		controlLocatorOptions.NameEquals = locator.NameEquals;
		controlLocatorOptions.AutomationId = locator.AutomationId;
		controlLocatorOptions.ClassName = locator.ClassName;
		controlLocatorOptions.ControlType = locator.ControlType;
		controlLocatorOptions.Backend = ControlBackend(locator.Strategy);
		controlLocatorOptions.MaxNodes = locator.MaxNodes ?? 2000;
		controlLocatorOptions.TimeoutMs = locator.TimeoutMs ?? 1200;
		controlLocatorOptions.MinScore = 14;
		return controlLocatorOptions;
	}

	public static Rect? ResolveExplicitRect(GuideLocatorSpec locator, Rect? baseRect)
	{
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		if (locator.Rect == null)
		{
			return null;
		}
		GuideRectSpec rect = locator.Rect;
		bool flag;
		switch ((rect.Space ?? "screen").Trim().ToLowerInvariant())
		{
		case "target":
		case "window":
		case "anchor":
		case "client":
		case "outer-window":
		case "window-frame":
		case "host":
		case "control":
		case "dwm-frame":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		_003F value;
		if (flag)
		{
			if (baseRect.HasValue)
			{
				Rect valueOrDefault = baseRect.GetValueOrDefault();
				double x = rect.X;
				if (x >= 0.0 && x <= 1.0)
				{
					x = rect.Y;
					if (x >= 0.0 && x <= 1.0)
					{
						x = rect.Width;
						if (x > 0.0 && x <= 1.0)
						{
							x = rect.Height;
							if (x > 0.0 && x <= 1.0)
							{
								value = new NormalizedRect(rect.X, rect.Y, rect.Width, rect.Height).Clamp().ToScreen(valueOrDefault);
								goto IL_0260;
							}
						}
					}
				}
				value = new Rect(((Rect)(ref valueOrDefault)).Left + rect.X, ((Rect)(ref valueOrDefault)).Top + rect.Y, rect.Width, rect.Height);
				goto IL_0260;
			}
			return null;
		}
		return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
		IL_0260:
		return (Rect)value;
	}

	public static bool IsControlRelativeRect(GuideRectSpec? rect)
	{
		string text = (rect?.Space ?? string.Empty).Trim().ToLowerInvariant();
		if (text == "control" || text == "anchor")
		{
			return true;
		}
		return false;
	}

	public static bool IsContentTarget(GuideLocatorSpec locator)
	{
		return string.Equals(locator.ContentMode, "target", StringComparison.OrdinalIgnoreCase);
	}

	public static bool HasControlAnchor(GuideLocatorSpec locator)
	{
		if (locator.NameContains.Count <= 0 && string.IsNullOrWhiteSpace(locator.NameEquals) && string.IsNullOrWhiteSpace(locator.AutomationId) && string.IsNullOrWhiteSpace(locator.ClassName))
		{
			return !string.IsNullOrWhiteSpace(locator.ControlType);
		}
		return true;
	}

	public static bool HasRecognitionAnchor(GuideLocatorSpec locator)
	{
		if (!HasControlAnchor(locator) && locator.ContentContains.Count <= 0 && locator.DocumentContains.Count <= 0 && string.IsNullOrWhiteSpace(locator.State))
		{
			return !string.IsNullOrWhiteSpace(locator.Role);
		}
		return true;
	}

	public static Rect? ResolveControlRelativeRect(GuideLocatorSpec locator, Rect anchorBounds, ICollection<string>? reasons = null)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (!IsControlRelativeRect(locator.Rect))
		{
			return null;
		}
		Rect? result = ResolveExplicitRect(locator, anchorBounds);
		if (result.HasValue)
		{
			reasons?.Add("locator:control-relative-rect");
		}
		return result;
	}

	public static Rect? Adjust(Rect? rect, GuideRectAdjustSpec? adjust)
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

	public static bool IsWindowStrategy(GuideLocatorSpec locator)
	{
		string text = (locator.Strategy ?? "auto").Trim().ToLowerInvariant();
		if (text == "window" || text == "process_window")
		{
			return true;
		}
		return false;
	}

	public static string NormalizeScope(string? scope)
	{
		string text = (scope ?? "host").Trim().ToLowerInvariant();
		return text switch
		{
			"child" => "process", 
			"external" => "process", 
			"target" => "process", 
			"desktop" => "host", 
			_ => text, 
		};
	}

	public static string ControlBackend(string? strategy)
	{
		if (!((strategy ?? string.Empty).Trim().ToLowerInvariant() == "win32"))
		{
			return "auto";
		}
		return "win32";
	}

	public static string? FirstNonEmpty(string? first, string? second)
	{
		if (string.IsNullOrWhiteSpace(first))
		{
			if (string.IsNullOrWhiteSpace(second))
			{
				return null;
			}
			return second;
		}
		return first;
	}
}
