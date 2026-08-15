namespace AlbumRobot.Sync.Core;

public sealed class SyncRuntime : IAsyncDisposable
{
    private readonly HttpClient qceHttpClient;
    private readonly HttpClient workerHttpClient;

    private SyncRuntime(
        SyncSettings settings,
        HttpClient qceHttpClient,
        HttpClient workerHttpClient,
        PendingStore pendingStore,
        QceDirectClient? qceClient,
        QceAlbumScanner? scanner,
        QceJsonImportService jsonImporter,
        LocalSyncOrchestrator orchestrator)
    {
        Settings = settings;
        this.qceHttpClient = qceHttpClient;
        this.workerHttpClient = workerHttpClient;
        PendingStore = pendingStore;
        QceClient = qceClient;
        Scanner = scanner;
        JsonImporter = jsonImporter;
        Orchestrator = orchestrator;
    }

    public SyncSettings Settings { get; }

    public PendingStore PendingStore { get; }

    public QceDirectClient? QceClient { get; }

    public QceAlbumScanner? Scanner { get; }

    public QceJsonImportService JsonImporter { get; }

    public LocalSyncOrchestrator Orchestrator { get; }

    public static async Task<SyncRuntime> CreateAsync(
        SyncSettings settings,
        string? qceAccessToken,
        string? syncToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();
        settings.Validate();

        Directory.CreateDirectory(settings.DataDirectory);
        var pendingPath = Path.Combine(settings.DataDirectory, "pending.sqlite");
        var pendingStore = new PendingStore(pendingPath);
        var qceHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var workerHttpClient = new HttpClient
        {
            BaseAddress = EnsureTrailingSlash(new Uri(settings.WorkerBaseUrl, UriKind.Absolute)),
            Timeout = TimeSpan.FromSeconds(30),
        };

        try
        {
            await pendingStore.InitializeAsync(cancellationToken);
            await pendingStore.RecoverSubmittingAsync(cancellationToken);
            QceDirectClient? qceClient = null;
            QceAlbumScanner? scanner = null;
            if (!string.IsNullOrWhiteSpace(qceAccessToken))
            {
                qceClient = new QceDirectClient(
                    qceHttpClient,
                    new QceDirectOptions(new Uri(settings.QceBaseUrl, UriKind.Absolute), qceAccessToken));
                scanner = new QceAlbumScanner(qceClient, pendingStore);
            }
            var batchClient = new BatchSyncClient(workerHttpClient, syncToken);
            var importer = new QceJsonImportService(pendingStore);
            var orchestrator = new LocalSyncOrchestrator(scanner, pendingStore, batchClient);
            return new SyncRuntime(
                settings,
                qceHttpClient,
                workerHttpClient,
                pendingStore,
                qceClient,
                scanner,
                importer,
                orchestrator);
        }
        catch
        {
            await pendingStore.DisposeAsync();
            qceHttpClient.Dispose();
            workerHttpClient.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await PendingStore.DisposeAsync();
        qceHttpClient.Dispose();
        workerHttpClient.Dispose();
    }

    private static Uri EnsureTrailingSlash(Uri address)
    {
        var text = address.AbsoluteUri.EndsWith('/') ? address.AbsoluteUri : address.AbsoluteUri + "/";
        return new Uri(text, UriKind.Absolute);
    }
}
