using System.Net;
using System.Net.Http.Json;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class LocalWorkerHostTests
{
    [Fact]
    public void ResolvesCommandShimToAnAbsolutePath()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var executableName = OperatingSystem.IsWindows() ? "pnpm.cmd" : "pnpm";
            var executablePath = Path.Combine(temporaryDirectory, executableName);
            File.WriteAllText(executablePath, "test shim");

            var resolved = LocalWorkerHost.FindExecutableOnPath(executableName, temporaryDirectory)
                ?? throw new InvalidOperationException("Executable was not resolved from the supplied PATH.");

            Assert.Equal(Path.GetFullPath(executablePath), resolved);
            Assert.True(Path.IsPathFullyQualified(resolved));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReusesAHealthyWorkerWithoutOwningAProcess()
    {
        using var healthClient = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { ok = true }),
            })));
        await using var host = new LocalWorkerHost(@"Z:\missing-workspace", healthClient);

        await host.EnsureReadyAsync(new Uri("http://127.0.0.1:8787"));

        Assert.False(host.StartedByDesktop);
    }

    [Fact]
    public async Task ReusesAHealthyRemoteWorkerWithoutStartingLocalProcesses()
    {
        using var healthClient = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { ok = true, configured = true }),
            })));
        await using var host = new LocalWorkerHost(@"Z:\missing-workspace", healthClient);

        await host.EnsureReadyAsync(new Uri("https://album.example.invalid"));

        Assert.False(host.StartedByDesktop);
    }

    [Fact]
    public async Task DoesNotStartLocalProcessesForAnUnavailableRemoteWorker()
    {
        using var healthClient = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { ok = false, configured = false }),
            })));
        await using var host = new LocalWorkerHost(@"Z:\missing-workspace", healthClient);

        var exception = await Assert.ThrowsAsync<LocalWorkerException>(() =>
            host.EnsureReadyAsync(new Uri("https://album.example.invalid")));

        Assert.Contains("运行时配置未完成", exception.Message);
        Assert.False(host.StartedByDesktop);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "albumrobot-worker-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
