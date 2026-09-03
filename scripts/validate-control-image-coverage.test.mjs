import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  excludedAbstractDocSlugs,
  galleryCatalogEntries,
  galleryGroupNames,
  galleryPaneSlugs,
  toKebabCase,
  validateControlImageCoverage,
} from "./validate-control-image-coverage.mjs";

function plainCaption(caption) {
  return caption.replaceAll("&&", "\0").replaceAll("&", "").replaceAll("\0", "&");
}

function accessKey(caption) {
  return /(?<!&)&(?!&)(?<key>.)/u
    .exec(caption)
    ?.groups?.key.toLocaleLowerCase("en-US") ?? null;
}

function enabledPaneAccessKeys(source, unavailableCaptions) {
  const captions = [
    ...source.matchAll(/\bCreateItem\(\s*"(?<caption>[^"\r\n]*)"/gu),
    ...source.matchAll(/\bText\s*=\s*"(?<caption>[^"\r\n]*)"/gu),
    ...source.matchAll(/\?\s*"[^"\r\n]*"\s*:\s*"(?<caption>[^"\r\n]*)"/gu),
  ]
    .map((match) => match.groups?.caption)
    .filter((caption) => caption !== undefined)
    .filter((caption) => !unavailableCaptions.has(plainCaption(caption)));

  return captions
    .map((caption) => ({ caption: plainCaption(caption), key: accessKey(caption) }))
    .filter(({ key }) => key !== null);
}

function duplicateAccessKeys(entries) {
  const keys = entries.map(({ key }) => key);

  return [...new Set(keys.filter((key, index) => keys.indexOf(key) !== index))].sort();
}

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

test("galleryCatalogEntries_WhenSourceListsCatalogEntries_ReturnsGroupsPagesAndPanes", () => {
  const source = `
    private static readonly string[] _groups = ["Charts", "Navigation"];
    private static readonly (string Group, string Name, Func<CompositeControlBase> Create)[] _catalog =
    [
        ("Charts", HorizontalBarChartPane.Title, static () => new HorizontalBarChartPane()),
        ("Navigation", MenuPane.Title, static () => new MenuPane()),
    ];`;

  assert.deepEqual(galleryCatalogEntries(source), [
    {
      group: "Charts",
      page: "HorizontalBarChart",
      pane: "HorizontalBarChartPane",
    },
    { group: "Navigation", page: "Menu", pane: "MenuPane" },
  ]);
  assert.deepEqual(galleryGroupNames(source), ["Charts", "Navigation"]);
});

test("repositoryGallery_WhenScanned_HasThePrimaryPageCatalog", async () => {
  const root = join(import.meta.dirname, "..");
  const source = await readFile(
    join(root, "examples", "Showcase", "Gallery.cs"),
    "utf8",
  );
  const entries = galleryCatalogEntries(source);
  const groups = galleryGroupNames(source);
  const pages = entries.map(({ page }) => page);

  assert.equal(entries.length, 68);
  assert.deepEqual(
    Object.fromEntries(
      groups.map((group) => [
        group,
        entries.filter((entry) => entry.group === group).length,
      ]),
    ),
    {
      Concepts: 5,
      Input: 17,
      Collections: 5,
      Navigation: 4,
      Layout: 9,
      Display: 10,
      Charts: 5,
      Progress: 3,
      Notifications: 2,
      Dialogs: 3,
      Windows: 5,
    },
  );
  assert.deepEqual(groups, [
    "Concepts",
    "Input",
    "Collections",
    "Navigation",
    "Layout",
    "Display",
    "Charts",
    "Progress",
    "Notifications",
    "Dialogs",
    "Windows",
  ]);
  assert.equal(new Set(pages).size, pages.length);
  assert.deepEqual(
    pages.filter((page) =>
      [
        "BreadcrumbItem",
        "CommandBarItem",
        "CommandBarSeparator",
        "MenuItem",
        "FilePicker",
      ].includes(page),
    ),
    [],
  );
  assert.ok(pages.includes("MessageBox"));
  assert.ok(pages.includes("OpenFilePicker"));
  assert.ok(pages.includes("SaveFilePicker"));
});

test("focusedOwnerPanes_WhenEnabledCaptionsDeclareAccessKeys_KeepThemPageWideUnique", async () => {
  const root = join(import.meta.dirname, "..");
  const cases = [
    {
      file: "BreadcrumbPane.cs",
      unavailable: new Set(["Archive", "Hidden cache", "Collapsed branch"]),
    },
    {
      file: "CommandBarPane.cs",
      unavailable: new Set(["Share", "Archive"]),
    },
  ];
  const duplicates = {};
  const inventories = [];

  for (const { file, unavailable } of cases) {
    const source = await readFile(
      join(root, "examples", "Showcase", "Panes", file),
      "utf8",
    );
    const entries = enabledPaneAccessKeys(source, unavailable);

    duplicates[file] = duplicateAccessKeys(entries);
    inventories.push(
      `${file}: ${entries.map(({ caption, key }) => `${key}:${caption}`).join(", ")}`,
    );
  }

  assert.deepEqual(
    duplicates,
    { "BreadcrumbPane.cs": [], "CommandBarPane.cs": [] },
    inventories.join("\n"),
  );
});

test("WrapPane_WhenVerticalLaneShortens_FitsFourColumnsWithBreathingRoom", async () => {
  const root = join(import.meta.dirname, "..");
  const source = await readFile(
    join(root, "examples", "Showcase", "Panes", "WrapPane.cs"),
    "utf8",
  );
  const verticalBlock = source.match(
    /var vertical = new Wrap(?<body>[\s\S]*?)var verticalStatus/u,
  )?.groups?.body;

  assert.notEqual(verticalBlock, undefined);
  const laneWidth = Number(
    /Width = Length\.Cells\((?<value>\d+)\)/u.exec(verticalBlock)?.groups?.value,
  );
  const lineSpacing = Number(
    /LineSpacing = (?<value>\d+)/u.exec(verticalBlock)?.groups?.value,
  );
  const cardWidths = [...verticalBlock.matchAll(/Card\("[^"]+", [^,]+, (?<value>\d+)\)/gu)]
    .map((match) => Number(match.groups?.value));
  const consumedWidth = cardWidths.reduce((sum, width) => sum + width, 0) +
    Math.max(0, cardWidths.length - 1) * lineSpacing;

  assert.equal(cardWidths.length, 4);
  assert.ok(
    consumedWidth <= laneWidth - 2,
    `vertical columns consume ${consumedWidth} cells inside a ${laneWidth}-cell lane`,
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
    ? `export const controls = [{ doc: "controls/charts/horizontal-bar-chart", page: "HorizontalBarChart" }];`
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
  await mkdir(join(root, "docs", "dialogs"), { recursive: true });

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
    assert.match(
      errors[0],
      /has no "controls\/charts\/horizontal-bar-chart" entry/,
    );
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
      "docs/controls/charts/horizontal-bar-chart.md: " +
        'scripts/control-image-manifest.mjs has no "controls/charts/horizontal-bar-chart" entry',
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
    await mkdir(join(root, "docs", "dialogs"), { recursive: true });

    assert.deepEqual(await validateControlImageCoverage(root), []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlImageCoverage_WhenPrimaryPagesOwnHelperAndDialogDocs_ValidatesBothFullPaths", async () => {
  const root = await mkdtemp(join(tmpdir(), "control-image-coverage-"));

  try {
    await mkdir(join(root, "examples", "Showcase"), { recursive: true });
    await writeFile(
      join(root, "examples", "Showcase", "Gallery.cs"),
      `
      private static readonly (string Group, string Name, Func<CompositeControlBase> Create)[] _catalog =
      [
          ("Navigation", BreadcrumbPane.Title, static () => new BreadcrumbPane()),
          ("Dialogs", MessageBoxPane.Title, static () => new MessageBoxPane()),
      ];`,
    );

    await mkdir(join(root, "scripts"), { recursive: true });
    await writeFile(
      join(root, "scripts", "control-image-manifest.mjs"),
      `export const controls = [
        { doc: "controls/navigation/breadcrumb-item", page: "Breadcrumb" },
        { doc: "dialogs/message-box", page: "MessageBox" },
      ];`,
    );

    await mkdir(join(root, "docs", "controls", "navigation"), {
      recursive: true,
    });
    await writeFile(
      join(root, "docs", "controls", "navigation", "breadcrumb-item.md"),
      "# BreadcrumbItem\n\n## Example\n",
    );
    await mkdir(join(root, "docs", "dialogs"), { recursive: true });
    await writeFile(
      join(root, "docs", "dialogs", "message-box.md"),
      "# MessageBox\n\n## Example\n",
    );
    await mkdir(join(root, "docs", "images", "controls"), {
      recursive: true,
    });

    assert.deepEqual(await validateControlImageCoverage(root), [
      "docs/controls/navigation/breadcrumb-item.md: " +
        "docs/images/controls/breadcrumb-item.png does not exist",
      "docs/controls/navigation/breadcrumb-item.md: does not reference " +
        "images/controls/breadcrumb-item.png in an Example image",
      "docs/dialogs/message-box.md: docs/images/controls/message-box.png does not exist",
      "docs/dialogs/message-box.md: does not reference " +
        "images/controls/message-box.png in an Example image",
    ]);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("excludedAbstractDocSlugs_MatchesTheReviewedAuthoringRolePages", () => {
  assert.deepEqual(
    [...excludedAbstractDocSlugs].sort(),
    [
      "animated-indicator-base",
      "composite-control",
      "container",
      "content-control",
      "headered-content-control",
      "input-base",
      "items-control",
      "pressable",
    ],
  );
});

test("repository_WhenScanned_HasNoControlImageCoverageGaps", async () => {
  const root = join(import.meta.dirname, "..");

  assert.deepEqual(await validateControlImageCoverage(root), []);
});
