import assert from "node:assert/strict";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import { validateDocumentationStructure } from "./validate-doc-structure.mjs";

async function withDocs(files, action) {
    const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-docs-"));

    try {
        for (const [relativePath, content] of Object.entries(files)) {
            const target = path.join(root, relativePath);
            await mkdir(path.dirname(target), { recursive: true });
            await writeFile(target, content, "utf8");
        }

        await action(root);
    } finally {
        await rm(root, { recursive: true, force: true });
    }
}

test("validateDocumentationStructure_WhenControlUsesCanonicalSpine_ReturnsNoErrors", async () => {
    await withDocs(
        {
            "docs/controls/input/sample.md": [
                "# Sample",
                "",
                "## Sample contract",
                "",
                "## API",
                "",
                "| Member | Contract |",
                "| --- | --- |",
                "| `Value` | Observable value. |",
                "",
                "## Behavior",
                "",
                "## Example",
                "",
                "## Test obligations",
                "",
            ].join("\n"),
        },
        async (root) => {
            assert.deepEqual(await validateDocumentationStructure(root), []);
        },
    );
});

test("validateDocumentationStructure_WhenControlSpineIsIncomplete_ReportsFile", async () => {
    await withDocs(
        {
            "docs/controls/sample.md":
                "# Sample\n\n## Sample contract\n\n## Example\n",
        },
        async (root) => {
            const errors = await validateDocumentationStructure(root);
            assert.equal(errors.length, 1);
            assert.match(errors[0], /docs\/controls\/sample\.md/u);
            assert.match(errors[0], /API/u);
        },
    );
});

test("validateDocumentationStructure_WhenHeadingIsInsideFence_IgnoresIt", async () => {
    await withDocs(
        {
            "docs/controls/sample.md": [
                "# Sample",
                "",
                "## Sample contract",
                "",
                "## API",
                "",
                "| Member | Contract |",
                "| --- | --- |",
                "| `Value` | Observable value. |",
                "",
                "```markdown",
                "## Test obligations",
                "```",
                "",
                "## Example",
                "",
                "## Test obligations",
                "",
            ].join("\n"),
        },
        async (root) => {
            assert.deepEqual(await validateDocumentationStructure(root), []);
        },
    );
});

test("validateDocumentationStructure_WhenConceptLacksProofSection_ReportsFile", async () => {
    await withDocs(
        {
            "docs/concepts/sample.md": "# Sample\n\n## Sample contract\n",
        },
        async (root) => {
            const errors = await validateDocumentationStructure(root);
            assert.equal(errors.length, 1);
            assert.match(errors[0], /docs\/concepts\/sample\.md/u);
            assert.match(errors[0], /Test obligations/u);
        },
    );
});

test("validateDocumentationStructure_WhenProtocolLacksSources_ReportsFile", async () => {
    await withDocs(
        {
            "docs/protocols/sample.md": [
                "# Sample",
                "",
                "## Sample contract",
                "",
                "## Test obligations",
                "",
            ].join("\n"),
        },
        async (root) => {
            const errors = await validateDocumentationStructure(root);
            assert.equal(errors.length, 1);
            assert.match(errors[0], /docs\/protocols\/sample\.md/u);
            assert.match(errors[0], /Sources/u);
        },
    );
});

test("validateDocumentationStructure_WhenDialogSectionsAreOutOfOrder_ReportsFile", async () => {
    await withDocs(
        {
            "docs/dialogs/sample.md": [
                "# Sample",
                "",
                "## Sample contract",
                "",
                "## API",
                "",
                "## Example",
                "",
                "## Interaction",
                "",
                "## Test obligations",
                "",
            ].join("\n"),
        },
        async (root) => {
            const errors = await validateDocumentationStructure(root);
            assert.equal(errors.length, 1);
            assert.match(errors[0], /docs\/dialogs\/sample\.md/u);
            assert.match(errors[0], /Example/u);
        },
    );
});
