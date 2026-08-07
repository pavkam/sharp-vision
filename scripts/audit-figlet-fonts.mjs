import { createHash } from "node:crypto";
import { readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

export const officialSource = Object.freeze({
  repository: "https://github.com/cmatsuoka/figlet",
  commit: "202a0a8110650a943f1125f536b3bb455cf72ee1",
  license: "BSD-3-Clause",
});

export const classySource = Object.freeze({
  repository: "https://github.com/patorjk/figlet.js",
  commit: "b95c2f03ccbc7e2a23e9fd030e8378c2d3b9dd0e",
  license: "MIT",
});

export const officialFontFiles = Object.freeze([
  "banner.flf",
  "big.flf",
  "block.flf",
  "bubble.flf",
  "digital.flf",
  "ivrit.flf",
  "lean.flf",
  "mini.flf",
  "mnemonic.flf",
  "script.flf",
  "shadow.flf",
  "slant.flf",
  "small.flf",
  "smscript.flf",
  "smshadow.flf",
  "smslant.flf",
  "standard.flf",
  "term.flf",
]);

const curatedFonts = Object.freeze([
  { file: "Classy.flf", source: classySource },
  ...officialFontFiles.map((file) => ({ file, source: officialSource })),
]);

const extractNotice = (bytes) => {
  const lines = bytes.toString("utf8").replaceAll("\r\n", "\n").replaceAll("\r", "\n").split("\n");
  const header = lines[0]?.trim().split(/\s+/u) ?? [];
  const count = Number.parseInt(header[5] ?? "0", 10);

  return Number.isSafeInteger(count) && count > 0
    ? lines.slice(1, 1 + count).join("\n").trim()
    : "";
};

const compareOrdinal = (left, right) => (left < right ? -1 : left > right ? 1 : 0);

const fontFiles = async (root) =>
  (await readdir(root, { withFileTypes: true }))
    .filter((entry) => entry.isFile() && /\.flf$/iu.test(entry.name))
    .map((entry) => entry.name)
    .sort(compareOrdinal);

export const createManifest = async (root) => {
  const actualFiles = await fontFiles(root);
  const expectedFiles = curatedFonts.map(({ file }) => file).sort(compareOrdinal);

  if (JSON.stringify(actualFiles) !== JSON.stringify(expectedFiles)) {
    throw new Error("The embedded FIGlet resources do not match the BSD/MIT allowlist.");
  }

  const fonts = [];

  for (const { file, source } of curatedFonts) {
    const bytes = await readFile(path.join(root, file));
    const parsed = path.parse(file);
    fonts.push({
      name: parsed.name,
      file,
      resource: `SharpVision.FigletFonts.Resources.Fonts.${file}`,
      format: parsed.ext.slice(1).toLowerCase(),
      sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length,
      notice: extractNotice(bytes),
      license: source.license,
      sourceRepository: source.repository,
      sourceCommit: source.commit,
    });
  }

  fonts.sort((left, right) => compareOrdinal(left.name, right.name));

  return { schema: 2, count: fonts.length, fonts };
};

export const validateManifest = async (root, manifest) => {
  const actual = await createManifest(root);

  if (JSON.stringify(actual) !== JSON.stringify(manifest)) {
    throw new Error("The FIGlet manifest does not match the embedded resources and pinned provenance.");
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

  if (!root || !output) {
    throw new Error("Usage: --source <font-folder> --output <manifest> [--check]");
  }

  if (process.argv.includes("--check")) {
    await validateManifest(root, JSON.parse(await readFile(output, "utf8")));
    return;
  }

  const manifest = await createManifest(root);
  await writeFile(output, `${JSON.stringify(manifest, null, 2)}\n`);
};

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  await main();
}
