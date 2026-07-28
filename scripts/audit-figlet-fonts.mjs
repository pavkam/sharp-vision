import { createHash } from "node:crypto";
import { readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const fontPattern = /\.(?:flf|tlf)$/iu;

const classify = (notice) => {
  if (/public domain/iu.test(notice)) return "public-domain";
  if (/permission is hereby granted|redistribution and use|freely distribut/iu.test(notice))
    return "permissive";
  if (/gnu general public license|\bgpl\b/iu.test(notice)) return "copyleft";
  if (/freeware/iu.test(notice)) return "freeware";
  if (/personal use|non-commercial|do not distribute|permission.*required/iu.test(notice))
    return "restricted";
  if (/copyright|\bby\b|author|created|designed/iu.test(notice))
    return "attribution-only";
  return "upstream-unverified";
};

const extractNotice = (bytes) => {
  if (bytes.length >= 4 && bytes.subarray(0, 4).equals(Buffer.from("PK\x03\x04", "binary"))) {
    return "Nested ZIP-compressed TOIlet font; no outer text notice.";
  }

  const lines = bytes.toString("utf8").replaceAll("\r\n", "\n").split("\n");
  const header = lines[0]?.trim().split(/\s+/u) ?? [];
  const count = Number.parseInt(header[5] ?? "0", 10);
  return Number.isSafeInteger(count) && count > 0
    ? lines.slice(1, 1 + count).join("\n").trim()
    : "";
};

const fontFiles = async (root) =>
  (await readdir(root, { withFileTypes: true }))
    .filter((entry) => entry.isFile() && fontPattern.test(entry.name))
    .map((entry) => entry.name)
    .sort((left, right) => left.localeCompare(right, "en", { sensitivity: "variant" }));

export const createManifest = async (root, source) => {
  if (!source?.repository || !source?.commit) {
    throw new TypeError("Source repository and commit are required.");
  }

  const fonts = [];
  const files = await fontFiles(root);
  const counts = new Map();

  for (const file of files) {
    const base = path.parse(file).name;
    counts.set(base, (counts.get(base) ?? 0) + 1);
  }

  for (const file of files) {
    const bytes = await readFile(path.join(root, file));
    const notice = extractNotice(bytes);
    const parsed = path.parse(file);
    fonts.push({
      name:
        counts.get(parsed.name) === 1
          ? parsed.name
          : `${parsed.name}.${parsed.ext.slice(1).toLowerCase()}`,
      file,
      format: path.extname(file).slice(1).toLowerCase(),
      sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length,
      notice,
      license: classify(notice),
    });
  }

  return {
    schema: 1,
    source: {
      repository: source.repository,
      commit: source.commit,
    },
    count: fonts.length,
    fonts,
  };
};

export const validateManifest = async (root, manifest) => {
  if (!manifest?.source?.repository || !manifest?.source?.commit) {
    throw new Error("Manifest source provenance is missing.");
  }

  const actual = await createManifest(root, manifest.source);

  if (actual.count !== manifest.count || actual.fonts.length !== manifest.fonts?.length) {
    throw new Error("Manifest font count does not match the source collection.");
  }

  const names = new Set();

  for (let index = 0; index < actual.fonts.length; index += 1) {
    const expected = manifest.fonts[index];
    const current = actual.fonts[index];

    if (!expected?.license) throw new Error(`License classification missing for ${current.file}.`);
    if (expected.file !== current.file) throw new Error(`Manifest entry missing for ${current.file}.`);
    if (expected.sha256 !== current.sha256) throw new Error(`Font hash drifted for ${current.file}.`);
    if (names.has(expected.name)) throw new Error(`Duplicate catalog name ${expected.name}.`);
    names.add(expected.name);
  }

  return actual;
};

const argument = (name) => {
  const index = process.argv.indexOf(name);
  return index < 0 ? undefined : process.argv[index + 1];
};

const main = async () => {
  const root = argument("--source");
  const output = argument("--output");
  const repository = argument("--repository") ?? "https://github.com/xero/figlet-fonts";
  const commit = argument("--commit");

  if (!root || !commit) {
    throw new Error("Usage: --source <folder> --commit <sha> [--repository <url>] [--output <json>]");
  }

  if (process.argv.includes("--check")) {
    if (!output) throw new Error("--check requires --output.");
    await validateManifest(root, JSON.parse(await readFile(output, "utf8")));
    return;
  }

  const manifest = await createManifest(root, { repository, commit });
  const json = `${JSON.stringify(manifest, null, 2)}\n`;

  if (output) await writeFile(output, json);
  else process.stdout.write(json);
};

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  await main();
}
