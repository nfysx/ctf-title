using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class CourseCatalog
{
	private sealed class CourseIndex
	{
		[JsonPropertyName("courses")]
		public string[] Courses { get; set; } = Array.Empty<string>();

		[JsonPropertyName("catalogUpdateManifestUrl")]
		public string? CatalogUpdateManifestUrl { get; set; }

		[JsonPropertyName("appUpdateManifestUrl")]
		public string? AppUpdateManifestUrl { get; set; }
	}

	private sealed class StageIndexManifest
	{
		[JsonPropertyName("stages")]
		public string[] Stages { get; set; } = Array.Empty<string>();
	}

	private sealed class CourseManifest
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = "";

		[JsonPropertyName("title")]
		public string? Title { get; set; }

		[JsonPropertyName("subtitle")]
		public string? Subtitle { get; set; }

		[JsonPropertyName("summary")]
		public string? Summary { get; set; }

		[JsonPropertyName("version")]
		public string? Version { get; set; }

		[JsonPropertyName("track")]
		public string? Track { get; set; }

		[JsonPropertyName("difficulty")]
		public string? Difficulty { get; set; }

		[JsonPropertyName("status")]
		public string? Status { get; set; }

		[JsonPropertyName("stageCount")]
		public int StageCount { get; set; }

		[JsonPropertyName("estimatedMinutes")]
		public int EstimatedMinutes { get; set; }

		[JsonPropertyName("authors")]
		public List<string> Authors { get; set; } = new List<string>();

		[JsonPropertyName("stagesDir")]
		public string? StagesDir { get; set; }

		[JsonPropertyName("i18nDir")]
		public string? I18nDir { get; set; }

		[JsonPropertyName("assetsDir")]
		public string? AssetsDir { get; set; }

		[JsonPropertyName("metadata")]
		public CourseMetadataManifest? Metadata { get; set; }

		[JsonPropertyName("content")]
		public CourseContentManifest? Content { get; set; }

		[JsonPropertyName("flow")]
		public CourseFlowManifest? Flow { get; set; }

		[JsonPropertyName("compatibility")]
		public CourseCompatibilityManifest? Compatibility { get; set; }

		[JsonPropertyName("updateManifestUrl")]
		public string? UpdateManifestUrl { get; set; }

		[JsonPropertyName("appUpdateManifestUrl")]
		public string? AppUpdateManifestUrl { get; set; }

		[JsonPropertyName("requirements")]
		public CourseRequirementNodeManifest? Requirements { get; set; }

		[JsonPropertyName("changelog")]
		public List<CourseChangelogEntryManifest> Changelog { get; set; } = new List<CourseChangelogEntryManifest>();
	}

	private sealed class CourseCompatibilityManifest
	{
		[JsonPropertyName("minHostVersion")]
		public string? MinHostVersion { get; set; }

		[JsonPropertyName("maxHostVersionExclusive")]
		public string? MaxHostVersionExclusive { get; set; }
	}

	private sealed class CourseMetadataManifest
	{
		[JsonPropertyName("title")]
		public string? Title { get; set; }

		[JsonPropertyName("subtitle")]
		public string? Subtitle { get; set; }

		[JsonPropertyName("summary")]
		public string? Summary { get; set; }

		[JsonPropertyName("track")]
		public string? Track { get; set; }

		[JsonPropertyName("difficulty")]
		public string? Difficulty { get; set; }

		[JsonPropertyName("estimatedMinutes")]
		public int EstimatedMinutes { get; set; }

		[JsonPropertyName("authors")]
		public List<string> Authors { get; set; } = new List<string>();
	}

	private sealed class CourseContentManifest
	{
		[JsonPropertyName("stagesDir")]
		public string? StagesDir { get; set; }

		[JsonPropertyName("i18nDir")]
		public string? I18nDir { get; set; }

		[JsonPropertyName("assetsDir")]
		public string? AssetsDir { get; set; }
	}

	private sealed class CourseFlowManifest
	{
		[JsonPropertyName("stages")]
		public List<CourseFlowStageManifest> Stages { get; set; } = new List<CourseFlowStageManifest>();
	}

	private sealed class CourseFlowStageManifest
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = "";

		[JsonPropertyName("path")]
		public string Path { get; set; } = "";
	}

	private sealed class CourseRequirementNodeManifest
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = "";

		[JsonPropertyName("title")]
		public string? Title { get; set; }

		[JsonPropertyName("description")]
		public string? Description { get; set; }

		[JsonPropertyName("allOf")]
		public List<CourseRequirementNodeManifest>? AllOf { get; set; }

		[JsonPropertyName("anyOf")]
		public List<CourseRequirementNodeManifest>? AnyOf { get; set; }

		[JsonPropertyName("kind")]
		public string? Kind { get; set; }

		[JsonPropertyName("label")]
		public string? Label { get; set; }

		[JsonPropertyName("toolId")]
		public string? ToolId { get; set; }

		[JsonPropertyName("targetId")]
		public string? TargetId { get; set; }

		[JsonPropertyName("fileName")]
		public string? FileName { get; set; }

		[JsonPropertyName("match")]
		public CourseRequirementMatchManifest? Match { get; set; }

		[JsonPropertyName("asset")]
		public CourseRequirementAssetManifest? Asset { get; set; }
	}

	private sealed class CourseRequirementMatchManifest
	{
		[JsonPropertyName("fileNames")]
		public string[]? FileNames { get; set; }

		[JsonPropertyName("pathContains")]
		public string[]? PathContains { get; set; }

		[JsonPropertyName("sha256")]
		public string? Sha256 { get; set; }

		[JsonPropertyName("size")]
		public long? Size { get; set; }
	}

	private sealed class CourseRequirementAssetManifest
	{
		[JsonPropertyName("path")]
		public string? Path { get; set; }
	}

	private sealed class CourseChangelogEntryManifest
	{
		[JsonPropertyName("version")]
		public string? Version { get; set; }

		[JsonPropertyName("date")]
		public string? Date { get; set; }

		[JsonPropertyName("text")]
		public string? Text { get; set; }
	}

	[CompilerGenerated]
	private sealed class _003CFlattenLeaves_003Ed__13 : IEnumerable<CourseRequirementNodeManifest>, IEnumerable, IEnumerator<CourseRequirementNodeManifest>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private CourseRequirementNodeManifest _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private IEnumerable<CourseRequirementNodeManifest> nodes;

		public IEnumerable<CourseRequirementNodeManifest> _003C_003E3__nodes;

		private IEnumerator<CourseRequirementNodeManifest> _003C_003E7__wrap1;

		private IEnumerator<CourseRequirementNodeManifest> _003C_003E7__wrap2;

		CourseRequirementNodeManifest IEnumerator<CourseRequirementNodeManifest>.Current
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
		public _003CFlattenLeaves_003Ed__13(int _003C_003E1__state)
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
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
					_003C_003E1__state = -1;
					_003C_003E7__wrap1 = nodes.GetEnumerator();
					_003C_003E1__state = -3;
					goto IL_00ef;
				case 1:
					_003C_003E1__state = -3;
					goto IL_00ef;
				case 2:
					{
						_003C_003E1__state = -4;
						goto IL_00d5;
					}
					IL_00ef:
					if (_003C_003E7__wrap1.MoveNext())
					{
						CourseRequirementNodeManifest current = _003C_003E7__wrap1.Current;
						if (IsLeaf(current))
						{
							_003C_003E2__current = current;
							_003C_003E1__state = 1;
							return true;
						}
						List<CourseRequirementNodeManifest> list = current.AnyOf ?? current.AllOf ?? new List<CourseRequirementNodeManifest>();
						_003C_003E7__wrap2 = FlattenLeaves(list).GetEnumerator();
						_003C_003E1__state = -4;
						goto IL_00d5;
					}
					_003C_003Em__Finally1();
					_003C_003E7__wrap1 = null;
					return false;
					IL_00d5:
					if (_003C_003E7__wrap2.MoveNext())
					{
						CourseRequirementNodeManifest current2 = _003C_003E7__wrap2.Current;
						_003C_003E2__current = current2;
						_003C_003E1__state = 2;
						return true;
					}
					_003C_003Em__Finally2();
					_003C_003E7__wrap2 = null;
					goto IL_00ef;
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
		IEnumerator<CourseRequirementNodeManifest> IEnumerable<CourseRequirementNodeManifest>.GetEnumerator()
		{
			_003CFlattenLeaves_003Ed__13 _003CFlattenLeaves_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CFlattenLeaves_003Ed__ = this;
			}
			else
			{
				_003CFlattenLeaves_003Ed__ = new _003CFlattenLeaves_003Ed__13(0);
			}
			_003CFlattenLeaves_003Ed__.nodes = _003C_003E3__nodes;
			return _003CFlattenLeaves_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<CourseRequirementNodeManifest>)this).GetEnumerator();
		}
	}

	[CompilerGenerated]
	private sealed class _003CLoadFromRoot_003Ed__8 : IEnumerable<CourseDefinition>, IEnumerable, IEnumerator<CourseDefinition>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private CourseDefinition _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private string root;

		public string _003C_003E3__root;

		public CourseCatalog _003C_003E4__this;

		private bool isBuiltIn;

		public bool _003C_003E3__isBuiltIn;

		private string[] _003C_003E7__wrap1;

		private int _003C_003E7__wrap2;

		CourseDefinition IEnumerator<CourseDefinition>.Current
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
		public _003CLoadFromRoot_003Ed__8(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E7__wrap1 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			CourseCatalog courseCatalog = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
			{
				_003C_003E1__state = -1;
				if (!Directory.Exists(root))
				{
					return false;
				}
				string path = Path.Combine(root, "courses", "index.json");
				if (!File.Exists(path))
				{
					if (IsValidCourseLayout(Path.Combine(root, "stages"), Path.Combine(root, "assets", "i18n"), Path.Combine(root, "assets")))
					{
						_003C_003E2__current = courseCatalog.BuiltInFallback(root, isBuiltIn);
						_003C_003E1__state = 1;
						return true;
					}
					goto IL_00ba;
				}
				CourseIndex courseIndex = null;
				try
				{
					courseIndex = JsonSerializer.Deserialize<CourseIndex>(File.ReadAllText(path), Options);
				}
				catch
				{
				}
				_003C_003E7__wrap1 = courseIndex?.Courses ?? Array.Empty<string>();
				_003C_003E7__wrap2 = 0;
				goto IL_0163;
			}
			case 1:
				_003C_003E1__state = -1;
				goto IL_00ba;
			case 2:
				{
					_003C_003E1__state = -1;
					goto IL_0155;
				}
				IL_00ba:
				return false;
				IL_0155:
				_003C_003E7__wrap2++;
				goto IL_0163;
				IL_0163:
				if (_003C_003E7__wrap2 < _003C_003E7__wrap1.Length)
				{
					string rel = _003C_003E7__wrap1[_003C_003E7__wrap2];
					string text = SafeCombine(root, rel);
					if (text != null && File.Exists(text))
					{
						CourseDefinition courseDefinition = courseCatalog.LoadCourse(root, text, isBuiltIn);
						if (courseDefinition != null)
						{
							_003C_003E2__current = courseDefinition;
							_003C_003E1__state = 2;
							return true;
						}
					}
					goto IL_0155;
				}
				_003C_003E7__wrap1 = null;
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<CourseDefinition> IEnumerable<CourseDefinition>.GetEnumerator()
		{
			_003CLoadFromRoot_003Ed__8 _003CLoadFromRoot_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CLoadFromRoot_003Ed__ = this;
			}
			else
			{
				_003CLoadFromRoot_003Ed__ = new _003CLoadFromRoot_003Ed__8(0)
				{
					_003C_003E4__this = _003C_003E4__this
				};
			}
			_003CLoadFromRoot_003Ed__.root = _003C_003E3__root;
			_003CLoadFromRoot_003Ed__.isBuiltIn = _003C_003E3__isBuiltIn;
			return _003CLoadFromRoot_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<CourseDefinition>)this).GetEnumerator();
		}
	}

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly PathService _paths;

	public CourseCatalog(PathService paths)
	{
		_paths = paths;
	}

	public IReadOnlyList<CourseDefinition> LoadCourses()
	{
		Dictionary<string, CourseDefinition> dictionary = new Dictionary<string, CourseDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (CourseDefinition item in LoadFromRoot(_paths.RepoRoot, isBuiltIn: true))
		{
			AddOrReplaceCourse(dictionary, item);
		}
		return dictionary.Values.OrderBy((CourseDefinition c) => (!c.IsBuiltIn) ? 1 : 0).ThenBy<CourseDefinition, string>((CourseDefinition c) => c.Id, StringComparer.OrdinalIgnoreCase).ToArray();
	}

	public CourseUpdateEndpoints LoadUpdateEndpoints()
	{
		string path = Path.Combine(_paths.RepoRoot, "courses", "index.json");
		try
		{
			CourseIndex courseIndex = JsonSerializer.Deserialize<CourseIndex>(File.ReadAllText(path), Options);
			return new CourseUpdateEndpoints(courseIndex?.CatalogUpdateManifestUrl, courseIndex?.AppUpdateManifestUrl);
		}
		catch
		{
			return new CourseUpdateEndpoints(null, null);
		}
	}

	private static void AddOrReplaceCourse(Dictionary<string, CourseDefinition> courses, CourseDefinition candidate)
	{
		if (!courses.TryGetValue(candidate.Id, out CourseDefinition value) || ShouldPreferCourse(candidate, value))
		{
			courses[candidate.Id] = candidate;
		}
	}

	private static bool ShouldPreferCourse(CourseDefinition candidate, CourseDefinition existing)
	{
		int num = CompareVersions(candidate.Version, existing.Version);
		if (num != 0)
		{
			return num > 0;
		}
		if (existing.IsBuiltIn != candidate.IsBuiltIn)
		{
			return candidate.IsBuiltIn;
		}
		return false;
	}

	private static int CompareVersions(string? left, string? right)
	{
		return SemanticVersion.Compare(left, right);
	}

	[IteratorStateMachine(typeof(_003CLoadFromRoot_003Ed__8))]
	private IEnumerable<CourseDefinition> LoadFromRoot(string root, bool isBuiltIn)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CLoadFromRoot_003Ed__8(-2)
		{
			_003C_003E4__this = this,
			_003C_003E3__root = root,
			_003C_003E3__isBuiltIn = isBuiltIn
		};
	}

	private CourseDefinition? LoadCourse(string root, string manifestPath, bool isBuiltIn)
	{
		try
		{
			CourseManifest courseManifest = JsonSerializer.Deserialize<CourseManifest>(File.ReadAllText(manifestPath), Options);
			if (courseManifest == null || string.IsNullOrWhiteSpace(courseManifest.Id) || !SemanticVersion.TryParse(courseManifest.Version, out SemanticVersion _))
			{
				return null;
			}
			string text = ResolveDir(root, First(courseManifest.StagesDir, courseManifest.Content?.StagesDir ?? ""), "stages");
			string text2 = ResolveDir(root, First(courseManifest.AssetsDir, courseManifest.Content?.AssetsDir ?? ""), "assets");
			string text3 = ResolveDir(root, First(courseManifest.I18nDir, courseManifest.Content?.I18nDir ?? ""), Path.Combine("assets", "i18n"));
			if (text == null || text2 == null || text3 == null)
			{
				return null;
			}
			if (!IsValidCourseLayout(text, text3, text2))
			{
				return null;
			}
			CourseDefinition obj = new CourseDefinition
			{
				Id = courseManifest.Id.Trim(),
				Title = First(courseManifest.Metadata?.Title, First(courseManifest.Title, courseManifest.Id)),
				Subtitle = First(courseManifest.Metadata?.Subtitle, courseManifest.Subtitle ?? ""),
				Summary = First(courseManifest.Metadata?.Summary, courseManifest.Summary ?? ""),
				Version = courseManifest.Version,
				FlowFingerprint = ComputeFlowFingerprint(text),
				Track = First(courseManifest.Metadata?.Track, courseManifest.Track ?? "Reverse"),
				Difficulty = First(courseManifest.Metadata?.Difficulty, courseManifest.Difficulty ?? "Beginner"),
				Authors = ((from author in courseManifest.Metadata?.Authors?.Where((string author) => !string.IsNullOrWhiteSpace(author))
					select author.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>()),
				Status = (courseManifest.Status ?? "installed")
			};
			int stageCount;
			if (courseManifest.StageCount <= 0)
			{
				CourseFlowManifest? flow = courseManifest.Flow;
				stageCount = ((flow != null && flow.Stages?.Count > 0) ? courseManifest.Flow.Stages.Count : CountStages(text));
			}
			else
			{
				stageCount = courseManifest.StageCount;
			}
			obj.StageCount = stageCount;
			obj.EstimatedMinutes = ((courseManifest.EstimatedMinutes > 0) ? courseManifest.EstimatedMinutes : (courseManifest.Metadata?.EstimatedMinutes ?? 0));
			obj.IsBuiltIn = isBuiltIn;
			obj.ContentRoot = Path.GetFullPath(root);
			obj.StagesDir = text;
			obj.I18nDir = text3;
			obj.AssetsDir = text2;
			obj.UpdateManifestUrl = courseManifest.UpdateManifestUrl;
			obj.AppUpdateManifestUrl = courseManifest.AppUpdateManifestUrl;
			obj.MinHostVersion = courseManifest.Compatibility?.MinHostVersion?.Trim() ?? "";
			obj.MaxHostVersionExclusive = courseManifest.Compatibility?.MaxHostVersionExclusive?.Trim() ?? "";
			obj.RequirementGroups = NormalizeRequirements(courseManifest.Requirements);
			obj.Changelog = courseManifest.Changelog.Select((CourseChangelogEntryManifest entry) => new CourseChangelogEntry
			{
				Version = (entry.Version ?? ""),
				Date = (entry.Date ?? ""),
				Text = (entry.Text ?? "")
			}).ToArray();
			return obj;
		}
		catch
		{
			return null;
		}
	}

	private CourseDefinition BuiltInFallback()
	{
		return BuiltInFallback(_paths.RepoRoot, isBuiltIn: true);
	}

	private CourseDefinition BuiltInFallback(string root, bool isBuiltIn)
	{
		string stagesDir = Path.Combine(root, "stages");
		return new CourseDefinition
		{
			Id = "freshman-re",
			Title = "逆向工程入门指北",
			Subtitle = "MoeCTF 新生逆向第一版",
			Summary = "从 DIE 识别、IDA 字符串和分支阅读、再到 dnSpy 托管逻辑的静态入门路线。",
			Version = "2026.1.1",
			FlowFingerprint = ComputeFlowFingerprint(stagesDir),
			Track = "Reverse",
			Difficulty = "Beginner",
			Status = "installed",
			StageCount = CountStages(stagesDir),
			EstimatedMinutes = 55,
			IsBuiltIn = isBuiltIn,
			ContentRoot = Path.GetFullPath(root),
			StagesDir = stagesDir,
			I18nDir = Path.Combine(root, "assets", "i18n"),
			AssetsDir = Path.Combine(root, "assets"),
			UpdateManifestUrl = null,
			AppUpdateManifestUrl = null,
			MinHostVersion = "0.0.0",
			MaxHostVersionExclusive = "9999.0.0",
			RequirementGroups = Array.Empty<CourseRequirementGroup>(),
			Changelog = Array.Empty<CourseChangelogEntry>()
		};
	}

	private static IReadOnlyList<CourseRequirementGroup> NormalizeRequirements(CourseRequirementNodeManifest? root)
	{
		List<CourseRequirementGroup> groups = new List<CourseRequirementGroup>();
		if (root == null)
		{
			return groups;
		}
		AddNode(root);
		return groups;
		void AddNode(CourseRequirementNodeManifest node)
		{
			if (IsLeaf(node))
			{
				CourseRequirement courseRequirement = ToRequirement(node);
				if (courseRequirement != null)
				{
					groups.Add(new CourseRequirementGroup
					{
						Id = node.Id.Trim(),
						Title = First(node.Title, node.Label ?? node.Id),
						Description = (node.Description ?? ""),
						Mode = "allOf",
						Items = new CourseRequirement[1] { courseRequirement }
					});
				}
			}
			else
			{
				List<CourseRequirementNodeManifest> anyOf = node.AnyOf;
				if (anyOf != null && anyOf.Count > 0)
				{
					CourseRequirement[] array = (from r in FlattenLeaves(node.AnyOf).Select(ToRequirement)
						where r != null
						select (r)).ToArray();
					if (array.Length != 0)
					{
						groups.Add(new CourseRequirementGroup
						{
							Id = node.Id.Trim(),
							Title = First(node.Title, node.Id),
							Description = (node.Description ?? ""),
							Mode = "anyOf",
							Items = array
						});
					}
				}
				else
				{
					anyOf = node.AllOf;
					if (anyOf != null && anyOf.Count > 0)
					{
						foreach (CourseRequirementNodeManifest item in node.AllOf)
						{
							AddNode(item);
						}
					}
				}
			}
		}
	}

	[IteratorStateMachine(typeof(_003CFlattenLeaves_003Ed__13))]
	private static IEnumerable<CourseRequirementNodeManifest> FlattenLeaves(IEnumerable<CourseRequirementNodeManifest> nodes)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CFlattenLeaves_003Ed__13(-2)
		{
			_003C_003E3__nodes = nodes
		};
	}

	private static bool IsLeaf(CourseRequirementNodeManifest node)
	{
		return !string.IsNullOrWhiteSpace(node.Kind);
	}

	private static CourseRequirement? ToRequirement(CourseRequirementNodeManifest node)
	{
		if (string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Kind))
		{
			return null;
		}
		string[] source = node.Match?.FileNames ?? Array.Empty<string>();
		return new CourseRequirement
		{
			Id = node.Id.Trim(),
			Kind = (string.Equals(node.Kind, "tool", StringComparison.OrdinalIgnoreCase) ? "tool" : "target"),
			Label = (node.Label ?? node.Id),
			Description = (node.Description ?? ""),
			ToolId = node.ToolId,
			FileName = ((string.IsNullOrWhiteSpace(node.FileName) && string.Equals(node.Kind, "target", StringComparison.OrdinalIgnoreCase)) ? node.TargetId : node.FileName),
			AssetPath = node.Asset?.Path,
			MatchNames = (from v in source
				where !string.IsNullOrWhiteSpace(v)
				select v.Trim()).ToArray(),
			MatchPathContains = (from v in node.Match?.PathContains ?? Array.Empty<string>()
				where !string.IsNullOrWhiteSpace(v)
				select v.Trim()).ToArray(),
			MatchSha256 = node.Match?.Sha256,
			MatchSize = node.Match?.Size
		};
	}

	private static string? ResolveDir(string root, string? rel, string fallback)
	{
		string text = (string.IsNullOrWhiteSpace(rel) ? fallback : rel);
		if (Path.IsPathRooted(text))
		{
			return null;
		}
		string fullPath = Path.GetFullPath(Path.Combine(root, text));
		string value = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return fullPath;
	}

	private static string First(string? value, string fallback)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		return fallback;
	}

	private static bool IsValidCourseLayout(string stagesDir, string i18nDir, string assetsDir)
	{
		if (Directory.Exists(assetsDir) && Directory.Exists(i18nDir) && File.Exists(Path.Combine(i18nDir, "zh-CN.json")))
		{
			return File.Exists(Path.Combine(stagesDir, "index.json"));
		}
		return false;
	}

	private static int CountStages(string stagesDir)
	{
		try
		{
			return (JsonSerializer.Deserialize<StageIndexManifest>(File.ReadAllText(Path.Combine(stagesDir, "index.json")), Options)?.Stages?.Length).GetValueOrDefault();
		}
		catch
		{
			return 0;
		}
	}

	private static string ComputeFlowFingerprint(string stagesDir)
	{
		try
		{
			string[] array = Directory.EnumerateFiles(stagesDir, "*.json", SearchOption.TopDirectoryOnly).OrderBy<string, string>((string path) => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToArray();
			using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			string[] array2 = array;
			foreach (string path2 in array2)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(Path.GetFileName(path2).ToLowerInvariant());
				incrementalHash.AppendData(bytes);
				incrementalHash.AppendData(new byte[1]);
				incrementalHash.AppendData(File.ReadAllBytes(path2));
				incrementalHash.AppendData(new byte[1]);
			}
			return Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
		}
		catch
		{
			return string.Empty;
		}
	}

	private static string? SafeCombine(string root, string rel)
	{
		if (string.IsNullOrWhiteSpace(rel) || Path.IsPathRooted(rel))
		{
			return null;
		}
		string fullPath = Path.GetFullPath(Path.Combine(root, rel));
		string value = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return fullPath;
	}
}
