using System.Net;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class LocalQceHostTests
{
    [Fact]
    public void FindsLauncherFromAChildDirectory()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var launcherPath = Path.Combine(
                temporaryDirectory,
                "qce-data",
                "runtime",
                "v6.2.3",
                "NapCat-QCE-Windows-x64",
                "launcher-user.bat");
            Directory.CreateDirectory(Path.GetDirectoryName(launcherPath)!);
            File.WriteAllText(launcherPath, "@echo off");

            var startDirectory = Path.Combine(temporaryDirectory, "apps", "sync", "bin");
            Directory.CreateDirectory(startDirectory);

            Assert.Equal(Path.GetFullPath(launcherPath), LocalQceHost.FindLauncher(startDirectory));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void FindsSingleCachedQuickLoginAccount()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var configDirectory = Path.Combine(temporaryDirectory, "config");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(Path.Combine(configDirectory, "napcat_123456789.json"), "{}");
            File.WriteAllText(Path.Combine(configDirectory, "napcat_protocol_123456789.json"), "{}");

            Assert.Equal("123456789", LocalQceHost.FindQuickLoginAccount(configDirectory));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task StartsTheBatchLauncherWithTheCachedQuickLoginAccount()
    {
        if (!OperatingSystem.IsWindows()) return;

        var temporaryDirectory = CreateTemporaryDirectory();
        var markerPath = Path.Combine(temporaryDirectory, "launcher-arguments.txt");
        try
        {
            Directory.CreateDirectory(Path.Combine(temporaryDirectory, "worker"));
            await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "worker", "package.json"), "{}");

            var qceDirectory = Path.Combine(temporaryDirectory, "qce-data", "runtime", "v6.2.3", "NapCat-QCE-Windows-x64");
            var configDirectory = Path.Combine(qceDirectory, "config");
            Directory.CreateDirectory(configDirectory);
            await File.WriteAllTextAsync(Path.Combine(configDirectory, "napcat_123456789.json"), "{}");

            var launcherPath = Path.Combine(qceDirectory, "launcher-user.bat");
            await File.WriteAllTextAsync(
                launcherPath,
                $"@echo off{Environment.NewLine}echo %* > \"{markerPath}\"{Environment.NewLine}echo %NAPCAT_QUICK_ACCOUNT% >> \"{markerPath}\"{Environment.NewLine}");

            using var healthClient = new HttpClient(new StubHandler(_ =>
                Task.FromResult(new HttpResponseMessage(
                    File.Exists(markerPath) ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable))));
            await using var host = new LocalQceHost(temporaryDirectory, healthClient);

            var result = await host.EnsureStartedAsync(new Uri("http://127.0.0.1:40653"));

            Assert.True(result.StartedByDesktop);
            var arguments = await File.ReadAllTextAsync(markerPath);
            Assert.Contains("-q", arguments);
            Assert.Contains("123456789", arguments);
            Assert.Contains("123456789", arguments.Split(Environment.NewLine)[1]);
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task ReusesAHealthyQceWithoutStartingAProcess()
    {
        using var healthClient = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        await using var host = new LocalQceHost(@"Z:\missing-workspace", healthClient);

        var result = await host.EnsureStartedAsync(new Uri("http://127.0.0.1:40653"));

        Assert.False(result.StartedByDesktop);
    }

    [Fact]
    public async Task RejectsRemoteQceAddresses()
    {
        using var healthClient = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        await using var host = new LocalQceHost(@"Z:\missing-workspace", healthClient);

        var exception = await Assert.ThrowsAsync<LocalQceException>(() =>
            host.EnsureStartedAsync(new Uri("https://qce.example.invalid")));

        Assert.Contains("只支持本机地址", exception.Message);
    }

    [Fact]
    public async Task StartsTheBatchLauncherThroughTheDotnetProcessRuntime()
    {
        if (!OperatingSystem.IsWindows()) return;

        var temporaryDirectory = CreateTemporaryDirectory();
        var markerPath = Path.Combine(temporaryDirectory, "launcher-started.txt");
        try
        {
            Directory.CreateDirectory(Path.Combine(temporaryDirectory, "worker"));
            await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "worker", "package.json"), "{}");
            var launcherPath = Path.Combine(temporaryDirectory, "qce-data", "launcher-user.bat");
            Directory.CreateDirectory(Path.GetDirectoryName(launcherPath)!);
            await File.WriteAllTextAsync(
                launcherPath,
                $"@echo off{Environment.NewLine}echo started > \"{markerPath}\"{Environment.NewLine}");

            using var healthClient = new HttpClient(new StubHandler(_ =>
                Task.FromResult(new HttpResponseMessage(
                    File.Exists(markerPath) ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable))));
            await using var host = new LocalQceHost(temporaryDirectory, healthClient);

            var result = await host.EnsureStartedAsync(new Uri("http://127.0.0.1:40653"));

            Assert.True(result.StartedByDesktop);
            Assert.True(File.Exists(markerPath));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "albumrobot-qce-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
