using System.Diagnostics;
using System.Net.Http;

namespace AlbumRobot.Sync.Core;

public sealed class LocalQceException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed record LocalQceStartResult(bool StartedByDesktop, bool UsedQuickLogin = false);

/// <summary>
/// Starts the local QCE full-mode launcher on demand.
/// An already healthy QCE is reused, and QCE/QQ are never stopped by this class.
/// </summary>
public sealed class LocalQceHost : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(2);
    private readonly HttpClient healthClient;
    private readonly bool ownsHealthClient;
    private readonly string? configuredWorkspaceRoot;
    private Process? launchProcess;
    private string? temporaryLauncherPath;

    public LocalQceHost(string? workspaceRoot = null, HttpClient? healthClient = null)
    {
        configuredWorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot) ? null : Path.GetFullPath(workspaceRoot);
        this.healthClient = healthClient ?? new HttpClient { Timeout = HealthTimeout };
        ownsHealthClient = healthClient is null;
    }

    public async Task<LocalQceStartResult> EnsureStartedAsync(
        Uri qceBaseAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(qceBaseAddress);
        if (!qceBaseAddress.IsLoopback)
        {
            throw new LocalQceException("一键启动 QCE 只支持本机地址，请将 QCE 地址设为 127.0.0.1 或 localhost。");
        }

        if (await IsHealthyAsync(qceBaseAddress, cancellationToken))
        {
            return new LocalQceStartResult(StartedByDesktop: false);
        }

        if (launchProcess is { HasExited: false })
        {
            await WaitForHealthAsync(qceBaseAddress, cancellationToken);
            return new LocalQceStartResult(StartedByDesktop: true);
        }

        var workspaceRoot = ResolveWorkspaceRoot();
        if (workspaceRoot is null)
        {
            throw new LocalQceException("未找到 AlbumRobot 仓库目录；请确认 qce-data 位于仓库根目录。");
        }

        var launcherPath = FindLauncher(workspaceRoot);
        if (launcherPath is null)
        {
            throw new LocalQceException("未找到 qce-data 下的 launcher-user.bat；请先安装或解压 QCE 到 AlbumRobot 仓库。");
        }

        var quickLoginAccount = FindQuickLoginAccount(Path.Combine(Path.GetDirectoryName(launcherPath)!, "config"));
        StartLauncher(launcherPath, quickLoginAccount);
        await WaitForHealthAsync(qceBaseAddress, cancellationToken);
        return new LocalQceStartResult(
            StartedByDesktop: true,
            UsedQuickLogin: quickLoginAccount is not null);
    }

    public static string? FindLauncher(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var qceDataDirectory = Path.Combine(directory.FullName, "qce-data");
            if (Directory.Exists(qceDataDirectory))
            {
                try
                {
                    var launcherPath = Directory.EnumerateFiles(
                            qceDataDirectory,
                            "launcher-user.bat",
                            SearchOption.AllDirectories)
                        .OrderBy(path => path.Length)
                        .FirstOrDefault();
                    if (launcherPath is not null) return Path.GetFullPath(launcherPath);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    // Continue walking parent directories if a nested QCE folder
                    // is temporarily unavailable or has restricted permissions.
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static string? FindQuickLoginAccount(string configDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        if (!Directory.Exists(configDirectory)) return null;

        try
        {
            var accounts = Directory.EnumerateFiles(configDirectory, "napcat_*.json", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .Where(name => name.StartsWith("napcat_", StringComparison.OrdinalIgnoreCase))
                .Select(name => name["napcat_".Length..])
                .Where(account => account.Length > 0 && account.All(char.IsAsciiDigit))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return accounts.Length == 1 ? accounts[0] : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // The launcher may have spawned QCE/NapCat children. They belong to the
        // user's desktop session and must remain running when Sync closes.
        launchProcess?.Dispose();
        launchProcess = null;
        TryDeleteFile(temporaryLauncherPath);
        temporaryLauncherPath = null;
        if (ownsHealthClient) healthClient.Dispose();
        await ValueTask.CompletedTask;
    }

    private string? ResolveWorkspaceRoot()
    {
        if (configuredWorkspaceRoot is not null)
        {
            return FindWorkspaceRoot(configuredWorkspaceRoot);
        }

        return FindWorkspaceRoot(AppContext.BaseDirectory);
    }

    private static string? FindWorkspaceRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json")) &&
                File.Exists(Path.Combine(directory.FullName, "worker", "package.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private void StartLauncher(string launcherPath, string? quickLoginAccount)
    {
        string? wrapperPath = null;
        try
        {
            var workingDirectory = Path.GetDirectoryName(launcherPath)!;
            var startInfo = new ProcessStartInfo
            {
                FileName = launcherPath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal,
            };
            if (quickLoginAccount is not null)
            {
                // Keep both NapCat quick-login channels available. A tiny
                // local wrapper lets cmd set the environment before invoking
                // the real launcher while preserving a visible QCE console.
                wrapperPath = Path.Combine(
                    Path.GetTempPath(),
                    $"albumrobot-qce-{Guid.NewGuid():N}.bat");
                File.WriteAllText(
                    wrapperPath,
                    $"@echo off{Environment.NewLine}" +
                    $"set \"NAPCAT_QUICK_ACCOUNT={quickLoginAccount}\"{Environment.NewLine}" +
                    $"call \"{launcherPath}\" -q {quickLoginAccount}{Environment.NewLine}");
                startInfo.FileName = wrapperPath;
            }

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };
            if (wrapperPath is not null)
            {
                process.Exited += (_, _) => TryDeleteFile(wrapperPath);
            }
            if (!process.Start())
            {
                process.Dispose();
                TryDeleteFile(wrapperPath);
                throw new InvalidOperationException("QCE launcher process could not be started.");
            }

            launchProcess?.Dispose();
            TryDeleteFile(temporaryLauncherPath);
            launchProcess = process;
            temporaryLauncherPath = wrapperPath;
        }
        catch (Exception exception)
        {
            TryDeleteFile(wrapperPath);
            throw new LocalQceException("QCE 启动失败；请确认 launcher-user.bat 可正常运行。", exception);
        }
    }

    private async Task WaitForHealthAsync(Uri qceBaseAddress, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + StartupTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsHealthyAsync(qceBaseAddress, cancellationToken)) return;
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        throw new LocalQceException("QCE 启动超时；请确认 QQ 已登录，并检查本机 40653 端口。");
    }

    private async Task<bool> IsHealthyAsync(Uri qceBaseAddress, CancellationToken cancellationToken)
    {
        try
        {
            var rootUri = new Uri(qceBaseAddress, "/");
            using var response = await healthClient.GetAsync(rootUri, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // The launcher may still be reading the wrapper; it will be
            // cleaned up on the next start or process exit.
        }
    }
}
