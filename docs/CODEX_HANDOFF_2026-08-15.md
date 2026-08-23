# AlbumRobot Codex 交接记录

更新时间：2026-08-15（Asia/Hong_Kong）
交接状态：`JSON-first real production path validated; GitHub Builds automation and Direct probes pending`

这份文件用于下一个 Codex 对话恢复工作上下文。它不替代产品决策文档：

1. 先读仓库根目录的 `PROJECT_PROGRESS_CODEX.md`；它是产品和架构的 canonical source of truth。
2. 再读仓库根目录的 `DEVELOPMENT_PLAN.md`；它是阶段、验收门和风险顺序。
3. 再读本文件、`docs/probes/qce.md`、`docs/adr/0001-phase-0-baseline.md` 和 `docs/adr/0002-json-first-mvp-auth-and-deployment.md`。
4. 不重新讨论 Q1～Q219，不执行 `git reset --hard`、`git checkout --` 或清理未提交文件。

## 1. 当前结论

- Phase 0 的 monorepo、Worker/D1 本地基线、隐私边界、Cloudflare local 探针骨架和合成 Vertical Slice 已完成并验证。
- QCE v6.2.3 Windows x64 已下载到本机并完成哈希校验；QCE 源码没有复制进仓库，也没有自动捆绑进 Sync 发布物。
- QCE standalone 的本地 API/WebUI 已验证；full mode 已完成 QQ 登录、目标群序号选择和本机脱敏 envelope 探针。
- 第一版可用路径已定为 `QCE 手工导出 JSON → Desktop Sync 本机解析 → SQLite Pending → 远端 Worker/D1 → 密码保护的 PWA`；真实 Direct 网易云卡片分支和跨扫描 ID 稳定性延后验证，但不取消。
- `QceDirectClient` 已实现并测试；`QceMessageNormalizer`、`QceAlbumScanner`、`LocalSyncOrchestrator`、`QceJsonExportNormalizer` 和 JSON 本地导入已接入测试。Direct 尚未取得的证据仍不得凭猜测锁定。
- 可视化 WPF Desktop Sync 已完成：本机 QCE 凭据自动读取、群列表/群选择、回看窗口、首次同步确认、JSON 导入降级、Pending 计数、Batch 上传和本地数据目录入口均由同一窗口操作。
- Desktop Sync 已通过真实 .NET 10 进程冒烟：loopback Worker 可自动迁移、启动并通过健康检查，可复用已有健康 Worker，窗口关闭只回收本次自有进程；用户不需要另开 Worker 终端。
- Worker 已加入共享密码 Session、登录限速和独立 Sync Token，Web Album API 与 Batch API 均受保护；生产 secrets 不进入仓库。
- Cloudflare OAuth、Asia Pacific 生产 D1、远端 migration、Worker、Runtime Secrets 与正式 Custom Domain 已完成；JSON-first 真实生产链路已脱敏验收，没有产生付费服务，也没有上传 QQ 原始数据。

## 2. 已完成的工程变更

### Monorepo 与工程基线

- pnpm workspace：`apps/web`、`worker`、`packages/contracts`。
- .NET 10 solution：`apps/sync/AlbumRobot.Sync.sln`，包含 Core、最小 WPF App、测试。
- 根目录脚本：build、lint、typecheck、test、privacy scan、local smoke、Phase 0 probe。
- Wrangler local/D1 配置、迁移、Worker CI、Prettier、隐私扫描和 ADR 已建立。

### Worker/D1 与 Web

- `worker/migrations/0001_phase0.sql`：groups、全局唯一 albums、group-scoped members、幂等 shares 和索引。
- `worker/src/index.ts`：公开 health、密码 Session、受保护 album browse/detail/feed/stats、Bearer Sync Token 保护的 `POST /api/sync/batch`；Batch 会拒绝原始/敏感字段并返回逐项 accepted、duplicate、invalid 回执。
- `worker/wrangler.local.jsonc`：仅保存合成本地配置；`worker/wrangler.jsonc`：生产 Static Assets/D1 配置，D1 ID 保留显式占位符并由 preflight 阻止误部署。
- `apps/web`：React/Vite 移动优先浏览首版，包含共享密码覆盖层、专辑 / 动态 / 统计、Search Mode、筛选排序、统计钻取、年份 → 月份定位、Shared Element 详情、移动 Bottom Sheet、桌面 Navigation Rail / Floating Sheet 和 Light-only Apple Music 视觉。
- `packages/contracts/src/index.ts`：ShareCandidate、Batch contract 和禁止上传字段集合。

### Sync Core

- `NeteaseAlbumDetector.cs`：优先结构化 album ID，其次标准网易云 album URL；文本猜测不会通过。
- `PendingStore.cs`：SQLite pending/submitting/invalid、崩溃恢复和回执应用。
- `BatchSyncClient.cs`：使用内存态 Sync Token，只上传标准化 ShareCandidate，不上传 Raw QQ 数据。
- `QceDirectClient.cs`：QCE 群列表、成员、消息分页 HTTP 边界；token 只进请求头，不进入 URL 或异常；响应保留为 `JsonDocument`。
- `QceMessageNormalizer.cs`：基于已验证 envelope 字段构造本地 `RawQQMessage`；不伪造缺失 ID/时间，不把普通 `textElement` 送入 Detector。
- `QceAlbumScanner.cs`：本地读取成员与消息、检测结构化网易云候选并写入 SQLite Pending。
- `LocalSyncOrchestrator.cs`：Pending/submitting 恢复、Batch 提交、逐项回执和失败恢复。
- `QceJsonExportNormalizer.cs` / `QceJsonImportService.cs`：本地解析 QCE JSON Export，保留已验证的字段形状，剔除嵌套原始 content 和敏感字段，仅把 Netease album 候选写入 Pending。
- `SyncSettings.cs` / `SyncRuntime.cs`：本地设置、QCE 凭据发现、SQLite 运行时和无 QCE 的 JSON-only fallback。
- `LocalWorkerHost.cs`：仅对 loopback Worker 自动迁移/启动；直接运行仓库内 Node/Wrangler 入口并持有进程树，远端地址不可用时不会启动本地替代。
- `apps/sync/src/AlbumRobot.Sync.App`：可视化 WPF Sync 窗口；所有完整 QQ 数据仍只在本机处理。
- `apps/sync/tests`：35 个 .NET 测试覆盖 Detector、Normalizer、Scanner、Pending、Batch、Orchestrator、JSON 导入、桌面运行时、Worker 托管、QCE HTTP 边界和 Sync Token 安全存储目标。

## 3. QCE 本地状态

### 下载与文件位置

- 官方包：<https://github.com/shuakami/qq-chat-exporter/releases/download/v6.2.3/NapCat-QCE-Windows-x64-v6.2.3.zip>
- 本地压缩包：`E:\AlbumRobot\qce-data\downloads\NapCat-QCE-Windows-x64-v6.2.3.zip`
- 本地解压目录：`E:\AlbumRobot\qce-data\runtime\v6.2.3\NapCat-QCE-Windows-x64`
- `qce-data/` 已被 `.gitignore` 忽略；不要把该目录加入 Git。
- SHA-256：`D079D3DD92E8B0314EDE0A53BF1F528729AF71ACEFDE8CCEABDFCA456B597E49`
- 当前已知 QQNT 路径：`D:\QQ.exe`。重新探测时先检查路径是否仍然存在，不要盲目假设。

### 已验证内容

- 包含 `launcher-user.bat`、`start-standalone.bat`、`qce-server.exe`、NapCat shell 和 QCE WebUI。
- standalone 曾在 `http://127.0.0.1:40654` 启动成功：`GET /` 返回 QCE API `6.2.3`，`GET /qce` 返回 WebUI。
- standalone 没有 live QQ bridge，不能作为真实群消息探针；groups 请求返回服务端失败不代表目标群不可用。
- WebUI 代码确认受保护 API 通过本地请求头传递凭据；`QceDirectClient` 已用单元测试锁定这一边界。
- 默认 full-mode WebUI：`http://localhost:40653/qce`。
- full-mode `40653` 已成功提供真实群列表、成员列表和消息分页；本机探针只写入脱敏字段形状。

### 当前剩余探针与用户参与点

JSON Export 的网易云专辑卡字段已在本机检查并用合成数据覆盖，现作为第一版支持的输入路径。Direct 扫描的同类卡片字段、历史覆盖和重复扫描 overlap 留待 MVP 部署后继续验证。用户不需要粘贴聊天、token 或 Raw Payload。

## 4. 验证记录

已通过：

- `pnpm install --frozen-lockfile`
- `pnpm format:check`
- `pnpm privacy:scan`
- `pnpm lint`
- `pnpm typecheck`
- `pnpm test`：Worker 13/13、Web 8/8 测试通过
- `pnpm build`：Vite build + Wrangler deploy dry-run 通过
- `dotnet restore apps/sync/AlbumRobot.Sync.sln --locked-mode`
- `dotnet build apps/sync/AlbumRobot.Sync.sln --no-restore`：0 warning / 0 error
- `dotnet test apps/sync/AlbumRobot.Sync.sln --no-restore`：35/35 通过
- `pnpm --filter @albumrobot/worker db:migrate:local`
- 启动 Worker 后 `pnpm smoke:local`：认证、accepted、replay duplicate、forbidden payload invalid、受保护 albums 全部通过
- 真实 .NET 10 Worker 托管冒烟：无 8787 自动迁移/启动/health；健康实例复用且不取得所有权；释放后只停止自有进程并关闭 8787
- `git diff --check`：通过（仅有 Windows 换行提示）

## 4.1 上传 Token 自动复用（2026-08-16）

- `SyncTokenCredentialStore` 已接入 Desktop Sync：远端 Worker 的 Sync Token 首次输入后，按 Worker 地址隔离写入当前 Windows 用户的 Credential Manager；后续点击“上传待同步”自动读取。
- 401 会删除对应 Worker 地址的本机缓存，允许用户重新输入；loopback Worker 继续使用合成本地 token，不写入凭据管理器。
- 本机没有可直接读取的现成 AlbumRobot production token；首次使用正式域名仍需要用户在 Sync Token 框输入一次。Token 不写入设置、SQLite、日志、URL、Git 或对话。
- 此次验证：Credential Manager 临时回环读写测试通过并已清理；.NET 35/35、build 0 warning / 0 error；完整 pnpm 门禁和 `git diff --check` 通过。

local smoke 需要先启动 Worker：

```powershell
pnpm --filter @albumrobot/worker dev
pnpm smoke:local
```

## 5. 下一次对话的精确工作顺序

1. 读取 `PROJECT_PROGRESS_CODEX.md`、`DEVELOPMENT_PLAN.md`、本文件和 `docs/probes/qce.md`。
2. 检查 `git status --short`、QCE 目录、40653 端口和 QQ 进程；保留所有未提交实现，不做破坏性清理。
3. 生产 D1、真实 database ID、远端 migration、Worker、四项 Runtime Secrets 和正式 Custom Domain 已完成；不要重复创建资源，也不要把 Cloudflare token 或 Runtime Secrets 写入仓库或对话。
4. 公开 GitHub 仓库的应用代码已合入 `main`，PR 与 `main` push 的 Node / .NET Actions 均通过；当前分支只用于固化生产路由和部署状态文档。
5. 第一版日常使用按 JSON-first runbook：把 Desktop Sync 的 Worker API 设为 `https://album.rocknrollliberty.dpdns.org`，首次在 Sync Token 中输入一次后由当前 Windows 用户的 Credential Manager 按 Worker 地址安全保存，在 QCE 手工导出 JSON 后导入并点击“上传待同步”；完整 QQ 数据仍只在本机处理，超过单批上限会自动连续提交。
6. JSON-first 真实生产路径已经验收；下一部署自动化步骤是在 Cloudflare 选择 **Continue with GitHub** 连接现有 AlbumRobot 仓库。Root directory 为 `/`，Build command 为 `pnpm --filter @albumrobot/web build`，Deploy command 为 `pnpm --dir worker run deploy`，Build Variables 设置 `NODE_VERSION=24` 和 `PNPM_VERSION=11.19.0`。
7. GitHub Builds 连接前，当前可恢复的生产发布命令是仓库根目录的 `pnpm deploy:worker`；不要另建 Hello World Worker、Pages 项目或第二个 D1。
8. MVP 可用后再继续 Direct 同卡片、跨扫描 ID 和历史 overlap 探针；仅记录脱敏字段形状并补充合成测试。
9. 每个阶段继续汇报：已完成变更、验证结果、风险、下一步；只有产品边界、部署成本、隐私、核心数据模型或目标不可行时询问用户。

## 6. 明确不要做的事

- 不重新讨论 Q1～Q219。
- 不启用 QQ Bot、CloudBase、VPS、Paid Cloudflare、多群 UI、移动原生 App、WebSocket/SSE 或自动常驻 Sync。
- 不把 QCE 源码复制进 AlbumRobot，不把 QCE 二进制静默捆绑进发布物。
- 不把完整 QQ/Raw 数据发往 Cloudflare、Git 或对话。
- 不在日志、fixture、异常和诊断包中保存 token、密码、cookie、QQ 原文或真实标识。
- 不在真实 QCE schema 未确认前猜测稳定 member ID、message ID、sequence 或网易云 card 字段。
- 不强制关闭用户原有 QQ；只清理由当前任务明确启动且已验证归属的临时进程。
- 不在生产 D1 ID、四项 Runtime Secrets、隐私复核和生产 preflight 未通过时进行公网部署。

## 7. 可直接复制的继续工作提示词

```text
你正在继续 E:\AlbumRobot 的 AlbumRobot 项目。先完整阅读：
1. E:\AlbumRobot\PROJECT_PROGRESS_CODEX.md
2. E:\AlbumRobot\DEVELOPMENT_PLAN.md
3. E:\AlbumRobot\docs\CODEX_HANDOFF_2026-08-15.md
4. E:\AlbumRobot\docs\probes\qce.md

不要重新讨论 Q1-Q219。沿用已有未提交实现，不执行 git reset --hard、git checkout -- 或删除未提交文件。

当前重点是固化已通过的 JSON-first 真实生产验收，并继续 Cloudflare GitHub Builds 自动部署与 Direct 探针。生产 D1、Worker、四项 Runtime Secrets、正式 Custom Domain、GitHub main 和 JSON-first 真实链路均已完成；请先检查 git status、QCE 本地目录、40653/8787 端口、QQ/QCE/AlbumRobot 进程，不要假设上轮自动化没有留下进程。

已完成并保留的实现：Phase 0 monorepo、Worker/D1 本地基线、隐私边界、合成 Vertical Slice、可视化 WPF Desktop Sync V1、QCE Direct/JSON Normalizer、SQLite Pending、Batch 上传、Worker/PWA 认证和生产部署门禁。首版路径是 QCE 手工导出 JSON → Desktop Sync → Worker/D1 → 密码保护 PWA；Direct 同 card 分支、跨扫描 ID 稳定性和历史 overlap 后续再验证。

自动托管 Worker 已验收：
- `LocalWorkerHost` 现在直接运行仓库内 `node.exe` 与 Wrangler JavaScript 入口，避免 `pnpm.cmd` shim 退出后遗留 workerd；真实 .NET 10 冒烟已验证自动迁移、启动、health、健康实例复用和只停止自有进程。
- 该行为只适用于 loopback 开发地址。生产 Desktop 使用远端 Cloudflare Worker；远端不可用时会给出安全错误，不会启动本地 Worker 冒充生产服务。
- `dotnet test apps/sync/AlbumRobot.Sync.sln --no-restore` 为 27/27，build 为 0 warning/0 error；Worker 测试为 13/13、Web 测试为 8/8，本地认证 smoke 通过。

本机 QCE 诊断证据：
- 40653 `qce-server` 与 QQ 保持运行；群列表、成员和 message envelope 曾在本机脱敏验证。
- `/api/messages/fetch` 曾在 QCE export 任务占用期间超时。该历史问题不阻塞 JSON-first MVP，也不能作为 Direct 稳定性的正面证据。
- 完整 QCE JSON 和任务状态继续留在被忽略的本地目录；不要读取、保存或输出其中的真实群标识、文件名、消息正文或 token。
- 8787 在无人使用时应关闭。Desktop loopback 自动托管已经验收，不要再要求用户另开 Worker 终端；生产 Desktop 直接使用远端 Cloudflare Worker。

下一步必须按顺序：
1. 阅读 `docs/runbooks/cloudflare-first-deploy.md`，确认现有生产状态；生产 D1、database ID、远端 migration、Worker、Secrets 和正式域名不要重复创建。
2. Desktop Sync 的 Worker API 已指向正式域名，真实 JSON-first 标准化批次已完成生产验收；后续日常导入继续遵守相同隐私边界，一次提交会自动分批清空 Pending。
3. 不要在对话或 Git 中输出凭据和 secrets，不要上传 Raw QQ、普通聊天正文或完整 JSON。
4. 在 Cloudflare 为现有 `albumrobot` Worker 连接公开 GitHub 仓库 `main`，显式设置 Node 24 / pnpm 11.19.0，关闭 Preview；不要新建 Hello World、Pages 项目、Worker 或 D1。
5. MVP 可用后才继续 Direct 同卡片、跨扫描 ID 和历史 overlap 探针。

每阶段汇报：已完成变更、验证结果、风险、下一步。只有产品边界、部署成本、隐私、核心数据模型或目标不可行时才提问。
```

## 8. 本次对话增量摘要（2026-08-15）

### 用户操作与真实输入边界

- 用户已退出并重新登录 QQ，QCE full mode/WebGUI 已打开；二维码曾过期后重新登录成功。
- WebGUI 群列表在 PowerShell 中显示乱码，原因是旧 Windows PowerShell 输出编码，不是群名数据损坏；后续本机 HTTP 读取改为 UTF-8，WPF UI 可正常显示 Unicode。
- 用户确认第一版可以手工使用 QCE JSON Export；当前交付路线改为 JSON-first，Direct API 保留为后续增强。
- 用户提供过三类 QCE JSON Export 样本用于字段核对：网易云歌曲卡（`meta.music`，不是专辑）、非网易云专辑卡（应忽略）和网易云专辑卡（`meta.news` + 网易云 album URL，正例）。真实内容、token、真实 ID、昵称和私有 URL 没有写入仓库或文档；代码测试使用 synthetic example。
- 用户明确要求桌面端必须有可视化界面，并希望“点击立即同步一键完成”，不需要单独启动 Worker。

### 代码与文档变化

- 新增/修改 QCE JSON Export Normalizer、JSON 本地导入、SyncSettings/Store、QCE 凭据发现、SyncRuntime、WPF 主窗口和桌面运行说明。
- WPF 窗口包含：QCE 检查连接、群选择、回看天数、立即同步、导入 QCE JSON、上传待同步、打开数据目录、扫描/候选/接受/重复无效统计和运行状态。
- `PendingStore` 使用 SQLite `Pooling=False` 并同步释放连接，以避免测试/桌面重启时数据库文件锁残留。
- `LocalWorkerHost.cs` 已通过真实 .NET 10 启动与所有权冒烟；不要在下一次对话中丢弃或重置它。
- 文档已更新：`PROJECT_PROGRESS_CODEX.md`、`README.md`、`docs/probes/qce.md`、本文件，并新增 `docs/runbooks/desktop-sync.md`。

### 验证记录

- .NET 测试最新已通过 27/27。
- .NET solution build 最新已通过 0 warning/0 error（包含 WPF App 和 `LocalWorkerHost` 编译）。
- 根目录格式、隐私扫描、Lint、TypeScript、Worker test/build 曾全部通过；文档格式修正后 `pnpm format:check` 和 `pnpm privacy:scan` 也通过。
- WPF 可视化与本地浏览器认证冒烟均通过；Web 密码覆盖层的错误态、成功态和 Album 页面均已检查，控制台无错误。
- 当前收尾检查时 40653 / 8787 均无监听，也没有 QQ、QCE 或 AlbumRobot 进程；后续仍必须重新检查，不能假设 UI 自动化不会留下进程。

### 隐私与安全不可违背项

- 不索取或输出 Raw Payload、聊天原文、token、cookie、真实群号、QQ 号、昵称或私有 URL。
- QCE token 只允许本机内存和请求头使用；不得进 URL、设置文件、日志、异常、fixture、Git 或对话。
- 完整 QQ 数据只在本机处理；Worker/D1 只接收标准化 ShareCandidate。
- 不执行 `git reset --hard`、`git checkout --`，不删除用户未提交文件，不强制关闭用户 QQ/QCE；只清理由当前测试明确启动且确认归属的进程。

## 9. Light-only PWA 浏览首版增量

- 用户明确覆盖原浅 / 深色决定：只实现浅色外观。`docs/adr/0003-light-only-apple-music-ui.md` 已记录覆盖关系；代码不存在 Dark media query。
- 新增 Worker 专辑搜索 / 筛选 / 排序、专辑详情与最近分享、动态、UTC+8 周 / 月 / 年统计 API；所有读取都受 Session 保护并按配置群隔离。
- 前端使用 React Query、Zustand 和 Motion；实现三标签、独立滚动保存、Apple Search Mode、动态 Sticky 日期、两级日期定位、统计成员钻取、Shared Element 详情、浏览器 Back 优先关闭详情、Reduced Motion。
- 使用 `scripts/seed-visual-local.mjs` 生成纯合成本地视觉数据；没有读取、输出或提交真实 QQ/QCE 内容。
- 浏览器实测覆盖 1280px、390px 和 320px；移动两列 Grid、Bottom Navigation、Feed、Stats、Bottom Sheet 下拉关闭与桌面 Floating Sheet 均通过；控制台 0 error / 0 warning，无横向溢出。
- 当前不是完整 Phase 4 / 5。Feed 分页 / windowing、pull-to-refresh、新动态提示、离线缓存、PWA install、Admin UI、完整手势所有权和 iOS / Android / QQ 内置浏览器真机 QA 仍待后续。

## 10. 首次生产部署增量

- 公开 GitHub 仓库已保留公开状态，JSON-first V1 经 PR 合入 `main`；PR 与 `main` push 的 GitHub Actions 均通过 Node / .NET 检查。
- GitHub CI 首轮发现并修复 Linux 隐私扫描路径与并行构建静态资源竞态；修复后的 Ubuntu Node 与 Windows .NET 任务均通过。
- 生产 D1、远端 migration、Worker、四项加密 Runtime Secrets 和正式 Custom Domain 已完成。正式入口为 `https://album.rocknrollliberty.dpdns.org`，`workers.dev` 与 Preview URL 已显式关闭。
- 正式入口已通过 health、HTML、错误 / 正确密码、受保护 Session、专辑读取、无 Token 401 和带 Token 空批次 200；Session Cookie 具备 `HttpOnly`、`Secure`、`SameSite=Lax`。
- 生产凭据没有进入 Git、日志或对话；Desktop Sync 的 Sync Token 仅保存在当前 Windows 用户的 Credential Manager，按 Worker 地址隔离，不进入设置、SQLite 或日志。
- Desktop UI 已用一份用户指定的真实 QCE JSON Export 完成生产验收；原文件仅在本机解析，Worker/D1 只接收标准化候选，受保护 API 与 D1 聚合只读检查确认数据可用。真实群、成员、消息、文件名与内容均未写入文档。
- 验收发现并修复了超过 100 条 Pending 需要重复点击的问题：编排器现在一次操作自动分批清空目标群队列、累计回执，WPF 显示完整 Pending 总数；合成回归测试覆盖跨批次行为。

## 11. JSON 通用 ID 误报修复与生产清理

- 根因已经确定：JSON Normalizer 会展开每个 QCE 元素的 `data`，旧 Detector 又把通用 `id` 当作专辑 ID，因此普通表情、贴纸和回复引用会生成缺元数据 Album；前端只是正确显示了云端占位值，并非视觉层故障。
- `NeteaseAlbumDetector` 已移除通用 `id` 入口，只保留明确专辑 ID 字段和经网易云 album URL 验证的分支。新增三条合成 .NET 回归覆盖裸 ID 拒绝、有效 URL 优先及 JSON 元素误报。
- Worker Album upsert 已防止 `Untitled` / `Unknown artist` 覆盖已有完整元数据，并新增一条 Worker 回归。
- 修复版已通过 Wrangler CLI 部署。清理前取得了 D1 Time Travel 恢复书签（不写入公开仓库，保存在本机 Windows Credential Locker 的 `AlbumRobot D1 Recovery` 项），精确复核并删除 24 个误报 Album 及其 51 条关联 Share。
- 清理后 D1 和密码会话 API 均复核通过：68 个 Album、76 条 Share，未知标题、未知艺人、缺失封面和孤立 Album 均为 0。
- 最新门禁：Worker 13/13、Web 8/8、.NET 27/27；完整 pnpm 门禁、.NET build（0 warning / 0 error）、production preflight 与 `git diff --check` 通过。
- 当前这些修改与之前的一键跨批提交改动仍在工作树中，尚未代表已经提交或推送；继续时不得重置或覆盖。
- Desktop Sync 已新增“启动 QCE”按钮；它会在被忽略的 `qce-data` 目录下递归查找 `launcher-user.bat`，存在唯一历史账号时通过 NapCat 快速登录参数优先复用本机授权，只启动或复用本机 full-mode QCE，不获取或输出 QCE token，也不会在关闭窗口时终止 QCE/QQ。普通 QQ 占用同一会话时仍需退出普通 QQ；NapCat 不支持直接注入已运行的普通 QQ。安装新版后需重启 Desktop Sync 才能看到按钮。

## 12. 远端 Worker 配置诊断（2026-08-16）

- 正式域名 DNS/TLS 可达，`GET /api/health` 实测 HTTP 200，但响应为 `ok:false`、`configured:false`；本机没有读取或输出 Secret 值。
- `wrangler secret list` 显示四个 Secret 名称存在，但这不能证明值已满足 Worker 的运行时校验；需在 Cloudflare 重新保存并部署 `PRIMARY_GROUP_ID`、`GROUP_PASSWORD`、`SESSION_SECRET`、`SYNC_TOKEN`，再复测 `configured:true`。
- Desktop Sync 已将该状态与网络不可达区分显示；在远端健康恢复前不要继续上传验收。远端 URL 仍只健康检查，不会启动本地 Worker。

## 13. Desktop 远端 Worker 一键同步（2026-08-16）

- “立即同步”和“上传待同步”统一通过 `SyncRuntime` 的远端 Batch API 推送；桌面 UI 不再调用 `LocalWorkerHost`，也不再启动、迁移或等待本地 Worker。
- Sync 启动只加载本地设置，不自动刷新 QCE/Worker；用户点击“启动 QCE”或“检查连接”后才主动读取群列表，避免启动时阻塞式失败弹窗。
- Worker 默认地址改为正式 HTTPS 域名；上传拒绝 loopback Worker，远端失败时标准化 Pending 保留在本机等待重试。
- 本次回归：.NET Release 测试 37/37、Release build 0 warning / 0 error；pnpm 全部门禁与 `git diff --check` 通过。
