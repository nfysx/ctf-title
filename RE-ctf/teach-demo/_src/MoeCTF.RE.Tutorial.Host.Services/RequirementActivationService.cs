using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class RequirementActivationService
{
	[CompilerGenerated]
	private sealed class _003CExpandInputPaths_003Ed__20 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private IEnumerable<string> inputPaths;

		public IEnumerable<string> _003C_003E3__inputPaths;

		private IEnumerator<string> _003C_003E7__wrap1;

		private IEnumerator<string> _003C_003E7__wrap2;

		string IEnumerator<string>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CExpandInputPaths_003Ed__20(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if ((uint)(num - -4) <= 1u || (uint)(num - 1) <= 1u)
			{
				try
				{
					if (num == -4 || num == 2)
					{
						try
						{
						}
						finally
						{
							_003C_003Em__Finally2();
						}
					}
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003C_003E7__wrap1 = null;
			_003C_003E7__wrap2 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				string current2;
				IEnumerable<string> enumerable;
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
					_003C_003E1__state = -1;
					_003C_003E7__wrap1 = inputPaths.Where((string p) => !string.IsNullOrWhiteSpace(p)).GetEnumerator();
					_003C_003E1__state = -3;
					goto IL_011a;
				case 1:
					_003C_003E1__state = -3;
					goto IL_011a;
				case 2:
					{
						_003C_003E1__state = -4;
						goto IL_0100;
					}
					IL_0100:
					if (_003C_003E7__wrap2.MoveNext())
					{
						string current = _003C_003E7__wrap2.Current;
						_003C_003E2__current = current;
						_003C_003E1__state = 2;
						return true;
					}
					_003C_003Em__Finally2();
					_003C_003E7__wrap2 = null;
					goto IL_011a;
					IL_011a:
					do
					{
						if (_003C_003E7__wrap1.MoveNext())
						{
							current2 = _003C_003E7__wrap1.Current;
							if (File.Exists(current2))
							{
								_003C_003E2__current = Path.GetFullPath(current2);
								_003C_003E1__state = 1;
								return true;
							}
							continue;
						}
						_003C_003Em__Finally1();
						_003C_003E7__wrap1 = null;
						return false;
					}
					while (!Directory.Exists(current2));
					try
					{
						enumerable = Directory.EnumerateFiles(current2, "*", SearchOption.AllDirectories);
					}
					catch
					{
						enumerable = Array.Empty<string>();
					}
					_003C_003E7__wrap2 = enumerable.GetEnumerator();
					_003C_003E1__state = -4;
					goto IL_0100;
				}
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003C_003E7__wrap1 != null)
			{
				_003C_003E7__wrap1.Dispose();
			}
		}

		private void _003C_003Em__Finally2()
		{
			_003C_003E1__state = -3;
			if (_003C_003E7__wrap2 != null)
			{
				_003C_003E7__wrap2.Dispose();
			}
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			_003CExpandInputPaths_003Ed__20 _003CExpandInputPaths_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CExpandInputPaths_003Ed__ = this;
			}
			else
			{
				_003CExpandInputPaths_003Ed__ = new _003CExpandInputPaths_003Ed__20(0);
			}
			_003CExpandInputPaths_003Ed__.inputPaths = _003C_003E3__inputPaths;
			return _003CExpandInputPaths_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private readonly PathService _paths;

	private RequirementActivationState _state = new RequirementActivationState();

	public RequirementActivationService(PathService paths)
	{
		_paths = paths;
		Load();
	}

	public void Load()
	{
		try
		{
			string requirementStatePath = _paths.RequirementStatePath;
			if (File.Exists(requirementStatePath))
			{
				_state = JsonSerializer.Deserialize<RequirementActivationState>(File.ReadAllText(requirementStatePath), JsonOptions) ?? new RequirementActivationState();
			}
		}
		catch
		{
			_state = new RequirementActivationState();
		}
	}

	public void Save()
	{
		Directory.CreateDirectory(_paths.AppDataDir);
		File.WriteAllText(_paths.RequirementStatePath, JsonSerializer.Serialize(_state, JsonOptions));
	}

	public string? FindToolExecutable(string toolId)
	{
		if (_state.Tools.TryGetValue(toolId, out string value) && File.Exists(value))
		{
			return value;
		}
		return _paths.FindBundledToolExecutable(toolId);
	}

	public string? GetConfiguredToolPath(string toolId)
	{
		if (!_state.Tools.TryGetValue(toolId, out string value) || !File.Exists(value))
		{
			return null;
		}
		return value;
	}

	public void SetToolPath(string toolId, string executablePath)
	{
		if (!File.Exists(executablePath))
		{
			throw new FileNotFoundException("Tool executable not found.", executablePath);
		}
		_state.Tools[toolId] = Path.GetFullPath(executablePath);
		Save();
	}

	public void ClearToolPath(string toolId)
	{
		_state.Tools.Remove(toolId);
		Save();
	}

	public string? FindTarget(string courseId, CourseRequirement requirement)
	{
		if (_state.CourseTargets.TryGetValue(courseId, out Dictionary<string, string> value) && value.TryGetValue(requirement.Id, out var value2) && Matches(requirement, value2))
		{
			return value2;
		}
		string text = ResolveTargetAssetPath(courseId, requirement, requirement.FileNameOrDefault());
		if (text != null && Matches(requirement, text))
		{
			return text;
		}
		string text2 = _paths.FindTarget(requirement.FileNameOrDefault());
		if (text2 == null || !Matches(requirement, text2))
		{
			return null;
		}
		return text2;
	}

	public void SetTargetPath(string courseId, CourseRequirement requirement, string sourceFile)
	{
		if (!File.Exists(sourceFile))
		{
			throw new FileNotFoundException("Target file not found.", sourceFile);
		}
		if (!Matches(requirement, sourceFile))
		{
			throw new InvalidDataException("Selected file does not match this course target.");
		}
		string text = ResolveTargetAssetPath(courseId, requirement, Path.GetFileName(sourceFile)) ?? throw new InvalidOperationException("Target asset path escapes content root.");
		Directory.CreateDirectory(Path.GetDirectoryName(text));
		File.Copy(sourceFile, text, overwrite: true);
		if (!_state.CourseTargets.TryGetValue(courseId, out Dictionary<string, string> value))
		{
			value = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			_state.CourseTargets[courseId] = value;
		}
		value[requirement.Id] = text;
		Save();
	}

	public RequirementActivationStatus StatusFor(string courseId, string groupId, CourseRequirement requirement)
	{
		string kindNormalized = requirement.KindNormalized;
		string text = ((kindNormalized == "tool") ? FindToolExecutable(requirement.ToolIdOrDefault()) : ((!(kindNormalized == "target")) ? null : FindTarget(courseId, requirement)));
		string text2 = text;
		bool isSelected = string.Equals(GetSelectedRequirement(courseId, groupId), requirement.Id, StringComparison.OrdinalIgnoreCase);
		return new RequirementActivationStatus(requirement, !string.IsNullOrWhiteSpace(text2) && File.Exists(text2), text2, ShortPath(text2), isSelected);
	}

	public bool IsGroupSatisfied(string courseId, CourseRequirementGroup group)
	{
		RequirementActivationStatus[] source = group.Items.Select((CourseRequirement item) => StatusFor(courseId, group.Id, item)).ToArray();
		if (!(group.ModeNormalized == "anyOf"))
		{
			return source.All((RequirementActivationStatus s) => s.IsSatisfied);
		}
		return source.Any((RequirementActivationStatus s) => s.IsSatisfied);
	}

	public bool AreRequirementsSatisfied(string courseId, IEnumerable<CourseRequirementGroup> groups)
	{
		return groups.All((CourseRequirementGroup group) => IsGroupSatisfied(courseId, group));
	}

	public string? GetSelectedRequirement(string courseId, string groupId)
	{
		if (!_state.CourseChoices.TryGetValue(courseId, out Dictionary<string, string> value) || !value.TryGetValue(groupId, out var value2))
		{
			return null;
		}
		return value2;
	}

	public void SelectRequirement(string courseId, string groupId, string requirementId)
	{
		if (!_state.CourseChoices.TryGetValue(courseId, out Dictionary<string, string> value))
		{
			value = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			_state.CourseChoices[courseId] = value;
		}
		value[groupId] = requirementId;
		Save();
	}

	public void AutoSelectSatisfiedRequirements(string courseId, IEnumerable<CourseRequirementGroup> groups)
	{
		bool flag = false;
		foreach (CourseRequirementGroup group in groups)
		{
			string selected = GetSelectedRequirement(courseId, group.Id);
			if (!string.IsNullOrWhiteSpace(selected) && group.Items.Any((CourseRequirement item) => string.Equals(item.Id, selected, StringComparison.OrdinalIgnoreCase) && StatusFor(courseId, group.Id, item).IsSatisfied))
			{
				continue;
			}
			CourseRequirement courseRequirement = group.Items.FirstOrDefault((CourseRequirement item) => StatusFor(courseId, group.Id, item).IsSatisfied);
			if (courseRequirement != null)
			{
				if (!_state.CourseChoices.TryGetValue(courseId, out Dictionary<string, string> value))
				{
					value = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					_state.CourseChoices[courseId] = value;
				}
				value[group.Id] = courseRequirement.Id;
				flag = true;
			}
		}
		if (flag)
		{
			Save();
		}
	}

	public CourseRequirement? FindMatchingRequirement(CourseDefinition course, IEnumerable<string> inputPaths, out string matchedPath)
	{
		foreach (string item in ExpandInputPaths(inputPaths))
		{
			foreach (CourseRequirement item2 in course.RequirementGroups.SelectMany((CourseRequirementGroup g) => g.Items))
			{
				if (Matches(item2, item))
				{
					matchedPath = item;
					return item2;
				}
			}
		}
		matchedPath = "";
		return null;
	}

	public void ActivateRequirement(CourseDefinition course, CourseRequirement requirement, string matchedPath)
	{
		if (requirement.KindNormalized == "tool")
		{
			SetToolPath(requirement.ToolIdOrDefault(), matchedPath);
		}
		else
		{
			SetTargetPath(course.Id, requirement, matchedPath);
		}
	}

	[IteratorStateMachine(typeof(_003CExpandInputPaths_003Ed__20))]
	public static IEnumerable<string> ExpandInputPaths(IEnumerable<string> inputPaths)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CExpandInputPaths_003Ed__20(-2)
		{
			_003C_003E3__inputPaths = inputPaths
		};
	}

	public static bool Matches(CourseRequirement requirement, string filePath)
	{
		if (!File.Exists(filePath))
		{
			return false;
		}
		string name = Path.GetFileName(filePath);
		IReadOnlyList<string> source;
		if (requirement.MatchNames.Count <= 0)
		{
			IReadOnlyList<string> readOnlyList = new List<string> { (requirement.KindNormalized == "tool") ? requirement.FileNameOrDefault(requirement.ToolExecutableName()) : requirement.FileNameOrDefault() };
			source = readOnlyList;
		}
		else
		{
			source = requirement.MatchNames;
		}
		if (!source.Any((string pattern) => FileNameMatches(name, pattern)))
		{
			return false;
		}
		if (requirement.MatchPathContains.Count > 0 && !requirement.MatchPathContains.All((string part) => filePath.Contains(part, StringComparison.OrdinalIgnoreCase)))
		{
			return false;
		}
		long? matchSize = requirement.MatchSize;
		if (matchSize.HasValue)
		{
			long valueOrDefault = matchSize.GetValueOrDefault();
			if (new FileInfo(filePath).Length != valueOrDefault)
			{
				return false;
			}
		}
		if (!string.IsNullOrWhiteSpace(requirement.MatchSha256))
		{
			using FileStream source2 = File.OpenRead(filePath);
			if (!string.Equals(Convert.ToHexString(SHA256.HashData(source2)), requirement.MatchSha256.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		return true;
	}

	private string? ResolveTargetAssetPath(string courseId, CourseRequirement requirement, string? fallbackFileName)
	{
		string text = requirement.AssetPathOrDefault(fallbackFileName).Replace('\\', '/');
		if (Path.IsPathRooted(text) || text.Split('/').Any((string part) => string.IsNullOrWhiteSpace(part) || part == ".."))
		{
			return null;
		}
		string text2 = courseId.Trim();
		if (text2.Length == 0 || text2.Contains('/') || text2.Contains('\\') || text2.Contains("..", StringComparison.Ordinal))
		{
			return null;
		}
		string text3 = "courses/" + text2 + "/assets/targets/";
		if (!text.StartsWith(text3, StringComparison.OrdinalIgnoreCase) || text.Length <= text3.Length)
		{
			return null;
		}
		string fullPath = Path.GetFullPath(Path.Combine(_paths.ContentRoot, text.Replace('/', Path.DirectorySeparatorChar)));
		string value = Path.GetFullPath(_paths.ContentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return fullPath;
	}

	private static bool FileNameMatches(string fileName, string pattern)
	{
		if (string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (!pattern.Contains('*') && !pattern.Contains('?'))
		{
			return false;
		}
		string pattern2 = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
		return Regex.IsMatch(fileName, pattern2, RegexOptions.IgnoreCase);
	}

	private static string ShortPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "";
		}
		string fileName = Path.GetFileName(path);
		string fileName2 = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
		if (!string.IsNullOrWhiteSpace(fileName2))
		{
			return fileName2 + "\\" + fileName;
		}
		return fileName;
	}
}
