# AlbumRobot

AlbumRobot is a local-first QQ album archive for one QQ group. The V1 data path is:

```text
Windows QQ → QCE → AlbumRobot Sync → Cloudflare Worker / D1 → React PWA
```

The repository now contains a deployed JSON-first MVP baseline: QCE JSON is parsed locally into a recoverable Pending queue, the desktop client uploads only normalized candidates with a Sync Token, and the password-protected React app provides a light-only Apple Music-style album, feed, stats, and album-detail browsing experience from a same-origin Worker/D1 deployment. Direct QCE scanning remains an enhancement and does not block the first usable release. The production Worker is available at `https://album.rocknrollliberty.dpdns.org`; no raw QQ data has been uploaded and no paid Cloudflare service is enabled.

## Repository layout

- `apps/web` — React + Vite mobile-first browsing UI.
- `apps/sync` — .NET 10 Windows Sync solution and local detector core.
- `worker` — Cloudflare Worker API and D1 migrations.
- `packages/contracts` — language-neutral TypeScript contract definitions used by Worker and Web.
- `fixtures/sanitized` — only minimal, reviewed, de-identified fixtures may be committed here.
- `docs/probes` — Phase 0 probe instructions and status records.
- `docs/adr` — implementation-level decisions.
- `docs/CODEX_HANDOFF_2026-08-15.md` — current Codex handoff, verified commands, blockers, and continuation prompt.
- `docs/runbooks/cloudflare-first-deploy.md` — guarded GitHub Builds, D1, runtime Secret, and custom-domain steps.

## Prerequisites

- Node.js 24+
- pnpm 11+
- .NET 10 SDK
- Windows QQ + QCE only when running the live QCE probe

Install JavaScript dependencies and validate the baseline:

```powershell
pnpm install
pnpm format:check
pnpm privacy:scan
pnpm typecheck
pnpm test
pnpm build
dotnet build apps/sync/AlbumRobot.Sync.sln
dotnet test apps/sync/AlbumRobot.Sync.sln
```

Initialize or manually run the synthetic local Worker + D1 session:

```powershell
pnpm --filter @albumrobot/worker db:migrate:local
pnpm --filter @albumrobot/worker dev
```

Run the visual Desktop Sync window on Windows:

```powershell
dotnet run --project apps/sync/src/AlbumRobot.Sync.App/AlbumRobot.Sync.App.csproj
```

The window reads QCE credentials from the current user's local QCE configuration, lists groups, imports QCE JSON locally, and uploads only normalized pending candidates. Production Desktop Sync sends both immediate and pending uploads directly to the configured remote Cloudflare Worker; it does not start or migrate a local Worker. The remote Sync Token stays in process memory and is persisted only in the current Windows user's Credential Manager. See [the Desktop Sync runbook](docs/runbooks/desktop-sync.md).

The API exposes public health, rate-limited group-password sessions, protected album browse/detail/feed/stats, and Sync-Token-protected batch endpoints. Static and API responses use restrictive security headers. The Web app proxies `/api` to `http://127.0.0.1:8787` during development. Local-only credentials in `worker/wrangler.local.jsonc` are synthetic and must never be copied to production.

For local visual QA, seed only fictional albums, members, and shares after the local Worker is running:

```powershell
pnpm seed:visual
pnpm dev:web
```

The visual seed is repeatable and never reads QCE or QQ data.

With the local Worker running in another terminal, run the repeatable synthetic smoke test:

```powershell
pnpm smoke:local
```

It creates a random synthetic group, obtains a local password session, verifies authenticated `accepted → duplicate`, verifies raw payload rejection, and verifies the protected Album browse response. It never uses QQ data.

Run the safe Phase 0 probe:

```powershell
pnpm probe:phase0
```

The safe Phase 0 probe never prints or persists the QCE access token and does not read QQ data. For an intentional live local sync, use the visual Desktop Sync window and follow [the Desktop Sync runbook](docs/runbooks/desktop-sync.md); it reads QQ data only on the local machine.

## Cloudflare production deployment

Production D1, runtime Secrets, Worker, and the custom domain are configured. The current recoverable release command is `pnpm deploy:worker`; Cloudflare GitHub Builds remains a follow-up automation step. Follow [the first-deploy runbook](docs/runbooks/cloudflare-first-deploy.md) and do not create a separate Pages project, static-file upload, second Worker, or second D1. The production Worker serves both `/api/*` and the built React SPA.

## Privacy boundary

Complete QQ messages, ordinary chat text, unrelated images, QCE tokens, passwords, sessions, local database files, logs, and raw parse errors remain local and are ignored by Git. Cloudflare receives only normalized `ShareCandidate` records. A sanitized fixture must be reviewed by a person before it is committed.

## Current external baseline

QCE `v6.2.3` Windows x64 is pinned and installed locally; the package hash, local path, verified full-mode envelope, and remaining Direct-card probe are recorded in `docs/probes/qce.md`. Do not silently follow `latest`. Production DNS/TLS and authentication boundaries have been checked; Cloudflare Free/D1 usage and mainland-China/mobile-browser reachability still require ongoing observation.
