using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ProcessManager : IDisposable
{
	private readonly ConcurrentDictionary<string, ManagedProcess> _procs = new ConcurrentDictionary<string, ManagedProcess>(StringComparer.OrdinalIgnoreCase);

	public IReadOnlyDictionary<string, ManagedProcess> Processes => _procs;

	public event Action? Changed;

	public ManagedProcess? ObserveExternal(string id, Process process, string? path = null, string? toolId = null, string[]? titleHints = null, string[]? processHints = null)
	{
		if (_procs.TryGetValue(id, out ManagedProcess value))
		{
			try
			{
				if (!value.Process.HasExited)
				{
					if (value.Process.Id == process.Id)
					{
						return value;
					}
					if (!string.Equals(value.Kind, "observed", StringComparison.OrdinalIgnoreCase))
					{
						return value;
					}
				}
			}
			catch
			{
			}
			_procs.TryRemove(id, out ManagedProcess _);
		}
		try
		{
			if (process.HasExited)
			{
				return null;
			}
		}
		catch
		{
			return null;
		}
		ManagedProcess managedProcess = new ManagedProcess
		{
			Id = id,
			Kind = "observed",
			Process = process,
			Path = path,
			ToolId = toolId,
			TitleHints = (titleHints ?? Array.Empty<string>()),
			ProcessHints = (processHints ?? Array.Empty<string>())
		};
		_procs[id] = managedProcess;
		try
		{
			process.EnableRaisingEvents = true;
			process.Exited += delegate
			{
				this.Changed?.Invoke();
			};
		}
		catch
		{
		}
		this.Changed?.Invoke();
		return managedProcess;
	}

	public void PruneExited()
	{
		bool flag = false;
		foreach (string item in _procs.Keys.ToList())
		{
			if (_procs.TryGetValue(item, out ManagedProcess value))
			{
				bool flag2 = false;
				try
				{
					flag2 = value.Process.HasExited;
				}
				catch
				{
					flag2 = true;
				}
				if (flag2)
				{
					_procs.TryRemove(item, out ManagedProcess _);
					flag = true;
				}
			}
		}
		if (flag)
		{
			this.Changed?.Invoke();
		}
	}

	public void Forget(string id)
	{
		_procs.TryRemove(id, out ManagedProcess _);
		this.Changed?.Invoke();
	}

	public void KillAll()
	{
		foreach (ManagedProcess item in _procs.Values.ToList())
		{
			try
			{
				if (string.Equals(item.Kind, "observed", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						item.Process.Dispose();
					}
					catch
					{
					}
					continue;
				}
				if (!item.Process.HasExited)
				{
					item.Process.Kill(entireProcessTree: true);
				}
				try
				{
					item.Process.Dispose();
				}
				catch
				{
				}
			}
			catch
			{
			}
		}
		_procs.Clear();
		this.Changed?.Invoke();
	}

	public void Dispose()
	{
		KillAll();
	}
}
