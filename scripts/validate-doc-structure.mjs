import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const canonicalControlSections = [
    /^## .+ contract$/u,
    /^## API$/u,
    /^## Example$/u,
    /^## Test obligations$/u,
];

const canonicalDialogSections = [
    /^## .+ contract$/u,
    /^## API$/u,
    /^## Interaction$/u,
    /^## Example$/u,
    /^## Test obligations$/u,
];

const categorySpines = new Map([
    ["docs/concepts/", [/^## .+ contract$/u, /^## Test obligations$/u]],
    ["docs/architecture/", [/^## .+ contract$/u, /^## Test obligations$/u]],
    ["docs/protocols/", [/^## .+ contract$/u, /^## Test obligations$/u]],
    ["docs/testing/", [/^## .+ contract$/u, /^## Required evidence$/u]],
]);

async function markdownFiles(directory) {
    const entries = await readdir(directory, { withFileTypes: true });
    const files = [];

    for (const entry of entries) {
        const entryPath = path.join(directory, entry.name);

        if (entry.isDirectory()) {
            files.push(...(await markdownFiles(entryPath)));
        } else if (entry.isFile() && entry.name.endsWith(".md")) {
            files.push(entryPath);
        }
    }

    return files.sort();
}

function headings(content) {
    const result = [];
    let fenced = false;

    for (const [index, line] of content.split(/\r?\n/u).entries()) {
        if (/^\s*```/u.test(line)) {
            fenced = !fenced;
            continue;
        }

        if (!fenced && /^#{1,2} /u.test(line)) {
            result.push({ line, number: index + 1 });
        }
    }

    return result;
}

function validateControl(relativePath, documentHeadings, content, errors) {
    if (relativePath === "docs/controls/index.md") {
        return;
    }

    const h2 = documentHeadings.filter(({ line }) => line.startsWith("## "));
    let previous = -1;

    for (const expected of canonicalControlSections) {
        const found = h2.findIndex(
            ({ line }, index) => index > previous && expected.test(line),
        );

        if (found < 0) {
            errors.push(
                `${relativePath} is missing an ordered section matching ${expected}`,
            );
            return;
        }

        previous = found;
    }

    if (h2.at(-1)?.line !== "## Test obligations") {
        errors.push(`${relativePath} must end with '## Test obligations'`);
    }

    const lines = content.split(/\r?\n/u);
    const apiStart = lines.findIndex((line) => line === "## API");
    const apiEnd = lines.findIndex(
        (line, index) => index > apiStart && line.startsWith("## "),
    );
    const apiLines = lines.slice(
        apiStart + 1,
        apiEnd < 0 ? lines.length : apiEnd,
    );

    if (!apiLines.some((line) => line.startsWith("|"))) {
        errors.push(
            `${relativePath} must summarize its API with a Markdown table`,
        );
    }
}

function validateOrderedSections(
    relativePath,
    documentHeadings,
    required,
    errors,
) {
    const h2 = documentHeadings.filter(({ line }) => line.startsWith("## "));
    let previous = -1;

    for (const expected of required) {
        const found = h2.findIndex(
            ({ line }, index) => index > previous && expected.test(line),
        );

        if (found < 0) {
            errors.push(
                `${relativePath} is missing an ordered section matching ${expected}`,
            );
            return;
        }

        previous = found;
    }
}

function validateCategory(relativePath, documentHeadings, required, errors) {
    if (
        relativePath.endsWith("/index.md") ||
        relativePath === "docs/protocols/coverage-matrix.md"
    ) {
        return;
    }

    const h2 = documentHeadings.filter(({ line }) => line.startsWith("## "));

    if (!required[0].test(h2[0]?.line ?? "")) {
        errors.push(
            `${relativePath} must start with a section matching ${required[0]}`,
        );
    }

    if (!required.at(-1).test(h2.at(-1)?.line ?? "")) {
        errors.push(
            `${relativePath} must end with a section matching ${required.at(-1)}`,
        );
    }

    if (relativePath.startsWith("docs/protocols/")) {
        const sources = h2.findIndex(({ line }) => line === "## Sources");
        const tests = h2.findIndex(
            ({ line }) => line === "## Test obligations",
        );

        if (sources < 0 || sources >= tests) {
            errors.push(
                `${relativePath} must contain '## Sources' before '## Test obligations'`,
            );
        }
    }
}

export async function validateDocumentationStructure(root) {
    const docs = path.join(root, "docs");
    const files = await markdownFiles(docs);
    const errors = [];

    for (const file of files) {
        const relativePath = path
            .relative(root, file)
            .replaceAll(path.sep, "/");
        const content = await readFile(file, "utf8");
        const documentHeadings = headings(content);
        const h1 = documentHeadings.filter(({ line }) => line.startsWith("# "));

        if (h1.length !== 1 || h1[0]?.number !== 1) {
            errors.push(`${relativePath} must have exactly one H1 on line 1`);
        }

        if (relativePath.startsWith("docs/controls/")) {
            validateControl(relativePath, documentHeadings, content, errors);
        }

        if (
            relativePath.startsWith("docs/dialogs/") &&
            relativePath !== "docs/dialogs/index.md"
        ) {
            validateOrderedSections(
                relativePath,
                documentHeadings,
                canonicalDialogSections,
                errors,
            );
        }

        for (const [prefix, required] of categorySpines) {
            if (relativePath.startsWith(prefix)) {
                validateCategory(
                    relativePath,
                    documentHeadings,
                    required,
                    errors,
                );
                break;
            }
        }
    }

    return errors;
}

async function main() {
    const root = process.cwd();
    const errors = await validateDocumentationStructure(root);

    if (errors.length > 0) {
        for (const error of errors) {
            console.error(error);
        }

        process.exitCode = 1;
        return;
    }

    console.log("Documentation structure is valid.");
}

const invokedPath = process.argv[1] ? path.resolve(process.argv[1]) : undefined;

if (invokedPath === fileURLToPath(import.meta.url)) {
    await main();
}
