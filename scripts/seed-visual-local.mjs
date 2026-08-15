const baseUrl = "http://127.0.0.1:8787";
const groupId = "local-probe-group";
const syncToken = "albumrobot-local-sync-token-not-production";

const albums = [
  ["980001", "潮汐之间", "北岸回声"],
  ["980002", "凌晨四点的电台", "玻璃公路"],
  ["980003", "漫长的夏日", "海边电影院"],
  ["980004", "蓝色房间", "低空飞行"],
  ["980005", "不眠航线", "城市气象台"],
  ["980006", "雨停以后", "沿江乐队"],
  ["980007", "缓慢燃烧", "岛屿来信"],
  ["980008", "白昼梦游", "凌晨俱乐部"],
  ["980009", "南方公路", "夏末唱片行"],
  ["980010", "月光侧面", "未完成乐团"],
  ["980011", "无人接听", "灰色星期天"],
  ["980012", "回声博物馆", "远山与海"],
  ["980013", "昨日天气", "空房间"],
  ["980014", "霓虹降落", "午夜放映室"],
  ["980015", "风经过这里", "慢速列车"],
];

const members = [
  ["visual-member-1", "林间"],
  ["visual-member-2", "阿蓝"],
  ["visual-member-3", "六月"],
  ["visual-member-4", "小岛"],
  ["visual-member-5", "北北"],
  ["visual-member-6", "晚风"],
];

const dates = [
  "2026-08-15T14:32:00+08:00",
  "2026-08-15T10:18:00+08:00",
  "2026-08-14T22:06:00+08:00",
  "2026-08-14T16:41:00+08:00",
  "2026-08-13T19:20:00+08:00",
  "2026-08-12T08:54:00+08:00",
  "2026-08-10T23:13:00+08:00",
  "2026-08-08T11:38:00+08:00",
  "2026-08-04T18:09:00+08:00",
  "2026-07-27",
  "2026-07-12",
  "2026-06-21",
  "2026-05-16",
  "2026-04-02",
  "2026-02-19",
];

const items = albums.map(([albumId, title, artist], index) => {
  const [memberId, nickname] = members[index % members.length];
  const sharedAt = dates[index];
  return {
    group_id: groupId,
    source_message_id: `visual-seed-${String(index + 1).padStart(3, "0")}`,
    source_order: index + 1,
    member_id: memberId,
    member_nickname: nickname,
    shared_at: sharedAt,
    date_precision: sharedAt.length === 10 ? "day" : "second",
    origin: "detected",
    album: {
      netease_album_id: albumId,
      title,
      artist,
      netease_url: `https://music.163.com/#/album?id=${albumId}`,
    },
  };
});

items.push(
  {
    ...items[0],
    source_message_id: "visual-seed-repeat-001",
    source_order: 16,
    shared_at: "2026-08-15T15:06:00+08:00",
  },
  {
    ...items[3],
    source_message_id: "visual-seed-repeat-002",
    source_order: 17,
    member_id: members[1][0],
    member_nickname: members[1][1],
    shared_at: "2026-08-15T15:42:00+08:00",
  },
);

const health = await fetch(`${baseUrl}/api/health`);
if (!health.ok || !(await health.json()).ok) {
  throw new Error("local Worker health check failed");
}

const response = await fetch(`${baseUrl}/api/sync/batch`, {
  method: "POST",
  headers: {
    authorization: `Bearer ${syncToken}`,
    "content-type": "application/json",
  },
  body: JSON.stringify({ group_id: groupId, items }),
});
if (!response.ok) throw new Error(`visual seed failed: ${response.status}`);

const body = await response.json();
const counts = body.items.reduce((result, item) => {
  result[item.status] = (result[item.status] ?? 0) + 1;
  return result;
}, {});

console.log(
  JSON.stringify({ ok: true, items: body.items.length, counts }, null, 2),
);
