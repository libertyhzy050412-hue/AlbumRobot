using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AlbumRobot.Sync.Core;
using Microsoft.Win32;

namespace AlbumRobot.Sync.App;

public partial class MainWindow : Window
{
    private readonly SyncSettingsStore settingsStore = new();
    private readonly QceAccessTokenProvider tokenProvider = new();
    private readonly LocalWorkerHost workerHost = new();
    private SyncSettings settings = new();
    private SyncRuntime? runtime;
    private bool runtimeHasQce;
    private string? runtimeSyncToken;
    private bool busy;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            settings = await settingsStore.LoadAsync();
            ApplySettingsToControls();
            await RefreshGroupsAsync();
        }
        catch (Exception exception)
        {
            SetStatus("本地设置加载失败，已使用默认设置。", isError: true);
            ShowSafeError(exception);
        }
    }

    private async void Window_OnClosed(object? sender, EventArgs e)
    {
        try
        {
            if (runtime is not null) await runtime.DisposeAsync();
        }
        finally
        {
            await workerHost.DisposeAsync();
        }
    }

    private async void RefreshGroupsButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshGroupsAsync();
    }

    private async void SyncButton_OnClick(object sender, RoutedEventArgs e)
    {
        var group = SelectedGroup();
        if (group is null)
        {
            SetStatus("请先选择目标群。", isError: true);
            return;
        }

        if (!await EnsureRuntimeAsync(requireQce: true, requireSyncToken: true) || runtime?.Scanner is null)
        {
            return;
        }

        try
        {
            SetBusy(true);
            if (!await EnsureWorkerReadyAsync()) return;

            var current = ReadSettingsFromControls() with
            {
                SelectedGroupId = group.GroupId,
                SelectedGroupName = group.GroupName,
            };
            await settingsStore.SaveAsync(current);
            settings = current;

            SetStatus("正在读取成员并扫描 QCE 消息…");
            var endTime = DateTimeOffset.Now;
            var startTime = endTime.AddDays(-settings.LookbackDays);
            if (!settings.InitialSyncCompleted)
            {
                var scan = await runtime.Scanner.ScanGroupAsync(
                    group.GroupId,
                    startTime,
                    endTime,
                    settings.PageSize);
                UpdateScanStats(scan);
                var pending = await PendingForGroupAsync(group.GroupId);
                UpdatePendingStatus(pending.Count);
                if (pending.Count == 0)
                {
                    SetStatus("首次扫描完成，没有发现可同步的网易云专辑。", isError: false);
                    LastRunText.Text = $"扫描完成：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}";
                    return;
                }

                var confirmation = MessageBox.Show(
                    $"首次扫描发现 {pending.Count} 条待同步候选。\n\n仅会上传标准化的群、成员、消息来源和专辑字段，不会上传 Raw QQ 消息。现在上传吗？",
                    "确认首次同步",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirmation != MessageBoxResult.Yes)
                {
                    SetStatus("已保留在本地待同步队列，尚未上传。", isError: false);
                    return;
                }

                SetStatus("正在提交标准化候选…");
                var submission = await runtime.Orchestrator.SubmitPendingAsync(group.GroupId);
                UpdateSubmissionStats(submission);
                settings = settings with { InitialSyncCompleted = true };
                await settingsStore.SaveAsync(settings);
                SetStatus("首次同步完成。", isError: false);
                LastRunText.Text = $"首次同步：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}";
            }
            else
            {
                var result = await runtime.Orchestrator.RunGroupAsync(
                    group.GroupId,
                    startTime,
                    endTime,
                    settings.PageSize);
                UpdateRunStats(result);
                SetStatus(result.Submitted == 0 ? "扫描完成，没有新的待同步候选。" : "增量同步完成。", isError: false);
                LastRunText.Text = $"最近同步：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}";
            }

            await RefreshPendingStatusAsync(group.GroupId);
        }
        catch (Exception exception)
        {
            SetStatus("同步失败；本地队列已保留，可稍后重试。", isError: true);
            ShowSafeError(exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ImportJsonButton_OnClick(object sender, RoutedEventArgs e)
    {
        var group = SelectedGroup();
        if (group is null)
        {
            SetStatus("请先选择目标群，再导入 JSON。", isError: true);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "QCE JSON (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            Title = "选择 QCE JSON 导出文件",
        };
        if (dialog.ShowDialog(this) != true) return;

        if (!await EnsureRuntimeAsync(requireQce: false) || runtime is null) return;
        try
        {
            SetBusy(true);
            settings = ReadSettingsFromControls() with
            {
                SelectedGroupId = group.GroupId,
                SelectedGroupName = group.GroupName,
            };
            await settingsStore.SaveAsync(settings);
            SetStatus("正在本机解析 JSON；原始内容不会离开本机…");
            var result = await runtime.JsonImporter.ImportFileAsync(dialog.FileName, group.GroupId);
            await RefreshPendingStatusAsync(group.GroupId);
            SetStatus($"JSON 导入完成：读取 {result.MessagesRead} 条消息，识别 {result.CandidatesDetected} 条网易云专辑候选。", isError: false);
        }
        catch (Exception exception)
        {
            SetStatus("JSON 导入失败；未上传原始文件。", isError: true);
            ShowSafeError(exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void SubmitPendingButton_OnClick(object sender, RoutedEventArgs e)
    {
        var group = SelectedGroup();
        if (group is null)
        {
            SetStatus("请先选择目标群。", isError: true);
            return;
        }

        if (!await EnsureRuntimeAsync(requireQce: false, requireSyncToken: true) || runtime is null) return;
        try
        {
            var pending = await PendingForGroupAsync(group.GroupId);
            if (pending.Count == 0)
            {
                SetStatus("当前目标群没有待同步候选。", isError: false);
                return;
            }

            var confirmation = MessageBox.Show(
                $"本地有 {pending.Count} 条待同步候选。确认提交到 Worker API 吗？",
                "确认上传",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes) return;

            SetBusy(true);
            if (!await EnsureWorkerReadyAsync()) return;
            SetStatus("正在提交本地标准化候选…");
            var result = await runtime.Orchestrator.SubmitPendingAsync(group.GroupId);
            UpdateSubmissionStats(result);
            settings = ReadSettingsFromControls() with
            {
                SelectedGroupId = group.GroupId,
                SelectedGroupName = group.GroupName,
                InitialSyncCompleted = true,
            };
            await settingsStore.SaveAsync(settings);
            SetStatus("待同步队列提交完成。", isError: false);
            await RefreshPendingStatusAsync(group.GroupId);
        }
        catch (Exception exception)
        {
            SetStatus("待同步提交失败；队列已保留，可重试。", isError: true);
            ShowSafeError(exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenDataFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(settings.DataDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{settings.DataDirectory}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            ShowSafeError(exception);
        }
    }

    private void TargetGroupComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtons();
        if (TargetGroupComboBox.SelectedItem is QceGroupSnapshot group)
        {
            _ = RefreshPendingStatusAsync(group.GroupId);
        }
    }

    private async Task RefreshGroupsAsync()
    {
        if (busy) return;
        try
        {
            SetBusy(true);
            SetStatus("正在连接本机 QCE…");
            if (!await EnsureRuntimeAsync(requireQce: true) || runtime?.QceClient is null) return;

            using var response = await runtime.QceClient.GetGroupsPageAsync();
            EnsureQceSuccess(response.RootElement);
            var groups = QceGroupNormalizer.ReadGroups(response.RootElement);
            TargetGroupComboBox.ItemsSource = groups;
            var selectedIndex = -1;
            if (!string.IsNullOrWhiteSpace(settings.SelectedGroupId))
            {
                for (var index = 0; index < groups.Count; index++)
                {
                    if (!groups[index].GroupId.Equals(settings.SelectedGroupId, StringComparison.Ordinal)) continue;
                    selectedIndex = index;
                    break;
                }
            }

            TargetGroupComboBox.SelectedIndex = selectedIndex;
            QceStatusText.Text = $"已连接 · 可访问 {groups.Count} 个群";
            SetStatus(groups.Count == 0 ? "QCE 已连接，但没有可访问群。" : "请选择目标群后开始同步。", isError: false);
        }
        catch (Exception exception)
        {
            QceStatusText.Text = "未连接";
            SetStatus(
                SelectedGroup() is null
                    ? "无法连接本机 QCE；请确认 QQ/QCE 已登录并监听 40653。"
                    : "Direct 暂不可用；仍可为已保存的目标群导入 QCE JSON。",
                isError: true);
            ShowSafeError(exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> EnsureRuntimeAsync(bool requireQce, bool requireSyncToken = false)
    {
        try
        {
            var requested = ReadSettingsFromControls();
            requested.Validate();
            var hasToken = tokenProvider.TryRead(out var accessToken);
            var syncToken = ReadSyncToken(requested.WorkerBaseUrl, requireSyncToken);
            if (requireQce && !hasToken)
            {
                QceStatusText.Text = "未找到本机 QCE 凭据";
                SetStatus("请先登录 QQ/QCE；token 只在本机内存中使用。", isError: true);
                return false;
            }

            var compatible = runtime is not null &&
                             (!requireQce || runtimeHasQce) &&
                             runtime.Settings.QceBaseUrl == requested.QceBaseUrl &&
                             runtime.Settings.WorkerBaseUrl == requested.WorkerBaseUrl &&
                             runtime.Settings.DataDirectory == requested.DataDirectory &&
                             string.Equals(runtimeSyncToken, syncToken, StringComparison.Ordinal);
            if (compatible) return true;

            if (runtime is not null) await runtime.DisposeAsync();
            runtime = await SyncRuntime.CreateAsync(requested, hasToken ? accessToken : null, syncToken);
            runtimeHasQce = hasToken;
            runtimeSyncToken = syncToken;
            settings = requested;
            DataDirectoryText.Text = settings.DataDirectory;
            return true;
        }
        catch (Exception exception)
        {
            SetStatus("桌面 Sync 配置无效。", isError: true);
            ShowSafeError(exception);
            return false;
        }
    }

    private string? ReadSyncToken(string workerBaseUrl, bool required)
    {
        var token = SyncTokenPasswordBox.Password.Trim();
        var workerUri = new Uri(workerBaseUrl, UriKind.Absolute);
        if (token.Length == 0 && workerUri.IsLoopback)
        {
            return "albumrobot-local-sync-token-not-production";
        }

        if (required && token.Length == 0)
        {
            throw new InvalidDataException("请输入 Cloudflare 中配置的 Sync Token；它不会写入本地设置。");
        }

        return token.Length == 0 ? null : token;
    }

    private async Task<bool> EnsureWorkerReadyAsync()
    {
        try
        {
            var requested = ReadSettingsFromControls();
            requested.Validate();
            SetStatus("正在准备本地 Worker…");
            await workerHost.EnsureReadyAsync(new Uri(requested.WorkerBaseUrl, UriKind.Absolute));
            return true;
        }
        catch (Exception exception)
        {
            SetStatus("本地 Worker 未就绪。", isError: true);
            ShowSafeError(exception);
            return false;
        }
    }

    private SyncSettings ReadSettingsFromControls()
    {
        if (!int.TryParse(LookbackDaysTextBox.Text, out var lookbackDays))
        {
            throw new FormatException("扫描回看天数必须是数字。");
        }

        return settings with
        {
            QceBaseUrl = QceBaseUrlTextBox.Text,
            WorkerBaseUrl = WorkerBaseUrlTextBox.Text,
            LookbackDays = lookbackDays,
        };
    }

    private void ApplySettingsToControls()
    {
        QceBaseUrlTextBox.Text = settings.QceBaseUrl;
        WorkerBaseUrlTextBox.Text = settings.WorkerBaseUrl;
        LookbackDaysTextBox.Text = settings.LookbackDays.ToString();
        DataDirectoryText.Text = settings.DataDirectory;
        if (!string.IsNullOrWhiteSpace(settings.SelectedGroupId))
        {
            TargetGroupComboBox.ItemsSource = new[]
            {
                new QceGroupSnapshot(
                    settings.SelectedGroupId,
                    settings.SelectedGroupName ?? "已保存的目标群",
                    0,
                    0,
                    null),
            };
            TargetGroupComboBox.SelectedIndex = 0;
        }
        UpdateButtons();
    }

    private QceGroupSnapshot? SelectedGroup() => TargetGroupComboBox.SelectedItem as QceGroupSnapshot;

    private async Task<IReadOnlyList<ShareCandidate>> PendingForGroupAsync(string groupId)
    {
        if (runtime is null) return Array.Empty<ShareCandidate>();
        return (await runtime.PendingStore.ListPendingAsync(100)).Where(item => item.GroupId == groupId).ToArray();
    }

    private async Task RefreshPendingStatusAsync(string groupId)
    {
        if (runtime is null) return;
        var pending = await PendingForGroupAsync(groupId);
        UpdatePendingStatus(pending.Count);
    }

    private void UpdateScanStats(QceScanResult result)
    {
        MessagesScannedText.Text = result.MessagesSeen.ToString();
        CandidatesText.Text = result.CandidatesDetected.ToString();
    }

    private void UpdateRunStats(LocalSyncRunResult result)
    {
        UpdateScanStats(result.Scan);
        UpdateSubmissionStats(new LocalSyncSubmissionResult(result.Submitted, result.Accepted, result.Duplicates, result.Invalid));
    }

    private void UpdateSubmissionStats(LocalSyncSubmissionResult result)
    {
        AcceptedText.Text = result.Accepted.ToString();
        DuplicatesInvalidText.Text = $"{result.Duplicates} / {result.Invalid}";
    }

    private void UpdatePendingStatus(int count)
    {
        PendingStatusText.Text = count == 0 ? "待同步：0" : $"待同步：{count}";
    }

    private void UpdateButtons()
    {
        var hasGroup = SelectedGroup() is not null;
        var enabled = !busy && hasGroup;
        SyncButton.IsEnabled = enabled && runtimeHasQce;
        ImportJsonButton.IsEnabled = enabled;
        SubmitPendingButton.IsEnabled = enabled;
        RefreshGroupsButton.IsEnabled = !busy;
        OpenDataFolderButton.IsEnabled = !busy;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        Mouse.OverrideCursor = value ? Cursors.Wait : null;
        UpdateButtons();
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.DarkSlateGray;
    }

    private static void EnsureQceSuccess(System.Text.Json.JsonElement response)
    {
        if (!response.TryGetProperty("success", out var success) || success.ValueKind != System.Text.Json.JsonValueKind.True)
        {
            throw new InvalidDataException("QCE returned an unsuccessful response.");
        }
    }

    private static void ShowSafeError(Exception exception)
    {
        var message = exception switch
        {
            QceApiException => "QCE 请求失败，请确认 QCE 版本和登录状态。",
            LocalWorkerException worker => worker.Message,
            HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized } => "Sync Token 无效，请核对后重试。",
            HttpRequestException => "Worker API 不可用，请先启动本地 Worker 或检查地址。",
            OperationCanceledException => "QCE 消息读取超时；请先等待或取消 QCE 导出任务，再重试。",
            InvalidDataException data => data.Message,
            FormatException format => format.Message,
            ArgumentException argument => argument.Message,
            _ => "操作失败；详细原始数据不会写入日志或上传。",
        };
        MessageBox.Show(message, "AlbumRobot Sync", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
