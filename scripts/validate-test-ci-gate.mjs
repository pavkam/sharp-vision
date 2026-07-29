// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const maskingPattern = /\|\|\s*(?:echo\b|true\b|:)/u;

/**
 * Validates that the Makefile `test-ci` recipe cannot silently mask a failing
 * test command and that it exercises every test project the publish workflow
 * depends on for a verified public API surface.
 *
 * @param {string} text The full contents of the Makefile.
 * @returns {string[]} Human-readable violations, empty when the gate is sound.
 */
export function validateTestCiGate(text) {
    const errors = [];
    const recipe = extractRecipe(text, "test-ci");

    if (recipe === null) {
        errors.push("Makefile: no 'test-ci' target was found.");
        return errors;
    }

    const lines = recipe.split(/\r?\n/u);

    for (const line of lines) {
        if (maskingPattern.test(line)) {
            errors.push(
                `Makefile: test-ci recipe line masks a failing command's exit status: '${line.trim()}'`,
            );
        }
    }

    const requiredProjects = [
        "tests/SharpVision.Terminal.Tests",
        "tests/SharpVision.Tests",
        "tests/SharpVision.Compatibility.Tests",
    ];

    for (const project of requiredProjects) {
        if (!recipe.includes(project)) {
            errors.push(`Makefile: test-ci recipe does not run '${project}'.`);
        }
    }

    return errors;
}

/**
 * Validates that the shared build-and-test composite action fails the job
 * when published test results record a failure, rather than only annotating
 * the run while returning success.
 *
 * @param {string} text The full contents of the composite action YAML.
 * @returns {string[]} Human-readable violations, empty when the gate is sound.
 */
export function validatePublishResultGate(text) {
    const errors = [];

    if (!/publish-unit-test-result-action/u.test(text)) {
        errors.push(
            "build-and-test action.yml: no publish-unit-test-result-action step was found.",
        );
        return errors;
    }

    if (!/action_fail:\s*(?:true|'true'|"true")/u.test(text)) {
        errors.push(
            "build-and-test action.yml: publish-unit-test-result-action must set action_fail: true.",
        );
    }

    return errors;
}

function extractRecipe(text, target) {
    const lines = text.split(/\r?\n/u);
    const header = new RegExp(`^${target}\\s*:`, "u");
    const startIndex = lines.findIndex((line) => header.test(line));

    if (startIndex === -1) {
        return null;
    }

    const recipeLines = [];

    for (let index = startIndex + 1; index < lines.length; index++) {
        const line = lines[index];

        if (line.length > 0 && !line.startsWith("\t")) {
            break;
        }

        recipeLines.push(line);
    }

    return recipeLines.join("\n");
}

async function main() {
    const root = process.cwd();
    const errors = [];

    const makefile = await readFile(path.join(root, "Makefile"), "utf8");
    errors.push(...validateTestCiGate(makefile));

    const actionPath = path.join(
        root,
        ".github",
        "actions",
        "build-and-test",
        "action.yml",
    );
    const action = await readFile(actionPath, "utf8");
    errors.push(...validatePublishResultGate(action));

    for (const error of errors) {
        console.error(error);
    }

    if (errors.length !== 0) {
        process.exitCode = 1;
    }
}

if (
    process.argv[1] !== undefined &&
    path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
    await main();
}
