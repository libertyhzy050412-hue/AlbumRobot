export type ShareOrigin = "detected" | "repaired";

export interface ShareCandidate {
  group_id: string;
  source_message_id: string;
  source_order?: number;
  member_id: string;
  member_nickname: string;
  shared_at: string;
  date_precision: "day" | "second";
  origin: ShareOrigin;
  album: {
    netease_album_id: string;
    title: string;
    artist: string;
    cover_url?: string;
    netease_url?: string;
  };
}

export interface SyncBatchRequest {
  group_id: string;
  items: ShareCandidate[];
}

export type SyncItemStatus = "accepted" | "duplicate" | "invalid" | "unknown";

export interface SyncItemReceipt {
  source_message_id: string;
  status: SyncItemStatus;
  reason?: string;
}

export interface SyncBatchResponse {
  batch_id: string;
  status: "complete" | "incomplete";
  items: SyncItemReceipt[];
}

export const forbiddenCloudFields = [
  "raw_payload",
  "raw_message",
  "message_text",
  "chat_context",
  "cookie",
  "access_token",
  "authorization",
] as const;
