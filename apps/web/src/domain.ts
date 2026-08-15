export type TabKey = "albums" | "feed" | "stats";
export type AlbumPeriod = "all" | "month" | "year";
export type AlbumSort = "recent" | "first" | "popular";
export type StatsPeriod = "week" | "month" | "year";

export type AlbumSummary = {
  netease_album_id: string;
  title: string;
  artist: string;
  cover_url?: string | null;
  netease_url?: string | null;
  first_shared_at?: string | null;
  last_shared_at?: string | null;
  distinct_sharers?: number;
  share_count?: number;
};

export type RecentShare = {
  share_id: string;
  member_id: string;
  member_nickname: string;
  shared_at: string;
  date_precision: "day" | "second";
  source_order?: number | null;
};

export type AlbumDetailResponse = {
  album: AlbumSummary;
  recent_shares: RecentShare[];
};

export type FeedItem = AlbumSummary & {
  share_id: string;
  source_message_id: string;
  member_id: string;
  member_nickname: string;
  shared_at: string;
  date_precision: "day" | "second";
  source_order?: number | null;
  is_repeat: number;
};

export type StatsRankingItem = {
  member_id: string;
  member_nickname: string;
  album_count: number;
};

export type StatsResponse = {
  period: StatsPeriod;
  period_start: string;
  album_count: number;
  ranking: StatsRankingItem[];
};

export type AlbumSelection = {
  album: AlbumSummary;
  transitionKey: string;
  shareContext?: FeedItem;
};
