import { lstat, readFile, readdir } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { fileURLToPath, pathToFileURL } from "node:url";

const ignoredDirectories = new Set([
    ".git",
    ".worktrees",
    "bin",
    "node_modules",
    "obj",
]);
const resourceNames = new Set([
    "DerivedCoreProperties.txt",
    "EastAsianWidth.txt",
    "emoji-data.txt",
    "GraphemeBreakProperty.txt",
    "GraphemeBreakTest.txt",
    "ReadMe.txt",
    "UnicodeData.txt",
]);
const referenceExtensions = new Set([
  ".csproj",
  ".js",
  ".json",
  ".mjs",
  ".props",
  ".sh",
  ".targets",
  ".yaml",
  ".yml",
]);

const isResource = (file) =>
    /\.(?:flf|tlf|zip)$/iu.test(file) || resourceNames.has(path.basename(file));

const walk = async (root, current = root) => {
    const files = [];

    for (const entry of await readdir(current, { withFileTypes: true })) {
        if (entry.isSymbolicLink()) continue;
        if (entry.isDirectory() && ignoredDirectories.has(entry.name)) continue;

        const absolute = path.join(current, entry.name);

        if (entry.isDirectory()) {
            files.push(...(await walk(root, absolute)));
        } else if (entry.isFile()) {
            files.push(path.relative(root, absolute));
        }
    }

    return files;
};

const requireFile = async (file, message) => {
    try {
        const stats = await lstat(file);

        if (!stats.isFile()) throw new Error(message);
    } catch (error) {
        if (error instanceof Error && error.message === message) throw error;
        throw new Error(message, { cause: error });
    }
};

export const validateExtern = async (root) => {
    if (typeof root !== "string" || root.length === 0) {
        throw new TypeError("root must be a non-empty path.");
    }

    const absoluteRoot = path.resolve(root);
    const extern = path.join(absoluteRoot, "extern");

    await requireFile(
        path.join(extern, "README.md"),
        "extern/README.md is required.",
    );

    try {
        const stats = await lstat(path.join(absoluteRoot, "data"));

        if (stats.isDirectory()) {
            throw new Error(
                "The legacy data directory is not allowed; move resources to extern.",
            );
        }
    } catch (error) {
        if (
            error instanceof Error &&
            "code" in error &&
            error.code === "ENOENT"
        ) {
            // Absence is the required state.
        } else {
            throw error;
        }
    }

    const files = await walk(absoluteRoot);
    const misplaced = files.find(
        (file) => isResource(file) && !file.startsWith(`extern${path.sep}`),
    );

  if (misplaced !== undefined) {
    throw new Error(`External resource '${misplaced}' is outside extern.`);
  }

  for (const file of files.filter((value) => referenceExtensions.has(path.extname(value)))) {
    const content = await readFile(path.join(absoluteRoot, file), "utf8");

    if (/data[\\/](?:figlet|unicode)[\\/]/u.test(content)) {
      throw new Error(`File '${file}' contains a legacy resource path.`);
    }
  }

    const packages = (await readdir(extern, { withFileTypes: true })).filter(
        (entry) => entry.isDirectory() && !entry.isSymbolicLink(),
    );

    for (const entry of packages) {
        const directory = path.join(extern, entry.name);
        const names = await readdir(directory);

        if (!names.includes("README.md")) {
            throw new Error(
                `extern/${entry.name} requires a README with provenance.`,
            );
        }

        if (
            !names.some((name) =>
                /^(?:license|notice|copying)(?:\.|$)/iu.test(name),
            )
        ) {
            throw new Error(
                `extern/${entry.name} requires license or notice material.`,
            );
        }
    }
};

const main = async () => {
    const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
    const root = path.resolve(scriptDirectory, "..");
    await validateExtern(root);
    process.stdout.write(
        "External resources are documented and contained in extern.\n",
    );
};

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
    await main();
}
