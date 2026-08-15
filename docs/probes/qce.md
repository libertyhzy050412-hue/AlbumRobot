# QCE probe status

Status: `JSON-first MVP input verified; full-mode live envelope verified; Direct card branch, ID stability, and history overlap deferred and pending`.

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

The first usable release therefore uses manual QCE JSON Export as its supported input path. Direct scanning remains implemented as an experimental follow-up and must not be described as verified until the evidence below is collected.

## Privacy boundary

The local selector uses an ordinal and displays group names only on the user's machine. Its output contains field names, types, and counts only. No live group identifier, member identifier, nickname, token, cookie, raw message, message text, or private URL is stored in this repository.

## Required evidence before the adapter is considered fully verified

- one real Direct API 网易云专辑 card structure, inspected locally only;
- Direct/JSON field differences for the same card and any stable mapping rule;
- repeated-scan stability of group/member/message identifiers;
- historical coverage and overlap behavior.

No live identifier, nickname, token, cookie, raw message, or private URL is stored in this repository.
