# ADR 0002: JSON-first MVP authentication and deployment boundary

Status: Accepted
Date: 2026-08-15

## Context

The Direct QCE message endpoint is temporarily occupied by a long-running export and the real Direct album-card overlap is not yet verified. The existing synthetic vertical slice is useful locally, but its Worker routes were open and its Wrangler configuration used a local placeholder database. Connecting that state to a public Cloudflare hostname would expose unprotected read and write routes.

## Decision

1. The first usable release uses QCE JSON export as the recommended input. JSON is parsed only by Desktop Sync; only normalized ShareCandidate records reach Cloudflare. Direct QCE remains a later enhancement.
2. One Cloudflare Worker serves both the React SPA and `/api/*`. A separate Pages project is not created.
3. Album reads require a long-lived, HMAC-signed HttpOnly session issued after the group shared password is verified. The token includes a fingerprint derived from the current password, so changing the configured password invalidates old sessions.
4. Batch writes require an independent high-entropy Sync Token and enforce the configured single target group.
5. Group-password attempts use a Cloudflare Rate Limiting binding with a single login-class key. This avoids storing client IPs while placing a low-cost bound on brute-force traffic per Cloudflare location.
6. Static Assets and API responses send restrictive security headers. The SPA may load cover images over HTTPS, while scripts and API connections remain same-origin.
7. Runtime values are Cloudflare encrypted Secrets. Synthetic local values live only in `wrangler.local.jsonc`; the production config contains no credential values and fails preflight while its D1 ID is still a placeholder.
8. Desktop Sync keeps the remote Sync Token in process memory while running and persists it, keyed by the normalized Worker URL, in the current Windows user's Windows Credential Manager. It never stores the token in settings, SQLite, logs, or the repository.
9. Production Desktop Sync uses only a remote HTTPS Worker URL for upload. The desktop UI does not start or migrate a local Worker; the repository's `LocalWorkerHost` remains a developer/test seam only. Remote failures leave Pending data local for retry.

## Consequences

- The MVP can be deployed without exposing open album or ingestion APIs.
- QCE export remains a manual step, but it avoids blocking on unverified Direct behavior and preserves the local-only privacy boundary.
- A user enters the Sync Token once per Worker URL; later uploads reuse the current user's protected Windows credential until the token is rotated or rejected.
- Production deployment needs one controlled D1 creation/migration and four runtime Secrets before health reports ready.
- GitHub connection occurs only after the production D1 ID is configured and the validated application commit is pushed.
