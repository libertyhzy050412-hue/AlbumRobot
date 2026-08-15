import { access, readFile } from "node:fs/promises";
import { constants } from "node:fs";

const root = new URL("..", import.meta.url);
const configUrl = new URL("worker/wrangler.jsonc", root);
const requiredFiles = [
  new URL("apps/web/dist/index.html", root),
  new URL("worker/migrations/0001_phase0.sql", root),
];

const config = await readFile(configUrl, "utf8");
const failures = [];

if (config.includes("REPLACE_WITH_D1_DATABASE_ID")) {
  failures.push(
    "worker/wrangler.jsonc still contains the D1 database placeholder",
  );
}
for (const localOnlyValue of [
  "albumrobot-local-password",
  "albumrobot-local-session-secret",
  "albumrobot-local-sync-token",
  "ALLOW_LOCAL_GROUP_OVERRIDE",
]) {
  if (config.includes(localOnlyValue)) {
    failures.push(
      `production config contains local-only value: ${localOnlyValue}`,
    );
  }
}
if (!config.includes('"name": "LOGIN_RATE_LIMITER"')) {
  failures.push("production config is missing the login rate limiter binding");
}
for (const file of requiredFiles) {
  try {
    await access(file, constants.R_OK);
  } catch {
    failures.push(`required deployment artifact is missing: ${file.pathname}`);
  }
}

if (failures.length > 0) {
  console.error("Production preflight failed:");
  for (const failure of failures) console.error(`- ${failure}`);
  process.exitCode = 1;
} else {
  console.log("Production preflight passed");
}
