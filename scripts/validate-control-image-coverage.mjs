#!/usr/bin/env node

import { access, readFile, readdir } from "node:fs/promises";
import { basename, join, relative, sep } from "node:path";
import { pathToFileURL } from "node:url";

// Pages that document an abstract authoring role rather than a concrete, instantiable control.
// Nothing in the Showcase gallery ever renders one on its own, so they carry no image and never
// will. This list is reviewed by hand: a page only leaves it once a concrete control replaces or
// wraps it with its own dedicated Gallery pane.
export const excludedAbstractDocSlugs = new Set([
  "composite-control",
  "container",
  "content-control",
  "headered-content-control",
  "items-control",
  "pressable",
]);

const galleryCatalogEntry = /\(\s*"[^"]+"\s*,\s*(?<pane>[A-Za-z0-9_]+)\.Title\s*,/gu;

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

  for (const match of gallerySource.matchAll(galleryCatalogEntry)) {
    const paneClass = match.groups?.pane;

    if (paneClass === undefined) {
      continue;
    }

    const name = paneClass.endsWith("Pane") ? paneClass.slice(0, -"Pane".length) : paneClass;

    slugs.set(toKebabCase(name), paneClass);
  }

  if (slugs.size === 0) {
    throw new Error("Gallery.cs does not contain a recognizable catalog entry.");
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
 * Finds every concrete control documentation page whose control has a dedicated Gallery capture
 * target but is missing its manifest entry, its rendered PNG asset, or the Markdown reference to
 * that asset. Ordinary link validation only follows references that already exist, so a page
 * missing both the asset and the reference - the exact gap a newly documented control can fall
 * into silently - is invisible to it; this walks the Gallery catalog outward instead, so a
 * concrete control can never finish undocumented by simply never being mentioned.
 *
 * @param {string} root The repository root.
 * @returns {Promise<string[]>} Validation messages; an empty array means every dedicated Gallery
 * page has a manifest entry, an asset, and a Markdown reference.
 */
export async function validateControlImageCoverage(root) {
  const gallerySource = await readFile(
    join(root, "examples", "Showcase", "Gallery.cs"),
    "utf8",
  );
  const paneSlugs = galleryPaneSlugs(gallerySource);

  const manifestModule = await import(
    pathToFileURL(join(root, "scripts", "control-image-manifest.mjs")).href
  );
  const manifestByDoc = new Map(
    manifestModule.controls.map((entry) => [basename(entry.doc), entry]),
  );

  const controlsRoot = join(root, "docs", "controls");
  const docFiles = (await findMarkdownFiles(controlsRoot)).filter(
    (path) => basename(path) !== "index.md",
  );

  const errors = [];

  for (const docPath of docFiles) {
    const slug = basename(docPath, ".md");

    if (excludedAbstractDocSlugs.has(slug)) {
      continue;
    }

    if (!paneSlugs.has(slug)) {
      continue;
    }

    const relativeDoc = relative(root, docPath).split(sep).join("/");
    const manifestEntry = manifestByDoc.get(slug);
    const assetPath = join(root, "docs", "images", "controls", `${slug}.png`);
    const hasAsset = await fileExists(assetPath);
    const docContent = await readFile(docPath, "utf8");
    const hasReference = docContent.includes(`images/controls/${slug}.png`);

    if (manifestEntry === undefined) {
      errors.push(
        `${relativeDoc}: ${paneSlugs.get(slug)} has a dedicated Gallery page but ` +
          `scripts/control-image-manifest.mjs has no "${slug}" entry`,
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
      "Every concrete control doc page with a dedicated Gallery page has a manifest entry, " +
        "asset, and Markdown reference.",
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
