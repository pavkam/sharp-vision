import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  SUITE_LEVEL_ALLOW_LIST,
  candidateBases,
  computeViolations,
  discoverSubjectTypes,
  discoverTestClasses,
  findFilesWithExtension,
  formatViolation,
  stripLongestSuffix,
  validateRepository,
  writeBaseline,
} from "./validate-test-class-naming.mjs";

async function makeFixtureRoot() {
  return await mkdtemp(join(tmpdir(), "sharpvision-test-names-"));
}

async function writeSource(root, relativePath, content) {
  const path = join(root, relativePath);
  await mkdir(join(path, ".."), { recursive: true });
  await writeFile(path, content, "utf8");
}

const factTestBody = [
  "namespace SharpVision.Tests.Widgets;",
  "",
  "public sealed class {CLASS}",
  "{",
  "    [Fact]",
  "    public void Something_WhenCalled_Works() { }",
  "}",
  "",
].join("\n");

function testClassFile(className) {
  return factTestBody.replace("{CLASS}", className);
}

test("stripLongestSuffix_WhenNameHasEvidenceTierSuffix_StripsLongestFirst", () => {
  assert.equal(stripLongestSuffix("WidgetSurfaceTests"), "Widget");
  assert.equal(stripLongestSuffix("WidgetPerformanceTests"), "Widget");
  assert.equal(stripLongestSuffix("WidgetConsumerTests"), "Widget");
  assert.equal(stripLongestSuffix("WidgetCompatibilityTests"), "Widget");
  assert.equal(stripLongestSuffix("WidgetTests"), "Widget");
});

test("candidateBases_WhenNameHasEvidenceTierSuffix_AlsoOffersTheBareTestsInterpretation", () => {
  // A subject type can itself end in a suffix word (FloatingSurface), so both the tier-specific
  // strip and the bare "Tests" strip must be offered as candidates, not just the tier-specific one.
  assert.deepEqual(candidateBases("WidgetSurfaceTests"), ["Widget", "WidgetSurface"]);
  assert.deepEqual(candidateBases("WidgetPerformanceTests"), ["Widget", "WidgetPerformance"]);
  assert.deepEqual(candidateBases("WidgetConsumerTests"), ["Widget", "WidgetConsumer"]);
  assert.deepEqual(candidateBases("WidgetCompatibilityTests"), ["Widget", "WidgetCompatibility"]);
  assert.deepEqual(candidateBases("WidgetTests"), ["Widget"]);
});

test("findFilesWithExtension_WhenDirectoryHasIgnoredSubdirectories_SkipsThem", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(root, "keep/Foo.cs", "// kept");
    await writeSource(root, ".claude/worktrees/stale/Foo.cs", "// stale worktree snapshot");
    await writeSource(root, "obj/Generated.cs", "// build output");
    await writeSource(root, "node_modules/pkg/Foo.cs", "// dependency");

    const files = await findFilesWithExtension(root, ".cs");

    assert.deepEqual(files.sort(), [join(root, "keep/Foo.cs")]);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("discoverSubjectTypes_WhenSrcAndExamplesDeclareTypes_CollectsBareNames", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(
      root,
      "src/Widget.cs",
      "namespace SharpVision;\n\npublic sealed class Widget\n{\n}\n",
    );
    await writeSource(
      root,
      "src/IChartControl.cs",
      "namespace SharpVision;\n\npublic interface IChartControl\n{\n}\n",
    );
    await writeSource(
      root,
      "examples/Showcase/Gadget.cs",
      "namespace Showcase;\n\ninternal readonly record struct Gadget;\n",
    );
    await writeSource(
      root,
      "src/ParameterStatus.cs",
      "namespace SharpVision;\n\npublic enum ParameterStatus\n{\n    End,\n    Default,\n}\n",
    );

    const types = await discoverSubjectTypes(root);

    assert.equal(types.has("Widget"), true);
    assert.equal(types.has("IChartControl"), true);
    assert.equal(types.has("ChartControl"), true);
    assert.equal(types.has("Gadget"), true);
    assert.equal(types.has("ParameterStatus"), true);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("discoverTestClasses_WhenFileHasNoFactOrTheory_IsIgnored", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(
      root,
      "tests/Helper/NotATestFixtureTests.cs",
      "namespace SharpVision.Tests;\n\npublic sealed class NotATestFixtureTests\n{\n}\n",
    );

    const classes = await discoverTestClasses(root);

    assert.deepEqual(classes, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("discoverTestClasses_WhenClassIsIndented_IsIgnored", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(
      root,
      "tests/Widgets/WidgetTests.cs",
      [
        "namespace SharpVision.Tests.Widgets;",
        "",
        "public sealed class WidgetTests",
        "{",
        "    [Fact]",
        "    public void Something_WhenCalled_Works() { }",
        "",
        "    private sealed class NestedHelperTests",
        "    {",
        "    }",
        "}",
        "",
      ].join("\n"),
    );

    const classes = await discoverTestClasses(root);

    assert.deepEqual(
      classes.map((entry) => entry.className),
      ["WidgetTests"],
    );
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("computeViolations_WhenClassNamesSubjectType_Passes", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(root, "src/Widget.cs", "namespace SharpVision;\n\npublic sealed class Widget\n{\n}\n");
    await writeSource(root, "tests/Widgets/WidgetTests.cs", testClassFile("WidgetTests"));
    await writeSource(root, "tests/Widgets/WidgetSurfaceTests.cs", testClassFile("WidgetSurfaceTests"));
    await writeSource(root, "tests/Widgets/WidgetPerformanceTests.cs", testClassFile("WidgetPerformanceTests"));
    await writeSource(root, "tests/Widgets/WidgetConsumerTests.cs", testClassFile("WidgetConsumerTests"));
    await writeSource(root, "tests/Widgets/WidgetCompatibilityTests.cs", testClassFile("WidgetCompatibilityTests"));

    const violations = await computeViolations(root);

    assert.deepEqual(violations, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("computeViolations_WhenSubjectNameItselfEndsInASuffixWord_PassesViaTheBareTestsInterpretation", async () => {
  const root = await makeFixtureRoot();

  try {
    // Regression coverage: FloatingSurface is a real subject type under src/ whose own name ends in
    // "Surface". Stripping the tier-specific "SurfaceTests" suffix from FloatingSurfaceTests yields
    // "Floating", which matches nothing; only the bare "Tests" strip ("FloatingSurface") matches the
    // subject. computeViolations must try every candidate, not just the most tier-specific one.
    await writeSource(
      root,
      "src/FloatingSurface.cs",
      "namespace SharpVision.Surfaces;\n\npublic abstract class FloatingSurface<TStyle>: object\n{\n}\n",
    );
    await writeSource(
      root,
      "tests/Surfaces/FloatingSurfaceTests.cs",
      testClassFile("FloatingSurfaceTests"),
    );

    assert.deepEqual(candidateBases("FloatingSurfaceTests"), ["Floating", "FloatingSurface"]);

    const violations = await computeViolations(root);

    assert.deepEqual(violations, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("computeViolations_WhenTierSpecificCandidateMatchesDirectly_StillPasses", async () => {
  const root = await makeFixtureRoot();

  try {
    // A genuine evidence-tier class (subject "Widget", not "WidgetSurface") must still pass via its
    // tier-specific candidate, alongside the FloatingSurface-style bare-Tests fallback above.
    await writeSource(root, "src/Widget.cs", "namespace SharpVision;\n\npublic sealed class Widget\n{\n}\n");
    await writeSource(root, "tests/Widgets/WidgetSurfaceTests.cs", testClassFile("WidgetSurfaceTests"));

    const violations = await computeViolations(root);

    assert.deepEqual(violations, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("computeViolations_WhenClassNamesAnEnumSubject_Passes", async () => {
  const root = await makeFixtureRoot();

  try {
    // Regression coverage: ParameterStatus is a real `public enum` under src/, and
    // ParameterStatusTests was wrongly reported as a violation because enum declarations were not
    // collected as subject types.
    await writeSource(
      root,
      "src/ParameterStatus.cs",
      "namespace SharpVision;\n\npublic enum ParameterStatus\n{\n    End,\n    Default,\n}\n",
    );
    await writeSource(
      root,
      "tests/Protocols/ParameterStatusTests.cs",
      testClassFile("ParameterStatusTests"),
    );

    const violations = await computeViolations(root);

    assert.deepEqual(violations, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("computeViolations_WhenClassIsMultiplexerPseudoterminalTests_PassesViaAllowList", async () => {
  const root = await makeFixtureRoot();

  try {
    assert.equal(SUITE_LEVEL_ALLOW_LIST.has("MultiplexerPseudoterminalTests"), true);
    await writeSource(
      root,
      "tests/Transport/MultiplexerPseudoterminalTests.cs",
      testClassFile("MultiplexerPseudoterminalTests"),
    );

    const violations = await computeViolations(root);

    assert.deepEqual(violations, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("computeViolations_WhenClassNamesNoSubjectType_Fails", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(root, "src/Widget.cs", "namespace SharpVision;\n\npublic sealed class Widget\n{\n}\n");
    await writeSource(root, "tests/Widgets/FooBarBazTests.cs", testClassFile("FooBarBazTests"));

    const violations = await computeViolations(root);

    assert.equal(violations.length, 1);
    assert.equal(violations[0].className, "FooBarBazTests");

    const message = formatViolation(violations[0]);
    assert.match(message, /^tests\/Widgets\/FooBarBazTests\.cs:\d+ FooBarBazTests does not name/);
    assert.match(
      message,
      /as FooBarBaz \(checked Tests\/SurfaceTests\/PerformanceTests\/ConsumerTests\/CompatibilityTests suffixes\)/,
    );
    assert.match(message, /suite-level allow-list or scripts\/test-class-naming-baseline\.txt\.$/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("computeViolations_WhenNeitherCandidateMatchesASubjectType_FailsAndListsBothInTheMessage", async () => {
  const root = await makeFixtureRoot();

  try {
    // Neither "Widget" nor "WidgetSurface" is declared under src/, so both interpretations of
    // WidgetSurfaceTests fail and the message names both, joined with "or".
    await writeSource(root, "tests/Widgets/WidgetSurfaceTests.cs", testClassFile("WidgetSurfaceTests"));

    const violations = await computeViolations(root);

    assert.equal(violations.length, 1);

    const message = formatViolation(violations[0]);
    assert.match(message, /as Widget or WidgetSurface \(checked/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("computeViolations_WhenClassIsOnSuiteLevelAllowList_Passes", async () => {
  const root = await makeFixtureRoot();
  const [allowListedName] = SUITE_LEVEL_ALLOW_LIST;

  try {
    await writeSource(root, "tests/Widgets/" + allowListedName + ".cs", testClassFile(allowListedName));

    const violations = await computeViolations(root);

    assert.deepEqual(violations, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateRepository_WhenViolationIsBaselined_ReportsNoError", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(root, "tests/Widgets/FooBarBazTests.cs", testClassFile("FooBarBazTests"));
    await writeSource(
      root,
      "scripts/test-class-naming-baseline.txt",
      "tests/Widgets/FooBarBazTests.cs#FooBarBazTests\n",
    );

    const { errors, staleBaselineEntries } = await validateRepository(root);

    assert.deepEqual(errors, []);
    assert.deepEqual(staleBaselineEntries, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateRepository_WhenBaselineEntryNoLongerReproduces_IsNonFatalAndReported", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(root, "src/Widget.cs", "namespace SharpVision;\n\npublic sealed class Widget\n{\n}\n");
    await writeSource(root, "tests/Widgets/WidgetTests.cs", testClassFile("WidgetTests"));
    await writeSource(
      root,
      "scripts/test-class-naming-baseline.txt",
      "tests/Widgets/AlreadyRenamedTests.cs#AlreadyRenamedTests\n",
    );

    const { errors, staleBaselineEntries } = await validateRepository(root);

    assert.deepEqual(errors, []);
    assert.deepEqual(staleBaselineEntries, [
      "tests/Widgets/AlreadyRenamedTests.cs#AlreadyRenamedTests",
    ]);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateRepository_WhenNewViolationIsNotBaselined_Fails", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(root, "tests/Widgets/FooBarBazTests.cs", testClassFile("FooBarBazTests"));
    await writeSource(root, "scripts/test-class-naming-baseline.txt", "");

    const { errors } = await validateRepository(root);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /FooBarBazTests does not name/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("writeBaseline_WhenWritten_IsSortedAndPathQualified", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(root, "tests/Zeta/ZetaWidgetTests.cs", testClassFile("ZetaWidgetTests"));
    await writeSource(root, "tests/Alpha/AlphaWidgetTests.cs", testClassFile("AlphaWidgetTests"));
    await mkdir(join(root, "scripts"), { recursive: true });

    const { keys, added, removed } = await writeBaseline(root);

    assert.deepEqual(keys, [
      "tests/Alpha/AlphaWidgetTests.cs#AlphaWidgetTests",
      "tests/Zeta/ZetaWidgetTests.cs#ZetaWidgetTests",
    ]);
    assert.deepEqual(added, keys);
    assert.deepEqual(removed, []);

    const written = await readFile(join(root, "scripts", "test-class-naming-baseline.txt"), "utf8");
    assert.equal(written, `${keys.join("\n")}\n`);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("writeBaseline_WhenRerunAfterRename_ReportsAddedAndRemoved", async () => {
  const root = await makeFixtureRoot();

  try {
    await writeSource(root, "tests/Widgets/FooBarBazTests.cs", testClassFile("FooBarBazTests"));
    await mkdir(join(root, "scripts"), { recursive: true });
    await writeBaseline(root);

    await rm(join(root, "tests/Widgets/FooBarBazTests.cs"));
    await writeSource(root, "tests/Widgets/QuuxCorgeTests.cs", testClassFile("QuuxCorgeTests"));

    const { added, removed } = await writeBaseline(root);

    assert.deepEqual(added, ["tests/Widgets/QuuxCorgeTests.cs#QuuxCorgeTests"]);
    assert.deepEqual(removed, ["tests/Widgets/FooBarBazTests.cs#FooBarBazTests"]);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("repository_WhenValidated_HasNoNewNamingViolations", async () => {
  const root = join(import.meta.dirname, "..");
  const { errors } = await validateRepository(root);

  assert.deepEqual(errors, []);
});
