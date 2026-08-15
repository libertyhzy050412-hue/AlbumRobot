import { readdir, readFile } from "node:fs/promises";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
const targets = ["fixtures/sanitized", "docs/probes", "docs/adr"];
const forbidden = [
  /access[_-]?token/i,
  /authorization\s*:/i,
  /cookie\s*:/i,
  /raw[_-]?payload/i,
  /raw[_-]?message/i,
  /message[_-]?text/i,
  /chat[_-]?context/i,
  /password\s*:/i,
  /Bearer\s+[A-Za-z0-9._-]+/i,
  /[A-Za-z]:\\Users\\/i,
];

async function walk(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) files.push(...(await walk(path)));
    else files.push(path);
  }
  return files;
}

const violations = [];
for (const target of targets) {
  const directory = join(root, target);
  for (const file of await walk(directory)) {
    const content = await readFile(file, "utf8");
    for (const [index, line] of content.split(/\r?\n/).entries()) {
      if (forbidden.some((pattern) => pattern.test(line))) {
        violations.push(`${relative(root, file)}:${index + 1}`);
      }
    }
  }
}

if (violations.length > 0) {
  console.error("Privacy scan failed:", violations.join(", "));
  process.exitCode = 1;
} else {
  console.log(`Privacy scan passed (${targets.join(", ")})`);
}
