using System;
using System.Collections.Generic;
using System.Linq;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ToolAdapter
{
	private static readonly Dictionary<string, ToolSpec> Specs = new Dictionary<string, ToolSpec>(StringComparer.OrdinalIgnoreCase)
	{
		["die"] = new ToolSpec
		{
			Id = "die",
			DisplayName = "DIE",
			Glyph = "DIE",
			WindowTitleHints = new string[3] { "Detect It Easy", "DIE", "die" },
			ProcessNameHints = new string[3] { "die", "diec", "diel" }
		},
		["ida"] = new ToolSpec
		{
			Id = "ida",
			DisplayName = "IDA",
			Glyph = "IDA",
			WindowTitleHints = new string[5] { "IDA", "Hex-Rays", "idapro", "Please confirm", "Load a new file" },
			ProcessNameHints = new string[3] { "ida", "ida64", "idat" }
		},
		["ghidra"] = new ToolSpec
		{
			Id = "ghidra",
			DisplayName = "Ghidra",
			Glyph = "GHD",
			WindowTitleHints = new string[4] { "Ghidra", "CodeBrowser", "Decompiler", "Listing" },
			ProcessNameHints = new string[3] { "ghidra", "java", "javaw" }
		},
		["dnspy"] = new ToolSpec
		{
			Id = "dnspy",
			DisplayName = "dnSpy",
			Glyph = "NET",
			WindowTitleHints = new string[1] { "dnSpy" },
			ProcessNameHints = new string[2] { "dnspy", "dnSpy" }
		},
		["x64dbg"] = new ToolSpec
		{
			Id = "x64dbg",
			DisplayName = "x64dbg",
			Glyph = "DBG",
			WindowTitleHints = new string[3] { "x64dbg", "x96dbg", "x32dbg" },
			ProcessNameHints = new string[3] { "x64dbg", "x96dbg", "x32dbg" }
		}
	};

	public IEnumerable<ToolSpec> All => Specs.Values;

	public ToolAdapter(PathService paths)
	{
	}

	public ToolSpec? Get(string toolId)
	{
		if (!Specs.TryGetValue(toolId, out ToolSpec value))
		{
			return null;
		}
		return value;
	}

	public string FormatLabel(string toolId)
	{
		return Get(toolId)?.DisplayName ?? toolId.ToUpperInvariant();
	}

	public string Glyph(string toolId)
	{
		return Get(toolId)?.Glyph ?? toolId.ToUpperInvariant();
	}

	public bool MatchesToolWindow(string toolId, string windowTitle, string processName)
	{
		ToolSpec toolSpec = Get(toolId);
		if (toolSpec == null)
		{
			return false;
		}
		if (toolSpec.ProcessNameHints.Any((string h) => processName.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
		{
			return true;
		}
		if (toolSpec.WindowTitleHints.Any((string h) => windowTitle.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
		{
			return true;
		}
		return false;
	}
}
