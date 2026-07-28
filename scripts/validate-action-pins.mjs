import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const immutableActionPattern = /^[^@\s]+\/[^@\s]+(?:\/[^@\s]+)*@[0-9a-f]{40}$/u;
const immutableDockerPattern = /^docker:\/\/.+@sha256:[0-9a-f]{64}$/u;
const usesPattern =
    /(?:^\s*|(?:[-\[{,]\s*)+)(?:uses|"uses"|'uses')\s*:\s*(?:"([^"]*)"|'([^']*)'|([^,}\s]+))/gu;

export function validateActionPins(text, file) {
    const errors = [];
    const lines = text.split(/\r?\n/u);

    for (let index = 0; index < lines.length; index++) {
        usesPattern.lastIndex = 0;

        for (const match of lines[index].matchAll(usesPattern)) {
            const reference = match[1] ?? match[2] ?? match[3];

            if (isImmutable(reference)) {
                continue;
            }

            errors.push(
                `${file}:${index + 1}: action reference '${reference}' must use a full 40-character lowercase commit SHA`,
            );
        }
    }

    return errors;
}

function isImmutable(reference) {
    if (reference.startsWith("./")) {
        return true;
    }

    if (reference.startsWith("docker://")) {
        return immutableDockerPattern.test(reference);
    }

    return immutableActionPattern.test(reference);
}

async function findActionFiles(root) {
    const files = [];
    await collectFiles(
        path.join(root, ".github", "workflows"),
        files,
        (name) => name.endsWith(".yml") || name.endsWith(".yaml"),
    );
    await collectFiles(
        path.join(root, ".github", "actions"),
        files,
        (name) => name === "action.yml" || name === "action.yaml",
    );
    return files.sort((left, right) => left.localeCompare(right));
}

async function collectFiles(directory, files, include) {
    let entries;

    try {
        entries = await readdir(directory, { withFileTypes: true });
    } catch (error) {
        if (error.code === "ENOENT") {
            return;
        }

        throw error;
    }

    for (const entry of entries) {
        const fullPath = path.join(directory, entry.name);

        if (entry.isDirectory()) {
            await collectFiles(fullPath, files, include);
        } else if (entry.isFile() && include(entry.name)) {
            files.push(fullPath);
        }
    }
}

async function main() {
    const root = process.cwd();
    const errors = [];

    for (const file of await findActionFiles(root)) {
        const relativeFile = path
            .relative(root, file)
            .split(path.sep)
            .join("/");
        const text = await readFile(file, "utf8");
        errors.push(...validateActionPins(text, relativeFile));
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
