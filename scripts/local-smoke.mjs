const baseUrl =
  process.env.ALBUMROBOT_WORKER_BASE_URL ?? "http://127.0.0.1:8787";
const suffix = crypto.randomUUID().slice(0, 8);
const groupId = `local-smoke-${suffix}`;
const sourceMessageId = `local-smoke-message-${suffix}`;
const groupPassword =
  process.env.ALBUMROBOT_GROUP_PASSWORD ?? "albumrobot-local-password";
const syncToken =
  process.env.ALBUMROBOT_SYNC_TOKEN ??
  "albumrobot-local-sync-token-not-production";
const payload = {
  group_id: groupId,
  items: [
    {
      group_id: groupId,
      source_message_id: sourceMessageId,
      source_order: 1,
      member_id: "local-smoke-member",
      member_nickname: "Local Smoke",
      shared_at: "2026-08-15T00:00:00+08:00",
      date_precision: "second",
      origin: "detected",
      album: {
        netease_album_id: "123456",
        title: "Local Smoke Album",
        artist: "Local Smoke Artist",
        netease_url: "https://music.163.com/#/album?id=123456",
      },
    },
  ],
};

async function post(body) {
  const response = await fetch(`${baseUrl}/api/sync/batch`, {
    method: "POST",
    headers: {
      authorization: `Bearer ${syncToken}`,
      "content-type": "application/json",
    },
    body: JSON.stringify(body),
  });
  if (!response.ok) throw new Error(`batch request failed: ${response.status}`);
  return response.json();
}

const health = await fetch(`${baseUrl}/api/health`);
if (!health.ok || !(await health.json()).ok)
  throw new Error("local health check failed");
const login = await fetch(`${baseUrl}/api/auth/session`, {
  method: "POST",
  headers: { "content-type": "application/json" },
  body: JSON.stringify({ password: groupPassword }),
});
if (!login.ok) throw new Error(`session request failed: ${login.status}`);
const sessionCookie = login.headers.get("set-cookie")?.split(";", 1)[0];
if (!sessionCookie) throw new Error("session cookie was not issued");
const first = await post(payload);
const second = await post(payload);
const rejected = await post({
  ...payload,
  items: [{ ...payload.items[0], raw_payload: { text: "forbidden" } }],
});
const albums = await fetch(
  `${baseUrl}/api/albums?group_id=${encodeURIComponent(groupId)}`,
  { headers: { cookie: sessionCookie } },
);
const albumBody = await albums.json();

if (first.items[0]?.status !== "accepted")
  throw new Error("first batch did not accept");
if (second.items[0]?.status !== "duplicate")
  throw new Error("replay did not become duplicate");
if (rejected.items[0]?.status !== "invalid")
  throw new Error("raw payload was not rejected");
if (albumBody.items?.length !== 1)
  throw new Error("album browse did not return one album");

console.log(
  JSON.stringify(
    {
      ok: true,
      group_id: groupId,
      first: first.items[0].status,
      replay: second.items[0].status,
      forbidden_payload: rejected.items[0].status,
      albums: albumBody.items.length,
    },
    null,
    2,
  ),
);
