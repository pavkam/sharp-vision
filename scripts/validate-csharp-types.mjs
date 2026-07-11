import { readdir, readFile } from "node:fs/promises";
import { basename, relative, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const ignoredDirectories = new Set([
  ".git",
  ".worktrees",
  "bin",
  "node_modules",
  "obj",
]);

const generatedFile = /(?:\.g|\.generated|\.designer)\.cs$/iu;
const ordinaryType = /\b(?:class|enum|interface|struct)\s+([\p{L}_][\p{L}\p{N}_]*)/gu;
const recordType = /\brecord(?:\s+(?:class|struct))?\s+([\p{L}_][\p{L}\p{N}_]*)/gu;
const delegateType = /\bdelegate\b([^;{]*?)\(/gu;
const identifier = /[\p{L}_][\p{L}\p{N}_]*/gu;

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

    if (entry.isFile() && entry.name.endsWith(".cs") && !generatedFile.test(entry.name)) {
      files.push(path);
    }
  }

  return files;
}

function collectTypes(content) {
  const visible = stripTriviaAndLiterals(content);
  const types = [];

  for (const match of visible.matchAll(recordType)) {
    types.push({ index: match.index, name: match[1] });
  }

  const recordRanges = [...visible.matchAll(recordType)].map((match) => ({
    end: match.index + match[0].length,
    start: match.index,
  }));

  for (const match of visible.matchAll(ordinaryType)) {
    if (!recordRanges.some((range) => match.index >= range.start && match.index < range.end)) {
      types.push({ index: match.index, name: match[1] });
    }
  }

  for (const match of visible.matchAll(delegateType)) {
    const names = [...match[1].matchAll(identifier)];
    const name = names.at(-1)?.[0];

    if (name !== undefined) {
      types.push({ index: match.index, name });
    }
  }

  return types
    .sort((left, right) => left.index - right.index)
    .map((value) => value.name);
}

function stripTriviaAndLiterals(content) {
  const result = [...content];
  var index = 0;

  while (index < content.length) {
    if (content[index] === "/" && content[index + 1] === "/") {
      index = eraseUntil(content, result, index, "\n", false);
      continue;
    }

    if (content[index] === "/" && content[index + 1] === "*") {
      index = eraseUntil(content, result, index, "*/", true);
      continue;
    }

    const quoteCount = countRun(content, index, '"');

    if (quoteCount >= 3) {
      index = eraseRawString(content, result, index, quoteCount);
      continue;
    }

    if (content[index] === "@" && content[index + 1] === '"') {
      index = eraseVerbatimString(content, result, index);
      continue;
    }

    if (content[index] === '"' || content[index] === "'") {
      index = eraseEscapedLiteral(content, result, index, content[index]);
      continue;
    }

    index++;
  }

  return result.join("");
}

function eraseUntil(content, result, start, terminator, includeTerminator) {
  const found = content.indexOf(terminator, start + 2);
  const end = found < 0
    ? content.length
    : found + (includeTerminator ? terminator.length : 0);

  erase(result, start, end);
  return end;
}

function eraseRawString(content, result, start, delimiterLength) {
  const delimiter = '"'.repeat(delimiterLength);
  const found = content.indexOf(delimiter, start + delimiterLength);
  const end = found < 0 ? content.length : found + delimiterLength;

  erase(result, start, end);
  return end;
}

function eraseVerbatimString(content, result, start) {
  var index = start + 2;

  while (index < content.length) {
    if (content[index] === '"' && content[index + 1] === '"') {
      index += 2;
      continue;
    }

    if (content[index] === '"') {
      index++;
      break;
    }

    index++;
  }

  erase(result, start, index);
  return index;
}

function eraseEscapedLiteral(content, result, start, terminator) {
  var index = start + 1;

  while (index < content.length) {
    if (content[index] === "\\") {
      index += 2;
      continue;
    }

    if (content[index] === terminator) {
      index++;
      break;
    }

    index++;
  }

  erase(result, start, index);
  return index;
}

function erase(result, start, end) {
  for (var index = start; index < end; index++) {
    if (result[index] !== "\n" && result[index] !== "\r") {
      result[index] = " ";
    }
  }
}

function countRun(content, start, value) {
  var length = 0;

  while (content[start + length] === value) {
    length++;
  }

  return length;
}

/**
 * Validates that each non-generated C# file contains at most one named type and
 * that a type-containing file is named exactly after that type.
 *
 * @param {string} root Directory to scan recursively.
 * @returns {Promise<string[]>} Validation messages; an empty array means success.
 */
export async function validateCSharpTypes(root) {
  const absoluteRoot = resolve(root);
  const files = await findCSharpFiles(absoluteRoot);
  const errors = [];

  for (const path of files) {
    const types = collectTypes(await readFile(path, "utf8"));
    const displayPath = relative(absoluteRoot, path);

    if (types.length > 1) {
      errors.push(
        `${displayPath} declares ${types.join(", ")}; each non-generated file must contain exactly one named type.`,
      );
      continue;
    }

    if (types.length === 1) {
      const expected = `${types[0]}.cs`;

      if (basename(path) !== expected) {
        errors.push(`${displayPath} declares ${types[0]} and must be named ${expected}.`);
      }
    }
  }

  return errors;
}

async function main() {
  const errors = await validateCSharpTypes(process.cwd());

  if (errors.length === 0) {
    console.log("Every non-generated C# named type has its own exactly named file.");
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
