import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { validateActionPins } from "./validate-action-pins.mjs";

const script = join(
    dirname(fileURLToPath(import.meta.url)),
    "validate-action-pins.mjs",
);

test("validateActionPins_WhenReferencesAreImmutable_ReturnsNoErrors", () => {
    const yaml = [
        "steps:",
        "  - uses: ./.github/actions/setup",
        "  - uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
        '  - uses: "actions/setup-dotnet@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0" # immutable pin',
        `  - uses: docker://alpine@sha256:${"a".repeat(64)}`,
        "",
    ].join("\n");

    assert.deepEqual(validateActionPins(yaml, "ci.yml"), []);
});

test("validateActionPins_WhenReferencesFloat_ReportsEachReferenceWithItsLine", () => {
    const yaml = [
        "steps:",
        "  - uses: actions/checkout@v7",
        "  - uses: owner/action@main",
        "  - uses: owner/action@1234abc",
        `  - uses: owner/action@${"A".repeat(40)}`,
        "",
    ].join("\n");

    const errors = validateActionPins(yaml, "ci.yml");

    assert.equal(errors.length, 4);
    assert.match(errors[0], /ci\.yml:2:.*actions\/checkout@v7/u);
    assert.match(errors[1], /ci\.yml:3:.*owner\/action@main/u);
    assert.match(errors[2], /ci\.yml:4:.*owner\/action@1234abc/u);
    assert.match(errors[3], /ci\.yml:5:.*owner\/action@A{40}/u);
});

test("validateActionPins_WhenUsesKeysAreQuotedOrInline_ReportsFloatingReferences", () => {
    const yaml = [
        'steps: [{ "uses": actions/checkout@v7 }]',
        "  - { 'uses': owner/action@main }",
        "",
    ].join("\n");

    const errors = validateActionPins(yaml, "ci.yml");

    assert.equal(errors.length, 2);
    assert.match(errors[0], /ci\.yml:1:.*actions\/checkout@v7/u);
    assert.match(errors[1], /ci\.yml:2:.*owner\/action@main/u);
});

test("validateActionPins_WhenNamedStepFloats_ReportsIndentedUsesKey", () => {
    const yaml = [
        "steps:",
        "  - name: Check out repository",
        "    uses: actions/checkout@v7",
        "",
    ].join("\n");

    const errors = validateActionPins(yaml, "ci.yml");

    assert.equal(errors.length, 1);
    assert.match(errors[0], /ci\.yml:3:.*actions\/checkout@v7/u);
});

test("cli_WhenNestedWorkflowAndCompositeActionFloat_ReportsBothFiles", async () => {
    const root = await mkdtemp(join(tmpdir(), "sharpvision-action-pins-"));

    try {
        const workflow = join(root, ".github", "workflows", "nested", "ci.yml");
        const action = join(root, ".github", "actions", "setup", "action.yml");
        await mkdir(dirname(workflow), { recursive: true });
        await mkdir(dirname(action), { recursive: true });
        await writeFile(workflow, "steps:\n  - uses: actions/checkout@v7\n");
        await writeFile(
            action,
            "runs:\n  steps:\n    - uses: owner/action@main\n",
        );

        const result = await runCli(root);

        assert.equal(result.exitCode, 1);
        assert.match(
            result.standardError,
            /\.github\/actions\/setup\/action\.yml:3:/u,
        );
        assert.match(
            result.standardError,
            /\.github\/workflows\/nested\/ci\.yml:2:/u,
        );
    } finally {
        await rm(root, { recursive: true, force: true });
    }
});

async function runCli(workingDirectory) {
    return await new Promise((resolve, reject) => {
        const child = spawn(process.execPath, [script], {
            cwd: workingDirectory,
            stdio: ["ignore", "pipe", "pipe"],
        });
        let standardOutput = "";
        let standardError = "";

        child.stdout.setEncoding("utf8");
        child.stdout.on("data", (value) => {
            standardOutput += value;
        });
        child.stderr.setEncoding("utf8");
        child.stderr.on("data", (value) => {
            standardError += value;
        });
        child.on("error", reject);
        child.on("close", (exitCode) => {
            resolve({ exitCode, standardOutput, standardError });
        });
    });
}
