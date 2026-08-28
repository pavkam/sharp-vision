import { createHash } from "node:crypto";
import { readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

export const upstreamSource = Object.freeze({
  repository: "https://github.com/KDE/syntax-highlighting",
  commit: "60cfa684b64cccde19bf12c74db52129709ed863",
});

// A definition in this set is original SharpVision work, not sourced from the pinned upstream
// checkout above - usually because upstream's own file carries no permissive (or no) license and
// cannot be redistributed (see extern/kde-syntax-highlighting/README.md for the audited reason a
// given upstream file was excluded). Its manifest entry records this project's own repository as
// its provenance and an empty sourceCommit, since there is no external commit to pin - the
// opposite of every other entry, which always has both a real sourceRepository and a real
// 40-character sourceCommit. This set is reviewed by hand and never derived, so a file only
// enters it through a deliberate decision recorded in that same file's own license header.
export const firstPartySource = Object.freeze({
  repository: "https://github.com/pavkam/sharp-vision",
});

export const firstPartyDefinitions = Object.freeze(new Set(["csharp.xml"]));

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
  "CC0-1.0": "CC0-1.0",
  Zlib: "Zlib",
});

const compareOrdinal = (left, right) => (left < right ? -1 : left > right ? 1 : 0);

const languageAttribute = (text, name) => {
  // A commented example is not the document element and must never supply audit metadata.
  const xmlWithoutComments = text.replace(/<!--[\s\S]*?-->/gu, "");
  const tagMatch = xmlWithoutComments.match(/<language\b[^>]*>/su);

  if (!tagMatch) {
    return null;
  }

  const attributeMatch = tagMatch[0].match(new RegExp(`\\b${name}="([^"]*)"`, "u"));
  return attributeMatch ? attributeMatch[1] : null;
};

const languageBooleanAttribute = (text, name) => {
  const value = languageAttribute(text, name)?.toLowerCase();
  return value === "true" || value === "1";
};

const spdxLicenseExpression = (text) => {
  // Preserve comment offsets while masking their fake elements, then search only the prolog
  // before the real document element. Syntax definitions may legitimately highlight the literal
  // text "SPDX-License-Identifier:" inside their body; that is content, not provenance.
  const elementView = text.replace(/<!--[\s\S]*?-->/gu, (comment) => " ".repeat(comment.length));
  const documentElementIndex = elementView.search(/<language\b/u);
  const header = text.slice(
    0,
    Math.min(documentElementIndex < 0 ? text.length : documentElementIndex, 2000),
  );
  const match = header.match(/SPDX-License-Identifier:\s*([^\r\n]+)/u);
  return match ? match[1].replace(/\s*(?:-->|\*\/).*$/u, "").trim() : null;
};

/**
 * Classifies one syntax-definition file's redistribution license.
 *
 * @param {string} text the complete XML source text.
 * @returns {string | null} the curated SPDX-ish identifier, or null when the file's stated
 *   license is missing, empty, ambiguous, or copyleft and must not be redistributed.
 */
export const classifyLicense = (text) => {
  const attribute = languageAttribute(text, "license");
  const attributeLicense =
    attribute && attribute in permissiveLicenseByAttribute
    ? permissiveLicenseByAttribute[attribute]
    : null;
  const spdxExpression = spdxLicenseExpression(text);

  if (spdxExpression === null) {
    return attributeLicense;
  }

  const spdxLicense = permissiveLicenseBySpdxId[spdxExpression] ?? null;

  if (!spdxLicense || (attribute !== null && attributeLicense !== spdxLicense)) {
    return null;
  }

  return spdxLicense;
};

const syntaxFiles = async (root) =>
  (await readdir(root, { withFileTypes: true }))
    .filter((entry) => entry.isFile() && /\.xml$/iu.test(entry.name))
    .map((entry) => entry.name)
    .sort(compareOrdinal);

/**
 * Builds the schema-2 manifest for the syntax definitions already staged under `root`, which
 * must contain only files this module's own {@link classifyLicense} accepts.
 *
 * @param {string} root the curated resource directory to scan.
 * @returns {Promise<object>} the manifest object, ready to serialize as `syntax.manifest.json`.
 */
export const createManifest = async (root) => {
  const files = await syntaxFiles(root);
  const definitions = [];
  const filesByLanguageName = new Map();

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

    if (filesByLanguageName.has(name)) {
      throw new Error(
        `More than one file declares the language name '${name}': ` +
          `'${filesByLanguageName.get(name)}' and '${file}'.`,
      );
    }

    filesByLanguageName.set(name, file);

    const isFirstParty = firstPartyDefinitions.has(file);

    definitions.push({
      name,
      file,
      resource: `SharpVision.SyntaxHighlighting.Resources.Syntax.${file}`,
      section: languageAttribute(text, "section") ?? "",
      extensions: languageAttribute(text, "extensions") ?? "",
      mimetype: languageAttribute(text, "mimetype") ?? "",
      alternativeNames: languageAttribute(text, "alternativeNames") ?? "",
      author: languageAttribute(text, "author") ?? "",
      priority: Number.parseInt(languageAttribute(text, "priority") ?? "0", 10),
      style: languageAttribute(text, "style") ?? "",
      hidden: languageBooleanAttribute(text, "hidden"),
      license,
      sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length,
      sourceRepository: isFirstParty ? firstPartySource.repository : upstreamSource.repository,
      sourceCommit: isFirstParty ? "" : upstreamSource.commit,
    });
  }

  definitions.sort((left, right) => compareOrdinal(left.name, right.name));

  return { schema: 2, count: definitions.length, definitions };
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
