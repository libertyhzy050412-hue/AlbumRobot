CREATE TABLE IF NOT EXISTS groups (
  group_id TEXT PRIMARY KEY,
  display_name TEXT,
  created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS albums (
  netease_album_id TEXT PRIMARY KEY,
  title TEXT NOT NULL,
  artist TEXT NOT NULL,
  cover_url TEXT,
  netease_url TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS members (
  group_id TEXT NOT NULL,
  member_id TEXT NOT NULL,
  nickname TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  PRIMARY KEY (group_id, member_id),
  FOREIGN KEY (group_id) REFERENCES groups(group_id)
);

CREATE TABLE IF NOT EXISTS shares (
  share_id INTEGER PRIMARY KEY AUTOINCREMENT,
  group_id TEXT NOT NULL,
  source_message_id TEXT NOT NULL,
  source_order INTEGER,
  member_id TEXT NOT NULL,
  netease_album_id TEXT NOT NULL,
  shared_at TEXT NOT NULL,
  date_precision TEXT NOT NULL CHECK (date_precision IN ('day', 'second')),
  origin TEXT NOT NULL CHECK (origin IN ('detected', 'repaired')),
  created_at TEXT NOT NULL,
  UNIQUE (group_id, source_message_id),
  FOREIGN KEY (group_id) REFERENCES groups(group_id),
  FOREIGN KEY (group_id, member_id) REFERENCES members(group_id, member_id),
  FOREIGN KEY (netease_album_id) REFERENCES albums(netease_album_id)
);

CREATE INDEX IF NOT EXISTS idx_shares_group_time
  ON shares (group_id, shared_at DESC, share_id DESC);

CREATE INDEX IF NOT EXISTS idx_shares_group_album
  ON shares (group_id, netease_album_id, shared_at DESC);
