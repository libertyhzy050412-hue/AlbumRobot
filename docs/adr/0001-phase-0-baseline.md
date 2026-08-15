# ADR 0001: Phase 0 monorepo baseline

## Status

Accepted — 2026-08-15.

## Decision

Use a small pnpm workspace for the Worker, Web, and shared TypeScript contracts, alongside a native .NET solution for Windows Sync. Keep QCE outside this repository and integrate through a local HTTP/JSON boundary. Use Wrangler local mode and a local D1 database until the ordinary password mechanism is complete.

## Rationale

This keeps the three runtime boundaries explicit without adding a monorepo orchestrator or copying GPL-licensed QCE source into AlbumRobot. The local-only boundary allows real contract and idempotency testing without exposing QQ data or creating a production cost.

## Consequences

- `apps/sync` owns all raw QQ processing and local persistence.
- `worker` accepts normalized candidates only.
- The first Worker endpoint is intentionally a local development endpoint; authentication and public deployment are Phase 3/6 work.
- Implementation details can evolve through later ADRs without reopening locked product decisions.
