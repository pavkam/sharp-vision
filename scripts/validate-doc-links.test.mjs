import assert from "node:assert/strict";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { validateMarkdownTree } from "./validate-doc-links.mjs";

test("validateMarkdownTree_WhenLinksAreValid_ReturnsNoErrors", async () => {
  const root = await mkdtemp(join(tmpdir(), "sharpvision-links-"));

  try {
    await writeFile(join(root, "index.md"), "[Details](guide.md#exact-section)\n");
    await writeFile(join(root, "guide.md"), "# Exact section\n");

    const errors = await validateMarkdownTree(root);

    assert.deepEqual(errors, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateMarkdownTree_WhenFragmentIsMissing_ReportsTarget", async () => {
  const root = await mkdtemp(join(tmpdir(), "sharpvision-links-"));

  try {
    await writeFile(join(root, "index.md"), "[Details](guide.md#missing)\n");
    await writeFile(join(root, "guide.md"), "# Existing section\n");

    const errors = await validateMarkdownTree(root);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /index\.md.*guide\.md#missing.*missing section/i);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateMarkdownTree_WhenFileIsMissing_ReportsTarget", async () => {
  const root = await mkdtemp(join(tmpdir(), "sharpvision-links-"));

  try {
    await writeFile(join(root, "index.md"), "[Details](missing.md)\n");

    const errors = await validateMarkdownTree(root);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /index\.md.*missing\.md.*missing file/i);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateMarkdownTree_WhenLinkIsInsideCodeFence_IgnoresLink", async () => {
  const root = await mkdtemp(join(tmpdir(), "sharpvision-links-"));

  try {
    await writeFile(join(root, "index.md"), "```md\n[Example](missing.md)\n```\n");

    const errors = await validateMarkdownTree(root);

    assert.deepEqual(errors, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
