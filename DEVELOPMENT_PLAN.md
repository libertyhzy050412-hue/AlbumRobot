---
project: AlbumRobot
document_role: executable-development-plan
document_version: 1.0
updated_at: 2026-08-15
source_of_truth: PROJECT_PROGRESS_CODEX.md v5.0
target_release: V1
status: ready-for-implementation
---

<!-- markdownlint-disable MD013 -->

# AlbumRobot V1 开发落地方案

> 本文把 `PROJECT_PROGRESS_CODEX.md` 中 Q1～Q219 的已锁定产品决策，转换为可执行的工程路线、阶段门、交付物和验收标准。
>
> 若本文与 `PROJECT_PROGRESS_CODEX.md` 冲突，以后者为准；实现过程中不得重新解释或扩大 V1 产品边界。

## 1. 交付目标

V1 最终交付一条真实、可重复、可恢复的数据链路：

```text
Windows QQ
  → QCE
  → AlbumRobot Sync（本地识别、修复、排队）
  → Cloudflare Worker /api/sync/batch
  → D1
  → Mobile-first React PWA
```

完成后应满足：

1. 管理员在 Windows 上手动打开 Sync，点击一次“立即同步”，即可把当前目标群中新出现的网易云专辑分享同步到云端；
2. 完整 QQ 消息、普通聊天正文、无关图片和失败 Raw Payload 不离开本机；
3. 群友通过共享密码进入 PWA，浏览“专辑 / 动态 / 统计”；
4. 同一来源消息可安全重传，不产生重复 Share；
5. 本地崩溃、网络超时、重复扫描或 Batch 重试后仍能收敛到正确状态；
6. 管理员能完成专辑编辑、Album / Member 合并、Album / Share 删除和密码轮换；
7. 移动端视觉与动效达到项目定义的 Apple Music 风格，核心 Bottom Sheet 与 Shared Element 可中断、可降级；
8. 全栈长期运行在 Cloudflare Free 范围内，不主动启用付费服务。

## 2. 当前状态与规划假设

### 2.1 当前仓库状态

- 产品和架构讨论已完成，Q1～Q219 均已有明确结论；
- 当前尚无应用代码、数据库迁移、测试或部署配置；
- 本机已有 .NET 10 SDK、Node.js 24、pnpm 11 和 Git；
- Cloudflare Wrangler 作为项目本地开发依赖安装，不要求全局安装；
- GitHub 仓库采用单一 monorepo，不拆分 Web、Worker 和 Sync 仓库。

### 2.2 域名就绪状态

2026-08-15 通过 Cloudflare DNS（`1.1.1.1`）和 Google DNS（`8.8.8.8`）交叉验证：

- `rocknrollliberty.dpdns.org` 已正确委派给 Cloudflare nameserver：
  `andronicus.ns.cloudflare.com`、`clara.ns.cloudflare.com`；
- `album.rocknrollliberty.dpdns.org` 当前为 NXDOMAIN，表示具体主机记录尚未创建；
- 该状态不阻塞开发。生产部署时应把 Worker 配置为该 hostname 的 Custom Domain，由 Cloudflare 自动创建 DNS 记录并签发证书；
- 在 Custom Domain 建立前无法验证该 hostname 的 HTTPS；部署后的验收必须重新检查全球 DNS、TLS 和大陆网络可达性。

### 2.3 计划口径

- 以一名全栈开发者顺序推进估算；“工程日”只表示工作量，不是固定发布日期；
- 所有阶段以验收门为准，不以“代码写完”或日期到期为准；
- Phase 0 的真实技术探针可以改变实现细节，但不能静默降低 LOCKED 产品目标；
- 只有探针证明目标不可行，且备选会改变产品、部署、费用、隐私或核心数据模型时，才向用户升级确认。

## 3. 总体工程策略

### 3.1 先打穿纵向切片

第一优先级不是搭完整页面或完整后台，而是让一条真实分享走完全链路：

```text
真实 QCE 消息
  → 解析到 netease_album_id
  → 本地 Pending Queue
  → Batch API accepted
  → D1 Album / Member / Share
  → PWA Album Grid 出现真实封面
```

在这条链路通过前，不投入完整 Admin、历史全量导入、复杂离线能力或 Motion 精修。

### 3.2 以风险排序，而非按页面排序

风险优先级如下：

1. QCE 是否能稳定提供群、成员、消息和网易云卡片结构；
2. 来源消息与成员是否存在可用的稳定 ID / sequence；
3. 网易云短链和 metadata 补全是否稳定；
4. Pending / Batch / Checkpoint 是否在失败场景下保持幂等；
5. Cloudflare Free 配额和 D1 查询模型是否可持续；
6. QQ 内置浏览器与移动浏览器对手势、History、PWA 和缓存的实际支持；
7. 最后才是视觉细节和桌面端精修。

### 3.3 每阶段都必须可演示、可回归、可回滚

每个 Phase 的合并条件统一包含：

- 有可运行演示路径；
- 有自动化测试或明确的人工测试清单；
- 有迁移 / 配置 / 回滚说明；
- 无 Raw QQ 数据、密码、Token、Session 或本地数据库进入 Git；
- 新的实现级决策写入 ADR；
- `PROJECT_PROGRESS_CODEX.md` 的里程碑状态得到同步更新。

## 4. 目标架构与模块映射

### 4.1 运行时架构

```text
┌────────────────────────────────────────────────────────────┐
│ Windows 本机                                               │
│                                                            │
│  QQ / QCE                                                  │
│     │ localhost API 或 JSON Export                         │
│     ▼                                                      │
│  AlbumRobot Sync                                           │
│  ├─ QceRuntimeManager                                      │
│  ├─ DirectApiProvider / JsonExportProvider                 │
│  ├─ RawQQMessage Normalizer                                │
│  ├─ NeteaseAlbumDetector                                   │
│  ├─ MetadataResolver + Cache                               │
│  ├─ Pending / ParseError / Ignored                         │
│  └─ BatchSyncClient + SyncState                            │
└───────────────────────┬────────────────────────────────────┘
                        │ 只上传标准化 ShareCandidate
                        ▼
┌────────────────────────────────────────────────────────────┐
│ Cloudflare Free                                            │
│                                                            │
│ Worker API                                                 │
│  ├─ Sync Auth + Batch Ingestion                            │
│  ├─ Shared Password / Admin Authorization                  │
│  ├─ Browse / Search / Feed / Stats                         │
│  └─ Admin Mutations + AdminAction                          │
│                        │                                   │
│                        ▼                                   │
│                       D1                                   │
│                                                            │
│ Workers Static Assets → React PWA                          │
└────────────────────────────────────────────────────────────┘
```

### 4.2 领域模块及调用关系

| 领域模块               | 主要职责                                          | 直接调用者                | 明确不负责                    |
| ---------------------- | ------------------------------------------------- | ------------------------- | ----------------------------- |
| `QceAdapter`           | 获取群、成员、消息并统一成 `RawQQMessage`         | Sync 扫描编排器           | 网易云判断、云端上传          |
| `NeteaseAlbumDetector` | 从可靠结构、URL 或短链确认 Album 身份             | Sync 扫描编排器、人工修复 | 模糊文本猜测、AI 识别         |
| `MetadataResolver`     | 补齐缺失 title / artist / cover / URL             | Detector、人工修复        | 改变 album ID、周期性全量刷新 |
| `LocalQueue`           | Pending、Parse Error、Ignored、崩溃恢复、本地幂等 | Sync 编排器、Sync UI      | 云端事实来源                  |
| `BatchIngestion`       | 校验标准化候选、D1 原子写入、逐项回执、Checkpoint | AlbumRobot Sync           | 接收 Raw QQ Payload           |
| `Catalog`              | 专辑列表、排序、时间范围、搜索                    | PWA 专辑页                | 动态流、成员主页              |
| `Feed`                 | 按日期连续展示 Share、时间锚点与游标              | PWA 动态页                | 常驻内容筛选                  |
| `Stats`                | 周 / 月 / 年不同专辑数与成员排行                  | PWA 统计页                | 热门专辑榜、图表 Dashboard    |
| `Auth`                 | 普通 Session、Admin Authorization、版本失效       | PWA、Admin API            | 个人账号、RBAC                |
| `Admin`                | 编辑、删除、Merge、密码轮换、轻量审计             | PWA Admin Mode            | 批量管理、Undo、回收站        |
| `OfflineCache`         | 最近只读数据和已加载封面、失效清理                | PWA                       | 完整 Offline-first、离线写入  |

## 5. 技术选型基线

这些均为实现级决策，可由探针或真实测试调整；调整需写 ADR，但不需要重新讨论产品需求。

| 层           | 选择                                         | 原因                                                                                      |
| ------------ | -------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Monorepo     | pnpm workspace + .NET solution               | Node 子项目共享锁文件，C# 保持原生工具链，避免引入重型 monorepo orchestrator              |
| Sync UI      | WPF + .NET 10 LTS                            | Windows-only 目标、依赖成熟、部署风险低；本机已具备 SDK / Desktop Runtime                 |
| Sync 架构    | MVVM + Core / Infrastructure 分层            | UI 与 QCE、SQLite、HTTP 可独立测试，不引入微服务式复杂度                                  |
| 本地数据库   | `Microsoft.Data.Sqlite` + 编号 SQL migration | 模型小、事务边界重要，直接 SQL 比 EF Core 更透明                                          |
| 本地日志     | Serilog rolling file                         | 结构化、可控字段、轮转成熟；日志过滤器必须先于 sink 生效                                  |
| Worker       | TypeScript + Hono + D1 prepared statements   | 路由数量已超出单文件适用范围，Hono 轻量且适配 Workers；数据访问保留显式 SQL               |
| API Contract | OpenAPI + 示例 JSON +跨语言 contract tests   | Web 与 Worker 为 TS、Sync 为 C#，使用语言中立契约防止 DTO 漂移                            |
| Web          | React + Vite + TypeScript                    | 已 LOCKED；保持 SPA，不引入 SSR                                                           |
| Server State | TanStack Query                               | 游标、预取、回前台刷新、缓存失效和并发请求控制成熟                                        |
| UI State     | Zustand                                      | 保存各 Tab 独立状态、Sheet / Search / Drill-down 状态，避免全局 Context 重渲染            |
| Motion       | Motion for React + 自定义 pointer 状态机     | 通用 spring / layout animation 交给库；Gesture Ownership 和 Shared Element 降级由项目控制 |
| 长列表       | TanStack Virtual，达到阈值后启用             | 避免一开始为小数据引入虚拟化复杂度，同时为历史 Feed 保留明确升级点                        |
| PWA          | `vite-plugin-pwa` 的 `injectManifest` 模式   | 需要自行控制 Session 失效清缓存、API 离线语义和封面缓存上限                               |
| Worker 测试  | Vitest + Workers 本地运行环境 / D1 local     | 尽量使用与生产相同的 Workers / D1 行为                                                    |
| Web E2E      | Playwright                                   | 覆盖 Chromium、WebKit、移动 viewport、History 和离线场景                                  |
| Sync 测试    | .NET test + 单元 / SQLite integration tests  | 可验证解析、队列、事务和崩溃恢复                                                          |

### 5.1 暂不采用

- 不使用 Next.js / SSR；
- 不使用微服务、消息队列、Event Sourcing 或通用插件平台；
- 不在 D1 上增加 ORM abstraction；
- 不使用 WebSocket / SSE；
- 不把 QCE 源码复制进 AlbumRobot；优先以外部本地进程 / HTTP 边界集成；
- 不为了桌面端视觉先引入跨平台桌面框架。

## 6. 推荐仓库结构

```text
AlbumRobot/
├─ apps/
│  ├─ web/
│  │  ├─ src/
│  │  │  ├─ app/                 # App Shell、导航、History 状态
│  │  │  ├─ features/
│  │  │  │  ├─ albums/
│  │  │  │  ├─ feed/
│  │  │  │  ├─ stats/
│  │  │  │  ├─ auth/
│  │  │  │  ├─ admin/
│  │  │  │  └─ offline/
│  │  │  ├─ motion/              # Token、手势状态机、Shared Element
│  │  │  ├─ api/
│  │  │  └─ styles/
│  │  ├─ public/
│  │  └─ tests/
│  └─ sync/
│     ├─ AlbumRobot.Sync.sln
│     ├─ src/
│     │  ├─ AlbumRobot.Sync.App/            # WPF / MVVM
│     │  ├─ AlbumRobot.Sync.Core/           # 领域模型与扫描编排
│     │  └─ AlbumRobot.Sync.Infrastructure/ # QCE、SQLite、HTTP、日志
│     └─ tests/
│        ├─ AlbumRobot.Sync.UnitTests/
│        └─ AlbumRobot.Sync.IntegrationTests/
├─ worker/
│  ├─ src/
│  │  ├─ routes/
│  │  ├─ middleware/
│  │  ├─ domain/
│  │  ├─ repositories/
│  │  └─ index.ts
│  ├─ migrations/
│  └─ tests/
├─ packages/
│  └─ contracts/
│     ├─ openapi.yaml
│     ├─ examples/
│     └─ src/                    # TS 类型与运行时校验 schema
├─ fixtures/
│  └─ sanitized/                # 仅脱敏、最小、人工复核过的 fixture
├─ docs/
│  ├─ adr/
│  ├─ probes/
│  ├─ runbooks/
│  └─ qa/
├─ scripts/
├─ .github/workflows/
├─ DEVELOPMENT_PLAN.md
├─ PROJECT_PROGRESS_CODEX.md
├─ README.md
├─ pnpm-workspace.yaml
└─ package.json
```

仓库初始化时先建立 `.gitignore` 和隐私检查，再允许真实探针产生任何文件。

## 7. 数据契约

### 7.1 `RawQQMessage`：只存在本地

`RawQQMessage` 是 Direct API 与 JSON Provider 的统一输出，至少表达：

```text
provider
group_source_id
message_source_id?
message_sequence?
sender_source_id?
sender_nickname?
sent_at?
sent_date?
source_order?
message_kind
structured_payload
```

要求：

- 未经 Detector 确认的 `structured_payload` 不进入网络请求；
- 字段允许缺失，不能在 Adapter 层伪造稳定 ID；
- Direct / JSON 差异由 Adapter 保存 provider-specific extension，但 Core 不依赖它；
- 真实 fixture 只能经过字段裁剪、ID 替换和正文删除后进入 `fixtures/sanitized`。

### 7.2 `ShareCandidate`：允许上传云端

建议契约：

```json
{
  "clientItemId": "uuid",
  "groupSourceId": "opaque-stable-id",
  "sourceMessageId": "opaque-stable-id",
  "sourceOrder": 123,
  "occurredAt": "2026-08-15T10:20:30+08:00",
  "occurredDate": "2026-08-15",
  "timePrecision": "instant",
  "origin": "detected",
  "member": {
    "sourceMemberId": "opaque-stable-id",
    "nickname": "当前昵称"
  },
  "album": {
    "neteaseAlbumId": "123456",
    "title": "Album",
    "artist": "Artist",
    "coverUrl": "https://...",
    "neteaseUrl": "https://music.163.com/#/album?id=123456"
  }
}
```

约束：

- `neteaseAlbumId`、群 / 成员 / 消息来源 ID 均按字符串传输，避免 JavaScript 整数精度问题；
- `title`、`artist` 必填且 Trim 首尾；
- 时间不完整时使用 `occurredDate + timePrecision=date`，不得构造伪时分；
- `origin` 只允许 `detected | repaired`；
- 不含普通正文、群名、无关媒体、完整 QCE payload、Token 或上下文消息。

### 7.3 Batch 回执

```json
{
  "batchId": "uuid",
  "complete": true,
  "checkpointAccepted": true,
  "items": [
    {
      "clientItemId": "uuid",
      "status": "accepted",
      "shareId": "uuid"
    }
  ],
  "serverTime": "2026-08-15T02:20:35Z"
}
```

逐项状态只允许：

- `accepted`：新 Share 已提交；
- `duplicate`：稳定来源键已存在，视为成功收敛；
- `invalid`：服务端完成校验并明确拒绝，返回稳定 `reasonCode`；
- `unknown` 不应作为服务端“完整成功”响应的一部分；网络超时、响应缺项或内部错误由客户端映射为 unknown。

## 8. 数据模型

### 8.1 Cloudflare D1

#### `groups`

- `id TEXT PRIMARY KEY`
- `source_group_id TEXT NOT NULL UNIQUE`
- `created_at INTEGER NOT NULL`
- `updated_at INTEGER NOT NULL`

V1 只有一个 Group，但所有 Group-scoped 表仍保留 `group_id`。

#### `albums`

- `id TEXT PRIMARY KEY`
- `netease_album_id TEXT NOT NULL UNIQUE`
- `title TEXT NOT NULL`
- `artist TEXT NOT NULL`
- `cover_url TEXT NULL`
- `netease_url TEXT NULL`
- `version INTEGER NOT NULL DEFAULT 1`
- `created_at INTEGER NOT NULL`
- `updated_at INTEGER NOT NULL`

Album 是全局实体。Merge 时来源 Share 迁移到目标 Album，然后硬删除来源 Album。

#### `members`

- `id TEXT PRIMARY KEY`
- `group_id TEXT NOT NULL`
- `source_member_id TEXT NULL`
- `current_nickname TEXT NOT NULL`
- `member_kind TEXT NOT NULL`：`qce | legacy`
- `created_at INTEGER NOT NULL`
- `updated_at INTEGER NOT NULL`

索引：`UNIQUE(group_id, source_member_id)`，对非空稳定 ID 生效。

#### `shares`

- `id TEXT PRIMARY KEY`
- `group_id TEXT NOT NULL`
- `album_id TEXT NOT NULL`
- `member_id TEXT NOT NULL`
- `source_message_id TEXT NOT NULL`
- `occurred_at INTEGER NULL`：UTC epoch milliseconds
- `occurred_date TEXT NOT NULL`：业务时区 `YYYY-MM-DD`
- `time_precision TEXT NOT NULL`：`instant | date`
- `source_order INTEGER NULL`
- `origin TEXT NOT NULL`：`detected | repaired`
- `created_at INTEGER NOT NULL`

关键约束：`UNIQUE(group_id, source_message_id)`。

#### `sync_checkpoints`

- `group_id TEXT PRIMARY KEY`
- `provider TEXT NOT NULL`
- `checkpoint_json TEXT NOT NULL`：只保留恢复所需最小游标
- `last_batch_id TEXT NULL`
- `last_completed_at INTEGER NULL`
- `last_result_json TEXT NULL`：只保存轻量 accepted / duplicate / invalid 摘要
- `updated_at INTEGER NOT NULL`

不建立完整 Batch Audit 表。

#### `auth_settings`

- `group_id TEXT PRIMARY KEY`
- `user_password_verifier TEXT NOT NULL`
- `user_auth_version INTEGER NOT NULL`
- `admin_password_verifier TEXT NOT NULL`
- `admin_auth_version INTEGER NOT NULL`
- `updated_at INTEGER NOT NULL`

密码变更使用事务同时更新 verifier 与对应 auth version。

#### `admin_actions`

- `id TEXT PRIMARY KEY`
- `group_id TEXT NOT NULL`
- `action_type TEXT NOT NULL`
- `target_type TEXT NOT NULL`
- `target_id TEXT NULL`
- `summary TEXT NOT NULL`
- `metadata_json TEXT NULL`：白名单字段
- `created_at INTEGER NOT NULL`

不记录密码、Token、完整请求体或 QQ 原文。

### 8.2 Sync 本地 SQLite

#### `sync_state`

保存目标群、Provider、已提交游标、最近观察游标、上次成功、首次扫描状态和 schema version。

#### `pending_items`

保存标准化 `ShareCandidate`、状态、重试次数、最后错误和 origin；约束 `UNIQUE(group_source_id, source_message_id)`。

#### `parse_errors`

保存错误类别、Detector 结果、必要索引字段和加密 Raw Payload。修复或忽略后立即清除 Raw Payload。

#### `ignored_items`

只保存稳定来源键、最小 fingerprint、忽略时间；正常扫描永久跳过，重新检查时显式删除或重置该状态。

#### `metadata_cache`

以 `netease_album_id` 为主键保存已确认 metadata、来源、schema version、验证状态和更新时间；不做固定周期刷新。

### 8.3 时间与排序规则

- 业务时区显式配置为 `Asia/Hong_Kong` / UTC+8，不依赖服务器本地时区；
- 实时时间保存 UTC instant，同时保存派生的本地日期；
- date-only Share 只保存本地日期和 `source_order`；
- Feed 排序必须对相同日期建立稳定 tie-breaker；
- Cursor 编码包含排序键与唯一 ID，不使用 offset pagination。

## 9. Sync 状态机与事务边界

### 9.1 一次“立即同步”

```text
启动
  → submitting 残留恢复为 pending
  → 检测 / 连接 QCE
  → 获取目标群与成员
  → 从 committed cursor 前方重叠扫描
  → Normalize RawQQMessage
  → ignored? 跳过
  → Detector
      ├─ 非目标消息：本轮忽略
      ├─ 可靠 Album：Pending Upsert
      └─ 疑似但无法确认：Parse Error
  → Metadata 补齐
  → pending → submitting
  → POST /api/sync/batch
      ├─ accepted / duplicate：删除 Pending
      ├─ invalid：转本地异常
      └─ unknown：恢复 pending
  → 仅在 Batch 完整回执后提交 committed cursor
  → 展示摘要
```

### 9.2 必须保持的原子性

本地 SQLite 同一个事务内完成：

1. 标记将提交的 Pending 为 `submitting`；
2. 应用服务端每条结果；
3. 把 unknown 恢复为 pending；
4. 删除 accepted / duplicate；
5. 把 invalid 转入异常；
6. 仅在 `complete=true` 且响应项完整时提交新游标。

若应用在事务前或事务中崩溃，启动恢复逻辑把遗留 `submitting` 统一恢复为 `pending`。

### 9.3 云端 Batch 原子性

处理顺序：

1. 完成请求级认证、大小限制和 schema 校验；
2. 对每条输入做纯校验，先得出所有 `invalid`；
3. 为其余条目构造 Album / Member Upsert、Share Insert 和 Checkpoint Update；
4. 使用 D1 `batch()` 一次提交；
5. 根据 Share Insert 的 `changes` 映射 `accepted / duplicate`；
6. D1 任一语句失败时整批返回非完整结果，客户端不得推进游标。

D1 官方文档说明 `batch()` 中的语句按顺序执行，并以事务方式在失败时回滚整个序列；实现时仍须用本地 D1 集成测试验证约束与回执映射。

### 9.4 无候选消息的游标推进

若扫描窗口内没有可上传 Candidate，但所有消息均已明确分类为“无关 / 已忽略 / 本地 Parse Error”，允许在本地事务中直接推进 committed cursor。该行为必须有测试，避免相同无关窗口永久重复扫描。

## 10. Worker API 设计

### 10.1 路由草案

#### 普通认证

- `POST /api/auth/session`
- `GET /api/auth/session`
- `DELETE /api/auth/session`

#### 管理员认证

- `POST /api/auth/admin`
- `GET /api/auth/admin`
- `DELETE /api/auth/admin`

Admin Mode 本身是前端瞬时状态，不在服务端持久化。

#### 浏览

- `GET /api/albums`
- `GET /api/albums/:albumId`
- `GET /api/feed`
- `GET /api/feed/calendar`
- `GET /api/stats?period=week|month|year`

#### Sync

- `POST /api/sync/batch`
- `GET /api/sync/checkpoint`

#### Admin

- `PATCH /api/admin/albums/:albumId`
- `DELETE /api/admin/albums/:albumId`
- `DELETE /api/admin/shares/:shareId`
- `POST /api/admin/albums/:albumId/merge`
- `GET /api/admin/members`
- `POST /api/admin/members/:memberId/merge`
- `GET /api/admin/sync-status`
- `PUT /api/admin/security/group-password`
- `PUT /api/admin/security/admin-password`

### 10.2 分页和查询

- Album Grid 与 Feed 均使用 opaque cursor；
- 默认 page size 由实现基准确定，并设置硬上限；
- 搜索先采用 SQLite `LIKE` + 规范化字段 + 必要索引，不引入外部搜索服务；
- “分享最多”使用不同 Member 数，不使用 Share 总数；
- Stats 的自然周 / 月 / 年边界由服务端按 UTC+8 计算；
- 所有 Group-scoped 查询都必须显式带 `group_id` 条件。

### 10.3 并发控制

- Album 更新必须携带 `expectedVersion`；
- SQL 更新条件包含 `id + version`，成功后 version 自增；
- 影响行数为 0 时返回 `409 VERSION_CONFLICT`；
- 前端保留本地输入并获取最新服务端数据，不自动 Merge、不强制覆盖。

## 11. 认证与安全设计

### 11.1 三套独立凭证

| 凭证       | 使用者        | 存放                                               | 失效机制                  |
| ---------- | ------------- | -------------------------------------------------- | ------------------------- |
| 群共享密码 | 普通 PWA 用户 | 服务端 verifier；浏览器仅持 HttpOnly Session       | `user_auth_version` 自增  |
| 管理员密码 | 管理设备      | 服务端 verifier；浏览器持独立 Admin Authorization  | `admin_auth_version` 自增 |
| Sync Token | Windows Sync  | Worker Secret + Windows Credential Manager / DPAPI | 手动轮换 Secret           |

### 11.2 密码 verifier 技术探针

首选 Web Crypto PBKDF2，并在 Cloudflare Free 的 Worker CPU 约束下做真实基准。验收要求：

- 不保存明文密码；
- verifier 有独立 salt，并结合 Worker Secret pepper；
- 登录路径 P95 CPU 留有安全余量；
- 若 PBKDF2 参数无法在 Free CPU 范围内稳定执行，改用基于秘密 pepper 的 HMAC verifier，同时强制高熵共享密码，并在 ADR 中记录安全模型和限制；
- 不因性能原因退化为裸 SHA-256(password)。

### 11.3 Session

- 普通 Session 与 Admin Authorization 使用两个不同的 HttpOnly、Secure、SameSite Cookie；
- Token 使用 Worker Secret 签名，包含 group、auth version、issued-at 和随机 nonce；
- 长期有效但每次受保护请求都校验当前 auth version；
- 冷启动只恢复授权，不恢复 Admin Mode；
- 密码变更事务提交后，旧 Token 下一次请求立即失效；
- PWA 收到明确 401 / auth-version mismatch 时立即清除 IndexedDB、API Cache 和封面 Cache。

### 11.4 Web 防护

- 写接口校验 `Origin` / `Sec-Fetch-Site`，并要求自定义 CSRF header；
- 登录失败使用统一错误，不暴露密码、群或账号状态；
- 登录接口有按 IP hash + 时间窗的低成本限速；
- 所有输入有长度、URL、枚举和 payload size 上限；
- 响应增加 CSP、`X-Content-Type-Options`、`Referrer-Policy` 等安全头；
- 管理操作统一写 `AdminAction`，metadata 采用白名单序列化。

## 12. PWA 实现方案

### 12.1 状态分层

- TanStack Query：服务端实体、分页、失效和前后台刷新；
- Zustand：当前 Tab、每 Tab 独立滚动 / 筛选 / 排序、Search Mode、Sheet、Drill-down、Admin Mode；
- URL / History：表达可以被系统返回逐层关闭的瞬时状态；
- IndexedDB：仅保存最近只读查询快照和 cache generation；
- Cache Storage：App Shell 与最近实际加载过的封面。

### 12.2 页面实现顺序

1. App Shell + 普通密码 Overlay；
2. Album Grid 基础读取、排序、时间筛选和 cursor；
3. 统一 Album Detail Sheet；
4. Feed 与日期分组；
5. Stats 与成员 Drill-down；
6. Search Mode；
7. Admin Mode 与编辑 / Merge / 删除；
8. Offline read cache；
9. Motion 与视觉精修；
10. 桌面 Navigation Rail / Floating Sheet 适配。

### 12.3 History 层级

系统返回按以下优先级消费：

```text
dirty edit confirm
  → edit mode
  → confirm/action sheet
  → Album Detail Sheet
  → member drill-down Search Mode
  → admin secondary page
  → 当前 App 页面历史
  → 离开站点
```

所有状态跳转必须有单元或 E2E 测试，避免 Android 返回键直接退出应用。

### 12.4 Motion 架构

建立统一 Motion Token：

- `micro`：按钮、图标、Segment；
- `ui`：Search、Sticky Header、列表状态；
- `spatial`：Sheet、Shared Element、Tab 空间关系；
- `responsiveSpring`：快、紧、小范围；
- `softSpring`：Sheet、封面、大范围位移；
- `enterEase / exitEase`：非空间 opacity / transform。

核心组件：

- `AlbumArtworkTransitionCoordinator`：记录源 rect、建立 portal overlay、返回源不可用时降级；
- `SheetGestureController`：`pointerdown` 后按方向和滚动位置判定 owner，并锁定到 `pointerup`；
- `MotionCapability`：Reduced Motion、低性能和浏览器能力检测；
- `ScrollStateRegistry`：每 Tab 独立保存，不因开关 Sheet 丢失位置。

性能验收：

- 动画过程不扩大虚拟列表 DOM；
- 图片解码和网络结果不能覆盖最新 Album 目标；
- 快速连续操作中断旧动画并追随最后意图；
- QQ 内置浏览器不支持的效果自动降级为稳定 opacity / transform；
- Reduced Motion 移除大范围位移和强弹性。

### 12.5 Offline 策略

- App Shell：precache；
- API 数据：应用层 Network-first，失败时读取最近成功快照；
- 封面：只缓存用户实际成功加载的 URL，限制条目和总量；
- 离线 UI 明确显示“离线内容”，不显示虚假的最新状态；
- 离线禁止 Admin 写操作；
- 重新联网后静默验证 Session，再决定刷新或清缓存；
- 登出、密码版本失效或服务端撤销时清除全部业务缓存。

## 13. 分阶段开发计划

### Phase 0 — 仓库基线与真实技术探针

预计：2～4 个工程日。

交付物：

- monorepo 最小骨架、锁文件、格式化和基础 CI；
- `.gitignore`、`SECURITY.md` 或隐私开发说明；
- `docs/probes/qce.md`；
- `docs/probes/netease.md`；
- `docs/probes/cloudflare.md`；
- 第一批 ADR；
- 经过人工复核的脱敏最小 fixture。

QCE 探针：

1. 检测 QCE 运行方式、localhost 地址、Token 获取和版本；
2. 读取群列表和目标群；
3. 验证历史消息分页、离线补消息和最大覆盖范围；
4. 记录 group / member / message ID、sequence 的真实类型与稳定性；
5. 抓取至少一条真实网易云专辑卡片结构；
6. 对比 Direct API 与 JSON Export 的字段差异；
7. 明确 QCE 自动发现、启动和升级兼容边界；
8. 检查 GPL-3.0 约束，V1 默认只调用用户本机已有 QCE，不复制其源码或静默捆绑分发。

网易云探针：

1. 标准 album URL 和 album ID 规范化；
2. 短链重定向行为；
3. metadata 字段和缺失行为；
4. cover 防盗链、失效和浏览器加载；
5. 无 Cookie、不同 UA、不同网络下的结果；
6. 请求失败、限流和缓存策略。

Cloudflare 探针：

1. Wrangler local + D1 migration；
2. D1 unique constraint、foreign key、`batch()` 回滚与 `changes`；
3. Worker + Static Assets 的同源 SPA 路由；
4. Web Crypto verifier 的 CPU 基准；
5. Free 配额、超额失败行为和请求大小；
6. 自定义域名、DNS / TLS；
7. 公网部署仍延后到普通密码机制完成后。

退出门：

- 能读取一条真实目标群专辑分享；
- 稳定来源键方案有证据；若没有，已形成可验证 fallback，而不是凭空猜测；
- Detector 能可靠得到 album ID / URL；
- D1 本地事务和幂等方案通过测试；
- 没有 Raw 数据进入 Git。

需要用户参与：完成 QQ / QCE 登录、选择真实目标群、允许本机读取小样本。用户无需把 Raw Payload 粘贴到对话或上传云端。

### Phase 1 — 最小 Vertical Slice

预计：4～6 个工程日。

范围：

- C# console / 极简 WPF 壳均可，先实现核心 pipeline；
- QCE Direct Provider 最小读取；
- Detector 识别真实卡片；
- SQLite Pending 最小表；
- Worker `/api/sync/batch`；
- D1 Album / Member / Share 最小 migration；
- PWA Album Grid 最小读取；
- 本地端到端脚本和一条真实数据演示。

退出门：

- 同一消息提交两次只产生一条 Share；
- 网络中断后重试可收敛；
- D1 关系正确；
- PWA 显示真实 Album；
- Raw Payload 未进入 Worker 日志、D1 或浏览器；
- 尚未公网部署也可在本机完整演示。

发布标记：`v0.1-vertical-slice`。

### Phase 2 — AlbumRobot Sync V1

预计：6～9 个工程日。

范围：

- WPF App Shell 与首次自动引导；
- QCE 发现、状态检测、连接和明确故障提示；
- 群选择与锁定；
- 首次历史扫描、汇总预览和可浏览结果；
- 日常增量、重叠窗口和 cursor；
- Direct / JSON 双 Provider；
- Metadata Cache；
- Pending / submitting / invalid / unknown 状态机；
- Parse Error、粘贴网易云链接修复、Ignored 与重新检查；
- 启动崩溃恢复；
- 结构化日志、轮转、脱敏诊断包；
- `%LOCALAPPDATA%\AlbumRobot` 数据目录；
- migration 前临时备份和成功后清理；
- 设置中的“打开数据文件夹”。

退出门：

- 首次扫描必须人工确认才上传；
- 日常同步一键完成；
- repaired 与自动识别共用同一 Pending / Batch 路径；
- 所有规定的崩溃点和网络失败点有 integration test；
- repaired / ignored 后 Raw Payload 已物理清除；
- 日志和诊断包通过隐私字段扫描。

发布标记：`v0.2-sync`。

### Phase 3 — Cloud API、认证与完整领域模型

预计：5～8 个工程日。

范围：

- 完整 D1 schema、索引和 migration；
- 普通认证与长期 Session；
- Admin Authorization 与 auth version；
- Sync Bearer Token；
- Album browse / search / sort / period；
- Feed cursor / calendar anchor；
- Stats；
- Sync Checkpoint / Sync Status；
- Album / Share / Member Admin mutations；
- optimistic concurrency；
- AdminAction；
- 访问与安全；
- Free 配额友好的查询基准。

退出门：

- 所有 API 按 OpenAPI contract 测试；
- 密码轮换使对应 Session / Authorization 失效，且互不误伤；
- Group 隔离测试覆盖每个 repository；
- Merge / Delete 事务和影响范围测试通过；
- 关键查询在代表性数据量下没有全表扫描；
- 普通密码机制通过后，才允许第一次公网部署。

发布标记：`v0.3-api`。

### Phase 4 — PWA Functional Complete

预计：7～10 个工程日。

范围：

- App Shell、Password Overlay、Light / Dark；
- 专辑页、排序、时间筛选、Search Mode、无限滚动；
- 动态页、Sticky 日期、时间跳转、新动态提示、回到最新；
- 统计页、Segment、成员 Drill-down；
- Album Detail Sheet 基础状态机；
- Admin Mode、管理工具、编辑 / Merge / Delete；
- 离线最近只读缓存和失效清理；
- PWA manifest / install 入口；
- 前后台生命周期和状态恢复；
- 宽屏 Navigation Rail / Floating Sheet 功能布局。

退出门：

- Q1～Q219 对应功能可逐项追踪到测试或 QA case；
- 冷启动、回前台、外跳网易云返回、系统 Back 行为正确；
- Tab / Sheet / Search 后滚动和筛选状态符合决策；
- Session 失效时业务缓存清除；
- 管理写操作离线时不可用；
- Chromium 与 WebKit E2E 主路径通过。

发布标记：`v0.4-functional`。

### Phase 5 — Apple Music 视觉与 Motion 精修

预计：6～10 个工程日。

范围：

- Theme / Typography / Spacing / Material Token；
- Large Title → Compact Title；
- Bottom Tab Bar 与 Navigation Rail；
- Album Grid 视觉密度；
- Shared Element 封面转场；
- Bottom Sheet drag、threshold、velocity、rubber-band；
- Gesture Ownership Lock；
- Tab spatial transition；
- Search transition；
- Stats / Grid reorder；
- Pull to refresh；
- Reduced Motion；
- blur / backdrop 性能降级；
- 图片解码、长列表与低性能设备优化。

退出门：

- iOS Safari、Android Chrome、QQ 内置浏览器完成真机录屏和 QA；
- 主要 Spatial Motion 可中断，不排队；
- Shared Element 源不可用时稳定降级；
- Reduced Motion 符合 Q73；
- 长 Feed 不随历史总量无限扩张 DOM；
- 没有由动画引起的状态错乱、误触或 Back 失效。

发布标记：`v0.5-visual-qa`。

### Phase 6 — 生产部署与真实群试运行

预计：3～5 个工程日，加至少 7 天观察期。

范围：

- Cloudflare Worker + Static Assets + D1 正式环境；
- 设置正式群密码、管理员密码和 Sync Token；
- 绑定 `album.rocknrollliberty.dpdns.org`；
- 先同步极小真实批次，再逐步扩大历史回填；
- 不同网络、QQ 内置浏览器、iOS Safari、Android Chrome 实测；
- 群友小范围试用；
- 修复阻断问题；
- 运维、恢复、密码轮换和 QCE 故障 runbook；
- 最终 V1 验收。

退出门：

- 域名、TLS、登录、读 API 和 Sync 均可用；
- 真实重传无重复；
- Free 配额有明显余量；
- 大陆网络实测结果有记录；
- 没有 P0 / P1 隐私、安全、数据完整性或主流程缺陷；
- 用户确认可向当前 QQ 群发布。

发布标记：`v1.0.0`。

### 13.1 总工作量参考

| 目标                      |         累计工作量参考 |
| ------------------------- | ---------------------: |
| 真实技术风险得到结论      |            2～4 工程日 |
| 本地真实 Vertical Slice   |           6～10 工程日 |
| Sync + Cloud 完整         |          17～27 工程日 |
| PWA Functional            |          24～37 工程日 |
| V1 视觉、部署、群试用就绪 | 33～52 工程日 + 观察期 |

影响区间的最大变量是 QCE Direct API 稳定性、网易云 metadata 获取方式、真实历史数据量和 QQ 内置浏览器 Motion 兼容性。

## 14. 测试与质量门

### 14.1 测试金字塔

#### Sync 单元测试

- 每种已知 QCE 消息形态的 Normalize；
- Detector 优先级；
- URL / album ID 规范化；
- date-only 与 source order；
- Metadata Cache 命中 / 缺失 / 失败；
- Pending 状态迁移；
- 脱敏器字段白名单。

#### Sync SQLite 集成测试

- 重复 scan Upsert；
- submitting 崩溃恢复；
- accepted / duplicate / invalid / unknown；
- Batch 回执缺项；
- cursor 原子提交；
- schema migration 备份与恢复；
- repaired / ignored 清 Raw Payload。

#### Worker 单元 / D1 集成测试

- schema validation；
- unique source key；
- Batch 全回滚；
- auth version；
- Group 隔离；
- 自然周 / 月 / 年边界；
- distinct Album / Member 统计口径；
- Merge / Delete 事务；
- optimistic concurrency；
- cursor 稳定性。

#### Contract tests

- OpenAPI examples 同时被 Worker schema、Web client 和 C# DTO 读取；
- 错误码、枚举和 nullability 不漂移；
- 脱敏 ShareCandidate golden file 不含禁止字段。

#### Web component / E2E

- 登录成功 / 失败 / 密码轮换；
- Album Grid 查询状态；
- Feed 时间跳转与新动态提示；
- Stats Drill-down 返回；
- Sheet / edit / confirm / Back 层级；
- Admin conflict；
- offline read 与 reconnect；
- cache purge；
- reduced motion。

### 14.2 真机矩阵

至少覆盖：

| 平台          | 浏览器 / 形态         | 核心关注                                    |
| ------------- | --------------------- | ------------------------------------------- |
| Windows 10/11 | AlbumRobot Sync       | QCE 发现、缩放、权限、安装 / 更新           |
| iPhone        | Safari / 添加到主屏幕 | Safe Area、Back、PWA 生命周期、Sheet 手势   |
| Android       | Chrome / PWA          | 系统返回、安装、触摸和缓存                  |
| Android / iOS | QQ 内置浏览器         | blur、pointer/touch、外跳网易云、返回上下文 |
| Desktop       | Chromium              | Navigation Rail、Floating Sheet、滚轮归属   |

### 14.3 缺陷分级

- P0：隐私泄露、密码 / Token 泄露、数据不可逆破坏、跨 Group 访问；禁止发布；
- P1：重复入库、丢 Share、游标错误、认证绕过、主流程不可用；禁止发布；
- P2：某平台主交互明显失效、状态错乱、严重性能问题；V1 前修复；
- P3：低频视觉或非阻断细节；可进入 V1.x backlog。

## 15. CI/CD 与 Git 工作流

### 15.1 分支策略

- `main` 始终保持可构建；
- 工作分支：`agent/<phase-or-feature>`；
- 每个 PR 只包含一个可说明的纵向目标；
- 默认先创建 Draft PR，自动检查通过并完成自测后转 Ready；
- schema / auth /隐私边界变更必须附 ADR 和迁移说明；
- 不直接在 main 上进行实验性 QCE 适配。

### 15.2 PR 必须包含

- 改了什么、为什么；
- 对用户 / 数据的影响；
- 测试证据；
- 数据迁移和回滚方式；
- 新增环境变量 / Secret；
- 隐私检查结果；
- 截图或录屏（涉及可见 UI / Motion 时）。

### 15.3 CI 检查

```text
pnpm install --frozen-lockfile
pnpm format:check
pnpm lint
pnpm typecheck
pnpm test
pnpm build
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
contract / migration / privacy fixture checks
```

按路径过滤 Node 与 .NET job，避免无关变更重复消耗 CI。

### 15.4 部署策略

- Phase 0～1：只使用 Wrangler local / D1 local；
- 普通密码机制完成前：禁止公网业务部署；
- 首次线上：极小数据、手动 workflow、显式检查域名和 Secret；
- 稳定后：main 合并可触发生产部署，但 D1 migration 仍需受控步骤；
- 生产 migration 先记录 D1 恢复点，再迁移，再 smoke test；
- 回滚优先回退 Worker 版本；数据 schema 使用 forward-fix migration，不运行未经验证的反向破坏脚本。

## 16. 隐私、安全与 Secret 管理

### 16.1 `.gitignore` 最低规则

必须排除：

```text
.env*
.dev.vars*
*.db
*.db-shm
*.db-wal
*.sqlite*
logs/
diagnostics/
fixtures/raw/
qce-data/
security.json
appsettings.Local.json
*.user
artifacts/
```

### 16.2 隐私自动检查

CI 对 fixture、日志样例和诊断包样例执行字段 denylist：

- 普通聊天正文；
- 群名 / 群号；
- 原始成员 ID / 昵称（诊断包）；
- access token / cookie / authorization；
- 本机绝对用户路径；
- 完整 Batch 请求体；
- 未知 Raw Payload 字段。

真实 fixture 提交前必须人工复核；自动脱敏不是唯一防线。

### 16.3 本地 Raw Payload 加密

- 使用 AES-GCM、随机 nonce 和版本化 envelope；
- 密钥随程序提供，仅防止直接打开 SQLite 即看到内容；
- 文档明确说明不具备强设备级安全；
- 不复用 nonce；
- repaired / ignored 时在同一事务内清除 ciphertext；
- 不把固定密钥或该机制描述为“安全存储用户聊天记录”。

## 17. 可观测性与成本控制

### 17.1 日志

Sync：

- request / sync run ID；
- provider、阶段、数量、耗时、错误码；
- 不记录正文、完整 Payload、昵称、稳定成员 ID、密码或 Token；
- 单文件大小和保留数量作为配置常量；
- 诊断包必须经过独立脱敏器。

Worker：

- request ID、route、status、duration、D1 rows read / written 摘要；
- Sync 仅记录 batch ID 和 counts；
- Admin 仅记录 action ID 和白名单摘要；
- V1 不接入付费第三方 APM。

### 17.2 Free 配额护栏

- 所有列表使用 cursor + page size 上限；
- 按 `group_id`、来源键、时间和常用排序建立组合索引；
- 不在请求中扫描全量历史；
- 静态资源由 Workers Static Assets 直接服务；
- API 失败时不无限自动重试；
- Sync Batch 设 item 数和请求体上限；
- 每个版本记录 Cloudflare dashboard 的 rows read / written 基线；
- 达到预警阈值时先限流、减少刷新和优化查询，不自动升级 Paid。

按 2026-08-15 官方文档，Workers Free 当前为 100,000 次动态请求 / 日，D1 Free 为 5,000,000 rows read / 日、100,000 rows written / 日和 5 GB 总存储；这些数字必须在 Phase 0 和每次生产发布前重新核对，不能视为永久合同。

## 18. 主要风险与预案

| 风险                      | 早期信号                             | 首选预案                                                      | 需要用户确认的条件                     |
| ------------------------- | ------------------------------------ | ------------------------------------------------------------- | -------------------------------------- |
| QCE Direct API 无稳定接口 | endpoint / schema 随版本频繁变化     | Adapter version matrix；自动探测；JSON Provider 降级          | 两条路径均不能满足历史 + 增量目标      |
| QCE 无稳定 message ID     | 重启或导出后 ID 变化                 | 依据真实样本设计 group + seq 或稳定 fingerprint，扩大 overlap | fallback 会明显增加误去重 / 丢数据风险 |
| Member 无稳定 ID          | 昵称变化即无法关联                   | 使用 QCE 可验证的 scoped ID；无法匹配则 Legacy Member         | 产品目标只能退化为昵称身份             |
| 网易云 metadata 不稳定    | 403、字段缺失、短链失败              | 卡片优先、缓存优先、按需补齐、人工链接修复                    | 需要付费 API 或上传到第三方服务        |
| QCE GPL 分发约束          | 自动捆绑会形成分发义务               | V1 只检测 / 启动用户本机已有 QCE，提供清晰安装指引            | 希望把 QCE 二进制随 Sync 安装包分发    |
| D1 Batch 行为与预期不同   | 本地 / 远端 changes 或 rollback 差异 | 缩小 batch、显式预校验、unique constraint、集成测试           | 需要更换数据库或付费服务               |
| Free 配额耗尽             | rows read 快速增长                   | 索引、缓存、降低刷新、限流、暂时失败                          | 必须升级付费才能维持 V1                |
| 大陆网络不稳定            | 多运营商实测超时                     | 先记录数据；只在实测失败后评估迁移                            | 改变 Cloudflare 部署基线               |
| QQ 内置浏览器 Motion 不足 | pointer / blur / history 异常        | feature detection，降级 opacity / transform，保功能           | 核心交互无法通过任何降级实现           |
| 离线缓存越权              | 密码轮换后旧内容仍显示               | auth generation、401 purge、缓存隔离、测试                    | 浏览器能力无法满足即时清理语义         |
| 历史数据规模超预期        | 首扫时间 / 内存过高                  | 流式分页、分批预览、windowing、小批同步                       | 必须改变首次导入体验或数据范围         |

## 19. V1 Definition of Done

### 数据链路

- QCE Direct 主路径和 JSON 备用路径可用；
- 真实网易云专辑卡片识别有 fixture 和测试；
- 首次历史扫描有预览和确认；
- 日常增量一键同步；
- Pending、重试、崩溃恢复和 Checkpoint 全部通过故障测试；
- duplicate 不产生重复 Share；
- date-only、source order、nickname 刷新和 Legacy Member 语义正确。

### 云端

- D1 migration 可从空库完整执行；
- Auth、Browse、Feed、Stats、Sync、Admin API 达到 contract；
- Album 全局唯一，Member / Share / Stats / Auth 按 Group 隔离；
- 密码版本失效、Admin 独立授权和 optimistic concurrency 正确；
- 删除 / Merge / Audit 符合锁定语义；
- Free 配额基准通过。

### PWA

- 专辑 / 动态 / 统计三页完整；
- Search、排序、时间筛选、统计 Drill-down、时间跳转完整；
- Bottom Sheet / Shared Element / Back / 滚动状态正确；
- Light / Dark、Safe Area、Reduced Motion、宽屏布局完整；
- 最近只读离线缓存和 Session 失效清理正确；
- 安装入口、回前台刷新和外跳返回符合决策。

### 隐私与安全

- Raw QQ 数据只在本机；
- Git、D1、Worker 日志和诊断包无禁止字段；
- Secret 均由 GitHub / Cloudflare Secret 或 Windows 安全存储管理；
- 普通 / Admin / Sync 凭证相互隔离；
- P0 / P1 安全缺陷为 0。

### 发布与运维

- `album.rocknrollliberty.dpdns.org` 可访问且 TLS 正常；
- D1 migration、Worker 回滚、密码轮换、QCE 故障和本地数据目录均有 runbook；
- 真机矩阵完成；
- 至少 7 天真实群观察无阻断缺陷；
- README 能让后续开发者从零启动本地环境。

## 20. 开发启动后的前 10 个动作

1. 建立 `.gitignore`、README、pnpm workspace、.NET solution 和基础 CI；
2. 建立 `docs/adr`、`docs/probes`、`fixtures/sanitized`；
3. 用项目本地依赖安装 Wrangler，并跑通 Worker + D1 local；
4. 安装 / 启动当前 QCE，记录版本和运行模式；
5. 在用户本机完成群列表、目标群、消息分页和成员字段探针；
6. 捕获一条真实网易云专辑卡片，仅生成脱敏最小 fixture；
7. 编写 `RawQQMessage`、`ShareCandidate` 和 Batch OpenAPI contract；
8. 实现 Detector 的第一个真实 fixture 测试；
9. 建立最小 D1 migration 和幂等 Batch integration test；
10. 打通一张真实专辑到本地 PWA 的 Vertical Slice。

完成第 10 项后，再开始扩展 Sync UI 和 PWA 页面。

## 21. 用户参与点与升级规则

当前没有未决产品问题。后续只在以下节点需要用户参与：

1. Phase 0：完成 QQ / QCE 登录并确认目标群；
2. Phase 0：在本机确认一条真实网易云卡片被正确识别；
3. 首次公网部署：完成 Cloudflare 登录、域名授权并设置群密码 / 管理员密码 / Sync Token；
4. Phase 5：用真实手机和 QQ 内置浏览器体验 Motion；
5. Phase 6：决定何时在群内发布 V1。

必须暂停并询问用户的情况：

- 需要改变 V1 产品边界；
- 需要改变 Cloudflare Free / 不租 VPS 的部署约束；
- 需要上传更多 QQ 原始数据或改变隐私边界；
- 需要启用付费服务；
- 需要不可逆地修改 Album / Member / Share 核心语义；
- 真实探针证明 LOCKED 目标不可行，必须做产品级降级。

其他实现级事项由开发者写 ADR 后继续推进。

## 22. 当前官方依据（2026-08-15 核对）

- [QCE 官方仓库](https://github.com/shuakami/qq-chat-exporter)：本地 QQ 聊天导出、JSON 输出、Windows 运行方式和 GPL-3.0 许可证；
- [QCE Releases](https://github.com/shuakami/qq-chat-exporter/releases)：实施 Phase 0 时固定并记录实际使用版本，不跟随 latest 静默漂移；
- [Cloudflare Workers Static Assets](https://developers.cloudflare.com/workers/static-assets/)：Worker 与 SPA 静态资产同一部署单元、SPA fallback 和 API 优先路由；
- [Cloudflare D1 local development](https://developers.cloudflare.com/d1/best-practices/local-development/)：Wrangler 本地 D1 开发与 migration 验证；
- [Cloudflare D1 `batch()`](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch)：顺序执行、事务与失败回滚语义；
- [Cloudflare Workers limits](https://developers.cloudflare.com/workers/platform/limits/) 与 [Workers / D1 pricing](https://developers.cloudflare.com/workers/platform/pricing/)：Free 配额与超额行为；
- [Cloudflare Web Crypto](https://developers.cloudflare.com/workers/runtime-apis/web-crypto/)：PBKDF2 / HMAC 等运行时能力；
- [.NET 10 概览](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview) 与 [.NET 生命周期](https://learn.microsoft.com/lifecycle/products/microsoft-net-and-net-core)：.NET 10 LTS 支持基线。

外部平台会变化。上述事实在 Phase 0、生产部署前和任何重大升级前都必须重新验证。
