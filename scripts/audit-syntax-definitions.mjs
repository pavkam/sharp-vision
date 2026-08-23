import { createHash } from "node:crypto";
import { readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

export const upstreamSource = Object.freeze({
  repository: "https://github.com/KDE/syntax-highlighting",
  commit: "60cfa684b64cccde19bf12c74db52129709ed863",
});

// Only unambiguous permissive licenses are curated into the embedded catalog. A syntax
// definition with no stated license, an empty license attribute, an ambiguous bare "BSD"
// (clause count unknown), or a copyleft license (GPL/LGPL/Artistic/FDL/WTFPL/Apache/...) is
// excluded from `SharpVision.SyntaxHighlighting` entirely; the loader still accepts such a
// file at runtime from an external path, matching upstream Kate's own filesystem pick-up model,
// so an application may add it on its own without this package redistributing it.
const permissiveLicenseByAttribute = Object.freeze({
  MIT: "MIT",
  "BSD-3-Clause": "BSD-3-Clause",
  BSD3: "BSD-3-Clause",
  "New BSD License": "BSD-3-Clause",
  "CC0 Public Domain Dedication, version 1.0, as published by Creative Commons": "CC0-1.0",
  "Public Domain": "Public-Domain",
  "zlib/libpng": "Zlib",
});

const permissiveLicenseBySpdxId = Object.freeze({
  MIT: "MIT",
  "BSD-3-Clause": "BSD-3-Clause",
});

const compareOrdinal = (left, right) => (left < right ? -1 : left > right ? 1 : 0);

const languageAttribute = (text, name) => {
  const tagMatch = text.match(/<language\b[^>]*>/su);

  if (!tagMatch) {
    return null;
  }

  const attributeMatch = tagMatch[0].match(new RegExp(`\\b${name}="([^"]*)"`, "u"));
  return attributeMatch ? attributeMatch[1] : null;
};

const spdxLicenseId = (text) => {
  const header = text.slice(0, 2000);
  const match = header.match(/SPDX-License-Identifier:\s*([A-Za-z0-9.+-]+)/u);
  return match ? match[1].replace(/[^A-Za-z0-9.+-].*$/u, "") : null;
};

/**
 * Classifies one syntax-definition file's redistribution license.
 *
 * @param {string} text the complete XML source text.
 * @returns {string | null} the curated SPDX-ish identifier, or null when the file's stated
 *   license is missing, empty, ambiguous, or copyleft and must not be redistributed.
 */
export const classifyLicense = (text) => {
  const spdxId = spdxLicenseId(text);

  if (spdxId && spdxId in permissiveLicenseBySpdxId) {
    return permissiveLicenseBySpdxId[spdxId];
  }

  const attribute = languageAttribute(text, "license");
  return attribute && attribute in permissiveLicenseByAttribute
    ? permissiveLicenseByAttribute[attribute]
    : null;
};

const syntaxFiles = async (root) =>
  (await readdir(root, { withFileTypes: true }))
    .filter((entry) => entry.isFile() && /\.xml$/iu.test(entry.name))
    .map((entry) => entry.name)
    .sort(compareOrdinal);

/**
 * Builds the schema-1 manifest for the syntax definitions already staged under `root`, which
 * must contain only files this module's own {@link classifyLicense} accepts.
 *
 * @param {string} root the curated resource directory to scan.
 * @returns {Promise<object>} the manifest object, ready to serialize as `syntax.manifest.json`.
 */
export const createManifest = async (root) => {
  const files = await syntaxFiles(root);
  const definitions = [];

  for (const file of files) {
    const bytes = await readFile(path.join(root, file));
    const text = bytes.toString("utf8");
    const license = classifyLicense(text);

    if (!license) {
      throw new Error(`'${file}' does not carry a curated permissive license.`);
    }

    const name = languageAttribute(text, "name");

    if (!name) {
      throw new Error(`'${file}' is missing a required 'name' attribute.`);
    }

    definitions.push({
      name,
      file,
      resource: `SharpVision.SyntaxHighlighting.Resources.Syntax.${file}`,
      section: languageAttribute(text, "section") ?? "",
      extensions: languageAttribute(text, "extensions") ?? "",
      mimetype: languageAttribute(text, "mimetype") ?? "",
      alternativeNames: languageAttribute(text, "alternativeNames") ?? "",
      author: languageAttribute(text, "author") ?? "",
      license,
      sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length,
      sourceRepository: upstreamSource.repository,
      sourceCommit: upstreamSource.commit,
    });
  }

  definitions.sort((left, right) => compareOrdinal(left.name, right.name));

  return { schema: 1, count: definitions.length, definitions };
};

/**
 * Re-derives the manifest from `root` and asserts it matches `manifest` exactly.
 *
 * @param {string} root the curated resource directory to scan.
 * @param {object} manifest the previously generated manifest to compare against.
 * @returns {Promise<object>} the freshly computed manifest, when it matches.
 */
export const validateManifest = async (root, manifest) => {
  const actual = await createManifest(root);

  if (JSON.stringify(actual) !== JSON.stringify(manifest)) {
    throw new Error("The syntax-definition manifest does not match the embedded resources.");
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
    throw new Error("Usage: --source <syntax-folder> --output <manifest> [--check]");
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
