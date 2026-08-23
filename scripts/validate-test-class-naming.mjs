#!/usr/bin/env node

import { readFile, readdir, writeFile } from "node:fs/promises";
import { dirname, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

// ".claude" holds Claude Code's own worktrees, each a full checkout of this repo pinned at some
// older commit. Walking into one makes this validator re-check that snapshot's stale test classes
// against today's source tree and fail, so a developer with a worktree open cannot run `make lint`.
const ignoredDirectories = new Set([
  ".claude",
  ".git",
  ".worktrees",
  "artifacts",
  "bin",
  "node_modules",
  "obj",
]);

// Evidence-tier suffixes first, generic "Tests" last. Every evidence-tier suffix itself ends in
// "Tests", so a class carrying one (e.g. "FooSurfaceTests") always matches two suffixes here: the
// tier-specific one and the bare one. Both candidate bases are tried against the subject-type set
// (see candidateBases()) because the subject itself can end in a suffix word, e.g. FloatingSurface
// -- stripping only the tier suffix from "FloatingSurfaceTests" leaves "Floating", which is not a
// type, while stripping the bare "Tests" leaves "FloatingSurface", which is. This list's order only
// picks which candidate stripLongestSuffix() reports as "primary" for display; it does not affect
// which classes pass, since every suffix that matches is tried.
const evidenceTierSuffixes = [
  "SurfaceTests",
  "PerformanceTests",
  "ConsumerTests",
  "CompatibilityTests",
  "Tests",
];

// Zero-indent (file-scoped namespaces put every test class at column zero), so this intentionally
// does not match nested/local classes.
const testClassPattern =
  /^(?:(?:public|internal|private|protected)\s+)?(?:(?:sealed|abstract|static|partial)\s+)*class\s+(\w+Tests)\b/;

// Not indent-restricted: a test class can legitimately be named after a private nested type.
// Covers class, record, record class, record struct, struct, interface, and enum declarations.
const typeDeclarationPattern =
  /^\s*(?:(?:public|internal|private|protected)\s+)?(?:(?:static|sealed|abstract|partial|readonly|unsafe|new)\s+)*(?:record\s+class|record\s+struct|record|class|struct|interface|enum)\s+(\w+)/;

const factOrTheoryPattern = /\[(?:Fact|Theory)\b/;

// Each entry proves a repository-wide or cross-cutting property that spans many subject types, so
// no single "<Type>" name would describe what it exercises. Verified class-by-class.
export const SUITE_LEVEL_ALLOW_LIST = new Set([
  "ConformanceTests", // official Unicode conformance corpus sweep
  "RandomizedInputTests", // whole-input-pipeline fuzz
  "CodeCompatibilityTests", // shipped numeric-contract freeze
  "AssemblyTests", // both projects: test-harness self-check; name-based so one entry covers both
  "PublicApiCompatibilityTests", // three-assembly shape oracle
  "FirstPartyPackageVersionTests", // cross-package version sanity
  "SealedControlsContractTests", // whole-catalog contract
  "LoaderAndChromeContractTests", // explicitly multi-subject styling contracts
  "PhaseThreePerformanceTests", // multi-stage milestone allocation gate
  "GraphicsBackendNamesTests", // assembly-wide backend naming invariants
  "MultiplexerPseudoterminalTests", // drives real tmux/screen binaries over a live pseudoterminal; asserts on raw process output, no single src subject
  "ChartSurfaceTests", // family fixture for five chart controls; compiler-verified in ComponentSurfaceCoverageTests
  "ComponentCompositionSurfaceTests", // cross-cutting shared surface/box-model/border contract spanning many controls
  "ComponentGeometrySurfaceTests", // cross-cutting shared surface/box-model/border contract spanning many controls
  "IntrinsicBorderSurfaceTests", // cross-cutting shared surface/box-model/border contract spanning many controls
  "BoxModelSurfaceTests", // cross-cutting shared surface/box-model/border contract spanning many controls
  "ComponentSurfaceTests", // cross-cutting shared surface/box-model/border contract spanning many controls
  "ActualScrollBarStyleThemeTests", // ActualScrollBarStyle proxy contract across the Container family plus TreeView/NavigationView re-declarations
  "AmbiguousWidthControlTests", // one-cell rendering under Ambiguous.Wide across five unrelated control/style types
  "CodeOwnedGlyphRenderingTests", // code-owned default glyphs survive theme swaps across five controls
  "GeneratedScrollBarStyleTests", // TreeView/NavigationView scrollbar proxy-forwarding parity, spans two test areas
  "ComponentRoleTests", // reflection-based architectural contract across the control catalog
  "SelectionParityTests", // six-control selection parity; no shared selection interface exists
  "DropDownGlyphThemingTests", // drop-down glyph anti-drift contract across InputStyle and three picker controls
  "BuiltInControlStyleTests", // theme-to-control style-profile mapping across the built-in control set
  "CuratedThemesTests", // whole-catalog invariants across every bundled theme
  "StrandedGlyphThemingTests", // stranded-glyph regression suite spanning a dozen control styles
  "StyleInvariantEnforcementTests", // one invariant held identically across many style value types and three mutation doors
  "StyleSectionRegistryTests", // assembly-wide styles.* section registry sweep
  "RandomizedLayoutTests", // whole-infrastructure fuzz spanning ownership, focus, capture, pointer, and render
  "ConsoleHostingConsumerTests", // console-hosting public-surface accessibility across five unrelated types
  "DataBindingConsumerTests", // public binding surface proven across five control types without friend access
  "FigletFontsPackedPackageConsumerTests", // whole-package pack plus external-consumer build proof
  "ComponentSurfaceCoverageTests", // whole-catalog mounted-behavior classification inventory
  "ComponentUnitCoverageTests", // whole-catalog unit-fixture coverage inventory
  "ComponentVisibilityCoverageTests", // whole-catalog Hidden/Collapsed classification inventory
  "DisplayPanelTests", // composed multi-control layout, damage, and byte-exact render proof
  "InteractiveControlTests", // eight-control family fixture for cross-control event ordering
  "TerminalInputTests", // raw terminal bytes fanned across five event kinds to final output
  "UnicodeGeometryTests", // ambiguous-width agreement across six text-consuming controls
  "CapabilityNegotiationTests", // negotiated-profile-before-first-frame ordering across TerminalProfile and Application
  "CellPolicyTests", // one immutable policy propagated from Application through ControlBase to the whole tree
  "ControlCapabilitiesTests", // capability cascade from Application resize through ControlBase inheritance
  "PostRouteAnchorMutationTests", // post-route anchor-staleness contract spanning Router, FocusManager, and Application
  "ProtocolRoutingTests", // protocol responses traversing Session, Application, and Dispatcher
  "InteractivePerformanceTests", // representative interactive-tree allocation gate spanning several composed controls
  "SyntaxDefinitionCorpusTests", // whole-embedded-catalog sweep: parses and tokenizes all 159 syntax definitions, no single src subject
]);

/**
 * Recursively collects every file below one directory whose name ends with the given extension,
 * skipping {@link ignoredDirectories}.
 *
 * @param {string} directory The directory to walk.
 * @param {string} extension The required file extension, including the leading dot.
 * @returns {Promise<string[]>} Absolute file paths.
 */
export async function findFilesWithExtension(directory, extension) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const path = resolve(directory, entry.name);

    if (entry.isDirectory()) {
      if (!ignoredDirectories.has(entry.name)) {
        files.push(...(await findFilesWithExtension(path, extension)));
      }

      continue;
    }

    if (entry.isFile() && entry.name.endsWith(extension)) {
      files.push(path);
    }
  }

  return files;
}

async function findFilesWithExtensionIfPresent(directory, extension) {
  try {
    return await findFilesWithExtension(directory, extension);
  } catch (error) {
    if (error?.code === "ENOENT") {
      return [];
    }

    throw error;
  }
}

/**
 * Collects the bare names of every type declared under `src/` and `examples/` below one root. An
 * interface named `IFoo` also contributes `Foo`, so the I-stripped convention used by controls such
 * as `IChartControl` -> `ChartSurfaceTests` resolves without a separate mapping table.
 *
 * @param {string} root The repository root.
 * @returns {Promise<Set<string>>} Every declared bare type name, plus I-stripped interface aliases.
 */
export async function discoverSubjectTypes(root) {
  const types = new Set();

  for (const directoryName of ["src", "examples"]) {
    const files = await findFilesWithExtensionIfPresent(resolve(root, directoryName), ".cs");

    for (const file of files) {
      const content = await readFile(file, "utf8");

      for (const line of content.split("\n")) {
        const match = typeDeclarationPattern.exec(line);

        if (match === null) {
          continue;
        }

        const name = match[1];
        types.add(name);

        if (/^I[A-Z]/u.test(name)) {
          types.add(name.slice(1));
        }
      }
    }
  }

  return types;
}

/**
 * Collects every top-level test class declared under `tests/` below one root. A file only
 * contributes classes when it also contains `[Fact]` or `[Theory]`, so non-test helper types that
 * happen to end in "Tests" are not swept in.
 *
 * @param {string} root The repository root.
 * @returns {Promise<{file: string, line: number, className: string}[]>} Discovered test classes,
 * with `file` relative to `root` using forward slashes.
 */
export async function discoverTestClasses(root) {
  const files = await findFilesWithExtensionIfPresent(resolve(root, "tests"), ".cs");
  const discovered = [];

  for (const file of files) {
    const content = await readFile(file, "utf8");

    if (!factOrTheoryPattern.test(content)) {
      continue;
    }

    const relativePath = relative(root, file).split(sep).join("/");
    const lines = content.split("\n");

    for (const [lineIndex, line] of lines.entries()) {
      const match = testClassPattern.exec(line);

      if (match === null) {
        continue;
      }

      discovered.push({ file: relativePath, line: lineIndex + 1, className: match[1] });
    }
  }

  return discovered;
}

/**
 * Computes every candidate base name for a test class name: one per evidence-tier suffix (including
 * the bare "Tests" suffix) that the class name ends with. A class passes if ANY candidate matches a
 * declared subject type, because the subject type itself may end in a suffix word (e.g.
 * `FloatingSurface`, whose test class `FloatingSurfaceTests` must not be judged only against the
 * "Floating" interpretation produced by stripping "SurfaceTests").
 *
 * @param {string} className The test class name; always ends in "Tests" by discovery construction.
 * @returns {string[]} Candidate base names, most tier-specific first; always at least one entry,
 * since "Tests" itself is always a matching suffix.
 */
export function candidateBases(className) {
  const bases = [];

  for (const suffix of evidenceTierSuffixes) {
    if (className.endsWith(suffix)) {
      bases.push(className.slice(0, -suffix.length));
    }
  }

  return bases;
}

/**
 * Reports the most tier-specific candidate base name, for display purposes only. Pass/fail
 * decisions use every candidate from {@link candidateBases}, not just this one.
 *
 * @param {string} className The test class name; always ends in "Tests" by discovery construction.
 * @returns {string} The primary base name to show in a violation message.
 */
export function stripLongestSuffix(className) {
  return candidateBases(className)[0];
}

function baselineKey(entry) {
  return `${entry.file}#${entry.className}`;
}

/**
 * Formats one naming violation in the house `path:line message` style.
 *
 * @param {{file: string, line: number, className: string}} entry The offending test class.
 * @returns {string} The violation message.
 */
export function formatViolation(entry) {
  const bases = candidateBases(entry.className);
  const basesText = bases.join(" or ");

  return (
    `${entry.file}:${entry.line} ${entry.className} does not name a type under src/ or ` +
    "examples/ as " +
    `${basesText} (checked Tests/SurfaceTests/PerformanceTests/ConsumerTests/CompatibilityTests ` +
    "suffixes), and is not in the suite-level allow-list or " +
    "scripts/test-class-naming-baseline.txt."
  );
}

/**
 * Computes every discovered test class that fails every candidate subject-type interpretation (see
 * {@link candidateBases}) and the suite-level allow-list, independent of the baseline. This is the
 * set the baseline ratchet is drawn from.
 *
 * @param {string} root The repository root.
 * @returns {Promise<{file: string, line: number, className: string}[]>} Unbaselined violations.
 */
export async function computeViolations(root) {
  const [subjectTypes, testClasses] = await Promise.all([
    discoverSubjectTypes(root),
    discoverTestClasses(root),
  ]);
  const violations = [];

  for (const entry of testClasses) {
    if (candidateBases(entry.className).some((base) => subjectTypes.has(base))) {
      continue;
    }

    if (SUITE_LEVEL_ALLOW_LIST.has(entry.className)) {
      continue;
    }

    violations.push(entry);
  }

  return violations;
}

/**
 * Resolves the baseline file path for one repository root.
 *
 * @param {string} root The repository root.
 * @returns {string} The absolute baseline path.
 */
export function baselinePath(root) {
  return resolve(root, "scripts", "test-class-naming-baseline.txt");
}

/**
 * Loads the grandfathered baseline for one repository root.
 *
 * @param {string} root The repository root.
 * @returns {Promise<Set<string>>} The baseline `path#ClassName` entries.
 */
export async function loadBaseline(root) {
  try {
    const content = await readFile(baselinePath(root), "utf8");
    return new Set(
      content
        .split("\n")
        .map((line) => line.trim())
        .filter((line) => line.length > 0),
    );
  } catch (error) {
    if (error?.code === "ENOENT") {
      return new Set();
    }

    throw error;
  }
}

/**
 * Validates every test class below one repository root against the naming rule.
 *
 * @param {string} root The repository root.
 * @returns {Promise<{errors: string[], staleBaselineEntries: string[]}>} `errors` holds new,
 * unbaselined violations in `path:line message` form; `staleBaselineEntries` holds baseline
 * entries that no longer reproduce (informational, non-fatal).
 */
export async function validateRepository(root) {
  const violations = await computeViolations(root);
  const violationKeys = new Set(violations.map(baselineKey));
  const baseline = await loadBaseline(root);

  const errors = violations
    .filter((entry) => !baseline.has(baselineKey(entry)))
    .map(formatViolation);
  const staleBaselineEntries = [...baseline]
    .filter((key) => !violationKeys.has(key))
    .sort();

  return { errors, staleBaselineEntries };
}

/**
 * Recomputes the baseline from the current tree and writes it, sorted and deduplicated.
 *
 * @param {string} root The repository root.
 * @returns {Promise<{keys: string[], added: string[], removed: string[]}>} The written keys, plus
 * what was added and removed relative to the previous baseline.
 */
export async function writeBaseline(root) {
  const [violations, oldBaseline] = await Promise.all([
    computeViolations(root),
    loadBaseline(root),
  ]);
  const keys = [...new Set(violations.map(baselineKey))].sort();
  const keySet = new Set(keys);

  await writeFile(baselinePath(root), keys.length === 0 ? "" : `${keys.join("\n")}\n`, "utf8");

  const added = keys.filter((key) => !oldBaseline.has(key));
  const removed = [...oldBaseline].filter((key) => !keySet.has(key)).sort();

  return { keys, added, removed };
}

async function main() {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");

  if (process.argv.includes("--write-baseline")) {
    const { keys, added, removed } = await writeBaseline(root);

    console.log(
      `Wrote ${keys.length} baseline entr${keys.length === 1 ? "y" : "ies"} to ` +
        `${relative(root, baselinePath(root))}.`,
    );
    console.log(`${added.length} added, ${removed.length} removed.`);

    for (const key of added) {
      console.log(`  + ${key}`);
    }

    for (const key of removed) {
      console.log(`  - ${key}`);
    }

    return;
  }

  const { errors, staleBaselineEntries } = await validateRepository(root);

  for (const error of errors) {
    console.error(error);
  }

  if (staleBaselineEntries.length > 0) {
    console.log(
      `${staleBaselineEntries.length} baseline entries no longer reproduce; run with ` +
        "--write-baseline to shrink the ratchet.",
    );
  }

  if (errors.length > 0) {
    process.exitCode = 1;
    return;
  }

  console.log("All test classes satisfy the naming convention (or are baselined).");
}

if (
  process.argv[1] !== undefined &&
  fileURLToPath(import.meta.url) === resolve(process.argv[1])
) {
  await main();
}
