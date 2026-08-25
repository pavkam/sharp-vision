import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import { extname, join, relative } from "node:path";
import test from "node:test";

import {
  findGitHubIssueIdentifiers,
  findUndocumentedDocumentMutationLifecycle,
  scanRepository
} from "./validate-doc-content.mjs";

async function markdownFiles(root) {
  const entries = await readdir(root, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const path = join(root, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await markdownFiles(path)));
    } else if (extname(entry.name) === ".md") {
      files.push(path);
    }
  }

  return files;
}

test("findGitHubIssueIdentifiers_WhenProseContainsIssueReference_ReportsLine", () => {
  const markdown = [
    "Use colors#256 and `#123456`.",
    "",
    "```text",
    "Issue #123 is example data.",
    "```",
    "",
    "Issue #456 tracks this gap.",
    "https://github.com/pavkam/sharp-vision/issues/789",
    "Upstream microsoft/codecoverage#232 and",
    "https://github.com/microsoft/terminal/issues/11276 are durable citations.",
  ].join("\n");

  const errors = findGitHubIssueIdentifiers(markdown);

  assert.deepEqual(errors, [
    "line 7: GitHub issue identifier #456",
    "line 8: GitHub issue URL",
  ]);
});

test("documentation_WhenScanned_ContainsNoGitHubIssueIdentifiers", async () => {
  const root = join(import.meta.dirname, "..", "docs");
  const files = await markdownFiles(root);
  const errors = [];

  for (const file of files) {
    const markdown = await readFile(file, "utf8");

    for (const error of findGitHubIssueIdentifiers(markdown)) {
      errors.push(`${relative(root, file)}: ${error}`);
    }
  }

  assert.deepEqual(errors, []);
});

// The failing fixture the gate needs to be worth having. Every violation the scanner was extended
// to catch lives outside Markdown, so a Markdown-only fixture would have stayed green through the
// entire period the rule was going unenforced.
test("findGitHubIssueIdentifiers_WhenSourceCommentCitesAnIssue_ReportsLine", () => {
  const source = [
    "// Guard against the reflow bug (#42).",
    'var message = "#000 and #12345 are color fixtures, not issues";',
    "#pragma warning disable CA1305",
    "// Upstream platform bug microsoft/codecoverage#232 stays resolvable.",
    "// https://github.com/microsoft/terminal/issues/11276 does too.",
    "// https://github.com/pavkam/sharp-vision/issues/7 does not."
  ].join("\n");

  const errors = findGitHubIssueIdentifiers(source, { fenced: false, literals: true });

  assert.deepEqual(errors, [
    "line 1: GitHub issue identifier #42",
    "line 6: GitHub issue URL"
  ]);
});

test("findGitHubIssueIdentifiers_WhenLiteralsAreNotStripped_StillSeesTheLiteral", () => {
  const source = 'var message = "tracked in #42";';

  const errors = findGitHubIssueIdentifiers(source, { fenced: false, literals: false });

  assert.deepEqual(errors, ["line 1: GitHub issue identifier #42"]);
});

test("findUndocumentedDocumentMutationLifecycle_WhenGuardedSetterOmitsExceptions_ReportsContract", () => {
  const source = [
    "/// <summary>Gets or sets text.</summary>",
    "/// <exception cref=\"ArgumentNullException\">The value is null.</exception>",
    "public string Text",
    "{",
    "    get;",
    "    set",
    "    {",
    "        VerifyMutable();",
    "    }",
    "}"
  ].join("\n");

  const errors = findUndocumentedDocumentMutationLifecycle(source);

  assert.deepEqual(errors, [
    "line 3: public document mutator omits InvalidOperationException and ObjectDisposedException"
  ]);
});

test("findUndocumentedDocumentMutationLifecycle_WhenBothExceptionsAreDocumented_Passes", () => {
  const source = [
    "/// <summary>Gets or sets text.</summary>",
    "/// <exception cref=\"InvalidOperationException\">Off dispatcher.</exception>",
    "/// <exception cref=\"ObjectDisposedException\">Disposed.</exception>",
    "public string Text",
    "{",
    "    set { VerifyMutable(); }",
    "}"
  ].join("\n");

  assert.deepEqual(findUndocumentedDocumentMutationLifecycle(source), []);
});

test("repository_WhenScanned_ContainsNoGitHubIssueReferences", async () => {
  const errors = await scanRepository(join(import.meta.dirname, ".."));

  assert.deepEqual(errors, []);
});
