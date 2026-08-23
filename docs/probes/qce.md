# QCE probe status

Status: `JSON-first production path verified; Direct bytesData card normalization implemented; live Direct/JSON equivalence, ID stability, and history overlap remain pending`.

## Local package

- Pinned release: QCE `v6.2.3` / Windows x64.
- Release artifact: `NapCat-QCE-Windows-x64-v6.2.3.zip`.
- Official download: <https://github.com/shuakami/qq-chat-exporter/releases/download/v6.2.3/NapCat-QCE-Windows-x64-v6.2.3.zip>
- SHA-256: `D079D3DD92E8B0314EDE0A53BF1F528729AF71ACEFDE8CCEABDFCA456B597E49`.
- The archive is extracted under the ignored local `qce-data/` directory. QCE source is not copied into the AlbumRobot repository.

The package contains `launcher-user.bat`, `start-standalone.bat`, `qce-server.exe`, the NapCat shell, and the QCE WebUI. The default full-mode WebUI address documented by the package is `http://localhost:40653/qce`.

## Evidence collected without QQ data

- QCE standalone mode was started on local port `40654` with an isolated local config directory and no automatic browser opening.
- `GET /` returned QCE API version `6.2.3`.
- `GET /qce` returned the QCE WebUI.
- Protected API requests require a local credential in request headers; the bundled WebUI sends two equivalent local auth headers.
- The documented groups endpoint is `GET /api/groups?page=1&limit=...&forceRefresh=false`.
- The documented members endpoint is `GET /api/groups/:groupCode/members?forceRefresh=false`.
- The bundled WebUI sends message fetch requests to `POST /api/messages/fetch` with `{ peer, page, limit, filter: { startTime, endTime } }`.
- Standalone mode cannot query the live QQ bridge. A token-authenticated groups request therefore reached the API but returned a server-side failure, which is expected and is not evidence about the target group.

## Full-mode live evidence

- Full mode is reachable on local port `40653` after the user completed QQ login; the local credential remains outside the repository.
- `GET /api/groups` returned a successful envelope with `success`, `data`, `timestamp`, and `requestId`. The data object contains `groups`, `totalCount`, page counters, and navigation flags.
- A group object contains `groupCode`, `groupName`, `memberCount`, `maxMember`, `remark`, and `avatarUrl`. The identifier is treated as an opaque string; its stability across repeated scans is not yet proven.
- `GET /api/groups/:groupCode/members` returned a successful array. The observed member shape includes opaque `uid`, `uin`, `nick`, `cardName`, `role`, join/speak timestamps, and moderation/status fields. Nickname values remain local-only.
- `POST /api/messages/fetch` returned a successful page with `messages`, `totalCount`, `currentPage`, `totalPages`, `hasNext`, and `fetchedAt`.
- The observed message envelope includes opaque string fields `msgId`, `msgSeq`, `peerUid`, `senderUid`, `fromUid`, timestamp strings `msgTime`/`timeStamp`, and an `elements` array. The element object is a tagged union with `elementType` and optional text, ark, JSON, struct, image, file, and other element branches.
- A local scan of the selected target's available first page established the envelope only; its observed non-empty branches were ordinary text, image, file, and system-tip elements, with no 网易云 URL-like value. No message body or raw payload was persisted.

The verified HTTP boundary is implemented and unit-tested in `QceDirectClient`. The next adapter step may normalize only the verified envelope fields; Netease card extraction remains an explicit detector input until a real card sample is inspected locally.

## JSON Export shape verified locally

The user-selected QCE JSON Export sample was inspected locally in memory; no real identifier, title, nickname, URL query, token, or raw message was stored. The observed export boundary is:

- the root may be a message array, a `{ "messages": [...] }` object, or a single message object;
- each message has `id`, optional `seq`, `timestamp`/`time`, a `sender` object, `type`, and `content.elements`;
- JSON card elements contain a data object whose `content` field is another JSON string;
- a Netease album card is represented by a nested `meta.news` branch with a Netease album URL, title/description, and optional preview image;
- music-song cards (`meta.music`) are not album candidates, and cards from other music providers are rejected;
- the local JSON normalizer drops the nested raw `content` string and sensitive fields such as token/authorization/cookie before constructing `RawQQMessage`.

The positive Netease album boundary is covered with synthetic tests in `QceJsonExportNormalizerTests` and `DesktopSyncSupportTests`. The real Direct message endpoint has not yet been observed carrying the same card branch, so the Direct/JSON equivalence remains intentionally conservative.

The Direct normalizer now parses a JSON object/array carried by `arkElement.bytesData` in memory before detector consumption. A regression test covers `meta.news` title, artist, Netease album URL, and preview extraction; malformed or non-structured `bytesData` remains available for the existing URL-only detector path. This does not claim that a raw live card sample has been persisted or verified.

The first usable release therefore uses manual QCE JSON Export as its supported input path. Direct scanning remains implemented as an experimental follow-up and must not be described as verified until the evidence below is collected.

## JSON-first production-path evidence

- One user-selected real export was imported through the WPF Desktop flow and parsed locally; the original file was not copied or uploaded.
- Only normalized ShareCandidate batches were submitted. Read-only D1 aggregates and the password-protected Album/Feed APIs confirmed that the accepted production data is available.
- The trial exposed the 100-item local Batch boundary. Desktop submission now drains all batches in one operation, aggregates receipts, and reports the full Pending count; a synthetic regression test covers a queue larger than one batch.
- This proves the supported JSON-first path only. It is not evidence for the still-unverified Direct card branch, identifier stability, or history overlap.

## Privacy boundary

The local selector uses an ordinal and displays group names only on the user's machine. Its output contains field names, types, and counts only. No live group identifier, member identifier, nickname, token, cookie, raw message, message text, or private URL is stored in this repository.

## Required evidence before the adapter is considered fully verified

- one real Direct API 网易云专辑 card structure, inspected locally only;
- Direct/JSON field differences for the same card and any stable mapping rule;
- repeated-scan stability of group/member/message identifiers;
- historical coverage and overlap behavior.

No live identifier, nickname, token, cookie, raw message, or private URL is stored in this repository.

## Generic element ID false-positive closure

A production aggregate review found that generic QCE element `data.id` values can be numeric even when the element is an ordinary face, sticker, or reply reference. Such values are not album evidence. The detector now accepts only album-specific ID keys or an album ID extracted from a supported Netease URL. Synthetic regressions cover generic-ID rejection, verified-URL precedence, and the flattened JSON Export element shape.

The Worker also preserves existing complete title and artist metadata when a later candidate contains the local `Untitled` / `Unknown artist` placeholders. Before removing the historical false positives, a D1 Time Travel recovery point was obtained and exact aggregate guards were checked; the post-cleanup snapshot reported no unknown-title, unknown-artist, missing-cover, or orphan albums. No live identifier or message content was used in the tests or written here.

On 2026-08-17, a read-only production aggregate found six recent Direct-origin album rows with the `Untitled` / `Unknown artist` / missing-cover placeholders while all six Netease URLs remained present. The Direct `bytesData` parser above is the corrective path; those existing rows require a later local re-scan or JSON re-import to refresh metadata and are not deleted by this change.
