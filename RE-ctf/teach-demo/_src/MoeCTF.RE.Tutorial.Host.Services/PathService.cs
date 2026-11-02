using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class PathService
{
	private string? _contentRoot;

	private string? _assetsDir;

	private string? _i18nDir;

	private string? _stagesDir;

	private readonly string? _workspaceRoot;

	public string RepoRoot { get; }

	public string ContentRoot => _contentRoot ?? RepoRoot;

	public string AssetsDir => _assetsDir ?? Path.Combine(ContentRoot, "assets");

	public string I18nDir => _i18nDir ?? Path.Combine(AssetsDir, "i18n");

	public string ToolsDir => ToolSearchRoots().FirstOrDefault(Directory.Exists) ?? Path.Combine(RepoRoot, "assets", "tools");

	public string TargetsDir => Path.Combine(AssetsDir, "targets");

	public string StagesDir => _stagesDir ?? Path.Combine(ContentRoot, "stages");

	public string AppDataDir => Path.Combine(RepoRoot, "config");

	public string ProgressPath => Path.Combine(AppDataDir, "progress.json");

	public string RequirementStatePath => Path.Combine(AppDataDir, "requirements.json");

	public PathService()
	{
		string fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "MoeCTF.RE.Tutorial"));
		if (BundledContentInstaller.Install(fullPath) || LooksLikeBuiltInContentRoot(fullPath))
		{
			RepoRoot = fullPath;
			_workspaceRoot = FindWorkspaceRoot(AppContext.BaseDirectory);
			return;
		}
		DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory);
		string text = null;
		int num = 0;
		while (num < 8 && directoryInfo != null)
		{
			if (LooksLikeBuiltInContentRoot(directoryInfo.FullName))
			{
				text = directoryInfo.FullName;
				break;
			}
			num++;
			directoryInfo = directoryInfo.Parent;
		}
		RepoRoot = text ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
		_workspaceRoot = FindWorkspaceRoot(AppContext.BaseDirectory);
	}

	public PathService(string repoRoot)
	{
		if (string.IsNullOrWhiteSpace(repoRoot))
		{
			throw new ArgumentException("repo root is required", "repoRoot");
		}
		RepoRoot = Path.GetFullPath(repoRoot);
		_workspaceRoot = FindWorkspaceRoot(RepoRoot);
	}

	private static bool LooksLikeBuiltInContentRoot(string root)
	{
		if (File.Exists(Path.Combine(root, "stages", "index.json")))
		{
			return File.Exists(Path.Combine(root, "assets", "i18n", "zh-CN.json"));
		}
		return false;
	}

	public void UseContentPaths(string contentRoot, string stagesDir, string i18nDir, string assetsDir)
	{
		_contentRoot = Path.GetFullPath(contentRoot);
		_stagesDir = Path.GetFullPath(stagesDir);
		_i18nDir = Path.GetFullPath(i18nDir);
		_assetsDir = Path.GetFullPath(assetsDir);
	}

	public void UseBuiltInContent()
	{
		_contentRoot = null;
		_stagesDir = null;
		_i18nDir = null;
		_assetsDir = null;
	}

	public string? FindTarget(string fileName)
	{
		foreach (string item in TargetSearchDirs())
		{
			string text = Path.Combine(item, fileName);
			if (File.Exists(text))
			{
				return text;
			}
		}
		return null;
	}

	public string? FindAnalysisTarget(string fileName, string toolId)
	{
		string text = FindTarget(fileName);
		if (text == null)
		{
			return null;
		}
		if (!toolId.Equals("dnspy", StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		string text2 = Path.ChangeExtension(text, ".dll");
		if (!File.Exists(text2))
		{
			return text;
		}
		return text2;
	}

	public string? FindToolExecutable(string toolId)
	{
		return FindBundledToolExecutable(toolId);
	}

	public string? FindBundledToolExecutable(string toolId)
	{
		string[] array = (from root in ToolSearchRoots()
			select Path.Combine(root, toolId)).Where(Directory.Exists).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		if (array.Length == 0)
		{
			return null;
		}
		string[] array2 = toolId.ToLowerInvariant() switch
		{
			"die" => new string[2] { "die.exe", "DIE.exe" }, 
			"ida" => new string[3] { "ida.exe", "ida64.exe", "idat.exe" }, 
			"ghidra" => new string[2] { "ghidraRun.bat", "support\\launch.bat" }, 
			"dnspy" => new string[2] { "dnSpy.exe", "dnspy.exe" }, 
			"x64dbg" => new string[4] { "release\\x64\\x64dbg.exe", "x64\\x64dbg.exe", "x64dbg.exe", "x96dbg.exe" }, 
			_ => Array.Empty<string>(), 
		};
		string[] array3 = array;
		foreach (string path in array3)
		{
			string[] array4 = array2;
			foreach (string path2 in array4)
			{
				string text = Path.Combine(path, path2);
				if (File.Exists(text))
				{
					return text;
				}
			}
		}
		return array.SelectMany((string dir) => Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)).FirstOrDefault();
	}

	private IEnumerable<string> TargetSearchDirs()
	{
		return new string[5]
		{
			TargetsDir,
			Path.Combine(RepoRoot, "assets", "targets"),
			Path.Combine(RepoRoot, "targets"),
			(_workspaceRoot == null) ? "" : Path.Combine(_workspaceRoot, "assets", "targets"),
			(_workspaceRoot == null) ? "" : Path.Combine(_workspaceRoot, "targets")
		}.Where((string s) => !string.IsNullOrWhiteSpace(s)).Distinct<string>(StringComparer.OrdinalIgnoreCase);
	}

	private IEnumerable<string> ToolSearchRoots()
	{
		return new string[2]
		{
			Path.Combine(RepoRoot, "assets", "tools"),
			(_workspaceRoot == null) ? "" : Path.Combine(_workspaceRoot, "assets", "tools")
		}.Where((string s) => !string.IsNullOrWhiteSpace(s)).Distinct<string>(StringComparer.OrdinalIgnoreCase);
	}

	private static string? FindWorkspaceRoot(string start)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(start);
		int num = 0;
		while (num < 10 && directoryInfo != null)
		{
			if (File.Exists(Path.Combine(directoryInfo.FullName, "MoeCTF.RE.Tutorial.slnx")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "assets", "tools")))
			{
				return directoryInfo.FullName;
			}
			num++;
			directoryInfo = directoryInfo.Parent;
		}
		return null;
	}
}
