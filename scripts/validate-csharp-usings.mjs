import { readdir, readFile } from "node:fs/promises";
import { basename, dirname, relative, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const ignoredDirectories = new Set([
  ".git",
  ".worktrees",
  "bin",
  "node_modules",
  "obj",
]);

const globalUsing = /^global\s+using\s+(.+);\s*$/gmu;
const localUsing = /^using\s+(.+);\s*$/gmu;
const namespaceDeclaration = /^namespace\s+[\p{L}_][\p{L}\p{N}_.]*\s*;/mu;
const assemblyAttribute = /^\s*\[assembly:/mu;
const aliasImport = /^([\p{L}_][\p{L}\p{N}_]*)\s*=\s*([\p{L}_][\p{L}\p{N}_]*)$/u;

async function findCSharpFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const path = resolve(directory, entry.name);

    if (entry.isDirectory()) {
      if (!ignoredDirectories.has(entry.name)) {
        files.push(...(await findCSharpFiles(path)));
      }

      continue;
    }

    if (entry.isFile() && entry.name.endsWith(".cs")) {
      files.push(path);
    }
  }

  return files;
}

function collectImports(content, pattern) {
  return [...content.matchAll(pattern)].map((match) => ({
    index: match.index,
    value: match[1].trim(),
  }));
}

function findProjectImports(path, root, importsByDirectory) {
  var directory = dirname(path);

  while (directory.startsWith(root)) {
    const imports = importsByDirectory.get(directory);

    if (imports !== undefined) {
      return imports;
    }

    if (directory === root) {
      break;
    }

    directory = dirname(directory);
  }

  return new Set();
}

/**
 * Validates that ordinary C# files place their file-scoped namespace before
 * local imports and do not repeat an import already owned by GlobalUsings.cs.
 * Assembly attribute files retain their required top-level-import exception.
 *
 * @param {string} root Directory to scan recursively.
 * @returns {Promise<string[]>} Validation messages; an empty array means success.
 */
export async function validateCSharpUsings(root) {
  const absoluteRoot = resolve(root);
  const files = await findCSharpFiles(absoluteRoot);
  const importsByDirectory = new Map();
  const contents = new Map();
  const errors = [];

  for (const path of files) {
    const content = await readFile(path, "utf8");
    contents.set(path, content);

    if (basename(path) === "GlobalUsings.cs") {
      importsByDirectory.set(
        dirname(path),
        new Set(collectImports(content, globalUsing).map((value) => value.value)),
      );
    }
  }

  for (const path of files) {
    if (basename(path) === "GlobalUsings.cs") {
      continue;
    }

    const content = contents.get(path);
    const imports = collectImports(content, localUsing);

    if (imports.length === 0) {
      continue;
    }

    const displayPath = relative(absoluteRoot, path);
    const namespaceMatch = content.match(namespaceDeclaration);

    if (!assemblyAttribute.test(content)) {
      if (namespaceMatch === null) {
        errors.push(
          `${displayPath} has top-level using directives; move top-level imports to GlobalUsings.cs.`,
        );
      } else if (imports[0].index < namespaceMatch.index) {
        errors.push(
          `${displayPath} has a using directive before its namespace declaration; the namespace declaration must precede using directives.`,
        );
      }
    }

    const projectImports = findProjectImports(path, absoluteRoot, importsByDirectory);

    for (const importValue of imports) {
      const aliasMatch = importValue.value.match(aliasImport);

      if (aliasMatch !== null && aliasMatch[1] === aliasMatch[2]) {
        errors.push(
          `${displayPath} declares the self-referential ${aliasMatch[1]} alias; an alias cannot reference itself.`,
        );
      }

      if (projectImports.has(importValue.value)) {
        errors.push(
          `${displayPath} imports ${importValue.value}, which is already declared globally by GlobalUsings.cs.`,
        );
      }
    }
  }

  return errors.sort();
}

async function main() {
  const errors = await validateCSharpUsings(process.cwd());

  if (errors.length === 0) {
    console.log("C# using directives follow namespace, project-global import, and alias rules.");
    return;
  }

  for (const error of errors) {
    console.error(error);
  }

  process.exitCode = 1;
}

const invokedPath = process.argv[1] === undefined
  ? undefined
  : pathToFileURL(resolve(process.argv[1])).href;

if (invokedPath === import.meta.url) {
  await main();
}
