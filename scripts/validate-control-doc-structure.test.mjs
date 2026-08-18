import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  PENDING_MIGRATION,
  SKIP_TIER_B,
  ancestorMemberSet,
  deriveDocumentedType,
  extractCheckableTokens,
  extractH2Headings,
  extractInheritanceEdges,
  extractSubsectionHeadings,
  findNearestHeadingBefore,
  parseMarkdownTables,
  parseSnapshotTypes,
  parseTypeReference,
  slugToPascalCase,
  splitTopLevelCommas,
  validateControlDocStructure,
  validateInheritanceSection,
  validateSectionSpine,
  validateTierA,
  validateTierB,
} from "./validate-control-doc-structure.mjs";

const fixtureSnapshot = `﻿namespace Fixture.Controls
{
    public abstract class ControlBase : System.IDisposable
    {
        public ControlBase() { }
        public bool IsEnabled { get; set; }
    }
    public sealed class Widget : Fixture.Controls.ControlBase
    {
        public Widget() { }
        public int Count { get; set; }
        public void Reset() { }
    }
    public sealed class Gadget : Fixture.Controls.ControlBase
    {
        public Gadget() { }
        public string Name { get; set; }
    }
}
`;

const validWidgetPage = `# Widget

## Overview

\`Widget\` is declared \`public sealed class Widget : ControlBase\`.

## Inheritance

\`\`\`mermaid
classDiagram
    ControlBase <|-- Widget
\`\`\`

## API

| Member    | Type   | Default | Description        |
| --------- | ------ | ------- | ------------------- |
| \`Count\`   | \`int\`  | \`0\`     | The current count.  |
| \`Reset()\` | \`void\` | —       | Resets the count.   |

## Example

\`\`\`csharp
var widget = new Widget();
\`\`\`

## Expected behavior

| Scope      | Observable evidence                                             |
| ---------- | ----------------------------------------------------------------- |
| Public API | Validation, defaults, state changes, and deterministic output.   |

- Count starts at zero.
`;

/**
 * Builds an isolated fixture repository root with the fixture compatibility snapshot and one
 * `docs/controls/widget.md` page, so each test can mutate the page text and assert the outcome
 * without touching the real repository.
 *
 * @param {string} pageText The full Markdown content for `docs/controls/widget.md`.
 * @returns {Promise<string>} The fixture root.
 */
async function buildFixtureRoot(pageText) {
  const root = await mkdtemp(join(tmpdir(), "control-doc-structure-"));

  await mkdir(join(root, "tests", "SharpVision.Compatibility.Tests", "Snapshots"), {
    recursive: true,
  });
  await writeFile(
    join(root, "tests", "SharpVision.Compatibility.Tests", "Snapshots", "SharpVision.verified.txt"),
    fixtureSnapshot,
  );

  await mkdir(join(root, "docs", "controls"), { recursive: true });
  await writeFile(join(root, "docs", "controls", "widget.md"), pageText);

  return root;
}

test("slugToPascalCase_WhenSlugIsHyphenated_CapitalizesEachSegment", () => {
  assert.equal(slugToPascalCase("list-view"), "ListView");
  assert.equal(slugToPascalCase("check-box"), "CheckBox");
  assert.equal(slugToPascalCase("control"), "Control");
});

test("deriveDocumentedType_WhenSlugIsOverridden_ReturnsTheOverrideKey", () => {
  assert.equal(deriveDocumentedType("control"), "ControlBase#0");
  assert.equal(deriveDocumentedType("composite-control"), "CompositeControlBase#0");
  assert.equal(deriveDocumentedType("context-menu"), SKIP_TIER_B);
});

test("deriveDocumentedType_WhenSlugIsNotOverridden_ReturnsTheDefaultKeyAtArityZero", () => {
  assert.equal(deriveDocumentedType("button"), "Button#0");
});

test("splitTopLevelCommas_WhenTextHasNestedGenerics_SplitsOnlyTopLevelCommas", () => {
  assert.deepEqual(splitTopLevelCommas("Foo<Bar,Baz>, Qux"), ["Foo<Bar,Baz>", "Qux"]);
  assert.deepEqual(splitTopLevelCommas("Solo"), ["Solo"]);
  assert.deepEqual(splitTopLevelCommas(""), []);
});

test("parseTypeReference_WhenTokenUsesMermaidGenericSyntax_NormalizesToAngleBrackets", () => {
  assert.deepEqual(parseTypeReference("Pressable~ButtonStyle~"), {
    simpleName: "Pressable",
    arity: 1,
    key: "Pressable#1",
  });
});

test("parseTypeReference_WhenTokenIsNamespaceQualified_UsesTheSimpleName", () => {
  assert.deepEqual(parseTypeReference("SharpVision.Controls.ControlBase"), {
    simpleName: "ControlBase",
    arity: 0,
    key: "ControlBase#0",
  });
});

test("parseSnapshotTypes_WhenGivenTheFixtureSnapshot_MapsEveryTypeToItsBaseAndMembers", () => {
  const map = parseSnapshotTypes(fixtureSnapshot);

  assert.equal(map.size, 3);
  assert.deepEqual(map.get("Widget#0").base, "ControlBase#0");
  assert.ok(map.get("Widget#0").members.has("Count"));
  assert.ok(map.get("Widget#0").members.has("Reset"));
  assert.ok(map.get("Widget#0").members.has("Widget"));
  assert.equal(map.get("ControlBase#0").base, "IDisposable#0");
  assert.ok(!map.has("IDisposable#0"));
});

test("ancestorMemberSet_WhenWalkingAChain_CollectsOwnAndInheritedMembers", () => {
  const map = parseSnapshotTypes(fixtureSnapshot);
  const members = ancestorMemberSet("Widget#0", map);

  assert.ok(members.has("Count"));
  assert.ok(members.has("IsEnabled"));
  assert.ok(members.has("ControlBase"));
});

test("extractH2Headings_WhenAHeadingLikeLineIsFenced_IsIgnored", () => {
  const lines = ["## Overview", "", "```text", "## Not a heading", "```", "", "## Inheritance"];

  assert.deepEqual(
    extractH2Headings(lines).map((h) => h.text),
    ["Overview", "Inheritance"],
  );
});

test("extractInheritanceEdges_WhenBodyMixesEdgeKinds_ExtractsOnlyInheritanceEdges", () => {
  const body = ["classDiagram", "    A <|-- B", "    B *-- C : owns", "    D ..|> E"].join("\n");

  assert.deepEqual(extractInheritanceEdges(body), [{ parent: "A", child: "B" }]);
});

test("extractCheckableTokens_WhenCellHasUnrelatedExtraProse_IsSkipped", () => {
  assert.deepEqual(extractCheckableTokens("See `Text`"), []);
  assert.deepEqual(extractCheckableTokens("Inherited"), []);
});

test("extractCheckableTokens_WhenCellIsAnInheritedMember_ReturnsThePlainToken", () => {
  assert.deepEqual(extractCheckableTokens("Inherited `Text`"), [{ name: "Text" }]);
});

test("extractCheckableTokens_WhenCellIsAnInheritedGroupedList_ReturnsOneTokenPerSpan", () => {
  assert.deepEqual(extractCheckableTokens("Inherited `Command`, `CommandParameter`"), [
    { name: "Command" },
    { name: "CommandParameter" },
  ]);
});

test("extractCheckableTokens_WhenCellIsAnInheritedMethod_StripsParens", () => {
  assert.deepEqual(extractCheckableTokens("Inherited `ResetBorder()`"), [{ name: "ResetBorder" }]);
});

test("extractCheckableTokens_WhenCellIsAGroupedList_ReturnsOneTokenPerSpan", () => {
  assert.deepEqual(extractCheckableTokens("`Width`, `Height`"), [{ name: "Width" }, { name: "Height" }]);
});

test("extractCheckableTokens_WhenCellIsAMultiParamMethod_StripsParensWithoutSplittingOnInnerCommas", () => {
  assert.deepEqual(extractCheckableTokens("`ScrollBy(int x, int y)`"), [{ name: "ScrollBy" }]);
});

test("extractCheckableTokens_WhenCellIsAnAttachedProperty_ReturnsAnOwnerPropertyToken", () => {
  assert.deepEqual(extractCheckableTokens("`Dock.Side`"), [{ owner: "Dock", property: "Side" }]);
});

test("extractCheckableTokens_WhenCellIsAnEmDash_ReturnsNoTokens", () => {
  assert.deepEqual(extractCheckableTokens("—"), []);
});

test("parseMarkdownTables_WhenLinesContainAFencedCodeBlock_IgnoresPipesInsideTheFence", () => {
  const lines = ["```text", "| not | a | table |", "```", "| Member | Type |", "| --- | --- |", "| `X` | `int` |"];
  const tables = parseMarkdownTables(lines);

  assert.equal(tables.length, 1);
  assert.deepEqual(tables[0].headerCells, ["Member", "Type"]);
  assert.deepEqual(tables[0].dataRows, [["`X`", "`int`"]]);
});

test("validateTierA_WhenAnEdgeContradictsTheSnapshot_ReportsTheActualBase", () => {
  const map = parseSnapshotTypes(fixtureSnapshot);
  const result = validateTierA("classDiagram\n    Gadget <|-- Widget", map);

  assert.equal(result.checked, 1);
  assert.equal(result.skipped, 0);
  assert.equal(result.errors.length, 1);
  assert.match(result.errors[0], /actual base is ControlBase/);
});

test("validateTierA_WhenAnEdgeReferencesATypeOutsideTheSnapshot_IsSkippedNotAnError", () => {
  const map = parseSnapshotTypes(fixtureSnapshot);
  const result = validateTierA("classDiagram\n    Ghost <|-- Widget", map);

  assert.equal(result.checked, 0);
  assert.equal(result.skipped, 1);
  assert.deepEqual(result.errors, []);
});

test("extractSubsectionHeadings_WhenDocumentMixesH2AndH3_ExtractsBothLevels", () => {
  const lines = ["## API", "", "### Attached properties", "", "#### Not tracked", "", "## Example"];

  assert.deepEqual(extractSubsectionHeadings(lines), [
    { text: "API", line: 0, level: 2 },
    { text: "Attached properties", line: 2, level: 3 },
    { text: "Example", line: 6, level: 2 },
  ]);
});

test("findNearestHeadingBefore_WhenSeveralHeadingsPrecedeTheLine_ReturnsTheClosestOne", () => {
  const headings = [
    { text: "API", line: 0 },
    { text: "Gadget", line: 5 },
  ];

  assert.equal(findNearestHeadingBefore(headings, 10).text, "Gadget");
  assert.equal(findNearestHeadingBefore(headings, 3).text, "API");
  assert.equal(findNearestHeadingBefore(headings, 0), undefined);
});

test("validateTierB_WhenAMemberDoesNotExist_ReportsItAndStripsMethodParensForRealMembers", () => {
  const map = parseSnapshotTypes(fixtureSnapshot);
  const lines = [
    "## API",
    "",
    "| Member       | Type   | Default | Description |",
    "| ------------ | ------ | ------- | ----------- |",
    "| `Reset(int)` | `void` | —       | Resets it.  |",
    "| `Bogus`      | `int`  | `0`     | Not real.   |",
  ];
  const headings = extractSubsectionHeadings(lines);

  const result = validateTierB(lines, headings, "Widget#0", map);

  assert.equal(result.checked, 2);
  assert.equal(result.skipped, 0);
  assert.equal(result.errors.length, 1);
  assert.match(result.errors[0], /member `Bogus` was not found on Widget/);
});

// Hole 1 (adversarial review): Tier B used to only look at the first table in `## API` plus any
// `### Attached properties` sub-table, so a fabricated member in a secondary-type table (like
// tab-control.md's `## TabItem` or tree-view.md's `## TreeViewItem`) passed silently. These three
// tests prove the generalized whole-document scan closes that gap.
test("validateTierB_WhenASecondaryTableSitsUnderAHeadingNamingASnapshotType_ChecksAgainstThatType", () => {
  const map = parseSnapshotTypes(fixtureSnapshot);
  const lines = [
    "## API",
    "",
    "| Member  | Type  | Default | Description |",
    "| ------- | ----- | ------- | ----------- |",
    "| `Count` | `int` | `0`     | Real.       |",
    "",
    "## Gadget",
    "",
    "| Member  | Type     | Default | Description |",
    "| ------- | -------- | ------- | ----------- |",
    "| `Name`  | `string` | `null`  | Real.       |",
    "| `Bogus` | `int`    | `0`     | Not real.   |",
  ];
  const headings = extractSubsectionHeadings(lines);

  const result = validateTierB(lines, headings, "Widget#0", map);

  assert.equal(result.errors.length, 1);
  assert.match(result.errors[0], /member `Bogus` was not found on Gadget/);
});

test("validateTierB_WhenASecondaryTableSitsUnderAHeadingNotNamingASnapshotType_FallsBackToThePrimaryType", () => {
  const map = parseSnapshotTypes(fixtureSnapshot);
  const lines = [
    "## API",
    "",
    "| Member  | Type  | Default | Description |",
    "| ------- | ----- | ------- | ----------- |",
    "| `Count` | `int` | `0`     | Real.       |",
    "",
    "## Notes",
    "",
    "| Member  | Type  | Default | Description |",
    "| ------- | ----- | ------- | ----------- |",
    "| `Bogus` | `int` | `0`     | Not real.   |",
  ];
  const headings = extractSubsectionHeadings(lines);

  const result = validateTierB(lines, headings, "Widget#0", map);

  assert.equal(result.errors.length, 1);
  assert.match(result.errors[0], /member `Bogus` was not found on Widget/);
});

test("validateTierB_WhenAnInheritedCellNamesARealOrFakeAncestorMember_PassesOrFailsAccordingly", () => {
  const map = parseSnapshotTypes(fixtureSnapshot);
  const lines = [
    "## API",
    "",
    "| Member                | Type   | Default | Description |",
    "| ---------------------- | ------ | ------- | ----------- |",
    "| Inherited `IsEnabled`  | `bool` | `true`  | Real ancestor member.  |",
    "| Inherited `FakeMember` | `bool` | `true`  | Not a real member.     |",
  ];
  const headings = extractSubsectionHeadings(lines);

  const result = validateTierB(lines, headings, "Widget#0", map);

  assert.equal(result.checked, 2);
  assert.equal(result.skipped, 0);
  assert.equal(result.errors.length, 1);
  assert.match(result.errors[0], /member `FakeMember` was not found on Widget/);
});

test("validateSectionSpine_WhenOrderIsCorrect_ReturnsNull", () => {
  const headings = ["Overview", "Inheritance", "API", "Example", "Expected behavior"].map(
    (text, line) => ({ text, line }),
  );

  assert.equal(validateSectionSpine(headings), null);
});

test("validateInheritanceSection_WhenTheFenceIsMissing_ReportsAViolation", () => {
  const lines = ["## Inheritance", "", "Prose only, no diagram.", "", "## API"];
  const headings = extractH2Headings(lines);

  assert.match(validateInheritanceSection(lines, headings).error, /has no fenced code block/);
});

test("validateControlDocStructure_WhenAPageIsFullyValid_ReportsNoErrors", async () => {
  const root = await buildFixtureRoot(validWidgetPage);

  try {
    const result = await validateControlDocStructure(root);

    assert.deepEqual(result.errors, []);
    assert.equal(result.stats.pagesChecked, 1);
    assert.equal(result.stats.tierAChecked, 1);
    assert.equal(result.stats.tierBChecked, 2);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlDocStructure_WhenASecondaryTypeTableHasAFabricatedMember_CatchesItEndToEnd", async () => {
  // Widget's page gains a topic section between API and Example - exactly tab-control.md's and
  // tree-view.md's real shape - documenting a second type (`Gadget`) with its own canonical table
  // and a member that does not exist on `Gadget` in the fixture snapshot.
  const withSecondaryTable = validWidgetPage.replace(
    "## Example",
    [
      "## Gadget",
      "",
      "| Member  | Type     | Default | Description |",
      "| ------- | -------- | ------- | ----------- |",
      "| `Name`  | `string` | `null`  | Real.       |",
      "| `Bogus` | `int`    | `0`     | Not real.   |",
      "",
      "## Example",
    ].join("\n"),
  );
  const root = await buildFixtureRoot(withSecondaryTable);

  try {
    const result = await validateControlDocStructure(root);

    assert.equal(result.errors.length, 1);
    assert.match(result.errors[0], /docs\/controls\/widget\.md: member `Bogus` was not found on Gadget/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlDocStructure_WhenTheH2SpineIsOutOfOrder_ReportsASpineViolation", async () => {
  // Swaps the Inheritance and API sections wholesale, each keeping its own valid content, so this
  // is a pure ordering violation: Overview, API, Inheritance, Example, Expected behavior. Neither
  // section's own content check should fire - only the spine order.
  const beforeInheritance = validWidgetPage.slice(0, validWidgetPage.indexOf("## Inheritance"));
  const inheritanceChunk = validWidgetPage.slice(
    validWidgetPage.indexOf("## Inheritance"),
    validWidgetPage.indexOf("## API"),
  );
  const apiChunk = validWidgetPage.slice(validWidgetPage.indexOf("## API"), validWidgetPage.indexOf("## Example"));
  const rest = validWidgetPage.slice(validWidgetPage.indexOf("## Example"));
  const reordered = beforeInheritance + apiChunk + inheritanceChunk + rest;
  const root = await buildFixtureRoot(reordered);

  try {
    const result = await validateControlDocStructure(root);

    assert.equal(result.errors.length, 1);
    assert.match(result.errors[0], /docs\/controls\/widget\.md: H2 spine violation/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlDocStructure_WhenInheritanceHasNoMermaidFence_ReportsAViolation", async () => {
  const broken = validWidgetPage.replace(
    "```mermaid\nclassDiagram\n    ControlBase <|-- Widget\n```",
    "`Widget` derives from `ControlBase`.",
  );
  const root = await buildFixtureRoot(broken);

  try {
    const result = await validateControlDocStructure(root);

    assert.equal(result.errors.length, 1);
    assert.match(result.errors[0], /## Inheritance has no fenced code block/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlDocStructure_WhenTheApiHeaderIsWrong_ReportsAViolation", async () => {
  const broken = validWidgetPage.replace(
    "| Member    | Type   | Default | Description        |",
    "| Member    | Default | Description        |",
  );
  const root = await buildFixtureRoot(broken);

  try {
    const result = await validateControlDocStructure(root);

    assert.equal(result.errors.length, 1);
    assert.match(result.errors[0], /## API table header is/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlDocStructure_WhenATopicSectionFollowsExample_ReportsASpineViolation", async () => {
  const withTrailingTopic = validWidgetPage.replace(
    "## Expected behavior",
    "## Notes\n\nSome extra notes.\n\n## Expected behavior",
  );
  const root = await buildFixtureRoot(withTrailingTopic);

  try {
    const result = await validateControlDocStructure(root);

    assert.equal(result.errors.length, 1);
    assert.match(result.errors[0], /H2 spine violation/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlDocStructure_WhenAPageIsNamedIndex_IsExempt", async () => {
  const root = await mkdtemp(join(tmpdir(), "control-doc-structure-"));

  try {
    await mkdir(join(root, "tests", "SharpVision.Compatibility.Tests", "Snapshots"), {
      recursive: true,
    });
    await writeFile(
      join(root, "tests", "SharpVision.Compatibility.Tests", "Snapshots", "SharpVision.verified.txt"),
      fixtureSnapshot,
    );
    await mkdir(join(root, "docs", "controls"), { recursive: true });
    await writeFile(join(root, "docs", "controls", "index.md"), "# Controls\n\nJust a catalog.\n");

    const result = await validateControlDocStructure(root);

    assert.deepEqual(result.errors, []);
    assert.equal(result.stats.pagesChecked, 0);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateControlDocStructure_WhenAPageIsPendingMigration_IsSkippedWithAWarning", async () => {
  const root = await mkdtemp(join(tmpdir(), "control-doc-structure-"));

  try {
    await mkdir(join(root, "tests", "SharpVision.Compatibility.Tests", "Snapshots"), {
      recursive: true,
    });
    await writeFile(
      join(root, "tests", "SharpVision.Compatibility.Tests", "Snapshots", "SharpVision.verified.txt"),
      fixtureSnapshot,
    );
    await mkdir(join(root, "docs", "controls"), { recursive: true });
    // Deliberately malformed: no spine, no diagram, no table. Proves the exemption skips every
    // check, not just some of them. The real PENDING_MIGRATION set is empty now that the
    // InputBase rework's five deferred pages have all migrated, so this test seeds one entry of
    // its own for the duration of the run, to exercise the general skip mechanism rather than any
    // page still awaiting migration.
    await writeFile(join(root, "docs", "controls", "pressable.md"), "# Pressable\n\nNot migrated yet.\n");
    PENDING_MIGRATION.add("docs/controls/pressable.md");

    const result = await validateControlDocStructure(root);

    assert.deepEqual(result.errors, []);
    assert.equal(result.stats.pagesExempt, 1);
    assert.equal(result.warnings.length, 1);
    assert.match(result.warnings[0], /docs\/controls\/pressable\.md: PENDING_MIGRATION/);
  } finally {
    PENDING_MIGRATION.delete("docs/controls/pressable.md");
    await rm(root, { recursive: true, force: true });
  }
});

test("PENDING_MIGRATION_IsEmptyNowThatTheInputBaseReworkPagesHaveMigrated", () => {
  assert.deepEqual([...PENDING_MIGRATION], []);
});

test("validateControlDocStructure_WhenAPageHasZeroCalloutsAndALongParagraph_Passes", async () => {
  const longParagraph = "A".repeat(900);
  const withLongParagraph = validWidgetPage.replace(
    "\`Widget\` is declared \`public sealed class Widget : ControlBase\`.",
    `\`Widget\` is declared \`public sealed class Widget : ControlBase\`. ${longParagraph}`,
  );
  const root = await buildFixtureRoot(withLongParagraph);

  try {
    const result = await validateControlDocStructure(root);

    assert.deepEqual(result.errors, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("repository_WhenScanned_SatisfiesTheControlPageContract", async () => {
  const root = join(import.meta.dirname, "..");
  const result = await validateControlDocStructure(root);

  assert.deepEqual(result.errors, []);
  assert.equal(result.stats.pagesExempt, PENDING_MIGRATION.size);
});
