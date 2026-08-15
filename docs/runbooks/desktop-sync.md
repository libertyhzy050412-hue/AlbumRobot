# Desktop Sync 使用说明

Desktop Sync 是一个可视化 WPF 窗口，用来完成本机 QCE / JSON 读取、网易云专辑识别、本地 Pending 队列和标准化 Batch 上传。完整 QQ 消息不会上传到 Worker。

## 启动

从仓库根目录启动桌面窗口：

```powershell
dotnet run --project apps/sync/src/AlbumRobot.Sync.App/AlbumRobot.Sync.App.csproj
```

也可以先执行 `dotnet build apps/sync/AlbumRobot.Sync.sln`，再直接运行 `apps/sync/src/AlbumRobot.Sync.App/bin/Debug/net10.0-windows/AlbumRobot.Sync.App.exe`。

不需要另开 Worker 终端：

- Worker API 是 `127.0.0.1` / `localhost` 时，桌面端会按需执行本地 D1 migration、启动仓库内 Wrangler、等待 `/api/health`，窗口关闭时只停止自己启动的进程树；
- 已有健康本地 Worker 时直接复用，关闭窗口不会停止它；
- Worker API 是 Cloudflare 远端地址时，只检查远端健康状态，绝不在远端失败时误启本地 Worker。

QCE full mode 的默认地址是 `http://127.0.0.1:40653`。QCE 凭据只从当前 Windows 用户的本机 QCE 配置读取；不要复制或粘贴 QCE token。

## JSON-first 首版操作

1. 首次连接 QCE 时选择一次目标群，让 Desktop Sync 保存本地目标群标识。以后即使 Direct 暂不可用，已保存目标群仍可用于 JSON 导入。
2. 在“Worker API”中填写本地地址、`workers.dev` 地址或正式域名。
3. 远端上传时，在“Sync Token”中粘贴 Cloudflare Worker 对应 Secret。Token 只保留在本次运行内存，不写入设置、SQLite 或日志。
4. 点击“导入 QCE JSON”。文件只在本机解析；识别到的网易云专辑进入 Pending，原始 JSON 不会被复制或上传。
5. 核对识别数量与 Pending 数量。
6. 点击“上传待同步”。只有标准化 ShareCandidate 会发送到 Worker；失败时 Pending 保留，可重试。
7. 打开 PWA，使用群共享密码进入并确认专辑出现。

## Direct 模式

点击“检查连接”可刷新 QCE 群列表；点击“立即同步”会执行 Direct 扫描、首次确认和上传。真实 Direct 网易云卡片映射与历史 overlap 仍需后续证据，因此 JSON-first 是当前首版推荐路径。

## 数据边界

- QCE token 只在本机内存中用于请求头，不写入 AlbumRobot 设置、日志或 URL。
- Sync Token 只保留在当前进程内存；远端请求通过 `Authorization: Bearer` 发送。
- JSON 导入会丢弃嵌套原始 `content` 字符串以及 token、authorization、cookie 等敏感字段。
- Worker 只接收 ShareCandidate：群、成员、消息来源、时间和网易云专辑字段。
- 未识别的歌曲卡、其他音乐平台卡片和普通聊天不会进入 Pending。
- 仓库不保存真实 QQ 群号、QQ 号、昵称、聊天原文、原始 JSON 或真实 URL。

## 常见故障

- “无法连接 QCE”：确认 QQ 已登录、QCE full mode 正在运行且端口为 40653；若目标群已保存，可改用 JSON 导入。
- “远端 Worker 当前不可用”：检查网络、Cloudflare 部署、`/api/health` 和 Worker API 地址；桌面端不会为远端地址启动本地进程。
- “Sync Token 无效”：核对 Cloudflare Worker 的 `SYNC_TOKEN` Secret，Token 不应写入设置文件。
- JSON 导入失败：重新从 QCE 导出 JSON；原始文件不会被复制到 AlbumRobot。
- 上传失败：不要删除本地数据目录，稍后使用“上传待同步”重试。
