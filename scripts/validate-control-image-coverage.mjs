#!/usr/bin/env node

import { access, readFile, readdir } from "node:fs/promises";
import { basename, join, relative, sep } from "node:path";
import { pathToFileURL } from "node:url";

// Pages that document an abstract authoring role rather than a concrete, instantiable control.
// Nothing in the Showcase gallery ever renders one on its own, so they carry no image and never
// will. This list is reviewed by hand: a page only leaves it once a concrete control replaces or
// wraps it with its own dedicated Gallery pane.
export const excludedAbstractDocSlugs = new Set([
  "animated-indicator-base",
  "composite-control",
  "container",
  "content-control",
  "headered-content-control",
  "input-base",
  "items-control",
  "pressable",
]);

const galleryCatalogEntry =
  /\(\s*"(?<group>[^"]+)"\s*,\s*(?<pane>[A-Za-z0-9_]+)\.Title\s*,/gu;
const galleryGroupsDeclaration =
  /private\s+static\s+readonly\s+string\[\]\s+_groups\s*=\s*\[(?<body>[\s\S]*?)\];/u;

/**
 * Extracts the stable sidebar-group order from the Gallery declaration.
 *
 * @param {string} gallerySource The contents of `examples/Showcase/Gallery.cs`.
 * @returns {string[]} Group names in sidebar order.
 * @throws {Error} The source contains no recognizable group declaration.
 */
export function galleryGroupNames(gallerySource) {
  const match = galleryGroupsDeclaration.exec(gallerySource);
  const body = match?.groups?.body;

  if (body === undefined) {
    throw new Error("Gallery.cs does not contain a recognizable group declaration.");
  }

  return [...body.matchAll(/"(?<group>[^"]+)"/gu)].map(
    (groupMatch) => groupMatch.groups.group,
  );
}

/**
 * Extracts the stable group, page, and pane identity from every Gallery catalog entry.
 *
 * @param {string} gallerySource The contents of `examples/Showcase/Gallery.cs`.
 * @returns {{group: string, page: string, pane: string}[]} Catalog entries in source order.
 * @throws {Error} The source contains no catalog entries.
 */
export function galleryCatalogEntries(gallerySource) {
  const entries = [];

  for (const match of gallerySource.matchAll(galleryCatalogEntry)) {
    const group = match.groups?.group;
    const pane = match.groups?.pane;

    if (group === undefined || pane === undefined) {
      continue;
    }

    const page = pane.endsWith("Pane") ? pane.slice(0, -"Pane".length) : pane;
    entries.push({ group, page, pane });
  }

  if (entries.length === 0) {
    throw new Error("Gallery.cs does not contain a recognizable catalog entry.");
  }

  return entries;
}

/**
 * Converts a PascalCase control/pane name to the kebab-case slug its documentation file and
 * `control-image-manifest.mjs` entry use, matching the convention every existing control follows
 * (`HorizontalBarChart` -> `horizontal-bar-chart`, `MenuItem` -> `menu-item`).
 *
 * @param {string} pascal The PascalCase name.
 * @returns {string} The kebab-case slug.
 */
export function toKebabCase(pascal) {
  return pascal
    .replaceAll(/([a-z0-9])([A-Z])/gu, "$1-$2")
    .replaceAll(/([A-Z]+)([A-Z][a-z])/gu, "$1-$2")
    .toLowerCase();
}

/**
 * Extracts every Gallery catalog page from `Gallery.cs` source and derives the documentation slug
 * a page's own pane class name implies. A pane's class name is always `<Name>Pane`, and `<Name>` is
 * always the exact string documentation files, the manifest, and the pane's own `Title` constant
 * share - the same convention the whole existing catalog already follows.
 *
 * @param {string} gallerySource The contents of `examples/Showcase/Gallery.cs`.
 * @returns {Map<string, string>} Derived doc slug to the owning pane class name.
 * @throws {Error} The source contains no catalog entries.
 */
export function galleryPaneSlugs(gallerySource) {
  const slugs = new Map();

  for (const entry of galleryCatalogEntries(gallerySource)) {
    slugs.set(toKebabCase(entry.page), entry.pane);
  }

  return slugs;
}

async function findMarkdownFiles(root) {
  const entries = await readdir(root, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const entryPath = join(root, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await findMarkdownFiles(entryPath)));
    } else if (entry.isFile() && entry.name.endsWith(".md")) {
      files.push(entryPath);
    }
  }

  return files;
}

async function fileExists(path) {
  try {
    await access(path);
    return true;
  } catch {
    return false;
  }
}

/**
 * Finds every concrete control and dialog documentation page that is missing its full-path
 * manifest entry, live primary Gallery page, rendered PNG asset, or Markdown reference to that
 * asset. Ordinary link validation only follows references that already exist, so a page missing
 * both the asset and the reference is otherwise invisible. Helper controls may deliberately map
 * to a named example on their primary owner page instead of receiving duplicate catalog entries.
 *
 * @param {string} root The repository root.
 * @returns {Promise<string[]>} Validation messages; an empty array means every dedicated Gallery
 * page has a valid primary-page manifest entry, an asset, and a Markdown reference.
 */
export async function validateControlImageCoverage(root) {
  const gallerySource = await readFile(
    join(root, "examples", "Showcase", "Gallery.cs"),
    "utf8",
  );
  const catalogEntries = galleryCatalogEntries(gallerySource);
  const catalogPages = new Set(catalogEntries.map(({ page }) => page));

  const manifestModule = await import(
    pathToFileURL(join(root, "scripts", "control-image-manifest.mjs")).href
  );
  const manifestByDoc = new Map(
    manifestModule.controls.map((entry) => [entry.doc, entry]),
  );

  const docsRoot = join(root, "docs");
  const controlDocs = (await findMarkdownFiles(join(docsRoot, "controls"))).filter(
    (path) => basename(path) !== "index.md",
  );
  const dialogDocs = (await findMarkdownFiles(join(docsRoot, "dialogs"))).filter(
    (path) => basename(path) !== "index.md",
  );
  const docFiles = [...controlDocs, ...dialogDocs];

  const errors = [];

  for (const docPath of docFiles) {
    const slug = basename(docPath, ".md");

    if (
      docPath.startsWith(join(docsRoot, "controls")) &&
      excludedAbstractDocSlugs.has(slug)
    ) {
      continue;
    }

    const relativeDoc = relative(root, docPath).split(sep).join("/");
    const manifestDoc = relative(docsRoot, docPath)
      .split(sep)
      .join("/")
      .slice(0, -".md".length);
    const manifestEntry = manifestByDoc.get(manifestDoc);
    const assetPath = join(root, "docs", "images", "controls", `${slug}.png`);
    const hasAsset = await fileExists(assetPath);
    const docContent = await readFile(docPath, "utf8");
    const hasReference = docContent.includes(`images/controls/${slug}.png`);

    if (manifestEntry === undefined) {
      errors.push(
        `${relativeDoc}: scripts/control-image-manifest.mjs has no "${manifestDoc}" entry`,
      );
    } else if (!catalogPages.has(manifestEntry.page)) {
      errors.push(
        `${relativeDoc}: manifest page "${manifestEntry.page}" is not a Gallery catalog page`,
      );
    }

    if (!hasAsset) {
      errors.push(`${relativeDoc}: docs/images/controls/${slug}.png does not exist`);
    }

    if (!hasReference) {
      errors.push(
        `${relativeDoc}: does not reference images/controls/${slug}.png in an Example image`,
      );
    }
  }

  return errors;
}

async function main() {
  const root = join(import.meta.dirname, "..");
  const errors = await validateControlImageCoverage(root);

  if (errors.length === 0) {
    console.log(
      "Every concrete control and dialog doc has a full-path manifest entry, asset, " +
        "Markdown reference, and live Gallery page.",
    );
    return;
  }

  for (const error of errors) {
    console.error(error);
  }

  process.exitCode = 1;
}

const invokedPath =
  process.argv[1] === undefined ? undefined : pathToFileURL(process.argv[1]).href;

if (invokedPath === import.meta.url) {
  await main();
}
