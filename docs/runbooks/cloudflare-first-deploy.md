# Cloudflare 首次部署

本 runbook 用于把 JSON-first MVP 部署为同源的 Cloudflare Worker + D1 + React SPA。完整 QCE JSON、聊天正文、QCE token 和桌面本地数据库都不得进入 GitHub 或 Cloudflare。

## 当前生产状态（2026-08-15）

- 公开 GitHub 仓库的应用代码已合入 `main`，PR 与 `main` push 的 Node / .NET GitHub Actions 均通过；
- 生产 D1、database ID、初始远端 migration、Worker、四项 Runtime Secrets 和正式 Custom Domain 均已完成；
- 正式入口为 `https://album.rocknrollliberty.dpdns.org`，已经通过无真实数据的健康、认证、读取和空 Batch 验收；
- Cloudflare GitHub Builds 尚未连接。当前可恢复的发布路径是仓库根目录运行 `pnpm deploy:worker`；连接 GitHub 后再把它切换为自动生产发布路径；
- `workers.dev` 只用于首次部署验收，绑定正式域名后已显式关闭；Preview URL 与非生产分支部署也保持关闭，避免误用生产 D1。

## 1. 本地发布门

从仓库根目录运行：

```powershell
pnpm install --frozen-lockfile
pnpm format:check
pnpm privacy:scan
pnpm lint
pnpm typecheck
pnpm test
pnpm build
dotnet test apps/sync/AlbumRobot.Sync.sln --no-restore
dotnet build apps/sync/AlbumRobot.Sync.sln --no-restore
git diff --check
```

只有这些检查通过，且 `git status` 中没有 `qce-data/`、数据库、日志、`.dev.vars`、token 或真实 JSON，才进入下一步。

## 2. 创建生产 D1

当前检查点：`albumrobot-prod` 已在 Asia Pacific 创建，database ID 已写入生产配置，`0001_phase0.sql` 已成功应用。首次部署继续执行时不要重复创建；本节保留为灾难恢复和新环境重建步骤。

在 Cloudflare Dashboard 的 D1 页面创建数据库：

```text
albumrobot-prod
```

复制 Cloudflare 返回的 database ID，只在本机把 `worker/wrangler.jsonc` 中的 `REPLACE_WITH_D1_DATABASE_ID` 替换为该 ID。不要把账号 token 或其他 Secret 写进该文件。

使用已登录的 Wrangler 应用远端 migration：

```powershell
pnpm --dir worker exec wrangler login
pnpm --dir worker run db:migrate:remote
```

生产 migration 不应由每次 Git push 自动执行。

## 3. 提交并推送 GitHub

现有远端仓库是：

```text
https://github.com/libertyhzy050412-hue/AlbumRobot
```

推送前再次运行 `pnpm privacy:scan` 并人工检查 staged files。QCE 本地目录、Raw JSON、本地 SQLite 和 Secret 必须继续留在 `.gitignore` 范围内。

## 4. 在 Cloudflare 连接 GitHub

在“创建 Worker”页面选择 **Continue with GitHub**，然后选择 `libertyhzy050412-hue/AlbumRobot`。

当前此项仍待完成；不要另建 Hello World Worker、Pages 项目或第二个 AlbumRobot Worker。应把现有 `albumrobot` Worker 连接到该仓库。

建议构建设置：

```text
Production branch: main
Root directory: /（仓库根目录）
Build command: pnpm --filter @albumrobot/web build
Deploy command: pnpm --dir worker run deploy
```

Build Variables 还必须显式设置：

```text
NODE_VERSION: 24
PNPM_VERSION: 11.19.0
```

Cloudflare 构建镜像的默认 Node/pnpm 主版本低于本仓库要求；根目录的 `.node-version` 也固定了 Node 24，但仍建议在 Dashboard 同时设置上述两项，避免镜像默认值变化。它们只是构建工具版本，不是 Runtime Secrets。

首版关闭非生产分支自动部署，避免 Preview 与生产 D1 混用。仓库根目录包含 `pnpm-lock.yaml` 和 workspace 配置；部署命令会从 `worker/wrangler.jsonc` 发布 API 和 `apps/web/dist` 静态资源。生产配置还包含一个每分钟 10 次的群密码登录限速绑定，不需要另建 Cloudflare 资源。

## 5. 设置 Worker Runtime Secrets

Worker 创建后，进入 **Settings → Variables & Secrets**，把以下四项都设为加密 Secret：

```text
PRIMARY_GROUP_ID  # 与桌面端保存的目标群稳定 ID 完全一致
GROUP_PASSWORD    # 群友进入网页时使用
SESSION_SECRET    # 独立高熵随机值，至少 32 bytes
SYNC_TOKEN        # 独立高熵随机值，至少 32 bytes
```

四项不得互相复用，也不得写入 GitHub、构建变量、截图、日志或聊天。Secret 配置完成后重新触发一次生产部署。

当前四项已配置为 Cloudflare 加密 Secret。可恢复副本只保存在本机 Windows Credential Locker 的 `AlbumRobot Production` 项；不得为了排错把值复制到日志或对话。

## 6. 部署验收

首次部署已先使用 Cloudflare 提供的 `workers.dev` 地址验收；绑定正式域名后 `workers.dev` 已关闭。以下 1～4 已在 `workers.dev` 和正式域名通过，带正确 Token 的空批次也已返回 200：

1. `GET /api/health` 返回 `ok: true`、`configured: true`；
2. 访问根路径显示群共享密码覆盖层；
3. 错误密码原位失败，正确密码进入专辑页；
4. 未携带 Sync Token 的 `/api/sync/batch` 返回 `401`；
5. 桌面端把 Worker API 改为 `workers.dev` 地址，输入 Sync Token；
6. 在本机导入 QCE JSON，确认只产生标准化 Pending，再上传一个极小批次；
7. 网页刷新后出现对应专辑；重复上传收敛为 duplicate。

## 7. 绑定正式域名

Custom Domain 已完成，并固化在 `worker/wrangler.jsonc`。灾难恢复或新账户重建时，可在 Worker 的 **Settings → Domains & Routes → Add → Custom Domain** 中添加：

```text
album.rocknrollliberty.dpdns.org
```

首次绑定前已确认该 hostname 没有冲突的 A、AAAA 或 CNAME；绑定完成后 DNS、TLS 与 `/api/health` 已通过。Desktop Sync 的 Worker API 应设为：

```text
https://album.rocknrollliberty.dpdns.org
```

## 8. JSON-first 日常流程

1. 在 QCE 中导出目标群 JSON；
2. 打开 AlbumRobot Sync，选择已保存目标群；
3. 点击“导入 QCE JSON”；
4. 核对识别数量和 Pending 数量；
5. 点击“上传待同步”；
6. 打开 PWA 确认专辑出现。

JSON 原文件始终留在本机。Direct QCE 扫描是后续增强，不阻塞本 MVP。
