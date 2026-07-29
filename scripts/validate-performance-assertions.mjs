// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const elapsedShouldPattern = /\bElapsed\b(?:[^\n;]|\n)*?\.Should[A-Za-z]*\(/u;

/**
 * Validates that an ordinary (non-dedicated-benchmark) performance test file
 * does not assert against wall-clock elapsed time. Ordinary CI and local
 * machines are not stable enough to gate on timing; only allocation and
 * semantic assertions may gate this lane. Dedicated benchmark suites are
 * expected to live outside the ordinary `Performance` test folders and are
 * therefore out of scope for this check.
 *
 * @param {string} text The full contents of a candidate test file.
 * @param {string} file The file path, used for diagnostics.
 * @returns {string[]} Human-readable violations, empty when the file is clean.
 */
export function validatePerformanceAssertions(text, file) {
    const errors = [];
    const lines = text.split(/\r?\n/u);

    for (let index = 0; index < lines.length; index++) {
        const window = lines.slice(index, index + 3).join("\n");

        if (elapsedShouldPattern.test(window) && lines[index].includes("Elapsed")) {
            errors.push(
                `${file}:${index + 1}: an ordinary performance test must not assert against elapsed time; report it instead.`,
            );
        }
    }

    return errors;
}

async function findPerformanceTestFiles(root) {
    const files = [];
    await collect(path.join(root, "tests"), files);
    return files.sort((left, right) => left.localeCompare(right));
}

async function collect(directory, files) {
    let entries;

    try {
        entries = await readdir(directory, { withFileTypes: true });
    } catch {
        return;
    }

    for (const entry of entries) {
        const full = path.join(directory, entry.name);

        if (entry.isDirectory()) {
            if (entry.name === "bin" || entry.name === "obj") {
                continue;
            }

            await collect(full, files);
        } else if (
            entry.isFile() &&
            entry.name.endsWith(".cs") &&
            full.split(path.sep).includes("Performance")
        ) {
            files.push(full);
        }
    }
}

async function main() {
    const root = process.cwd();
    const errors = [];

    for (const file of await findPerformanceTestFiles(root)) {
        const relativeFile = path
            .relative(root, file)
            .split(path.sep)
            .join("/");
        const text = await readFile(file, "utf8");
        errors.push(...validatePerformanceAssertions(text, relativeFile));
    }

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
