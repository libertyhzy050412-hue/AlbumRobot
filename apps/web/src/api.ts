import type {
  AlbumDetailResponse,
  AlbumPeriod,
  AlbumSort,
  AlbumSummary,
  FeedItem,
  StatsPeriod,
  StatsResponse,
} from "./domain";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { credentials: "same-origin" });
  if (!response.ok)
    throw new ApiError(response.status, `HTTP ${response.status}`);
  return (await response.json()) as T;
}

export async function fetchAlbums(options: {
  search: string;
  period: AlbumPeriod | StatsPeriod;
  sort: AlbumSort;
}): Promise<AlbumSummary[]> {
  const parameters = new URLSearchParams({
    limit: "100",
    period: options.period,
    sort: options.sort,
  });
  if (options.search.trim()) parameters.set("search", options.search.trim());
  const response = await getJson<{ items: AlbumSummary[] }>(
    `/api/albums?${parameters.toString()}`,
  );
  return response.items;
}

export function fetchAlbumDetail(
  albumId: string,
): Promise<AlbumDetailResponse> {
  return getJson(`/api/albums/${encodeURIComponent(albumId)}`);
}

export async function fetchFeed(): Promise<FeedItem[]> {
  const response = await getJson<{ items: FeedItem[] }>("/api/feed?limit=100");
  return response.items;
}

export function fetchStats(period: StatsPeriod): Promise<StatsResponse> {
  return getJson(`/api/stats?period=${period}`);
}
