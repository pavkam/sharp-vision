import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  excludedAbstractDocSlugs,
  galleryPaneSlugs,
  toKebabCase,
  validateControlImageCoverage,
} from "./validate-control-image-coverage.mjs";

test("toKebabCase_WhenNameIsCompoundPascalCase_InsertsHyphensAndLowercases", () => {
  assert.equal(toKebabCase("HorizontalBarChart"), "horizontal-bar-chart");
  assert.equal(toKebabCase("MenuItem"), "menu-item");
  assert.equal(toKebabCase("Control"), "control");
  assert.equal(toKebabCase("JsonView"), "json-view");
});

test("galleryPaneSlugs_WhenSourceListsCatalogEntries_DerivesOneSlugPerPane", () => {
  const source = `
    private static readonly (string Group, string Name, Func<CompositeControlBase> Create)[] _catalog =
    [
        ("Charts", HorizontalBarChartPane.Title, static () => new HorizontalBarChartPane()),
        ("Navigation", MenuItemPane.Title, static () => new MenuItemPane()),
    ];`;

  const slugs = galleryPaneSlugs(source);

  assert.deepEqual(
    [...slugs.entries()].sort(),
    [
      ["horizontal-bar-chart", "HorizontalBarChartPane"],
      ["menu-item", "MenuItemPane"],
    ],
  );
});

test("galleryPaneSlugs_WhenSourceHasNoCatalogEntries_Throws", () => {
  assert.throws(
    () => galleryPaneSlugs("// no catalog here"),
    /does not contain a recognizable catalog entry/,
  );
});

/**
 * Builds an isolated fixture tree with a Gallery catalog, a manifest, and one documentation page,
 * so the fixture below can prove both the green and the red path without touching the real repo.
 *
 * @param {object} options Fixture shape controls.
 * @param {boolean} options.includeManifestEntry Whether the manifest lists the chart doc.
 * @param {boolean} options.includeAsset Whether the rendered PNG exists on disk.
 * @param {boolean} options.includeReference Whether the doc page references the PNG.
 * @returns {Promise<string>} The fixture root.
 */
async function buildFixture({ includeManifestEntry, includeAsset, includeReference }) {
  const root = await mkdtemp(join(tmpdir(), "control-image-coverage-"));

  await mkdir(join(root, "examples", "Showcase"), { recursive: true });
  await writeFile(
    join(root, "examples", "Showcase", "Gallery.cs"),
    `
    private static readonly (string Group, string Name, Func<CompositeControlBase> Create)[] _catalog =
    [
        ("Charts", HorizontalBarChartPane.Title, static () => new HorizontalBarChartPane()),
    ];`,
  );

  await mkdir(join(root, "scripts"), { recursive: true });
  const manifestEntries = includeManifestEntry
    ? `export const controls = [{ doc: "charts/horizontal-bar-chart", page: "HorizontalBarChart" }];`
    : "export const controls = [];";
  await writeFile(join(root, "scripts", "control-image-manifest.mjs"), manifestEntries);

  await mkdir(join(root, "docs", "controls", "charts"), { recursive: true });
  const reference = includeReference
    ? "![The HorizontalBarChart control rendered in the live showcase](../../images/controls/horizontal-bar-chart.png)\n"
    : "";
  await writeFile(
    join(root, "docs", "controls", "charts", "horizontal-bar-chart.md"),
    `# HorizontalBarChart\n\n## Example\n\n${reference}`,
  );
  await writeFile(join(root, "docs", "controls", "charts", "index.md"), "# Charts\n");

  await mkdir(join(root, "docs", "images", "controls"), { recursive: true });

  if (includeAsset) {
    await writeFile(join(root, "docs", "images", "controls", "horizontal-bar-chart.png"), "fake-png");
  }

  return root;
}

test("validateControlImageCoverage_WhenManifestAssetAndReferenceAllExist_ReportsNoErrors", async () => {
  const root = await buildFixture({
    includeManifestEntry: true,
    includeAsset: true,
    includeReference: true,
  });

  try {
    assert.deepEqual(await validateControlImageCoverage(root), []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlImageCoverage_WhenManifestAssetAndReferenceAreAllMissing_ReportsAllThreeGaps", async () => {
  const root = await buildFixture({
    includeManifestEntry: false,
    includeAsset: false,
    includeReference: false,
  });

  try {
    const errors = await validateControlImageCoverage(root);

    assert.equal(errors.length, 3);
    assert.match(errors[0], /has no "horizontal-bar-chart" entry/);
    assert.match(errors[1], /horizontal-bar-chart\.png does not exist/);
    assert.match(errors[2], /does not reference images\/controls\/horizontal-bar-chart\.png/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlImageCoverage_WhenOnlyTheManifestEntryIsMissing_ReportsOnlyThatGap", async () => {
  const root = await buildFixture({
    includeManifestEntry: false,
    includeAsset: true,
    includeReference: true,
  });

  try {
    const errors = await validateControlImageCoverage(root);

    assert.deepEqual(errors, [
      "docs/controls/charts/horizontal-bar-chart.md: HorizontalBarChartPane has a dedicated " +
        'Gallery page but scripts/control-image-manifest.mjs has no "horizontal-bar-chart" entry',
    ]);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlImageCoverage_WhenAnExcludedAbstractPageHasAMatchingCatalogEntry_IsStillSkipped", async () => {
  const root = await mkdtemp(join(tmpdir(), "control-image-coverage-"));

  try {
    await mkdir(join(root, "examples", "Showcase"), { recursive: true });
    await writeFile(
      join(root, "examples", "Showcase", "Gallery.cs"),
      `
      private static readonly (string Group, string Name, Func<CompositeControlBase> Create)[] _catalog =
      [
          ("Concepts", CompositeControlPane.Title, static () => new CompositeControlPane()),
      ];`,
    );

    await mkdir(join(root, "scripts"), { recursive: true });
    await writeFile(
      join(root, "scripts", "control-image-manifest.mjs"),
      "export const controls = [];",
    );

    await mkdir(join(root, "docs", "controls"), { recursive: true });
    await writeFile(
      join(root, "docs", "controls", "composite-control.md"),
      "# CompositeControl\n\n## Example\n",
    );

    await mkdir(join(root, "docs", "images", "controls"), { recursive: true });

    assert.deepEqual(await validateControlImageCoverage(root), []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("excludedAbstractDocSlugs_MatchesTheReviewedAuthoringRolePages", () => {
  assert.deepEqual(
    [...excludedAbstractDocSlugs].sort(),
    [
      "composite-control",
      "container",
      "content-control",
      "headered-content-control",
      "items-control",
      "pressable",
    ],
  );
});

test("repository_WhenScanned_HasNoControlImageCoverageGaps", async () => {
  const root = join(import.meta.dirname, "..");

  assert.deepEqual(await validateControlImageCoverage(root), []);
});
