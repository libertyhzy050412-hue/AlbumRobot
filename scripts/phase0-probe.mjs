import { execFile } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);
const repositoryRoot = fileURLToPath(new URL("..", import.meta.url));
const qceBaseUrl =
  process.env.ALBUMROBOT_QCE_BASE_URL ?? "http://127.0.0.1:40653";
const result = {
  generated_at: new Date().toISOString(),
  privacy: {
    raw_qq_read: false,
    token_persisted: false,
    remote_cloudflare_contacted: false,
  },
  qce: { base_url: qceBaseUrl, endpoints: [] },
  cloudflare: {},
  netease: {
    parser: "C# local detector; live card/metadata pending sanitized sample",
    supplied_url: Boolean(process.env.ALBUMROBOT_NETEASE_PROBE_URL),
  },
};

async function probe(url) {
  try {
    const response = await fetch(url, { redirect: "manual" });
    return {
      url,
      status: response.status,
      location: response.headers.get("location") ?? undefined,
    };
  } catch (error) {
    return {
      url,
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

for (const path of ["/", "/qce", "/api", "/api/health"]) {
  result.qce.endpoints.push(await probe(`${qceBaseUrl}${path}`));
}

try {
  const command = process.execPath;
  const args = [
    fileURLToPath(
      new URL(
        "../worker/node_modules/wrangler/bin/wrangler.js",
        import.meta.url,
      ),
    ),
    "--version",
  ];
  const { stdout } = await execFileAsync(command, args, {
    cwd: repositoryRoot,
    windowsHide: true,
  });
  result.cloudflare.wrangler = stdout.trim();
  result.cloudflare.local_config = "worker/wrangler.local.jsonc";
} catch (error) {
  result.cloudflare.wrangler = "unavailable; run pnpm install first";
  result.cloudflare.error =
    error instanceof Error ? error.message : String(error);
}

const outputDirectory = fileURLToPath(
  new URL("../artifacts/phase0/", import.meta.url),
);
await mkdir(outputDirectory, { recursive: true });
await writeFile(
  join(outputDirectory, "probe-result.json"),
  `${JSON.stringify(result, null, 2)}\n`,
  "utf8",
);
console.log(JSON.stringify(result, null, 2));
console.log(
  "Phase 0 probe result written to artifacts/phase0/probe-result.json (ignored by Git).",
);
