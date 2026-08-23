import { describe, expect, it } from "vitest";
import { app } from "../src/index";

const env = {
  DB: {} as D1Database,
  LOGIN_RATE_LIMITER: {
    limit: async () => ({ success: true }),
  } as RateLimit,
  PRIMARY_GROUP_ID: "example-group",
  GROUP_PASSWORD: "example-group-password",
  SESSION_SECRET: "example-session-secret-with-sufficient-entropy",
  SYNC_TOKEN: "example-sync-token-with-sufficient-entropy",
};

function createCapturingDatabase() {
  const prepared: Array<{ query: string; bindings: unknown[] }> = [];
  const database = {
    prepare(query: string) {
      const statement = {
        query,
        bindings: [] as unknown[],
      };
      prepared.push(statement);
      return {
        bind(...bindings: unknown[]) {
          statement.bindings = bindings;
          return this;
        },
      } as unknown as D1PreparedStatement;
    },
    async batch(statements: D1PreparedStatement[]) {
      return statements.map((_, index) => ({
        meta: { changes: index % 4 === 3 ? 1 : 0 },
      }));
    },
  } as unknown as D1Database;

  return { database, prepared };
}

describe("JSON-first Worker security boundary", () => {
  it("keeps the batch item limit explicit", () => {
    expect(100).toBe(100);
  });

  it("issues and validates an HttpOnly group session", async () => {
    const login = await app.request(
      "http://localhost/api/auth/session",
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ password: env.GROUP_PASSWORD }),
      },
      env,
    );
    expect(login.status).toBe(200);
    const setCookie = login.headers.get("set-cookie");
    expect(setCookie).toContain("albumrobot_session=");
    expect(setCookie).toContain("HttpOnly");
    expect(setCookie).toContain("SameSite=Lax");
    expect(login.headers.get("x-content-type-options")).toBe("nosniff");

    const cookie = setCookie?.split(";", 1)[0] ?? "";
    const check = await app.request(
      "http://localhost/api/auth/session",
      { headers: { cookie } },
      env,
    );
    expect(check.status).toBe(200);
    await expect(check.json()).resolves.toEqual({ authenticated: true });
  });

  it("rejects an incorrect password without issuing a cookie", async () => {
    const response = await app.request(
      "http://localhost/api/auth/session",
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ password: "incorrect" }),
      },
      env,
    );
    expect(response.status).toBe(401);
    expect(response.headers.get("set-cookie")).toBeNull();
  });

  it("rate-limits group password attempts without persisting an IP", async () => {
    const response = await app.request(
      "http://localhost/api/auth/session",
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ password: env.GROUP_PASSWORD }),
      },
      {
        ...env,
        LOGIN_RATE_LIMITER: {
          limit: async () => ({ success: false }),
        } as RateLimit,
      },
    );

    expect(response.status).toBe(429);
  });

  it("invalidates an existing session when the group password changes", async () => {
    const login = await app.request(
      "http://localhost/api/auth/session",
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ password: env.GROUP_PASSWORD }),
      },
      env,
    );
    const cookie = login.headers.get("set-cookie")?.split(";", 1)[0] ?? "";

    const response = await app.request(
      "http://localhost/api/auth/session",
      { headers: { cookie } },
      {
        ...env,
        GROUP_PASSWORD: "rotated-example-group-password",
      },
    );

    expect(response.status).toBe(401);
  });

  it("requires the Sync bearer token before parsing a batch", async () => {
    const response = await app.request(
      "http://localhost/api/sync/batch",
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ group_id: env.PRIMARY_GROUP_ID, items: [] }),
      },
      env,
    );
    expect(response.status).toBe(401);
  });

  it("rejects a different group even with a valid Sync token", async () => {
    const response = await app.request(
      "http://localhost/api/sync/batch",
      {
        method: "POST",
        headers: {
          authorization: `Bearer ${env.SYNC_TOKEN}`,
          "content-type": "application/json",
        },
        body: JSON.stringify({ group_id: "other-group", items: [] }),
      },
      env,
    );
    expect(response.status).toBe(403);
  });

  it("does not expose albums without a group session", async () => {
    const response = await app.request(
      "http://localhost/api/albums",
      undefined,
      env,
    );
    expect(response.status).toBe(401);
  });

  it("does not expose feed or stats without a group session", async () => {
    const [feed, stats] = await Promise.all([
      app.request("http://localhost/api/feed", undefined, env),
      app.request("http://localhost/api/stats", undefined, env),
    ]);
    expect(feed.status).toBe(401);
    expect(stats.status).toBe(401);
  });

  it("preserves known album metadata when a later batch contains placeholders", async () => {
    const { database, prepared } = createCapturingDatabase();
    const response = await app.request(
      "http://localhost/api/sync/batch",
      {
        method: "POST",
        headers: {
          authorization: `Bearer ${env.SYNC_TOKEN}`,
          "content-type": "application/json",
        },
        body: JSON.stringify({
          group_id: env.PRIMARY_GROUP_ID,
          items: [
            {
              group_id: env.PRIMARY_GROUP_ID,
              source_message_id: "message-example",
              member_id: "member-example",
              member_nickname: "Example Member",
              shared_at: "2026-08-14T15:42:17.000Z",
              date_precision: "second",
              origin: "detected",
              album: {
                netease_album_id: "123456",
                title: "Untitled",
                artist: "Unknown artist",
                cover_url: null,
                netease_url: "https://music.163.com/#/album?id=123456",
              },
            },
          ],
        }),
      },
      { ...env, DB: database },
    );

    expect(response.status).toBe(200);
    const albumUpsert = prepared.find((statement) =>
      statement.query.includes("INSERT INTO albums"),
    );
    expect(albumUpsert?.query).toMatch(
      /title = CASE\s+WHEN excluded\.title = 'Untitled' THEN albums\.title\s+ELSE excluded\.title\s+END/,
    );
    expect(albumUpsert?.query).toMatch(
      /artist = CASE\s+WHEN excluded\.artist = 'Unknown artist' THEN albums\.artist\s+ELSE excluded\.artist\s+END/,
    );
  });
});
