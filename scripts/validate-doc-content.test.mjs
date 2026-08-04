import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import { extname, join, relative } from "node:path";
import test from "node:test";

import { findGitHubIssueIdentifiers } from "./validate-doc-content.mjs";

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
    "https://github.com/example/project/issues/789",
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
