import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";
import { relative, resolve } from "node:path";
import test from "node:test";

const showcaseRoot = resolve(
    import.meta.dirname,
    "..",
    "examples",
    "Showcase",
);
const appearanceAssignment =
    /\b(?:Foreground|Background|BorderColor|ShadowForeground|ShadowBackground|UnderlineColor|FillColor|TrackColor|ThumbColor|ButtonColor|HeaderForeground|HeaderBackground|GridLineColor|DividerColor|SelectionIndicatorColor|HeadColor|TrailColor|IndeterminateColor)\s*=|\bValue\s*=\s*Color\.|\.SetAppearance\s*\(|\bAppearance\s*=/gu;
const conceptAssignments = new Map([
    ["Panes/ColorPickerPane.cs|Value = Color.", 2],
    ["Panes/ChaseIndicatorPane.cs|HeadColor =", 2],
    ["Panes/ChaseIndicatorPane.cs|TrailColor =", 1],
    ["Panes/ChaseIndicatorPane.cs|TrackColor =", 1],
    ["Panes/TabControlPane.cs|DividerColor =", 2],
    ["Panes/TabControlPane.cs|SelectionIndicatorColor =", 2],
]);

test("Showcase_WhenControlsAreComposed_UsesSemanticDefaultsOutsideAppearanceConcepts", async () => {
    const files = await findCSharpFiles(showcaseRoot);
    const violations = [];
    const remainingConceptAssignments = new Map(conceptAssignments);

    for (const path of files) {
        const content = await readFile(path, "utf8");
        const relativePath = relative(showcaseRoot, path);

        for (const match of content.matchAll(appearanceAssignment)) {
            const line = content.slice(0, match.index).split("\n").length;
            const assignment = match[0].trim();
            const key = `${relativePath}|${assignment}`;
            const remaining = remainingConceptAssignments.get(key) ?? 0;

            if (remaining > 0) {
                remainingConceptAssignments.set(key, remaining - 1);
            } else {
                violations.push(`${relativePath}:${line}: ${assignment}`);
            }
        }
    }

    const missingConceptAssignments = [...remainingConceptAssignments]
        .filter(([, count]) => count !== 0)
        .map(([key, count]) => `${key} (${count} missing)`);

    assert.deepEqual(
        [...violations, ...missingConceptAssignments],
        [],
        "Only dedicated appearance concept pages may assign the exact options they demonstrate.",
    );
});

async function findCSharpFiles(directory) {
    const entries = await readdir(directory, { withFileTypes: true });
    const files = [];

    for (const entry of entries) {
        const path = resolve(directory, entry.name);

        if (entry.isDirectory()) {
            files.push(...(await findCSharpFiles(path)));
        } else if (entry.isFile() && entry.name.endsWith(".cs")) {
            files.push(path);
        }
    }

    return files.sort();
}
