# Desktop Sync 使用说明

Desktop Sync 是一个可视化 WPF 窗口，用来完成本机 QCE / JSON 读取、网易云专辑识别、本地 Pending 队列和标准化 Batch 上传。完整 QQ 消息不会上传到 Worker。

## 启动

从仓库根目录启动桌面窗口：

```powershell
dotnet run --project apps/sync/src/AlbumRobot.Sync.App/AlbumRobot.Sync.App.csproj
```

也可以先执行 `dotnet build apps/sync/AlbumRobot.Sync.sln`，再直接运行 `apps/sync/src/AlbumRobot.Sync.App/bin/Debug/net10.0-windows/AlbumRobot.Sync.App.exe`。

不需要另开 Worker 终端：桌面端生产流程只向配置的 Cloudflare HTTPS Worker API 发送标准化数据，不会启动、迁移或等待本地 Worker。Worker 不可用时，Pending 会保留在本机，修复远端后可再次上传。

QCE full mode 的默认地址是 `http://127.0.0.1:40653`。QCE 凭据只从当前 Windows 用户的本机 QCE 配置读取；不要复制或粘贴 QCE token。

Desktop Sync 的“启动 QCE”按钮只对本机 QCE 地址生效。它会先检查 40653 是否已有健康 QCE；如果没有，则会在仓库根目录被忽略的 `qce-data` 目录下递归查找 `launcher-user.bat`（当前安装包位于 `runtime\\v6.2.3\\NapCat-QCE-Windows-x64`）。当 QCE 配置目录里只有一个历史账号时，按钮会自动以 NapCat 快速登录参数启动，优先复用本机授权；没有唯一历史账号时才使用普通二维码流程。已有 QCE 会直接复用，窗口关闭时不会停止 QCE 或 QQ。若仍提示“当前账号已登录，无法重复登录”，说明普通 QQ 进程仍占用该会话，需要完全退出普通 QQ 后再启动 QCE；NapCat 不支持把插件直接注入已运行的普通 QQ 进程。

如需手动验证快速登录，可在 QCE 目录执行（将占位符替换为 QCE 日志中列出的历史账号）：

```bat
launcher-user.bat -q <QQ号>
```

`-q` 只选择本机已有授权记录，不是 QQ 密码，也不会把凭据写入 AlbumRobot。

## JSON-first 首版操作

1. 首次连接 QCE 时选择一次目标群，让 Desktop Sync 保存本地目标群标识。以后即使 Direct 暂不可用，已保存目标群仍可用于 JSON 导入。
2. 在“远端 Worker API”中填写 `workers.dev` 地址或正式域名；桌面端上传不再使用本地 Worker。
3. 首次对某个远端 Worker 上传时，在“Sync Token”中粘贴 Cloudflare Worker 对应 Secret。Desktop Sync 会按 Worker 地址把它保存到当前 Windows 用户的 Credential Manager；之后无需再次粘贴。Token 不写入设置、SQLite、日志或仓库。
4. 点击“导入 QCE JSON”。文件只在本机解析；识别到的网易云专辑进入 Pending，原始 JSON 不会被复制或上传。
5. 核对识别数量与 Pending 数量。
6. 点击一次“上传待同步”。只有标准化 ShareCandidate 会发送到 Worker；超过单批上限时 Desktop 会自动连续提交到当前目标群队列清空并累计结果，失败时未确认的 Pending 保留，可重试。
7. 打开 PWA，使用群共享密码进入并确认专辑出现。

## Direct 模式

点击“检查连接”可刷新 QCE 群列表；点击“立即同步”会执行 Direct 扫描、首次确认并直接通过远端 Worker 上传，和“上传待同步”使用同一 Batch API。真实 Direct 网易云卡片映射与历史 overlap 仍需后续证据，因此 JSON-first 是当前首版推荐路径。

## 数据边界

- QCE token 只在本机内存中用于请求头，不写入 AlbumRobot 设置、日志或 URL。
- Sync Token 只在进程内存中使用，并按 Worker 地址隔离保存在当前 Windows 用户的 Credential Manager；远端请求通过 `Authorization: Bearer` 发送。
- JSON 导入会丢弃嵌套原始 `content` 字符串以及 token、authorization、cookie 等敏感字段。
- Worker 只接收 ShareCandidate：群、成员、消息来源、时间和网易云专辑字段。
- 未识别的歌曲卡、其他音乐平台卡片和普通聊天不会进入 Pending。
- 仓库不保存真实 QQ 群号、QQ 号、昵称、聊天原文、原始 JSON 或真实 URL。

## 常见故障

- “无法连接 QCE”：确认 QQ 已登录、QCE full mode 正在运行且端口为 40653；若目标群已保存，可改用 JSON 导入。
- “远端 Worker 已连接，但运行时配置未完成”：域名和网络是通的，但 Worker 的运行时配置没有全部生效。请在 Cloudflare Worker 的 **Settings → Variables & Secrets** 中检查 `PRIMARY_GROUP_ID`、`GROUP_PASSWORD`、`SESSION_SECRET`、`SYNC_TOKEN` 四项，确认保存后重新部署；不要把值复制到聊天或仓库。
- “远端 Worker 当前不可用”：检查网络、Cloudflare 部署、`/api/health` 和远端 Worker API 地址；桌面端不会启动本地 Worker。
- “Sync Token 无效”：核对 Cloudflare Worker 的 `SYNC_TOKEN` Secret；Desktop Sync 会删除该 Worker 地址对应的本机缓存，下次上传时重新输入一次即可。
- JSON 导入失败：重新从 QCE 导出 JSON；原始文件不会被复制到 AlbumRobot。
- 上传失败：不要删除本地数据目录，稍后使用“上传待同步”重试。
- 网页出现 `Untitled` / `Unknown artist`：确认使用包含 Direct `arkElement.bytesData` 解析修复的新版 Sync，然后按相同回看窗口重新扫描或重新导入 JSON；重复分享也会先更新专辑元数据再返回 duplicate。当前版本不会把普通 QCE 元素的裸 `id` 当作专辑；不要通过前端隐藏数据问题，也不要直接删除生产记录。
