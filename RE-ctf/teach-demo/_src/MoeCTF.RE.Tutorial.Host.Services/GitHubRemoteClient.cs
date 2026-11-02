using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MoeCTF.RE.Tutorial.Host.Services;

internal sealed class GitHubRemoteClient
{
	private sealed record RemoteCandidate(Uri Uri, bool IsDirect, bool IsGitHub, bool UseGitHubRawHeaders = false);

	private sealed record MeasuredCandidate(RemoteCandidate Candidate, TimeSpan Latency);

	private sealed record SpeedMeasurement(TimeSpan Latency, DateTimeOffset MeasuredAt);

	private sealed record SeenManifest(object Value, SemanticVersion Version, DateTimeOffset PublishedAt);

	private sealed record ManifestCandidate<T>(T Value, SemanticVersion Version, DateTimeOffset PublishedAt, TimeSpan Latency, RemoteCandidate Candidate) where T : class;

	private sealed class SemanticVersionComparer : IComparer<SemanticVersion>
	{
		public static SemanticVersionComparer Instance { get; } = new SemanticVersionComparer();

		public int Compare(SemanticVersion? x, SemanticVersion? y)
		{
			if (x != null)
			{
				return x.CompareTo(y);
			}
			if (y != null)
			{
				return -1;
			}
			return 0;
		}
	}

	private const int ProbeBytes = 4096;

	private const int ManifestLimitBytes = 2097152;

	private const int RawFileLimitBytes = 134217728;

	private static readonly TimeSpan DirectTimeout = TimeSpan.FromMilliseconds(1200.0);

	private static readonly TimeSpan MirrorTimeout = TimeSpan.FromSeconds(8.0);

	private static readonly TimeSpan SpeedCacheLifetime = TimeSpan.FromMinutes(5.0);

	private readonly HttpClient _http;

	private readonly SemaphoreSlim _requestSlots = new SemaphoreSlim(4, 4);

	private readonly ConcurrentDictionary<string, SpeedMeasurement> _speedCache = new ConcurrentDictionary<string, SpeedMeasurement>(StringComparer.OrdinalIgnoreCase);

	private readonly ConcurrentDictionary<string, SeenManifest> _seenManifests = new ConcurrentDictionary<string, SeenManifest>(StringComparer.OrdinalIgnoreCase);

	public GitHubRemoteClient(HttpClient http)
	{
		_http = http;
	}

	public async Task<T> GetManifestAsync<T>(Uri uri, JsonSerializerOptions options, Func<T, string?> versionSelector, Func<T, string?> publishedAtSelector, CancellationToken cancellationToken) where T : class
	{
		IReadOnlyList<RemoteCandidate> readOnlyList = BuildManifestCandidates(uri);
		if (readOnlyList.Count == 1 && !readOnlyList[0].IsGitHub)
		{
			return DeserializeManifest<T>(await FetchBytesAsync(readOnlyList[0].Uri, 2097152, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false), options);
		}
		RemoteCandidate candidate2 = readOnlyList.First((RemoteCandidate candidate) => candidate.IsDirect);
		Task<ManifestCandidate<T>> task = FetchManifestCandidateSafeAsync(candidate2, DirectTimeout, options, versionSelector, publishedAtSelector, cancellationToken);
		using CancellationTokenSource mirrorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task<IReadOnlyList<ManifestCandidate<T>>> mirrorTask = FetchMirrorManifestCandidatesAsync(readOnlyList.Where((RemoteCandidate candidate) => !candidate.IsDirect).ToArray(), options, versionSelector, publishedAtSelector, mirrorCancellation.Token);
		string key = ManifestKey(uri);
		ManifestCandidate<T> directResult = await task.ConfigureAwait(continueOnCapturedContext: false);
		if ((object)directResult != null)
		{
			mirrorCancellation.Cancel();
			try
			{
				await mirrorTask.ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
			}
			SeenManifest authoritative = new SeenManifest(directResult.Value, directResult.Version, directResult.PublishedAt);
			_seenManifests.AddOrUpdate(key, authoritative, (string _, SeenManifest _) => authoritative);
			return directResult.Value;
		}
		ManifestCandidate<T> manifestCandidate = (await mirrorTask.ConfigureAwait(continueOnCapturedContext: false)).ToList().OrderByDescending((ManifestCandidate<T> result) => result.Version, SemanticVersionComparer.Instance).ThenByDescending((ManifestCandidate<T> result) => result.PublishedAt)
			.ThenBy((ManifestCandidate<T> result) => result.Latency)
			.FirstOrDefault();
		if ((object)manifestCandidate == null)
		{
			if (_seenManifests.TryGetValue(key, out SeenManifest value) && value.Value is T result2)
			{
				return result2;
			}
			throw new HttpRequestException("No usable GitHub manifest source responded.");
		}
		while (true)
		{
			if (!_seenManifests.TryGetValue(key, out SeenManifest value2))
			{
				if (_seenManifests.TryAdd(key, new SeenManifest(manifestCandidate.Value, manifestCandidate.Version, manifestCandidate.PublishedAt)))
				{
					return manifestCandidate.Value;
				}
				continue;
			}
			int num = manifestCandidate.Version.CompareTo(value2.Version);
			if (num < 0 || (num == 0 && manifestCandidate.PublishedAt < value2.PublishedAt))
			{
				return (T)value2.Value;
			}
			if (num == 0 && manifestCandidate.PublishedAt == value2.PublishedAt)
			{
				return manifestCandidate.Value;
			}
			if (_seenManifests.TryUpdate(key, new SeenManifest(manifestCandidate.Value, manifestCandidate.Version, manifestCandidate.PublishedAt), value2))
			{
				break;
			}
		}
		return manifestCandidate.Value;
	}

	public async Task<byte[]> DownloadVerifiedAsync(Uri uri, string expectedSha256, CancellationToken cancellationToken)
	{
		string expected = NormalizeSha256(expectedSha256);
		IReadOnlyList<RemoteCandidate> readOnlyList = BuildRawCandidates(uri);
		if (readOnlyList.Count == 1 && !readOnlyList[0].IsGitHub)
		{
			byte[] obj = await FetchBytesAsync(readOnlyList[0].Uri, 134217728, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			EnsureHash(obj, expected);
			return obj;
		}
		List<Exception> failures = new List<Exception>();
		RemoteCandidate remoteCandidate = readOnlyList.First((RemoteCandidate candidate) => candidate.IsDirect);
		using CancellationTokenSource mirrorProbeSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task<IReadOnlyList<MeasuredCandidate>> mirrorRankingTask = RankHealthyMirrorsAsync(readOnlyList.Where((RemoteCandidate candidate) => !candidate.IsDirect).ToArray(), mirrorProbeSource.Token);
		try
		{
			byte[] bytes = await FetchBytesAsync(remoteCandidate.Uri, 134217728, DirectTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			EnsureHash(bytes, expected);
			mirrorProbeSource.Cancel();
			try
			{
				await mirrorRankingTask.ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
			}
			return bytes;
		}
		catch (Exception ex2) when (!IsCallerCancellation(ex2, cancellationToken))
		{
			failures.Add(ex2);
		}
		foreach (MeasuredCandidate item in await mirrorRankingTask.ConfigureAwait(continueOnCapturedContext: false))
		{
			try
			{
				byte[] obj2 = await FetchBytesAsync(item.Candidate.Uri, 134217728, MirrorTimeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				EnsureHash(obj2, expected);
				return obj2;
			}
			catch (Exception ex3) when (!IsCallerCancellation(ex3, cancellationToken))
			{
				failures.Add(ex3);
			}
		}
		throw new HttpRequestException("No source returned the expected SHA-256.", new AggregateException(failures));
	}

	public async Task<IReadOnlyList<Uri>> GetReleaseDownloadCandidatesAsync(Uri uri, CancellationToken cancellationToken)
	{
		IReadOnlyList<RemoteCandidate> candidates = BuildReleaseCandidates(uri);
		if (candidates.Count == 1 && !candidates[0].IsGitHub)
		{
			return new Uri[1] { uri };
		}
		Task<MeasuredCandidate>[] directTasks = (from candidate in candidates
			where candidate.IsDirect
			select ProbeSafeAsync(candidate, DirectTimeout, cancellationToken)).ToArray();
		Task<IReadOnlyList<MeasuredCandidate>> mirrorTask = RankHealthyMirrorsAsync(candidates.Where((RemoteCandidate candidate) => !candidate.IsDirect).ToArray(), cancellationToken);
		await Task.WhenAll(directTasks.Cast<Task>().Append(mirrorTask)).ConfigureAwait(continueOnCapturedContext: false);
		List<MeasuredCandidate> list = (from task in directTasks
			select task.Result into result
			where (object)result != null
			select (result)).ToList();
		list.AddRange(mirrorTask.Result);
		List<Uri> list2 = (from candidate in list
			orderby candidate.Latency
			select candidate.Candidate.Uri).ToList();
		foreach (RemoteCandidate item in candidates)
		{
			if (!list2.Contains(item.Uri))
			{
				list2.Add(item.Uri);
			}
		}
		return list2;
	}

	private async Task<ManifestCandidate<T>?> FetchManifestCandidateSafeAsync<T>(RemoteCandidate candidate, TimeSpan timeout, JsonSerializerOptions options, Func<T, string?> versionSelector, Func<T, string?> publishedAtSelector, CancellationToken cancellationToken) where T : class
	{
		try
		{
			return ParseManifestCandidate(latency: Stopwatch.StartNew().Elapsed, bytes: await FetchBytesAsync(candidate.Uri, 2097152, timeout, cancellationToken, candidate.UseGitHubRawHeaders).ConfigureAwait(continueOnCapturedContext: false), options: options, versionSelector: versionSelector, publishedAtSelector: publishedAtSelector, candidate: candidate);
		}
		catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
		{
			return null;
		}
	}

	private async Task<IReadOnlyList<ManifestCandidate<T>>> FetchMirrorManifestCandidatesAsync<T>(IReadOnlyList<RemoteCandidate> candidates, JsonSerializerOptions options, Func<T, string?> versionSelector, Func<T, string?> publishedAtSelector, CancellationToken cancellationToken) where T : class
	{
		return (from result in await Task.WhenAll((await RankHealthyMirrorsAsync(candidates, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Select((MeasuredCandidate measured) => FetchManifestCandidateSafeAsync(measured.Candidate, MirrorTimeout, options, versionSelector, publishedAtSelector, cancellationToken))).ConfigureAwait(continueOnCapturedContext: false)
			where (object)result != null
			select (result)).ToArray();
	}

	private async Task<MeasuredCandidate?> ProbeSafeAsync(RemoteCandidate candidate, TimeSpan timeout, CancellationToken cancellationToken)
	{
		try
		{
			return await ProbeAsync(candidate, timeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
		{
			return null;
		}
	}

	private async Task<IReadOnlyList<MeasuredCandidate>> RankHealthyMirrorsAsync(IReadOnlyList<RemoteCandidate> candidates, CancellationToken cancellationToken)
	{
		return (from candidate in await Task.WhenAll(candidates.Select(async delegate(RemoteCandidate candidate)
			{
				string cacheKey = SpeedKey(candidate.Uri);
				if (_speedCache.TryGetValue(cacheKey, out SpeedMeasurement value) && DateTimeOffset.UtcNow - value.MeasuredAt < SpeedCacheLifetime)
				{
					return new MeasuredCandidate(candidate, value.Latency);
				}
				try
				{
					MeasuredCandidate measuredCandidate = await ProbeAsync(candidate, MirrorTimeout, cancellationToken);
					_speedCache[cacheKey] = new SpeedMeasurement(measuredCandidate.Latency, DateTimeOffset.UtcNow);
					return measuredCandidate;
				}
				catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
				{
					return (MeasuredCandidate)null;
				}
			})).ConfigureAwait(continueOnCapturedContext: false)
			where (object)candidate != null
			orderby candidate.Latency
			select (candidate)).ToArray();
	}

	private async Task<MeasuredCandidate> ProbeAsync(RemoteCandidate candidate, TimeSpan timeout, CancellationToken cancellationToken)
	{
		await _requestSlots.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			using (CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				timeoutSource.CancelAfter(timeout);
				using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, WithCacheBuster(candidate.Uri));
				request.Headers.Range = new RangeHeaderValue(0L, 4095L);
				Stopwatch stopwatch = Stopwatch.StartNew();
				using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token).ConfigureAwait(continueOnCapturedContext: false);
				response.EnsureSuccessStatusCode();
				MeasuredCandidate result;
				await using (Stream stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(continueOnCapturedContext: false))
				{
					byte[] array = new byte[4096];
					if (await stream.ReadAsync(array.AsMemory(), timeoutSource.Token).ConfigureAwait(continueOnCapturedContext: false) <= 0)
					{
						throw new InvalidDataException("Probe returned an empty response.");
					}
					result = new MeasuredCandidate(candidate, stopwatch.Elapsed);
				}
				return result;
			}
			IL_0433:;
		}
		finally
		{
			_requestSlots.Release();
		}
		throw null;
	}

	private async Task<byte[]> FetchBytesAsync(Uri uri, int maximumBytes, TimeSpan? timeout, CancellationToken cancellationToken, bool useGitHubRawHeaders = false)
	{
		await _requestSlots.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			using (CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				if (timeout.HasValue)
				{
					timeoutSource.CancelAfter(timeout.Value);
				}
				using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, WithCacheBuster(uri));
				if (useGitHubRawHeaders)
				{
					request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
					request.Headers.UserAgent.ParseAdd("MoeCTF.RE.Tutorial/0.1");
				}
				using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token).ConfigureAwait(continueOnCapturedContext: false);
				response.EnsureSuccessStatusCode();
				long? contentLength = response.Content.Headers.ContentLength;
				if (contentLength.HasValue)
				{
					long valueOrDefault = contentLength.GetValueOrDefault();
					if (valueOrDefault > maximumBytes)
					{
						throw new InvalidDataException($"Response exceeds {maximumBytes} bytes.");
					}
				}
				byte[] result;
				await using (Stream stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(continueOnCapturedContext: false))
				{
					result = await ReadLimitedAsync(stream, maximumBytes, timeoutSource.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
				return result;
			}
			IL_048e:;
		}
		finally
		{
			_requestSlots.Release();
		}
		throw null;
	}

	private static async Task<byte[]> ReadLimitedAsync(Stream stream, int maximumBytes, CancellationToken cancellationToken)
	{
		using MemoryStream output = new MemoryStream(Math.Min(maximumBytes, 65536));
		byte[] buffer = new byte[65536];
		while (true)
		{
			int num = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (num == 0)
			{
				return output.ToArray();
			}
			if (output.Length + num > maximumBytes)
			{
				break;
			}
			await output.WriteAsync(buffer.AsMemory(0, num), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		throw new InvalidDataException($"Response exceeds {maximumBytes} bytes.");
	}

	private static ManifestCandidate<T> ParseManifestCandidate<T>(byte[] bytes, JsonSerializerOptions options, Func<T, string?> versionSelector, Func<T, string?> publishedAtSelector, RemoteCandidate candidate, TimeSpan latency) where T : class
	{
		T val = DeserializeManifest<T>(bytes, options);
		if (!SemanticVersion.TryParse(versionSelector(val), out SemanticVersion version))
		{
			throw new InvalidDataException("Manifest version is not valid SemVer.");
		}
		DateTimeOffset result = DateTimeOffset.MinValue;
		string text = publishedAtSelector(val);
		if (!string.IsNullOrWhiteSpace(text))
		{
			DateTimeOffset.TryParse(text, out result);
		}
		return new ManifestCandidate<T>(val, version, result, latency, candidate);
	}

	private static T DeserializeManifest<T>(byte[] bytes, JsonSerializerOptions options) where T : class
	{
		return JsonSerializer.Deserialize<T>(TrimUtf8Bom(bytes), options) ?? throw new InvalidDataException("Manifest JSON is empty.");
	}

	private static ReadOnlySpan<byte> TrimUtf8Bom(byte[] bytes)
	{
		return bytes.AsSpan().StartsWith("\ufeff"u8) ? bytes.AsSpan(3) : ((Span<byte>)bytes);
	}

	private static string NormalizeSha256(string value)
	{
		string text = value.Trim().ToLowerInvariant();
		if (text.Length != 64 || !text.All(Uri.IsHexDigit))
		{
			throw new InvalidDataException("Expected SHA-256 is invalid.");
		}
		return text;
	}

	private static void EnsureHash(byte[] bytes, string expected)
	{
		if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), expected, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("SHA-256 mismatch.");
		}
	}

	private static bool IsCallerCancellation(Exception exception, CancellationToken cancellationToken)
	{
		if (exception is OperationCanceledException)
		{
			return cancellationToken.IsCancellationRequested;
		}
		return false;
	}

	private static IReadOnlyList<RemoteCandidate> BuildRawCandidates(Uri uri)
	{
		if (TryParseGitHubRaw(uri, out string owner, out string repository, out string reference, out string path))
		{
			string text = string.Join('/', new string[3] { owner, repository, reference }.Concat(path.Split('/')).Select(Uri.EscapeDataString));
			string text2 = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
			string text3 = Uri.EscapeDataString(owner) + "/" + Uri.EscapeDataString(repository) + "@" + Uri.EscapeDataString(reference) + "/" + text2;
			Uri uri2 = (uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ? uri : new Uri("https://raw.githubusercontent.com/" + text));
			return new RemoteCandidate[4]
			{
				new RemoteCandidate(uri2, IsDirect: true, IsGitHub: true),
				new RemoteCandidate(new Uri("https://cdn.jsdelivr.net/gh/" + text3), IsDirect: false, IsGitHub: true),
				new RemoteCandidate(new Uri("https://fastly.jsdelivr.net/gh/" + text3), IsDirect: false, IsGitHub: true),
				new RemoteCandidate(new Uri("https://gcore.jsdelivr.net/gh/" + text3), IsDirect: false, IsGitHub: true)
			};
		}
		return new RemoteCandidate[1]
		{
			new RemoteCandidate(uri, IsDirect: true, IsGitHub: false)
		};
	}

	private static IReadOnlyList<RemoteCandidate> BuildManifestCandidates(Uri uri)
	{
		IReadOnlyList<RemoteCandidate> readOnlyList = BuildRawCandidates(uri);
		if (!TryParseGitHubRaw(uri, out string owner, out string repository, out string reference, out string path))
		{
			return readOnlyList;
		}
		string value = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
		Uri uri2 = new Uri($"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/contents/{value}?ref={Uri.EscapeDataString(reference)}");
		return new RemoteCandidate[1]
		{
			new RemoteCandidate(uri2, IsDirect: true, IsGitHub: true, UseGitHubRawHeaders: true)
		}.Concat(readOnlyList.Select((RemoteCandidate candidate) => candidate with
		{
			IsDirect = false
		})).ToArray();
	}

	private static IReadOnlyList<RemoteCandidate> BuildReleaseCandidates(Uri uri)
	{
		if (IsGitHubRelease(uri))
		{
			return new RemoteCandidate[2]
			{
				new RemoteCandidate(uri, IsDirect: true, IsGitHub: true),
				new RemoteCandidate(new Uri("https://gh-proxy.com/" + uri.AbsoluteUri), IsDirect: false, IsGitHub: true)
			};
		}
		return new RemoteCandidate[1]
		{
			new RemoteCandidate(uri, IsDirect: true, IsGitHub: false)
		};
	}

	private static bool TryParseGitHubRaw(Uri uri, out string owner, out string repository, out string reference, out string path)
	{
		owner = (repository = (reference = (path = "")));
		string[] array = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (string.Equals(uri.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) && array.Length >= 4)
		{
			owner = array[0];
			repository = array[1];
			if (array.Length >= 6 && string.Equals(array[2], "refs", StringComparison.OrdinalIgnoreCase) && (string.Equals(array[3], "heads", StringComparison.OrdinalIgnoreCase) || string.Equals(array[3], "tags", StringComparison.OrdinalIgnoreCase)))
			{
				reference = array[4];
				path = string.Join('/', array.Skip(5));
			}
			else
			{
				reference = array[2];
				path = string.Join('/', array.Skip(3));
			}
			return true;
		}
		if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) && array.Length >= 5 && string.Equals(array[2], "raw", StringComparison.OrdinalIgnoreCase))
		{
			owner = array[0];
			repository = array[1];
			reference = array[3];
			path = string.Join('/', array.Skip(4));
			return true;
		}
		if (IsJsDelivrHost(uri.Host) && array.Length >= 4 && string.Equals(array[0], "gh", StringComparison.OrdinalIgnoreCase))
		{
			int num = array[2].LastIndexOf('@');
			if (num <= 0 || num == array[2].Length - 1)
			{
				return false;
			}
			owner = array[1];
			repository = array[2].Substring(0, num);
			string text = array[2];
			int num2 = num + 1;
			reference = text.Substring(num2, text.Length - num2);
			path = string.Join('/', array.Skip(3));
			return true;
		}
		return false;
	}

	private static bool IsJsDelivrHost(string host)
	{
		if (!host.Equals("cdn.jsdelivr.net", StringComparison.OrdinalIgnoreCase) && !host.Equals("fastly.jsdelivr.net", StringComparison.OrdinalIgnoreCase))
		{
			return host.Equals("gcore.jsdelivr.net", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsGitHubRelease(Uri uri)
	{
		if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
		{
			return uri.AbsolutePath.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static Uri WithCacheBuster(Uri uri)
	{
		UriBuilder uriBuilder = new UriBuilder(uri);
		string text = "moectfCacheBust=" + Uri.EscapeDataString(Guid.NewGuid().ToString("N"));
		uriBuilder.Query = (string.IsNullOrWhiteSpace(uriBuilder.Query) ? text : (uriBuilder.Query.TrimStart('?') + "&" + text));
		return uriBuilder.Uri;
	}

	private static string ManifestKey(Uri uri)
	{
		return uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
	}

	private static string SpeedKey(Uri uri)
	{
		return uri.Scheme.ToLowerInvariant() + "://" + uri.Authority.ToLowerInvariant();
	}
}
