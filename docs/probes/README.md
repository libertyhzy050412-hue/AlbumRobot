# Phase 0 technical probes

Phase 0 answers external-runtime questions with evidence. Results belong in `docs/probes/` as sanitized text only; raw QQ data, QCE tokens, cookies, full URLs containing private query values, and local database files must never be committed.

## QCE

1. Install a pinned Windows QCE release from the official repository.
2. Log in locally and confirm the QCE WebUI is available at `http://localhost:40653/qce`.
3. Run `pnpm probe:phase0` from this repository. Set `ALBUMROBOT_QCE_BASE_URL` only if the base URL differs.
4. Record the verified QCE version, reachable local endpoints, and the observed group/member/message/card field names in `docs/probes/qce.md` after manually redacting identifiers.

The current repository does not guess QCE API endpoints or fabricate stable IDs. The C# adapter is designed to accept a provider-specific response and normalize it only after the live sample proves the field mapping.

## Netease

The detector accepts structured card data and reliable album URLs/IDs. It does not infer albums from ordinary text. A sanitized card fixture and a real URL/metadata result can be added only after review.

## Cloudflare

Use the local Worker and D1 migration commands from the root README. `pnpm probe:phase0` records Wrangler availability and local configuration without contacting a remote account. Production account, Custom Domain, DNS, TLS, mainland reachability, and Free-plan quota checks are intentionally deferred until the deployment phase.
