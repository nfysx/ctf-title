using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed class HostUpdateInstaller
{
	private sealed class HostUpdateRequest
	{
		[JsonPropertyName("schemaVersion")]
		public string SchemaVersion { get; set; } = "1";

		[JsonPropertyName("dataRoot")]
		public string DataRoot { get; set; } = "";

		[JsonPropertyName("downloadedExecutablePath")]
		public string DownloadedExecutablePath { get; set; } = "";

		[JsonPropertyName("targetExecutablePath")]
		public string TargetExecutablePath { get; set; } = "";

		[JsonPropertyName("expectedSize")]
		public long ExpectedSize { get; set; }

		[JsonPropertyName("expectedSha256")]
		public string ExpectedSha256 { get; set; } = "";

		[JsonPropertyName("oldProcessId")]
		public int OldProcessId { get; set; }

		[JsonPropertyName("oldProcessStartTimeUtcTicks")]
		public long OldProcessStartTimeUtcTicks { get; set; }

		[JsonPropertyName("createdAtUtc")]
		public DateTimeOffset CreatedAtUtc { get; set; }
	}

	public const string ApplyUpdateArgument = "--apply-update";

	public const string ProductExecutableName = "MoeCTF.RE.Tutorial.exe";

	public const string ProductDataDirectoryName = "MoeCTF.RE.Tutorial";

	private const string RequestSchemaVersion = "1";

	private static readonly TimeSpan OldProcessExitTimeout = TimeSpan.FromMinutes(2.0);

	private static readonly TimeSpan ApplyWorkerReadyTimeout = TimeSpan.FromSeconds(10.0);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private readonly HttpClient _httpClient;

	private readonly GitHubRemoteClient _remoteClient;

	public HostUpdateInstaller(HttpClient? httpClient = null)
	{
		_httpClient = httpClient ?? new HttpClient
		{
			Timeout = TimeSpan.FromMinutes(5.0)
		};
		_remoteClient = new GitHubRemoteClient(_httpClient);
	}

	public static string GetDefaultDataRoot()
	{
		return Path.Combine(AppContext.BaseDirectory, "MoeCTF.RE.Tutorial");
	}

	public static bool ShouldShowApplyFailureDialog(string requestPath)
	{
		try
		{
			return File.Exists(ReadyMarkerPath(Path.GetFullPath(requestPath)));
		}
		catch
		{
			return true;
		}
	}

	public async Task<HostUpdateInstallResult> DownloadAsync(HostUpdateResult update, string dataRoot, CancellationToken cancellationToken = default(CancellationToken))
	{
		string text = ValidateUpdate(update);
		if (text != null)
		{
			return HostUpdateInstallResult.Failed(text);
		}
		string updatesRoot;
		try
		{
			updatesRoot = GetUpdatesRoot(dataRoot);
			Directory.CreateDirectory(updatesRoot);
		}
		catch (Exception ex)
		{
			return HostUpdateInstallResult.Failed("无法创建程序更新目录：" + ex.Message);
		}
		HostUpdateManifest manifest = update.Manifest;
		string value = SanitizeFileNamePart(manifest.AppVersion);
		string finalPath = Path.Combine(updatesRoot, $"host-{value}-{manifest.Sha256.Substring(0, 12).ToLowerInvariant()}.exe");
		string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".download";
		try
		{
			if (File.Exists(finalPath) && (await VerifyFileAsync(finalPath, manifest.Size.Value, manifest.Sha256, cancellationToken)).Success)
			{
				return new HostUpdateInstallResult
				{
					Success = true,
					Message = "程序更新已经下载完成。",
					DownloadedExecutablePath = finalPath
				};
			}
			IReadOnlyList<Uri> readOnlyList = await _remoteClient.GetReleaseDownloadCandidatesAsync(new Uri(update.DownloadUrl, UriKind.Absolute), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			Exception inner = null;
			foreach (Uri item in readOnlyList)
			{
				try
				{
					await DownloadCandidateAsync(item, temporaryPath, manifest.Size.Value, manifest.Sha256, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					File.Move(temporaryPath, finalPath, overwrite: true);
					return new HostUpdateInstallResult
					{
						Success = true,
						Message = "程序更新已经下载完成。",
						DownloadedExecutablePath = finalPath
					};
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception ex3)
				{
					inner = ex3;
					TryDeleteFile(temporaryPath);
				}
			}
			throw new HttpRequestException("所有程序更新下载源均校验失败。", inner);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			TryDeleteFile(temporaryPath);
			return HostUpdateInstallResult.Failed("程序更新下载已取消。");
		}
		catch (Exception ex5)
		{
			TryDeleteFile(temporaryPath);
			return HostUpdateInstallResult.Failed("程序更新下载失败：" + ex5.Message);
		}
	}

	private async Task DownloadCandidateAsync(Uri uri, string temporaryPath, long expectedSize, string expectedSha256, CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		response.EnsureSuccessStatusCode();
		long? contentLength = response.Content.Headers.ContentLength;
		if (contentLength.HasValue)
		{
			long valueOrDefault = contentLength.GetValueOrDefault();
			if (valueOrDefault > expectedSize)
			{
				throw new InvalidDataException("下载文件大于更新清单声明的大小。");
			}
		}
		await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await using FileStream destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
		using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		byte[] buffer = new byte[131072];
		long totalBytes = 0L;
		while (true)
		{
			int num = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (num == 0)
			{
				break;
			}
			totalBytes += num;
			if (totalBytes > expectedSize)
			{
				throw new InvalidDataException("下载文件大于更新清单声明的大小。");
			}
			hash.AppendData(buffer, 0, num);
			await destination.WriteAsync(buffer.AsMemory(0, num), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		await destination.FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		destination.Flush(flushToDisk: true);
		if (totalBytes != expectedSize)
		{
			throw new InvalidDataException($"下载文件大小不一致：应为 {expectedSize} 字节，实际为 {totalBytes} 字节。");
		}
		byte[] hashAndReset = hash.GetHashAndReset();
		if (!CryptographicOperations.FixedTimeEquals(right: Convert.FromHexString(expectedSha256), left: hashAndReset))
		{
			throw new InvalidDataException("下载文件的 SHA-256 与更新清单不一致。");
		}
	}

	public HostUpdateInstallResult CreateRequestAndLaunch(HostUpdateResult update, string dataRoot, string downloadedExecutablePath, string targetExecutablePath, int? oldProcessId = null)
	{
		string text = ValidateUpdate(update);
		if (text != null)
		{
			return HostUpdateInstallResult.Failed(text);
		}
		string updatesRoot;
		string fullPath;
		string fullPath2;
		try
		{
			updatesRoot = GetUpdatesRoot(dataRoot);
			Directory.CreateDirectory(updatesRoot);
			fullPath = Path.GetFullPath(downloadedExecutablePath);
			fullPath2 = Path.GetFullPath(targetExecutablePath);
			EnsurePathIsInside(updatesRoot, fullPath, "下载的新程序不在更新目录内。");
			EnsureTargetExecutable(fullPath2);
		}
		catch (Exception ex)
		{
			return HostUpdateInstallResult.Failed("无法创建程序更新申请：" + ex.Message);
		}
		if (!File.Exists(fullPath))
		{
			return HostUpdateInstallResult.Failed("无法创建程序更新申请：下载的新程序不存在。");
		}
		int num = oldProcessId ?? Environment.ProcessId;
		long oldProcessStartTimeUtcTicks = 0L;
		try
		{
			using Process process = Process.GetProcessById(num);
			oldProcessStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
		}
		catch
		{
		}
		HostUpdateRequest value = new HostUpdateRequest
		{
			SchemaVersion = "1",
			DataRoot = Path.GetFullPath(dataRoot),
			DownloadedExecutablePath = fullPath,
			TargetExecutablePath = fullPath2,
			ExpectedSize = update.Manifest.Size.Value,
			ExpectedSha256 = update.Manifest.Sha256,
			OldProcessId = num,
			OldProcessStartTimeUtcTicks = oldProcessStartTimeUtcTicks,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};
		string text2 = Path.Combine(updatesRoot, "apply-" + Guid.NewGuid().ToString("N") + ".json");
		string text3 = text2 + ".request.tmp";
		try
		{
			TryDeleteFile(ReadyMarkerPath(text2));
			TryDeleteFile(FailureMarkerPath(text2));
			TryDeleteFile(LogPath(text2));
			File.WriteAllText(text3, JsonSerializer.Serialize(value, JsonOptions));
			File.Move(text3, text2);
			Process process2 = Process.Start(new ProcessStartInfo
			{
				FileName = fullPath,
				UseShellExecute = false,
				WorkingDirectory = Path.GetDirectoryName(fullPath2),
				ArgumentList = { "--apply-update", text2 }
			});
			if (process2 == null)
			{
				throw new InvalidOperationException("Windows 没有启动新版本程序。");
			}
			using (process2)
			{
				HostUpdateInstallResult hostUpdateInstallResult = WaitForApplyWorkerReady(process2, text2);
				if (!hostUpdateInstallResult.Success)
				{
					return hostUpdateInstallResult;
				}
			}
			return new HostUpdateInstallResult
			{
				Success = true,
				Message = "程序更新已经准备好，关闭当前窗口后会自动安装。",
				DownloadedExecutablePath = fullPath,
				RequestPath = text2
			};
		}
		catch (Exception ex2)
		{
			TryDeleteFile(text3);
			TryDeleteFile(text2);
			return HostUpdateInstallResult.Failed("无法启动程序更新：" + ex2.Message);
		}
	}

	public async Task<HostUpdateInstallResult> DownloadAndLaunchAsync(HostUpdateResult update, string dataRoot, string targetExecutablePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		HostUpdateInstallResult hostUpdateInstallResult = await DownloadAsync(update, dataRoot, cancellationToken);
		if (!hostUpdateInstallResult.Success || string.IsNullOrWhiteSpace(hostUpdateInstallResult.DownloadedExecutablePath))
		{
			return hostUpdateInstallResult;
		}
		return CreateRequestAndLaunch(update, dataRoot, hostUpdateInstallResult.DownloadedExecutablePath, targetExecutablePath, null);
	}

	public static async Task<HostUpdateInstallResult> ApplyUpdateAsync(string requestJsonPath, CancellationToken cancellationToken = default(CancellationToken))
	{
		string replacementTemporaryPath = null;
		string requestPath = null;
		try
		{
			requestPath = Path.GetFullPath(requestJsonPath);
			AppendApplyLog(requestPath, "apply worker started");
			if (!File.Exists(requestPath))
			{
				return HostUpdateInstallResult.Failed("更新申请文件不存在。");
			}
			HostUpdateRequest request = JsonSerializer.Deserialize<HostUpdateRequest>(await File.ReadAllTextAsync(requestPath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false), JsonOptions);
			if (request == null || !string.Equals(request.SchemaVersion, "1", StringComparison.Ordinal))
			{
				return FailApply(requestPath, "更新申请文件格式不受支持。");
			}
			string updatesRoot = GetUpdatesRoot(request.DataRoot);
			string fullPath = Path.GetFullPath(request.DownloadedExecutablePath);
			string targetPath = Path.GetFullPath(request.TargetExecutablePath);
			string processPath = Environment.ProcessPath;
			if (string.IsNullOrWhiteSpace(processPath))
			{
				return FailApply(requestPath, "无法确定当前更新程序的路径。");
			}
			string currentExecutablePath = Path.GetFullPath(processPath);
			EnsurePathIsInside(updatesRoot, requestPath, "更新申请文件不在更新目录内。");
			EnsurePathIsInside(updatesRoot, fullPath, "更新程序不在更新目录内。");
			EnsureTargetExecutable(targetPath);
			ValidateExpectedFile(request.ExpectedSize, request.ExpectedSha256);
			if (!string.Equals(currentExecutablePath, fullPath, StringComparison.OrdinalIgnoreCase))
			{
				return FailApply(requestPath, "执行更新的程序与申请文件记录不一致。");
			}
			HostUpdateInstallResult hostUpdateInstallResult = await VerifyFileAsync(currentExecutablePath, request.ExpectedSize, request.ExpectedSha256, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!hostUpdateInstallResult.Success)
			{
				return FailApply(requestPath, hostUpdateInstallResult.Message);
			}
			if (!File.Exists(targetPath))
			{
				return FailApply(requestPath, "要替换的旧版程序不存在：" + targetPath);
			}
			string targetDirectory = Path.GetDirectoryName(targetPath) ?? throw new InvalidOperationException("无法确定程序所在目录。");
			WriteReadyMarker(requestPath);
			AppendApplyLog(requestPath, "request verified; waiting for old process to exit");
			await WaitForOldProcessAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			AppendApplyLog(requestPath, "old process exited; copying replacement");
			replacementTemporaryPath = Path.Combine(targetDirectory, ".MoeCTF.RE.Tutorial.exe." + Guid.NewGuid().ToString("N") + ".tmp");
			HostUpdateInstallResult hostUpdateInstallResult2 = await CopyAndVerifyAsync(currentExecutablePath, replacementTemporaryPath, request.ExpectedSize, request.ExpectedSha256, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!hostUpdateInstallResult2.Success)
			{
				return FailApply(requestPath, hostUpdateInstallResult2.Message);
			}
			File.Move(replacementTemporaryPath, targetPath, overwrite: true);
			replacementTemporaryPath = null;
			AppendApplyLog(requestPath, "replacement committed; restarting application");
			Process process = Process.Start(new ProcessStartInfo
			{
				FileName = targetPath,
				UseShellExecute = false,
				WorkingDirectory = targetDirectory
			});
			if (process == null)
			{
				return FailApply(requestPath, "新版本已经安装，但无法自动重新启动。请手动打开程序。");
			}
			process.Dispose();
			MarkRequestCompleted(requestPath);
			TryDeleteFile(ReadyMarkerPath(requestPath));
			TryDeleteFile(FailureMarkerPath(requestPath));
			AppendApplyLog(requestPath, "update completed");
			return new HostUpdateInstallResult
			{
				Success = true,
				Message = "程序更新已经安装。"
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return FailApply(requestPath ?? requestJsonPath, "程序更新已取消。");
		}
		catch (Exception ex2)
		{
			return FailApply(requestPath ?? requestJsonPath, "程序更新安装失败：" + ex2.Message);
		}
		finally
		{
			if (replacementTemporaryPath != null)
			{
				TryDeleteFile(replacementTemporaryPath);
			}
		}
	}

	public static void CleanupCompletedUpdates(string dataRoot)
	{
		try
		{
			string updatesRoot = GetUpdatesRoot(dataRoot);
			if (!Directory.Exists(updatesRoot))
			{
				return;
			}
			foreach (string item in Directory.EnumerateFiles(updatesRoot, "*.completed.json"))
			{
				try
				{
					if (DateTime.UtcNow - File.GetLastWriteTimeUtc(item) < TimeSpan.FromMinutes(1.0))
					{
						continue;
					}
					HostUpdateRequest hostUpdateRequest = JsonSerializer.Deserialize<HostUpdateRequest>(File.ReadAllText(item), JsonOptions);
					if (hostUpdateRequest != null)
					{
						string fullPath = Path.GetFullPath(hostUpdateRequest.DownloadedExecutablePath);
						EnsurePathIsInside(updatesRoot, fullPath, "更新程序不在更新目录内。");
						if (!string.Equals(fullPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
						{
							TryDeleteFile(fullPath);
						}
					}
					TryDeleteFile(item);
				}
				catch
				{
				}
			}
			CleanupOldPartialFiles(updatesRoot, "*.download");
			CleanupOldPartialFiles(updatesRoot, "*.request.tmp");
			CleanupOldPartialFiles(updatesRoot, "apply-*.ready");
			CleanupOldPartialFiles(updatesRoot, "apply-*.failed.txt");
			CleanupOldPartialFiles(updatesRoot, "apply-*.log");
			CleanupOldPartialFiles(updatesRoot, "apply-*.json");
		}
		catch
		{
		}
	}

	private static string? ValidateUpdate(HostUpdateResult update)
	{
		if (!update.Success || !update.UpdateAvailable || update.Manifest == null)
		{
			return "没有可安装的程序更新。";
		}
		if (string.IsNullOrWhiteSpace(update.DownloadUrl) || !Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttps && result.Scheme != Uri.UriSchemeHttp))
		{
			return "程序更新下载地址无效。";
		}
		try
		{
			ValidateExpectedFile(update.Manifest.Size.GetValueOrDefault(), update.Manifest.Sha256);
		}
		catch (Exception ex)
		{
			return ex.Message;
		}
		return null;
	}

	private static void ValidateExpectedFile(long size, string? sha256)
	{
		if (size <= 0)
		{
			throw new InvalidDataException("更新清单中的程序大小无效。");
		}
		if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
		{
			throw new InvalidDataException("更新清单中的 SHA-256 无效。");
		}
	}

	private static async Task WaitForOldProcessAsync(HostUpdateRequest request, CancellationToken cancellationToken)
	{
		if (request.OldProcessId <= 0 || request.OldProcessId == Environment.ProcessId)
		{
			return;
		}
		Process oldProcess = null;
		try
		{
			oldProcess = Process.GetProcessById(request.OldProcessId);
			if (request.OldProcessStartTimeUtcTicks > 0 && oldProcess.StartTime.ToUniversalTime().Ticks != request.OldProcessStartTimeUtcTicks)
			{
				return;
			}
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(OldProcessExitTimeout);
			try
			{
				await oldProcess.WaitForExitAsync(timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				throw new TimeoutException("等待旧版程序退出超时。请关闭旧版程序后重试。");
			}
		}
		catch (ArgumentException)
		{
		}
		finally
		{
			oldProcess?.Dispose();
		}
	}

	private static async Task<HostUpdateInstallResult> VerifyFileAsync(string path, long expectedSize, string expectedSha256, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			if (!fileInfo.Exists)
			{
				return HostUpdateInstallResult.Failed("更新程序不存在：" + path);
			}
			if (fileInfo.Length != expectedSize)
			{
				return HostUpdateInstallResult.Failed($"更新程序大小不一致：应为 {expectedSize} 字节，实际为 {fileInfo.Length} 字节。");
			}
			HostUpdateInstallResult result;
			await using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
			{
				byte[] obj = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				result = (CryptographicOperations.FixedTimeEquals(right: Convert.FromHexString(expectedSha256), left: obj) ? new HostUpdateInstallResult
				{
					Success = true,
					Message = "校验通过。"
				} : HostUpdateInstallResult.Failed("更新程序的 SHA-256 校验失败。"));
			}
			return result;
		}
		catch (Exception ex)
		{
			return HostUpdateInstallResult.Failed("无法校验更新程序：" + ex.Message);
		}
	}

	private static async Task<HostUpdateInstallResult> CopyAndVerifyAsync(string sourcePath, string destinationPath, long expectedSize, string expectedSha256, CancellationToken cancellationToken)
	{
		HostUpdateInstallResult result;
		await using (FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			HostUpdateInstallResult hostUpdateInstallResult;
			await using (FileStream destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
			{
				using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
				byte[] buffer = new byte[131072];
				long totalBytes = 0L;
				while (true)
				{
					int num = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (num == 0)
					{
						break;
					}
					totalBytes += num;
					hash.AppendData(buffer, 0, num);
					await destination.WriteAsync(buffer.AsMemory(0, num), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				await destination.FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				destination.Flush(flushToDisk: true);
				if (totalBytes != expectedSize)
				{
					hostUpdateInstallResult = HostUpdateInstallResult.Failed("复制后的更新程序大小不一致。");
				}
				else
				{
					byte[] hashAndReset = hash.GetHashAndReset();
					hostUpdateInstallResult = (CryptographicOperations.FixedTimeEquals(right: Convert.FromHexString(expectedSha256), left: hashAndReset) ? new HostUpdateInstallResult
					{
						Success = true,
						Message = "校验通过。"
					} : HostUpdateInstallResult.Failed("复制后的更新程序 SHA-256 校验失败。"));
				}
			}
			result = hostUpdateInstallResult;
		}
		return result;
	}

	private static string GetUpdatesRoot(string dataRoot)
	{
		if (string.IsNullOrWhiteSpace(dataRoot))
		{
			throw new ArgumentException("数据目录不能为空。", "dataRoot");
		}
		return Path.Combine(Path.GetFullPath(dataRoot), "updates");
	}

	private static void EnsureTargetExecutable(string targetPath)
	{
		if (!string.Equals(Path.GetFileName(targetPath), "MoeCTF.RE.Tutorial.exe", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("更新目标必须是 MoeCTF.RE.Tutorial.exe。");
		}
	}

	private static void EnsurePathIsInside(string rootPath, string candidatePath, string errorMessage)
	{
		string value = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!Path.GetFullPath(candidatePath).StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException(errorMessage);
		}
	}

	private static string SanitizeFileNamePart(string value)
	{
		HashSet<char> invalid = Path.GetInvalidFileNameChars().ToHashSet();
		string text = new string(value.Where((char character) => !invalid.Contains(character)).ToArray()).Trim();
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return "update";
	}

	private static void MarkRequestCompleted(string requestPath)
	{
		try
		{
			string destFileName = Path.ChangeExtension(requestPath, ".completed.json");
			File.Move(requestPath, destFileName, overwrite: true);
		}
		catch
		{
		}
	}

	private static HostUpdateInstallResult WaitForApplyWorkerReady(Process process, string requestPath)
	{
		string path = ReadyMarkerPath(requestPath);
		string text = FailureMarkerPath(requestPath);
		DateTime dateTime = DateTime.UtcNow + ApplyWorkerReadyTimeout;
		while (DateTime.UtcNow < dateTime)
		{
			if (File.Exists(path))
			{
				return new HostUpdateInstallResult
				{
					Success = true,
					Message = "更新程序已准备就绪。"
				};
			}
			if (File.Exists(text))
			{
				return HostUpdateInstallResult.Failed(ReadFailureMarker(text));
			}
			try
			{
				if (process.HasExited)
				{
					return HostUpdateInstallResult.Failed(File.Exists(text) ? ReadFailureMarker(text) : $"更新程序在准备完成前退出，退出代码 {process.ExitCode}。详情见 {LogPath(requestPath)}");
				}
			}
			catch (InvalidOperationException)
			{
				return HostUpdateInstallResult.Failed("无法监视更新程序进程。");
			}
			Thread.Sleep(50);
		}
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch
		{
		}
		return HostUpdateInstallResult.Failed("更新程序未在 10 秒内准备完成，当前程序不会退出。详情见 " + LogPath(requestPath));
	}

	private static HostUpdateInstallResult FailApply(string requestPath, string message)
	{
		AppendApplyLog(requestPath, "FAILED: " + message);
		try
		{
			File.WriteAllText(FailureMarkerPath(requestPath), message);
		}
		catch
		{
		}
		return HostUpdateInstallResult.Failed(message);
	}

	private static void WriteReadyMarker(string requestPath)
	{
		string text = ReadyMarkerPath(requestPath);
		string text2 = text + ".tmp";
		File.WriteAllText(text2, DateTimeOffset.UtcNow.ToString("O"));
		File.Move(text2, text, overwrite: true);
	}

	private static void AppendApplyLog(string requestPath, string message)
	{
		try
		{
			File.AppendAllText(LogPath(requestPath), $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
		}
		catch
		{
		}
	}

	private static string ReadFailureMarker(string failurePath)
	{
		try
		{
			string text = File.ReadAllText(failurePath).Trim();
			return string.IsNullOrWhiteSpace(text) ? "更新程序准备失败。" : text;
		}
		catch
		{
			return "更新程序准备失败。";
		}
	}

	private static string ReadyMarkerPath(string requestPath)
	{
		return Path.ChangeExtension(requestPath, ".ready");
	}

	private static string FailureMarkerPath(string requestPath)
	{
		return Path.ChangeExtension(requestPath, ".failed.txt");
	}

	private static string LogPath(string requestPath)
	{
		return Path.ChangeExtension(requestPath, ".log");
	}

	private static void CleanupOldPartialFiles(string updatesRoot, string pattern)
	{
		foreach (string item in Directory.EnumerateFiles(updatesRoot, pattern))
		{
			try
			{
				if (DateTime.UtcNow - File.GetLastWriteTimeUtc(item) > TimeSpan.FromDays(1.0))
				{
					TryDeleteFile(item);
				}
			}
			catch
			{
			}
		}
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}
}
