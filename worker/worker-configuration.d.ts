interface Env {
  DB: D1Database;
  LOGIN_RATE_LIMITER: RateLimit;
  PRIMARY_GROUP_ID: string;
  GROUP_PASSWORD: string;
  SESSION_SECRET: string;
  SYNC_TOKEN: string;
  ALLOW_LOCAL_GROUP_OVERRIDE?: string;
}
