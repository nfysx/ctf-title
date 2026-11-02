using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class TargetProcessObserver
{
	public void ObserveCurrentTools(ProcessManager processes, IEnumerable<string> toolIds, Func<string, ToolSpec?> getTool, Func<string, string?> resolveToolExecutable)
	{
		foreach (string item in toolIds.Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			ToolSpec toolSpec = getTool(item);
			if (toolSpec != null)
			{
				ObserveTool(processes, toolSpec, resolveToolExecutable(item));
			}
		}
	}

	public ManagedProcess? ObserveTool(ProcessManager processes, ToolSpec spec, string? expectedExe)
	{
		List<Process> list = new List<Process>();
		Process[] processes2 = Process.GetProcesses();
		foreach (Process process in processes2)
		{
			if (ProcessMatchesTool(process, spec, expectedExe))
			{
				list.Add(process);
			}
			else
			{
				process.Dispose();
			}
		}
		List<Process> list2 = (from p in list
			group p by p.Id into g
			select g.First()).ToList();
		Process process2 = SelectBestToolCandidate(list2, spec, expectedExe);
		foreach (Process item in list2)
		{
			if (item != process2)
			{
				item.Dispose();
			}
		}
		if (process2 == null)
		{
			return null;
		}
		ManagedProcess? managedProcess = processes.ObserveExternal("tool:" + spec.Id, process2, expectedExe, spec.Id, spec.WindowTitleHints, spec.ProcessNameHints);
		if (managedProcess?.Process != process2)
		{
			process2.Dispose();
		}
		return managedProcess;
	}

	public void ObserveCurrentTargets(ProcessManager processes, IEnumerable<string> targetFiles, Func<string, string?> resolveTarget)
	{
		foreach (string targetFile in targetFiles)
		{
			string text = resolveTarget(targetFile);
			if (text != null)
			{
				ObserveTarget(processes, targetFile, text);
			}
		}
	}

	public ManagedProcess? ObserveTarget(ProcessManager processes, string targetFile, string targetPath)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetFile);
		if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
		{
			return null;
		}
		List<Process> list = new List<Process>();
		Process[] processesByName = Process.GetProcessesByName(fileNameWithoutExtension);
		foreach (Process process in processesByName)
		{
			if (ProcessMatchesTarget(process, targetPath, fileNameWithoutExtension))
			{
				list.Add(process);
			}
			else
			{
				process.Dispose();
			}
		}
		if (list.Count != 1)
		{
			foreach (Process item in list)
			{
				item.Dispose();
			}
			return null;
		}
		Process process2 = list[0];
		ManagedProcess? managedProcess = processes.ObserveExternal("target:" + targetFile, process2, targetPath, null, new string[1] { targetFile }, new string[1] { fileNameWithoutExtension });
		if (managedProcess?.Process != process2)
		{
			process2.Dispose();
		}
		return managedProcess;
	}

	private static bool ProcessMatchesTarget(Process process, string targetPath, string stem)
	{
		try
		{
			if (process.HasExited)
			{
				return false;
			}
			if (!string.Equals(process.ProcessName, stem, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			string text = TryGetMainModulePath(process);
			return string.IsNullOrWhiteSpace(text) || string.Equals(Path.GetFullPath(text), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static bool ProcessMatchesTool(Process process, ToolSpec spec, string? expectedExe)
	{
		try
		{
			if (process.HasExited)
			{
				return false;
			}
			string text = TryGetMainModulePath(process);
			if (!string.IsNullOrWhiteSpace(expectedExe) && !string.IsNullOrWhiteSpace(text) && string.Equals(Path.GetFullPath(text), Path.GetFullPath(expectedExe), StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!string.IsNullOrWhiteSpace(expectedExe) && string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			string processName = process.ProcessName;
			if (!spec.ProcessNameHints.Any((string h) => string.Equals(processName, h, StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}
			if (string.IsNullOrWhiteSpace(expectedExe))
			{
				return true;
			}
			string directoryName = Path.GetDirectoryName(Path.GetFullPath(expectedExe));
			if (string.IsNullOrWhiteSpace(directoryName))
			{
				return true;
			}
			string value = directoryName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			return Path.GetFullPath(text).StartsWith(value, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static string? TryGetMainModulePath(Process process)
	{
		try
		{
			return process.MainModule?.FileName;
		}
		catch
		{
			return null;
		}
	}

	private static Process? SelectBestToolCandidate(IReadOnlyList<Process> candidates, ToolSpec spec, string? expectedExe)
	{
		if (candidates.Count == 0)
		{
			return null;
		}
		if (candidates.Count == 1)
		{
			return candidates[0];
		}
		var array = (from p in candidates
			select new
			{
				Process = p,
				Score = ToolCandidateScore(p, spec, expectedExe)
			} into x
			where x.Score > 0
			orderby x.Score descending, SafeProcessId(x.Process) descending
			select x).ToArray();
		if (array.Length == 0)
		{
			return null;
		}
		if (string.IsNullOrWhiteSpace(expectedExe) && array.Length > 1 && array[0].Score == array[1].Score && !HasTitleHint(array[0].Process, spec))
		{
			return null;
		}
		return array[0].Process;
	}

	private static int ToolCandidateScore(Process process, ToolSpec spec, string? expectedExe)
	{
		int num = 0;
		string text = TryGetMainModulePath(process);
		if (!string.IsNullOrWhiteSpace(expectedExe) && !string.IsNullOrWhiteSpace(text))
		{
			string fullPath = Path.GetFullPath(expectedExe);
			string fullPath2 = Path.GetFullPath(text);
			if (string.Equals(fullPath2, fullPath, StringComparison.OrdinalIgnoreCase))
			{
				num += 1000;
			}
			string directoryName = Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				string value = directoryName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
				if (fullPath2.StartsWith(value, StringComparison.OrdinalIgnoreCase))
				{
					num += 500;
				}
			}
		}
		try
		{
			if (process.MainWindowHandle != IntPtr.Zero)
			{
				num += 100;
			}
		}
		catch
		{
		}
		if (HasTitleHint(process, spec))
		{
			num += 200;
		}
		if (spec.ProcessNameHints.Any((string h) => string.Equals(process.ProcessName, h, StringComparison.OrdinalIgnoreCase)))
		{
			num += 50;
		}
		return num;
	}

	private static bool HasTitleHint(Process process, ToolSpec spec)
	{
		try
		{
			string title = process.MainWindowTitle;
			return !string.IsNullOrWhiteSpace(title) && spec.WindowTitleHints.Any((string h) => title.Contains(h, StringComparison.OrdinalIgnoreCase));
		}
		catch
		{
			return false;
		}
	}

	private static int SafeProcessId(Process process)
	{
		try
		{
			return process.Id;
		}
		catch
		{
			return 0;
		}
	}
}
