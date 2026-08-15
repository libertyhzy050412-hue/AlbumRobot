using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace AlbumRobot.Sync.Core;

public sealed class LocalWorkerException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Starts the repository's local Worker on demand and keeps ownership explicit.
/// An already healthy Worker is reused and is never stopped by this class.
/// </summary>
public sealed class LocalWorkerHost : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(2);
    private readonly HttpClient healthClient;
    private readonly bool ownsHealthClient;
    private readonly string? configuredWorkspaceRoot;
    private Process? ownedWorkerProcess;
    private Task? ownedOutputDrain;
    private Task? ownedErrorDrain;

    public LocalWorkerHost(string? workspaceRoot = null, HttpClient? healthClient = null)
    {
        configuredWorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot) ? null : Path.GetFullPath(workspaceRoot);
        this.healthClient = healthClient ?? new HttpClient { Timeout = HealthTimeout };
        ownsHealthClient = healthClient is null;
    }

    public bool StartedByDesktop => ownedWorkerProcess is { HasExited: false };

    public async Task EnsureReadyAsync(Uri workerBaseAddress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workerBaseAddress);
        if (await IsHealthyAsync(workerBaseAddress, cancellationToken)) return;

        if (!workerBaseAddress.IsLoopback)
        {
            throw new LocalWorkerException("远端 Worker 当前不可用；请检查网络、部署状态和 Worker 地址。");
        }

        if (ownedWorkerProcess is { HasExited: false })
        {
            await WaitForHealthAsync(workerBaseAddress, cancellationToken);
            return;
        }

        var workspaceRoot = ResolveWorkspaceRoot();
        if (workspaceRoot is null)
        {
            throw new LocalWorkerException("未找到本地 Worker 运行目录；请确认桌面端位于 AlbumRobot 仓库内。");
        }

        await RunMigrationAsync(workspaceRoot, cancellationToken);
        if (await IsHealthyAsync(workerBaseAddress, cancellationToken)) return;

        StartWorker(workspaceRoot);
        try
        {
            await WaitForHealthAsync(workerBaseAddress, cancellationToken);
        }
        catch
        {
            await StopOwnedWorkerAsync();
            throw;
        }
    }

    public static string? FindWorkspaceRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var packagePath = Path.Combine(directory.FullName, "package.json");
            var workerPath = Path.Combine(directory.FullName, "worker", "package.json");
            if (File.Exists(packagePath) && File.Exists(workerPath)) return directory.FullName;
            directory = directory.Parent;
        }

        return null;
    }

    public static string? FindExecutableOnPath(string executableName, string? path = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        if (Path.IsPathFullyQualified(executableName))
        {
            return File.Exists(executableName) ? Path.GetFullPath(executableName) : null;
        }

        path ??= Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;
        foreach (var rawDirectory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = rawDirectory.Trim().Trim('"');
            if (directory.Length == 0) continue;
            try
            {
                var candidate = Path.GetFullPath(Path.Combine(directory, executableName));
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Ignore malformed PATH entries and continue to the next one.
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopOwnedWorkerAsync();
        if (ownsHealthClient) healthClient.Dispose();
    }

    private string? ResolveWorkspaceRoot()
    {
        if (configuredWorkspaceRoot is not null)
        {
            return FindWorkspaceRoot(configuredWorkspaceRoot);
        }

        return FindWorkspaceRoot(AppContext.BaseDirectory);
    }

    private async Task RunMigrationAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        using var process = CreateWranglerProcess(
            workspaceRoot,
            "d1",
            "migrations",
            "apply",
            "albumrobot-local",
            "--config",
            "wrangler.local.jsonc",
            "--local");
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Wrangler migration process could not be started.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
            if (process.ExitCode != 0)
            {
                throw new LocalWorkerException("本地 Worker 数据库初始化失败。");
            }
        }
        catch (LocalWorkerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            TryKillProcess(process);
            throw new LocalWorkerException("无法初始化本地 Worker；请确认 Node.js 已安装且项目依赖完整。", exception);
        }
    }

    private void StartWorker(string workspaceRoot)
    {
        try
        {
            var process = CreateWranglerProcess(
                workspaceRoot,
                "dev",
                "--config",
                "wrangler.local.jsonc",
                "--local");
            if (!process.Start()) throw new InvalidOperationException("Wrangler development process could not be started.");
            ownedOutputDrain = DrainAsync(process.StandardOutput);
            ownedErrorDrain = DrainAsync(process.StandardError);
            ownedWorkerProcess = process;
        }
        catch (Exception exception)
        {
            throw new LocalWorkerException("本地 Worker 启动失败。", exception);
        }
    }

    private async Task WaitForHealthAsync(Uri workerBaseAddress, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + StartupTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsHealthyAsync(workerBaseAddress, cancellationToken)) return;
            if (ownedWorkerProcess is { HasExited: true })
            {
                throw new LocalWorkerException("本地 Worker 启动后立即退出；请检查 Node.js 和项目依赖。");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        throw new LocalWorkerException("本地 Worker 启动超时，请稍后重试。");
    }

    private async Task<bool> IsHealthyAsync(Uri workerBaseAddress, CancellationToken cancellationToken)
    {
        try
        {
            var healthUri = new Uri(
                new Uri(EnsureTrailingSlash(workerBaseAddress), UriKind.Absolute),
                "api/health");
            using var response = await healthClient.GetAsync(healthUri, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            var payload = await response.Content.ReadFromJsonAsync<WorkerHealthResponse>(cancellationToken: cancellationToken);
            return payload?.Ok == true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static Process CreateWranglerProcess(string workspaceRoot, params string[] arguments)
    {
        var nodeName = OperatingSystem.IsWindows() ? "node.exe" : "node";
        var nodePath = FindExecutableOnPath(nodeName)
            ?? throw new LocalWorkerException("未找到 Node.js；请先安装 Node.js，并重新打开桌面端。");
        var workerDirectory = Path.Combine(workspaceRoot, "worker");
        var wranglerPath = Path.Combine(workerDirectory, "node_modules", "wrangler", "bin", "wrangler.js");
        if (!File.Exists(wranglerPath))
        {
            throw new LocalWorkerException("未找到项目内 Wrangler；请先在 AlbumRobot 仓库安装依赖。");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = nodePath,
            WorkingDirectory = workerDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(wranglerPath);
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        return new Process { StartInfo = startInfo, EnableRaisingEvents = true };
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is not null)
        {
        }
    }

    private async Task StopOwnedWorkerAsync()
    {
        var process = ownedWorkerProcess;
        ownedWorkerProcess = null;
        var outputDrain = ownedOutputDrain;
        var errorDrain = ownedErrorDrain;
        ownedOutputDrain = null;
        ownedErrorDrain = null;
        if (process is null) return;

        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            if (outputDrain is not null) await IgnoreDrainFailureAsync(outputDrain);
            if (errorDrain is not null) await IgnoreDrainFailureAsync(errorDrain);
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task IgnoreDrainFailureAsync(Task drainTask)
    {
        try
        {
            await drainTask;
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string EnsureTrailingSlash(Uri address) =>
        address.AbsoluteUri.EndsWith('/') ? address.AbsoluteUri : address.AbsoluteUri + "/";

    private sealed record WorkerHealthResponse(bool Ok);
}
