import { Hono } from "hono";
import type {
  ShareCandidate,
  SyncBatchResponse,
  SyncItemReceipt,
} from "@albumrobot/contracts";
import { forbiddenCloudFields } from "@albumrobot/contracts";
import {
  clearSessionCookie,
  createSessionCookie,
  createSessionToken,
  hasRuntimeSecrets,
  isCrossSiteRequest,
  readBearerToken,
  readSessionCookie,
  secretsEqual,
  verifySessionToken,
} from "./auth";
import { businessPeriodStart, type StatsPeriod } from "./business-time";

export const app = new Hono<{ Bindings: Env }>();

const jsonError = (
  message: string,
  status: 400 | 401 | 403 | 404 | 429 | 500 | 503,
) =>
  new Response(JSON.stringify({ error: message }), {
    status,
    headers: {
      "cache-control": "no-store",
      "content-type": "application/json; charset=utf-8",
    },
  });

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function containsForbiddenField(value: unknown): boolean {
  if (Array.isArray(value)) return value.some(containsForbiddenField);
  if (!isRecord(value)) return false;
  if (forbiddenCloudFields.some((field) => field in value)) return true;
  return Object.values(value).some(containsForbiddenField);
}

function allowsLocalGroupOverride(env: Env): boolean {
  return env.ALLOW_LOCAL_GROUP_OVERRIDE === "true";
}

function selectedGroup(c: {
  req: { query(name: string): string | undefined };
  env: Env;
}): string {
  return allowsLocalGroupOverride(c.env)
    ? (c.req.query("group_id") ?? c.env.PRIMARY_GROUP_ID)
    : c.env.PRIMARY_GROUP_ID;
}

function boundedLimit(value: string | undefined, fallback: number): number {
  const parsed = Number(value ?? fallback);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.min(Math.max(Math.trunc(parsed), 1), 100);
}

function albumPeriodBoundary(period: string | undefined): string | null {
  if (period === "week") return businessPeriodStart("week");
  if (period === "month") return businessPeriodStart("month");
  if (period === "year") return businessPeriodStart("year");
  return null;
}

function statsPeriod(value: string | undefined): StatsPeriod {
  return value === "month" || value === "year" ? value : "week";
}

app.use("*", async (c, next) => {
  await next();
  c.header(
    "content-security-policy",
    "default-src 'none'; frame-ancestors 'none'",
  );
  c.header(
    "permissions-policy",
    "camera=(), geolocation=(), microphone=(), payment=(), usb=()",
  );
  c.header("referrer-policy", "no-referrer");
  c.header("x-content-type-options", "nosniff");
  c.header("x-frame-options", "DENY");
});

function isCandidate(value: unknown, groupId: string): value is ShareCandidate {
  if (
    !isRecord(value) ||
    value.group_id !== groupId ||
    containsForbiddenField(value)
  )
    return false;
  const album = value.album;
  return (
    typeof value.source_message_id === "string" &&
    value.source_message_id.length > 0 &&
    typeof value.member_id === "string" &&
    value.member_id.length > 0 &&
    typeof value.member_nickname === "string" &&
    value.member_nickname.length > 0 &&
    typeof value.shared_at === "string" &&
    (value.date_precision === "day" || value.date_precision === "second") &&
    (value.origin === "detected" || value.origin === "repaired") &&
    isRecord(album) &&
    typeof album.netease_album_id === "string" &&
    /^\d+$/.test(album.netease_album_id) &&
    typeof album.title === "string" &&
    album.title.trim().length > 0 &&
    typeof album.artist === "string" &&
    album.artist.trim().length > 0
  );
}

app.get("/api/health", async (c) => {
  try {
    const result = await c.env.DB.prepare("SELECT 1 AS ok").first<{
      ok: number;
    }>();
    return c.json({
      ok: result?.ok === 1 && hasRuntimeSecrets(c.env),
      phase: "json-first-mvp",
      database: result?.ok === 1 ? "ready" : "unavailable",
      configured: hasRuntimeSecrets(c.env),
    });
  } catch {
    return c.json(
      {
        ok: false,
        phase: "json-first-mvp",
        database: "unavailable",
        configured: hasRuntimeSecrets(c.env),
      },
      503,
    );
  }
});

app.post("/api/auth/session", async (c) => {
  if (!hasRuntimeSecrets(c.env))
    return jsonError("service is not configured", 503);
  if (isCrossSiteRequest(c.req.header("sec-fetch-site")))
    return jsonError("request is not allowed", 403);
  const rateLimit = await c.env.LOGIN_RATE_LIMITER.limit({
    key: "group-password-login",
  });
  if (!rateLimit.success) return jsonError("too many attempts", 429);

  let payload: unknown;
  try {
    payload = await c.req.json();
  } catch {
    return jsonError("request body must be JSON", 400);
  }
  const password =
    isRecord(payload) && typeof payload.password === "string"
      ? payload.password
      : "";
  if (
    password.length === 0 ||
    password.length > 256 ||
    !(await secretsEqual(password, c.env.GROUP_PASSWORD))
  ) {
    return jsonError("password is incorrect", 401);
  }

  const token = await createSessionToken(c.env);
  c.header("set-cookie", createSessionCookie(token, c.req.url));
  c.header("cache-control", "no-store");
  return c.json({ authenticated: true });
});

app.get("/api/auth/session", async (c) => {
  if (!hasRuntimeSecrets(c.env))
    return jsonError("service is not configured", 503);
  const authenticated = await verifySessionToken(
    readSessionCookie(c.req.header("cookie")),
    c.env,
  );
  if (!authenticated) return jsonError("authentication required", 401);
  c.header("cache-control", "no-store");
  return c.json({ authenticated: true });
});

app.delete("/api/auth/session", (c) => {
  if (isCrossSiteRequest(c.req.header("sec-fetch-site")))
    return jsonError("request is not allowed", 403);
  c.header("set-cookie", clearSessionCookie(c.req.url));
  c.header("cache-control", "no-store");
  return c.json({ authenticated: false });
});

app.get("/api/albums", async (c) => {
  if (!hasRuntimeSecrets(c.env))
    return jsonError("service is not configured", 503);
  const authenticated = await verifySessionToken(
    readSessionCookie(c.req.header("cookie")),
    c.env,
  );
  if (!authenticated) return jsonError("authentication required", 401);
  const groupId = selectedGroup(c);
  const limit = boundedLimit(c.req.query("limit"), 60);
  const search = (c.req.query("search") ?? "").trim().slice(0, 80);
  const searchPattern = `%${search.toLocaleLowerCase()}%`;
  const periodBoundary = albumPeriodBoundary(c.req.query("period"));
  const sort = c.req.query("sort");
  const orderBy =
    sort === "first"
      ? "first_shared_at DESC, a.netease_album_id DESC"
      : sort === "popular"
        ? "distinct_sharers DESC, last_shared_at DESC, a.netease_album_id DESC"
        : "last_shared_at DESC, a.netease_album_id DESC";
  const result = await c.env.DB.prepare(
    `SELECT a.netease_album_id, a.title, a.artist, a.cover_url, a.netease_url,
            MIN(s.shared_at) AS first_shared_at,
            MAX(s.shared_at) AS last_shared_at,
            COUNT(DISTINCT s.member_id) AS distinct_sharers,
            COUNT(*) AS share_count
       FROM albums a
       JOIN shares s ON s.netease_album_id = a.netease_album_id
      WHERE s.group_id = ?1
        AND (?2 IS NULL OR s.shared_at >= ?2)
        AND (
          ?3 = '' OR
          LOWER(a.title) LIKE ?4 OR
          LOWER(a.artist) LIKE ?4 OR
          EXISTS (
            SELECT 1
              FROM shares matching_share
              JOIN members matching_member
                ON matching_member.group_id = matching_share.group_id
               AND matching_member.member_id = matching_share.member_id
             WHERE matching_share.group_id = ?1
               AND matching_share.netease_album_id = a.netease_album_id
               AND LOWER(matching_member.nickname) LIKE ?4
          )
        )
      GROUP BY a.netease_album_id
      ORDER BY ${orderBy}
      LIMIT ?5`,
  )
    .bind(groupId, periodBoundary, search, searchPattern, limit)
    .all();
  return c.json({ items: result.results, next_cursor: null });
});

app.get("/api/albums/:albumId", async (c) => {
  if (!hasRuntimeSecrets(c.env))
    return jsonError("service is not configured", 503);
  const authenticated = await verifySessionToken(
    readSessionCookie(c.req.header("cookie")),
    c.env,
  );
  if (!authenticated) return jsonError("authentication required", 401);

  const albumId = c.req.param("albumId");
  if (!/^\d+$/u.test(albumId)) return jsonError("album not found", 404);
  const groupId = selectedGroup(c);
  const album = await c.env.DB.prepare(
    `SELECT a.netease_album_id, a.title, a.artist, a.cover_url, a.netease_url,
            MIN(s.shared_at) AS first_shared_at,
            MAX(s.shared_at) AS last_shared_at,
            COUNT(DISTINCT s.member_id) AS distinct_sharers,
            COUNT(*) AS share_count
       FROM albums a
       JOIN shares s ON s.netease_album_id = a.netease_album_id
      WHERE s.group_id = ?1 AND a.netease_album_id = ?2
      GROUP BY a.netease_album_id`,
  )
    .bind(groupId, albumId)
    .first();
  if (!album) return jsonError("album not found", 404);

  const recentShares = await c.env.DB.prepare(
    `SELECT CAST(s.share_id AS TEXT) AS share_id,
            s.member_id, m.nickname AS member_nickname,
            s.shared_at, s.date_precision, s.source_order
       FROM shares s
       JOIN members m
         ON m.group_id = s.group_id AND m.member_id = s.member_id
      WHERE s.group_id = ?1 AND s.netease_album_id = ?2
      ORDER BY s.shared_at DESC, s.source_order DESC, s.share_id DESC
      LIMIT 12`,
  )
    .bind(groupId, albumId)
    .all();

  return c.json({ album, recent_shares: recentShares.results });
});

app.get("/api/feed", async (c) => {
  if (!hasRuntimeSecrets(c.env))
    return jsonError("service is not configured", 503);
  const authenticated = await verifySessionToken(
    readSessionCookie(c.req.header("cookie")),
    c.env,
  );
  if (!authenticated) return jsonError("authentication required", 401);

  const groupId = selectedGroup(c);
  const limit = boundedLimit(c.req.query("limit"), 80);
  const result = await c.env.DB.prepare(
    `SELECT CAST(s.share_id AS TEXT) AS share_id,
            s.source_message_id, s.member_id,
            m.nickname AS member_nickname,
            s.netease_album_id, a.title, a.artist, a.cover_url, a.netease_url,
            s.shared_at, s.date_precision, s.source_order,
            CASE WHEN EXISTS (
              SELECT 1
                FROM shares previous
               WHERE previous.group_id = s.group_id
                 AND previous.member_id = s.member_id
                 AND previous.netease_album_id = s.netease_album_id
                 AND previous.share_id < s.share_id
            ) THEN 1 ELSE 0 END AS is_repeat
       FROM shares s
       JOIN members m
         ON m.group_id = s.group_id AND m.member_id = s.member_id
       JOIN albums a ON a.netease_album_id = s.netease_album_id
      WHERE s.group_id = ?1
      ORDER BY s.shared_at DESC, s.source_order DESC, s.share_id DESC
      LIMIT ?2`,
  )
    .bind(groupId, limit)
    .all();
  return c.json({ items: result.results, next_cursor: null });
});

app.get("/api/stats", async (c) => {
  if (!hasRuntimeSecrets(c.env))
    return jsonError("service is not configured", 503);
  const authenticated = await verifySessionToken(
    readSessionCookie(c.req.header("cookie")),
    c.env,
  );
  if (!authenticated) return jsonError("authentication required", 401);

  const groupId = selectedGroup(c);
  const period = statsPeriod(c.req.query("period"));
  const boundary = businessPeriodStart(period);
  const total = await c.env.DB.prepare(
    `SELECT COUNT(DISTINCT netease_album_id) AS album_count
       FROM shares
      WHERE group_id = ?1 AND shared_at >= ?2`,
  )
    .bind(groupId, boundary)
    .first<{ album_count: number }>();
  const ranking = await c.env.DB.prepare(
    `SELECT s.member_id, m.nickname AS member_nickname,
            COUNT(DISTINCT s.netease_album_id) AS album_count
       FROM shares s
       JOIN members m
         ON m.group_id = s.group_id AND m.member_id = s.member_id
      WHERE s.group_id = ?1 AND s.shared_at >= ?2
      GROUP BY s.member_id, m.nickname
      ORDER BY album_count DESC, m.nickname ASC
      LIMIT 50`,
  )
    .bind(groupId, boundary)
    .all();

  return c.json({
    period,
    period_start: boundary,
    album_count: total?.album_count ?? 0,
    ranking: ranking.results,
  });
});

app.post("/api/sync/batch", async (c) => {
  if (!hasRuntimeSecrets(c.env))
    return jsonError("service is not configured", 503);
  const syncToken = readBearerToken(c.req.header("authorization"));
  if (!syncToken || !(await secretsEqual(syncToken, c.env.SYNC_TOKEN)))
    return jsonError("sync authorization required", 401);

  let payload: unknown;
  try {
    payload = await c.req.json();
  } catch {
    return jsonError("request body must be JSON", 400);
  }

  if (
    !isRecord(payload) ||
    typeof payload.group_id !== "string" ||
    !Array.isArray(payload.items)
  ) {
    return jsonError("invalid batch envelope", 400);
  }
  const groupId = payload.group_id;
  if (groupId !== c.env.PRIMARY_GROUP_ID && !allowsLocalGroupOverride(c.env))
    return jsonError("group is not allowed", 403);
  const rawItems = payload.items;
  if (rawItems.length > 100) return jsonError("batch item limit is 100", 400);

  const now = new Date().toISOString();
  const statements: D1PreparedStatement[] = [];
  const receipts: SyncItemReceipt[] = [];

  for (const rawItem of rawItems) {
    if (!isCandidate(rawItem, groupId)) {
      receipts.push({
        source_message_id:
          isRecord(rawItem) && typeof rawItem.source_message_id === "string"
            ? rawItem.source_message_id
            : "unknown",
        status: "invalid",
        reason: "candidate schema or group mismatch",
      });
      continue;
    }

    const item = rawItem;

    statements.push(
      c.env.DB.prepare(
        `INSERT INTO groups (group_id, created_at) VALUES (?1, ?2)
         ON CONFLICT(group_id) DO NOTHING`,
      ).bind(item.group_id, now),
      c.env.DB.prepare(
        `INSERT INTO albums (netease_album_id, title, artist, cover_url, netease_url, created_at, updated_at)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?6)
         ON CONFLICT(netease_album_id) DO UPDATE SET
           title = CASE
             WHEN excluded.title = 'Untitled' THEN albums.title
             ELSE excluded.title
           END,
           artist = CASE
             WHEN excluded.artist = 'Unknown artist' THEN albums.artist
             ELSE excluded.artist
           END,
           cover_url = COALESCE(excluded.cover_url, albums.cover_url),
           netease_url = COALESCE(excluded.netease_url, albums.netease_url),
           updated_at = excluded.updated_at`,
      ).bind(
        item.album.netease_album_id,
        item.album.title.trim(),
        item.album.artist.trim(),
        item.album.cover_url ?? null,
        item.album.netease_url ?? null,
        now,
      ),
      c.env.DB.prepare(
        `INSERT INTO members (group_id, member_id, nickname, created_at, updated_at)
         VALUES (?1, ?2, ?3, ?4, ?4)
         ON CONFLICT(group_id, member_id) DO UPDATE SET
           nickname = excluded.nickname,
           updated_at = excluded.updated_at`,
      ).bind(item.group_id, item.member_id, item.member_nickname.trim(), now),
      c.env.DB.prepare(
        `INSERT INTO shares
          (group_id, source_message_id, source_order, member_id, netease_album_id,
           shared_at, date_precision, origin, created_at)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9)
         ON CONFLICT(group_id, source_message_id) DO NOTHING`,
      ).bind(
        item.group_id,
        item.source_message_id,
        item.source_order ?? null,
        item.member_id,
        item.album.netease_album_id,
        item.shared_at,
        item.date_precision,
        item.origin,
        now,
      ),
    );
  }

  try {
    const results =
      statements.length > 0 ? await c.env.DB.batch(statements) : [];
    let resultIndex = 0;
    for (const rawItem of rawItems) {
      if (!isCandidate(rawItem, groupId)) continue;
      const item = rawItem;
      const shareResult = results[resultIndex + 3];
      const changes = shareResult?.meta?.changes ?? 0;
      receipts.push({
        source_message_id: item.source_message_id,
        status: changes > 0 ? "accepted" : "duplicate",
      });
      resultIndex += 4;
    }
  } catch {
    return jsonError("batch transaction failed", 500);
  }

  const response: SyncBatchResponse = {
    batch_id: crypto.randomUUID(),
    status: receipts.every((item) => item.status !== "unknown")
      ? "complete"
      : "incomplete",
    items: receipts,
  };
  return c.json(response);
});

app.notFound((c) => c.json({ error: "not found" }, 404));

export default app;
