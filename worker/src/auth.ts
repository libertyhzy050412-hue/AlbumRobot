const encoder = new TextEncoder();
const sessionCookieName = "albumrobot_session";
const sessionLifetimeSeconds = 60 * 60 * 24 * 365 * 5;

type SessionPayload = {
  version: 1;
  expiresAt: number;
  passwordFingerprint: string;
};

function encodeBase64Url(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary)
    .replaceAll("+", "-")
    .replaceAll("/", "_")
    .replace(/=+$/u, "");
}

function decodeBase64Url(value: string): Uint8Array | null {
  try {
    const normalized = value.replaceAll("-", "+").replaceAll("_", "/");
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=");
    return Uint8Array.from(atob(padded), (character) =>
      character.charCodeAt(0),
    );
  } catch {
    return null;
  }
}

async function hmac(secret: string, value: string): Promise<Uint8Array> {
  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  return new Uint8Array(
    await crypto.subtle.sign("HMAC", key, encoder.encode(value)),
  );
}

function fixedTimeEqual(left: Uint8Array, right: Uint8Array): boolean {
  let difference = left.length ^ right.length;
  const length = Math.max(left.length, right.length);
  for (let index = 0; index < length; index += 1) {
    difference |= (left[index] ?? 0) ^ (right[index] ?? 0);
  }
  return difference === 0;
}

async function passwordFingerprint(env: Env): Promise<string> {
  return encodeBase64Url(
    await hmac(env.SESSION_SECRET, `password:${env.GROUP_PASSWORD}`),
  );
}

export function hasRuntimeSecrets(env: Env): boolean {
  return (
    typeof env.PRIMARY_GROUP_ID === "string" &&
    env.PRIMARY_GROUP_ID.trim().length > 0 &&
    typeof env.GROUP_PASSWORD === "string" &&
    env.GROUP_PASSWORD.length >= 8 &&
    typeof env.SESSION_SECRET === "string" &&
    env.SESSION_SECRET.length >= 32 &&
    typeof env.SYNC_TOKEN === "string" &&
    env.SYNC_TOKEN.length >= 32
  );
}

export async function secretsEqual(
  candidate: string,
  expected: string,
): Promise<boolean> {
  const [candidateDigest, expectedDigest] = await Promise.all([
    crypto.subtle.digest("SHA-256", encoder.encode(candidate)),
    crypto.subtle.digest("SHA-256", encoder.encode(expected)),
  ]);
  return fixedTimeEqual(
    new Uint8Array(candidateDigest),
    new Uint8Array(expectedDigest),
  );
}

export async function createSessionToken(env: Env): Promise<string> {
  const payload: SessionPayload = {
    version: 1,
    expiresAt: Math.floor(Date.now() / 1000) + sessionLifetimeSeconds,
    passwordFingerprint: await passwordFingerprint(env),
  };
  const encodedPayload = encodeBase64Url(
    encoder.encode(JSON.stringify(payload)),
  );
  const signature = encodeBase64Url(
    await hmac(env.SESSION_SECRET, encodedPayload),
  );
  return `${encodedPayload}.${signature}`;
}

export async function verifySessionToken(
  token: string | null,
  env: Env,
): Promise<boolean> {
  if (!token) return false;
  const segments = token.split(".");
  if (segments.length !== 2) return false;
  const [encodedPayload, encodedSignature] = segments;
  const signature = decodeBase64Url(encodedSignature);
  if (!signature) return false;
  const expectedSignature = await hmac(env.SESSION_SECRET, encodedPayload);
  if (!fixedTimeEqual(signature, expectedSignature)) return false;

  const payloadBytes = decodeBase64Url(encodedPayload);
  if (!payloadBytes) return false;
  try {
    const payload = JSON.parse(
      new TextDecoder().decode(payloadBytes),
    ) as Partial<SessionPayload>;
    return (
      payload.version === 1 &&
      typeof payload.expiresAt === "number" &&
      payload.expiresAt > Math.floor(Date.now() / 1000) &&
      payload.passwordFingerprint === (await passwordFingerprint(env))
    );
  } catch {
    return false;
  }
}

export function readSessionCookie(
  cookieHeader: string | undefined,
): string | null {
  if (!cookieHeader) return null;
  for (const segment of cookieHeader.split(";")) {
    const separator = segment.indexOf("=");
    if (separator < 0) continue;
    const name = segment.slice(0, separator).trim();
    if (name !== sessionCookieName) continue;
    return segment.slice(separator + 1).trim() || null;
  }
  return null;
}

export function createSessionCookie(token: string, requestUrl: string): string {
  const secure = new URL(requestUrl).protocol === "https:" ? "; Secure" : "";
  return `${sessionCookieName}=${token}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${sessionLifetimeSeconds}${secure}`;
}

export function clearSessionCookie(requestUrl: string): string {
  const secure = new URL(requestUrl).protocol === "https:" ? "; Secure" : "";
  return `${sessionCookieName}=; Path=/; HttpOnly; SameSite=Lax; Max-Age=0${secure}`;
}

export function readBearerToken(
  authorizationHeader: string | undefined,
): string | null {
  if (!authorizationHeader) return null;
  const match = /^Bearer\s+(.+)$/iu.exec(authorizationHeader);
  return match?.[1]?.trim() || null;
}

export function isCrossSiteRequest(secFetchSite: string | undefined): boolean {
  return secFetchSite?.toLowerCase() === "cross-site";
}
