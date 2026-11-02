using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class ContentUpdater
{
	private enum HostCompatibilityStatus
	{
		Invalid,
		BelowMinimum,
		Compatible,
		AtOrAboveMaximum
	}

	private sealed class CourseIndex
	{
		[JsonPropertyName("catalogVersion")]
		public string? CatalogVersion { get; set; }

		[JsonPropertyName("contentVersion")]
		public string? ContentVersion { get; set; }

		[JsonPropertyName("courses")]
		public string[] Courses { get; set; } = Array.Empty<string>();
	}

	private sealed class CourseManifest
	{
		[JsonPropertyName("version")]
		public string? Version { get; set; }
	}

	private sealed class UpdateTransaction
	{
		[JsonPropertyName("backedUp")]
		public List<UpdateBackupEntry> BackedUp { get; set; } = new List<UpdateBackupEntry>();

		[JsonPropertyName("created")]
		public List<string> Created { get; set; } = new List<string>();
	}

	private sealed class UpdateBackupEntry
	{
		[JsonPropertyName("path")]
		public string Path { get; set; } = "";

		[JsonPropertyName("backup")]
		public string Backup { get; set; } = "";
	}

	[CompilerGenerated]
	private sealed class _003CBuildPruneList_003Ed__20 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private string courseId;

		public string _003C_003E3__courseId;

		public ContentUpdater _003C_003E4__this;

		private IReadOnlySet<string> keptFiles;

		public IReadOnlySet<string> _003C_003E3__keptFiles;

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
		public _003CBuildPruneList_003Ed__20(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if ((uint)(num - -4) <= 1u || num == 1)
			{
				try
				{
					if (num == -4 || num == 1)
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
				int num = _003C_003E1__state;
				ContentUpdater contentUpdater = _003C_003E4__this;
				if (num != 0)
				{
					if (num != 1)
					{
						return false;
					}
					_003C_003E1__state = -4;
					goto IL_0115;
				}
				_003C_003E1__state = -1;
				IReadOnlyList<string> readOnlyList = (string.IsNullOrWhiteSpace(courseId) ? ManagedContentRoots() : CourseOwnedRoots(courseId));
				_003C_003E7__wrap1 = readOnlyList.GetEnumerator();
				_003C_003E1__state = -3;
				goto IL_012f;
				IL_0115:
				while (_003C_003E7__wrap2.MoveNext())
				{
					string current = _003C_003E7__wrap2.Current;
					string text = contentUpdater.ToRepoRelativePath(current);
					if (text != null && !keptFiles.Contains(text) && AllowedContentPath(text) && (string.IsNullOrWhiteSpace(courseId) || IsCourseOwnedPath(courseId, text)))
					{
						_003C_003E2__current = text;
						_003C_003E1__state = 1;
						return true;
					}
				}
				_003C_003Em__Finally2();
				_003C_003E7__wrap2 = null;
				goto IL_012f;
				IL_012f:
				string text2;
				do
				{
					if (_003C_003E7__wrap1.MoveNext())
					{
						string current2 = _003C_003E7__wrap1.Current;
						text2 = contentUpdater.SafeAssetPath(current2);
						continue;
					}
					_003C_003Em__Finally1();
					_003C_003E7__wrap1 = null;
					return false;
				}
				while (text2 == null || !Directory.Exists(text2));
				_003C_003E7__wrap2 = Directory.EnumerateFiles(text2, "*", SearchOption.AllDirectories).GetEnumerator();
				_003C_003E1__state = -4;
				goto IL_0115;
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
			_003CBuildPruneList_003Ed__20 _003CBuildPruneList_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CBuildPruneList_003Ed__ = this;
			}
			else
			{
				_003CBuildPruneList_003Ed__ = new _003CBuildPruneList_003Ed__20(0)
				{
					_003C_003E4__this = _003C_003E4__this
				};
			}
			_003CBuildPruneList_003Ed__.keptFiles = _003C_003E3__keptFiles;
			_003CBuildPruneList_003Ed__.courseId = _003C_003E3__courseId;
			return _003CBuildPruneList_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	private const string SupportedSchemaVersion = "1";

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private static readonly Version CurrentHostVersion = typeof(ContentUpdater).Assembly.GetName().Version ?? new Version(0, 1, 0);

	private static readonly string CurrentHostSemanticVersion = $"{CurrentHostVersion.Major}.{CurrentHostVersion.Minor}.{Math.Max(0, CurrentHostVersion.Build)}";

	private readonly PathService _paths;

	private readonly GitHubRemoteClient _remote;

	public ContentUpdater(PathService paths, HttpClient? http = null)
	{
		_paths = paths;
		_remote = new GitHubRemoteClient(http ?? new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(20.0)
		});
	}

	public void RecoverInterruptedUpdate()
	{
		string updateRoot = Path.Combine(_paths.RepoRoot, ".content-update");
		string path = Path.Combine(updateRoot, "transaction.json");
		if (!File.Exists(path))
		{
			try
			{
				DeleteDirectoryUnderRepoIfExists(updateRoot);
				return;
			}
			catch
			{
				return;
			}
		}
		try
		{
			UpdateTransaction updateTransaction = JsonSerializer.Deserialize<UpdateTransaction>(File.ReadAllText(path), Options);
			if (updateTransaction != null)
			{
				Dictionary<string, string> backedUp = updateTransaction.BackedUp.ToDictionary<UpdateBackupEntry, string, string>((UpdateBackupEntry item) => item.Path, (UpdateBackupEntry item) => Path.Combine(updateRoot, item.Backup.Replace('/', Path.DirectorySeparatorChar)), StringComparer.OrdinalIgnoreCase);
				RollBackAppliedUpdate(backedUp, updateTransaction.Created.ToHashSet<string>(StringComparer.OrdinalIgnoreCase));
			}
		}
		finally
		{
			try
			{
				DeleteDirectoryUnderRepoIfExists(updateRoot);
			}
			catch
			{
			}
		}
	}

	public async Task<HostUpdateResult> CheckHostUpdateAsync(string? manifestUrl, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(manifestUrl))
		{
			return new HostUpdateResult
			{
				Success = true,
				Message = "当前版本暂不支持程序更新。"
			};
		}
		HostUpdateManifest hostUpdateManifest;
		try
		{
			hostUpdateManifest = await _remote.GetManifestAsync(new Uri(manifestUrl, UriKind.Absolute), Options, (HostUpdateManifest value) => value.AppVersion, (HostUpdateManifest value) => value.PublishedAt, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2)
		{
			return new HostUpdateResult
			{
				Success = false,
				Message = "无法获取程序更新清单：" + ex2.Message
			};
		}
		if (hostUpdateManifest == null || string.IsNullOrWhiteSpace(hostUpdateManifest.AppVersion))
		{
			return new HostUpdateResult
			{
				Success = false,
				Message = "程序更新清单不完整。",
				Manifest = hostUpdateManifest
			};
		}
		if (!string.Equals((hostUpdateManifest.SchemaVersion ?? "").Trim(), "1", StringComparison.OrdinalIgnoreCase))
		{
			return new HostUpdateResult
			{
				Success = false,
				Message = "程序更新格式版本不支持：" + hostUpdateManifest.SchemaVersion,
				Manifest = hostUpdateManifest
			};
		}
		if (CompareVersions(hostUpdateManifest.AppVersion, CurrentHostSemanticVersion) <= 0)
		{
			return new HostUpdateResult
			{
				Success = true,
				Message = "程序已经是最新版本：" + CurrentHostSemanticVersion,
				Manifest = hostUpdateManifest
			};
		}
		bool flag = string.IsNullOrWhiteSpace(hostUpdateManifest.DownloadUrl) || string.IsNullOrWhiteSpace(hostUpdateManifest.Sha256) || hostUpdateManifest.Sha256.Length != 64 || !hostUpdateManifest.Sha256.All(Uri.IsHexDigit);
		if (!flag)
		{
			long? size = hostUpdateManifest.Size;
			bool flag2 = ((!size.HasValue || size.GetValueOrDefault() <= 0) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return new HostUpdateResult
			{
				Success = false,
				Message = "程序更新清单缺少有效的下载地址、SHA-256 或文件大小。",
				Manifest = hostUpdateManifest
			};
		}
		string absoluteUri;
		try
		{
			absoluteUri = ResolveManifestRelativeUrl(manifestUrl, hostUpdateManifest.DownloadUrl).AbsoluteUri;
		}
		catch (Exception ex3)
		{
			return new HostUpdateResult
			{
				Success = false,
				Message = "程序更新下载地址无效：" + ex3.Message,
				Manifest = hostUpdateManifest
			};
		}
		return new HostUpdateResult
		{
			Success = true,
			UpdateAvailable = true,
			DownloadUrl = absoluteUri,
			Message = $"发现程序新版本 {hostUpdateManifest.AppVersion}，当前版本 {CurrentHostSemanticVersion}。点击“安装程序更新”继续。",
			Manifest = hostUpdateManifest
		};
	}

	public async Task<ContentUpdateResult> CheckAndDownloadAsync(string? manifestUrl, CancellationToken cancellationToken = default(CancellationToken))
	{
		return await CheckAndDownloadCoreAsync(manifestUrl, null, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<ContentUpdateResult> CheckAndDownloadCourseAsync(CourseDefinition course, CancellationToken cancellationToken = default(CancellationToken))
	{
		return await CheckAndDownloadCoreAsync(course.UpdateManifestUrl, course.Id, course.Version, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<ContentUpdateResult> CheckAndDownloadCoreAsync(string? manifestUrl, string? expectedCourseId, string? installedCourseVersion, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(manifestUrl))
		{
			return new ContentUpdateResult
			{
				Success = false,
				Message = "当前课程暂时没有配置在线更新。"
			};
		}
		ContentUpdateManifest manifest;
		try
		{
			manifest = await _remote.GetManifestAsync(new Uri(manifestUrl, UriKind.Absolute), Options, (ContentUpdateManifest value) => string.IsNullOrWhiteSpace(value.CourseVersion) ? value.ContentVersion : value.CourseVersion, (ContentUpdateManifest value) => value.PublishedAt, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2)
		{
			return new ContentUpdateResult
			{
				Success = false,
				Message = "无法获取课程更新清单：" + ex2.Message
			};
		}
		if (!string.IsNullOrWhiteSpace(expectedCourseId) && (manifest == null || string.IsNullOrWhiteSpace(manifest.CourseId) || string.IsNullOrWhiteSpace(manifest.CourseVersion) || !string.IsNullOrWhiteSpace(manifest.ContentVersion)))
		{
			return new ContentUpdateResult
			{
				Success = false,
				Message = "课程更新地址没有返回独立课程清单，已拒绝旧式全量更新。",
				Manifest = manifest
			};
		}
		bool isCourseUpdate = !string.IsNullOrWhiteSpace(manifest?.CourseId);
		string remoteVersion = ((!isCourseUpdate) ? manifest?.ContentVersion : manifest?.CourseVersion);
		if (manifest == null || manifest.Files.Count == 0 || string.IsNullOrWhiteSpace(remoteVersion))
		{
			return new ContentUpdateResult
			{
				Success = false,
				Message = "课程更新清单不完整。",
				Manifest = manifest
			};
		}
		if (!SemanticVersion.TryParse(remoteVersion, out SemanticVersion version))
		{
			return new ContentUpdateResult
			{
				Success = false,
				Message = "课程更新版本不是有效的 SemVer。",
				Manifest = manifest
			};
		}
		if (isCourseUpdate && !string.IsNullOrWhiteSpace(expectedCourseId) && !string.Equals(manifest.CourseId, expectedCourseId, StringComparison.OrdinalIgnoreCase))
		{
			return new ContentUpdateResult
			{
				Success = false,
				Message = $"课程更新清单属于 {manifest.CourseId}，当前课程是 {expectedCourseId}。",
				Manifest = manifest
			};
		}
		if (!string.Equals((manifest.SchemaVersion ?? "").Trim(), "1", StringComparison.OrdinalIgnoreCase))
		{
			return new ContentUpdateResult
			{
				Success = false,
				Message = "课程内容格式版本不支持：" + manifest.SchemaVersion,
				Manifest = manifest
			};
		}
		string normalizedMinimum;
		string normalizedMaximum;
		switch (EvaluateHostCompatibility(manifest.MinHostVersion, manifest.MaxHostVersionExclusive, isCourseUpdate, out normalizedMinimum, out normalizedMaximum))
		{
		case HostCompatibilityStatus.Invalid:
			return new ContentUpdateResult
			{
				Success = false,
				Message = "课程更新清单中的兼容程序版本范围无效。",
				Manifest = manifest
			};
		case HostCompatibilityStatus.BelowMinimum:
			return new ContentUpdateResult
			{
				Success = false,
				RequiresHostUpdate = true,
				RequiredHostVersion = normalizedMinimum,
				HostUpdateManifestUrl = manifest.HostUpdateManifestUrl,
				Message = $"这次课程更新需要程序版本 >= {normalizedMinimum}；当前版本是 {CurrentHostSemanticVersion}。请先更新程序。",
				Manifest = manifest
			};
		case HostCompatibilityStatus.AtOrAboveMaximum:
			return new ContentUpdateResult
			{
				Success = false,
				Message = $"这次课程更新支持的程序版本范围是 >= {normalizedMinimum}, < {normalizedMaximum}；当前版本是 {CurrentHostSemanticVersion}，无法安装这次课程更新。",
				Manifest = manifest
			};
		default:
		{
			string text = ((!isCourseUpdate) ? CurrentContentVersion() : (string.IsNullOrWhiteSpace(installedCourseVersion) ? CurrentCourseVersion(manifest.CourseId) : installedCourseVersion));
			if (!SemanticVersion.TryParse(text, out version))
			{
				return new ContentUpdateResult
				{
					Success = false,
					Message = "本地课程版本不是有效的 SemVer。",
					Manifest = manifest
				};
			}
			if (CompareVersions(remoteVersion, text) <= 0)
			{
				return new ContentUpdateResult
				{
					Success = true,
					Message = (isCourseUpdate ? (manifest.CourseId + " 已经是最新版本：" + text) : ("课程目录已经是最新版本：" + text)),
					Manifest = manifest
				};
			}
			string updateRoot = Path.Combine(_paths.RepoRoot, ".content-update");
			string stagingDir = Path.Combine(updateRoot, "staging");
			string backupDir = Path.Combine(updateRoot, "backup");
			try
			{
				DeleteDirectoryUnderRepoIfExists(updateRoot);
				Directory.CreateDirectory(stagingDir);
				IReadOnlyList<ContentUpdateFile> files = NormalizeManifestFiles(manifest);
				ValidateRequiredContentSet(manifest, files);
				foreach (ContentUpdateFile item in files)
				{
					await DownloadRawFile(manifestUrl, item, stagingDir, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				ValidateStagedJson(stagingDir);
				ValidateStagedCourseManifest(manifest, files, stagingDir);
				ApplyStagedUpdate(manifest, files, stagingDir, backupDir);
				return new ContentUpdateResult
				{
					Success = true,
					PendingActivation = true,
					Message = (isCourseUpdate ? (manifest.CourseId + " 已更新到 " + remoteVersion + "。") : ("课程目录已更新到 " + remoteVersion + "。")),
					Manifest = manifest
				};
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex4)
			{
				return new ContentUpdateResult
				{
					Success = false,
					Message = "课程更新失败：" + ex4.Message,
					Manifest = manifest
				};
			}
			finally
			{
				try
				{
					DeleteDirectoryUnderRepoIfExists(updateRoot);
				}
				catch
				{
				}
			}
		}
		}
	}

	private static IReadOnlyList<ContentUpdateFile> NormalizeManifestFiles(ContentUpdateManifest manifest)
	{
		List<ContentUpdateFile> list = new List<ContentUpdateFile>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ContentUpdateFile file in manifest.Files)
		{
			string text = NormalizeRawPath(file.Path);
			if (text == null || !AllowedContentPath(text))
			{
				throw new InvalidOperationException("disallowed update path: " + file.Path);
			}
			if (!string.IsNullOrWhiteSpace(manifest.CourseId) && !IsCourseOwnedPath(manifest.CourseId, text))
			{
				throw new InvalidOperationException("path is outside course " + manifest.CourseId + ": " + text);
			}
			if (string.IsNullOrWhiteSpace(file.Sha256))
			{
				throw new InvalidOperationException("missing checksum: " + text);
			}
			if (!hashSet.Add(text))
			{
				throw new InvalidOperationException("duplicate update path: " + text);
			}
			list.Add(new ContentUpdateFile
			{
				Path = text,
				Url = file.Url,
				Sha256 = file.Sha256
			});
		}
		return list;
	}

	private static void ValidateRequiredContentSet(ContentUpdateManifest manifest, IReadOnlyList<ContentUpdateFile> files)
	{
		HashSet<string> hashSet = files.Select((ContentUpdateFile f) => f.Path).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (!string.IsNullOrWhiteSpace(manifest.CourseId))
		{
			string text = manifest.CourseId.Trim();
			bool num = hashSet.Contains("courses/" + text + ".json") || hashSet.Contains("courses/" + text + "/course.json");
			bool flag = hashSet.Contains("stages/" + text + "/index.json") || hashSet.Contains("courses/" + text + "/stages/index.json");
			if (!num)
			{
				throw new InvalidOperationException($"course manifest must include courses/{text}.json or courses/{text}/course.json");
			}
			if (!flag)
			{
				throw new InvalidOperationException("course manifest must include the stage index for " + text);
			}
			return;
		}
		string[] array = new string[5] { "courses/index.json", "stages/index.json", "assets/i18n/zh-CN.json", "schemas/course.schema.json", "schemas/guide-stage.schema.json" };
		foreach (string text2 in array)
		{
			if (!hashSet.Contains(text2))
			{
				throw new InvalidOperationException("manifest must include " + text2);
			}
		}
	}

	private static void ValidateStagedJson(string stagingDir)
	{
		foreach (string item in Directory.EnumerateFiles(stagingDir, "*.json", SearchOption.AllDirectories))
		{
			using FileStream utf8Json = File.OpenRead(item);
			try
			{
				using (JsonDocument.Parse(utf8Json))
				{
				}
			}
			catch (JsonException innerException)
			{
				string relativePath = Path.GetRelativePath(stagingDir, item);
				throw new InvalidOperationException("invalid staged json: " + relativePath, innerException);
			}
		}
	}

	private static void ValidateStagedCourseManifest(ContentUpdateManifest manifest, IReadOnlyList<ContentUpdateFile> files, string stagingDir)
	{
		if (string.IsNullOrWhiteSpace(manifest.CourseId))
		{
			return;
		}
		string courseId = NormalizeCourseId(manifest.CourseId);
		ContentUpdateFile contentUpdateFile = files.FirstOrDefault((ContentUpdateFile file) => string.Equals(file.Path, "courses/" + courseId + ".json", StringComparison.OrdinalIgnoreCase) || string.Equals(file.Path, "courses/" + courseId + "/course.json", StringComparison.OrdinalIgnoreCase));
		if (contentUpdateFile == null)
		{
			throw new InvalidOperationException("staged course manifest is missing");
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(stagingDir, contentUpdateFile.Path.Replace('/', Path.DirectorySeparatorChar))));
		JsonElement rootElement = jsonDocument.RootElement;
		JsonElement value;
		string? a = (rootElement.TryGetProperty("id", out value) ? value.GetString() : null);
		JsonElement value2;
		string text = (rootElement.TryGetProperty("version", out value2) ? value2.GetString() : null);
		if (!string.Equals(a, courseId, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("staged course id does not match the update manifest");
		}
		if (!string.Equals(text, manifest.CourseVersion, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("staged course version does not match the update manifest");
		}
		if (!SemanticVersion.TryParse(text, out SemanticVersion _))
		{
			throw new InvalidOperationException("staged course version is not valid SemVer");
		}
		if (!rootElement.TryGetProperty("compatibility", out var value3) || value3.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidOperationException("staged course compatibility is missing");
		}
		JsonElement value4;
		string a2 = ((!value3.TryGetProperty("minHostVersion", out value4)) ? null : value4.GetString()?.Trim());
		JsonElement value5;
		string a3 = ((!value3.TryGetProperty("maxHostVersionExclusive", out value5)) ? null : value5.GetString()?.Trim());
		if (!string.Equals(a2, manifest.MinHostVersion.Trim(), StringComparison.Ordinal) || !string.Equals(a3, manifest.MaxHostVersionExclusive?.Trim(), StringComparison.Ordinal))
		{
			throw new InvalidOperationException("staged course compatibility does not match the update manifest");
		}
		if (!rootElement.TryGetProperty("content", out var value6) || value6.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidOperationException("staged course content paths are missing");
		}
		string[] array = new string[4] { "stagesDir", "i18nDir", "assetsDir", "stageIndex" };
		foreach (string text2 in array)
		{
			JsonElement value7;
			string text3 = NormalizeRawPath(value6.TryGetProperty(text2, out value7) ? value7.GetString() : null);
			if (text3 == null || !IsCourseOwnedPath(courseId, text3 + "/.boundary-check"))
			{
				throw new InvalidOperationException("staged course " + text2 + " is outside its namespace");
			}
		}
	}

	private async Task DownloadRawFile(string manifestUrl, ContentUpdateFile file, string stagingDir, CancellationToken cancellationToken)
	{
		string rel = file.Path;
		Uri uri = ResolveRawFileUrl(manifestUrl, file.Url, rel);
		byte[] bytes = await _remote.DownloadVerifiedAsync(uri, file.Sha256 ?? "", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string fullPath = Path.GetFullPath(Path.Combine(stagingDir, rel.Replace('/', Path.DirectorySeparatorChar)));
		string value = Path.GetFullPath(stagingDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("path traversal: " + rel);
		}
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
		await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void ApplyStagedUpdate(ContentUpdateManifest manifest, IReadOnlyList<ContentUpdateFile> files, string stagingDir, string backupDir)
	{
		HashSet<string> hashSet = files.Select((ContentUpdateFile f) => f.Path).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet2 = NormalizeDeletePaths(manifest.Delete, manifest.CourseId);
		if (manifest.Prune)
		{
			foreach (string item in BuildPruneList(hashSet, manifest.CourseId))
			{
				hashSet2.Add(item);
			}
		}
		Directory.CreateDirectory(backupDir);
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet3 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			foreach (string item2 in hashSet2.Concat(hashSet).Distinct<string>(StringComparer.OrdinalIgnoreCase))
			{
				string text = SafeAssetPath(item2) ?? throw new InvalidOperationException("invalid update path: " + item2);
				if (File.Exists(text))
				{
					string text2 = Path.Combine(backupDir, item2.Replace('/', Path.DirectorySeparatorChar));
					Directory.CreateDirectory(Path.GetDirectoryName(text2));
					File.Copy(text, text2, overwrite: true);
					dictionary[item2] = text2;
				}
			}
			foreach (ContentUpdateFile file in files)
			{
				if (!File.Exists(SafeAssetPath(file.Path) ?? throw new InvalidOperationException("invalid staged path: " + file.Path)) && !dictionary.ContainsKey(file.Path))
				{
					hashSet3.Add(file.Path);
				}
			}
			WriteTransactionRecord(backupDir, dictionary, hashSet3);
			foreach (string item3 in hashSet2)
			{
				string path = SafeAssetPath(item3) ?? throw new InvalidOperationException("invalid delete path: " + item3);
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			foreach (ContentUpdateFile file2 in files)
			{
				string sourceFileName = Path.Combine(stagingDir, file2.Path.Replace('/', Path.DirectorySeparatorChar));
				string text3 = SafeAssetPath(file2.Path) ?? throw new InvalidOperationException("invalid staged path: " + file2.Path);
				Directory.CreateDirectory(Path.GetDirectoryName(text3));
				File.Copy(sourceFileName, text3, overwrite: true);
			}
		}
		catch
		{
			RollBackAppliedUpdate(dictionary, hashSet3);
			throw;
		}
		finally
		{
			CleanupEmptyManagedDirectories();
		}
	}

	private static void WriteTransactionRecord(string backupDir, IReadOnlyDictionary<string, string> backedUp, IReadOnlySet<string> created)
	{
		string updateRoot = Directory.GetParent(backupDir)?.FullName ?? throw new InvalidOperationException("invalid backup directory");
		UpdateTransaction value = new UpdateTransaction
		{
			BackedUp = (from kv in backedUp.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> kv) => kv.Key, StringComparer.OrdinalIgnoreCase)
				select new UpdateBackupEntry
				{
					Path = kv.Key,
					Backup = Path.GetRelativePath(updateRoot, kv.Value).Replace(Path.DirectorySeparatorChar, '/')
				}).ToList(),
			Created = created.OrderBy<string, string>((string v) => v, StringComparer.OrdinalIgnoreCase).ToList()
		};
		File.WriteAllText(Path.Combine(updateRoot, "transaction.json"), JsonSerializer.Serialize(value, Options));
	}

	private HashSet<string> NormalizeDeletePaths(IEnumerable<string> rawPaths, string? courseId)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string rawPath in rawPaths)
		{
			string text = NormalizeRawPath(rawPath);
			if (text == null || !AllowedContentPath(text) || text == "manifest.json")
			{
				throw new InvalidOperationException("disallowed delete path: " + rawPath);
			}
			if (!string.IsNullOrWhiteSpace(courseId) && !IsCourseOwnedPath(courseId, text))
			{
				throw new InvalidOperationException("delete path is outside course " + courseId + ": " + text);
			}
			hashSet.Add(text);
		}
		return hashSet;
	}

	[IteratorStateMachine(typeof(_003CBuildPruneList_003Ed__20))]
	private IEnumerable<string> BuildPruneList(IReadOnlySet<string> keptFiles, string? courseId)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CBuildPruneList_003Ed__20(-2)
		{
			_003C_003E4__this = this,
			_003C_003E3__keptFiles = keptFiles,
			_003C_003E3__courseId = courseId
		};
	}

	private static IReadOnlyList<string> ManagedContentRoots()
	{
		return new string[5] { "courses", "stages", "assets/i18n", "assets/media", "schemas" };
	}

	private static IReadOnlyList<string> CourseOwnedRoots(string courseId)
	{
		string text = NormalizeCourseId(courseId);
		return new string[4]
		{
			"courses/" + text,
			"stages/" + text,
			"assets/i18n/" + text,
			"assets/media/" + text
		};
	}

	private void RollBackAppliedUpdate(IReadOnlyDictionary<string, string> backedUp, IReadOnlySet<string> created)
	{
		foreach (string item in created)
		{
			string text = SafeAssetPath(item);
			if (text != null && File.Exists(text))
			{
				File.Delete(text);
			}
		}
		foreach (KeyValuePair<string, string> item2 in backedUp)
		{
			item2.Deconstruct(out var key, out var value);
			string rel = key;
			string text2 = value;
			string text3 = SafeAssetPath(rel);
			if (text3 != null && File.Exists(text2))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(text3));
				File.Copy(text2, text3, overwrite: true);
			}
		}
	}

	private void CleanupEmptyManagedDirectories()
	{
		foreach (string item in ManagedContentRoots())
		{
			string text = SafeAssetPath(item);
			if (text == null || !Directory.Exists(text))
			{
				continue;
			}
			foreach (string item2 in from d in Directory.EnumerateDirectories(text, "*", SearchOption.AllDirectories)
				orderby d.Length descending
				select d)
			{
				try
				{
					if (!Directory.EnumerateFileSystemEntries(item2).Any())
					{
						Directory.Delete(item2);
					}
				}
				catch
				{
				}
			}
		}
	}

	private string CurrentContentVersion()
	{
		try
		{
			CourseIndex courseIndex = JsonSerializer.Deserialize<CourseIndex>(File.ReadAllText(Path.Combine(_paths.ContentRoot, "courses", "index.json")), Options);
			if (!string.IsNullOrWhiteSpace(courseIndex?.CatalogVersion))
			{
				return courseIndex.CatalogVersion;
			}
			if (!string.IsNullOrWhiteSpace(courseIndex?.ContentVersion))
			{
				return courseIndex.ContentVersion;
			}
			string text = courseIndex?.Courses.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(text))
			{
				return "0.0.0";
			}
			string text2 = SafeAssetPath(text);
			if (text2 == null || !File.Exists(text2))
			{
				return "0.0.0";
			}
			CourseManifest courseManifest = JsonSerializer.Deserialize<CourseManifest>(File.ReadAllText(text2), Options);
			return string.IsNullOrWhiteSpace(courseManifest?.Version) ? "0.0.0" : courseManifest.Version;
		}
		catch
		{
			return "0.0.0";
		}
	}

	private string CurrentCourseVersion(string courseId)
	{
		try
		{
			string text = NormalizeCourseId(courseId);
			string[] array = new string[2]
			{
				"courses/" + text + ".json",
				"courses/" + text + "/course.json"
			};
			foreach (string rel in array)
			{
				string text2 = SafeAssetPath(rel);
				if (text2 != null && File.Exists(text2))
				{
					CourseManifest courseManifest = JsonSerializer.Deserialize<CourseManifest>(File.ReadAllText(text2), Options);
					if (!string.IsNullOrWhiteSpace(courseManifest?.Version))
					{
						return courseManifest.Version;
					}
				}
			}
		}
		catch
		{
		}
		return "0.0.0";
	}

	private Uri ResolveRawFileUrl(string manifestUrl, string? fileUrl, string rel)
	{
		string text = fileUrl;
		if (string.IsNullOrWhiteSpace(text))
		{
			Uri uri = new Uri(manifestUrl);
			text = ((uri.AbsolutePath.Contains("/courses/", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.EndsWith("/update.json", StringComparison.OrdinalIgnoreCase)) ? ("../../" + rel) : rel);
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out Uri result))
		{
			return new Uri(new Uri(manifestUrl), text);
		}
		return result;
	}

	private static Uri ResolveManifestRelativeUrl(string manifestUrl, string value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out Uri result))
		{
			return new Uri(new Uri(manifestUrl), value);
		}
		return result;
	}

	private string? SafeAssetPath(string rel)
	{
		string text = NormalizeRawPath(rel);
		if (text == null || (!AllowedContentPath(text) && !ManagedContentRoots().Contains<string>(text, StringComparer.OrdinalIgnoreCase)))
		{
			return null;
		}
		string fullPath = Path.GetFullPath(Path.Combine(_paths.RepoRoot, text.Replace('/', Path.DirectorySeparatorChar)));
		string value = Path.GetFullPath(_paths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return fullPath;
	}

	private string? ToRepoRelativePath(string fullPath)
	{
		string fullPath2 = Path.GetFullPath(fullPath);
		string text = Path.GetFullPath(_paths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath2.StartsWith(text, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		string text2 = fullPath2;
		int length = text.Length;
		return text2.Substring(length, text2.Length - length).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
	}

	private static string? NormalizeRawPath(string? rel)
	{
		if (string.IsNullOrWhiteSpace(rel))
		{
			return null;
		}
		string text = rel.Replace('\\', '/').TrimStart('/').Trim();
		if (Path.IsPathRooted(text) || text.Split('/').Any((string p) => ((p != null && p.Length == 0) || p == "..") ? true : false))
		{
			return null;
		}
		return text;
	}

	private static bool AllowedContentPath(string rel)
	{
		string text = rel.ToLowerInvariant();
		if (text.StartsWith("assets/targets/", StringComparison.Ordinal) || text.Contains("/assets/targets/", StringComparison.Ordinal))
		{
			return false;
		}
		if (!(text == "manifest.json") && !text.StartsWith("courses/", StringComparison.Ordinal) && !text.StartsWith("stages/", StringComparison.Ordinal) && !text.StartsWith("assets/i18n/", StringComparison.Ordinal) && !text.StartsWith("assets/media/", StringComparison.Ordinal) && !text.StartsWith("schemas/", StringComparison.Ordinal))
		{
			return false;
		}
		if (text.EndsWith(".pdb", StringComparison.Ordinal) || text.EndsWith(".id0", StringComparison.Ordinal) || text.EndsWith(".id1", StringComparison.Ordinal) || text.EndsWith(".id2", StringComparison.Ordinal) || text.EndsWith(".nam", StringComparison.Ordinal) || text.EndsWith(".til", StringComparison.Ordinal) || text.EndsWith(".idb", StringComparison.Ordinal) || text.EndsWith(".i64", StringComparison.Ordinal))
		{
			return false;
		}
		return !text.EndsWith(".exe", StringComparison.Ordinal) && !text.EndsWith(".dll", StringComparison.Ordinal) && !text.EndsWith(".deps.json", StringComparison.Ordinal) && !text.EndsWith(".runtimeconfig.json", StringComparison.Ordinal);
	}

	private static bool IsCourseOwnedPath(string courseId, string rel)
	{
		string text = NormalizeCourseId(courseId);
		string text2 = NormalizeRawPath(rel) ?? string.Empty;
		string value = "courses/" + text + "/assets/targets/";
		if (text2.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!string.Equals(text2, "courses/" + text + ".json", StringComparison.OrdinalIgnoreCase) && !string.Equals(text2, "courses/" + text + "/course.json", StringComparison.OrdinalIgnoreCase) && !text2.StartsWith("courses/" + text + "/", StringComparison.OrdinalIgnoreCase) && !text2.StartsWith("stages/" + text + "/", StringComparison.OrdinalIgnoreCase) && !text2.StartsWith("assets/i18n/" + text + "/", StringComparison.OrdinalIgnoreCase))
		{
			return text2.StartsWith("assets/media/" + text + "/", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static string NormalizeCourseId(string courseId)
	{
		string text = courseId.Trim();
		if (text.Length == 0 || text.Contains('/') || text.Contains('\\') || text.Contains("..", StringComparison.Ordinal))
		{
			throw new InvalidOperationException("invalid course id: " + courseId);
		}
		return text;
	}

	private static bool Sha256Matches(byte[] bytes, string? expected)
	{
		if (string.IsNullOrWhiteSpace(expected))
		{
			return false;
		}
		return string.Equals(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), expected.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
	}

	private static HostCompatibilityStatus EvaluateHostCompatibility(string? minimum, string? maximumExclusive, bool requireMaximum, out string normalizedMinimum, out string? normalizedMaximum)
	{
		normalizedMinimum = (string.IsNullOrWhiteSpace(minimum) ? "0.0.0" : minimum.Trim());
		normalizedMaximum = (string.IsNullOrWhiteSpace(maximumExclusive) ? null : maximumExclusive.Trim());
		if (!SemanticVersion.TryParse(normalizedMinimum, out SemanticVersion version) || !SemanticVersion.TryParse(CurrentHostSemanticVersion, out SemanticVersion version2))
		{
			return HostCompatibilityStatus.Invalid;
		}
		if (normalizedMaximum == null)
		{
			if (requireMaximum)
			{
				return HostCompatibilityStatus.Invalid;
			}
			if (version2.CompareTo(version) >= 0)
			{
				return HostCompatibilityStatus.Compatible;
			}
			return HostCompatibilityStatus.BelowMinimum;
		}
		if (!SemanticVersion.TryParse(normalizedMaximum, out SemanticVersion version3) || version.CompareTo(version3) >= 0)
		{
			return HostCompatibilityStatus.Invalid;
		}
		if (version2.CompareTo(version) < 0)
		{
			return HostCompatibilityStatus.BelowMinimum;
		}
		if (version2.CompareTo(version3) < 0)
		{
			return HostCompatibilityStatus.Compatible;
		}
		return HostCompatibilityStatus.AtOrAboveMaximum;
	}

	private static int CompareVersions(string? left, string? right)
	{
		return SemanticVersion.Compare(left, right);
	}

	private void DeleteDirectoryUnderRepoIfExists(string path)
	{
		if (Directory.Exists(path))
		{
			string value = Path.GetFullPath(_paths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			string fullPath = Path.GetFullPath(path);
			if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("refusing to delete outside content root");
			}
			Directory.Delete(fullPath, recursive: true);
		}
	}
}
