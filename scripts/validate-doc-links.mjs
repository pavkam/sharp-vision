import { readdir, readFile, stat } from "node:fs/promises";
import { dirname, relative, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const ignoredDirectories = new Set([
  ".git",
  ".worktrees",
  "artifacts",
  "bin",
  "node_modules",
  "obj",
]);

const externalSchemes = /^(?:data|http|https|mailto|tel):/iu;
const markdownLink = /!?\[[^\]]*\]\((?<target><[^>]+>|[^\s)]+)(?:\s+["'][^"']*["'])?\)/gu;

function stripCodeFences(content) {
  var insideFence = false;

  return content
    .split("\n")
    .map((line) => {
      if (/^\s{0,3}(?:```|~~~)/u.test(line)) {
        insideFence = !insideFence;
        return "";
      }

      return insideFence ? "" : line;
    })
    .join("\n");
}

function toAnchor(heading) {
  return heading
    .replace(/<[^>]*>/gu, "")
    .replace(/[`*_~]/gu, "")
    .trim()
    .toLocaleLowerCase("en-US")
    .replace(/[^\p{L}\p{N}\s_-]/gu, "")
    .replace(/\s/gu, "-");
}

function collectAnchors(content) {
  const anchors = new Set();
  const duplicateCounts = new Map();

  for (const line of stripCodeFences(content).split("\n")) {
    const match = /^\s{0,3}#{1,6}\s+(?<heading>.*?)(?:\s+#+)?\s*$/u.exec(line);

    if (match?.groups?.heading === undefined) {
      continue;
    }

    const baseAnchor = toAnchor(match.groups.heading);
    const duplicateCount = duplicateCounts.get(baseAnchor) ?? 0;
    const anchor = duplicateCount === 0 ? baseAnchor : `${baseAnchor}-${duplicateCount}`;

    duplicateCounts.set(baseAnchor, duplicateCount + 1);
    anchors.add(anchor);
  }

  return anchors;
}

async function findMarkdownFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const path = resolve(directory, entry.name);

    if (entry.isDirectory()) {
      if (!ignoredDirectories.has(entry.name)) {
        files.push(...(await findMarkdownFiles(path)));
      }

      continue;
    }

    if (entry.isFile() && entry.name.endsWith(".md")) {
      files.push(path);
    }
  }

  return files;
}

function decodeTarget(rawTarget) {
  const unwrapped = rawTarget.startsWith("<") && rawTarget.endsWith(">")
    ? rawTarget.slice(1, -1)
    : rawTarget;

  return decodeURIComponent(unwrapped);
}

async function targetExists(path) {
  try {
    return (await stat(path)).isFile();
  } catch {
    return false;
  }
}

/**
 * Validates every local Markdown file and section link below a directory.
 *
 * @param {string} root Directory to scan.
 * @returns {Promise<string[]>} Validation messages; an empty array means success.
 */
export async function validateMarkdownTree(root) {
  const absoluteRoot = resolve(root);
  const files = await findMarkdownFiles(absoluteRoot);
  const contentByPath = new Map();
  const anchorsByPath = new Map();
  const errors = [];

  for (const path of files) {
    const content = await readFile(path, "utf8");

    contentByPath.set(path, content);
    anchorsByPath.set(path, collectAnchors(content));
  }

  for (const [sourcePath, sourceContent] of contentByPath) {
    const visibleContent = stripCodeFences(sourceContent);
    const lines = visibleContent.split("\n");

    for (const [lineIndex, line] of lines.entries()) {
      for (const match of line.matchAll(markdownLink)) {
        const rawTarget = match.groups?.target;

        if (rawTarget === undefined || externalSchemes.test(rawTarget)) {
          continue;
        }

        let target;

        try {
          target = decodeTarget(rawTarget);
        } catch {
          errors.push(`${relative(absoluteRoot, sourcePath)}:${lineIndex + 1} ${rawTarget} has invalid URL encoding`);
          continue;
        }

        const fragmentIndex = target.indexOf("#");
        const filePart = fragmentIndex < 0 ? target : target.slice(0, fragmentIndex);
        const fragment = fragmentIndex < 0 ? "" : target.slice(fragmentIndex + 1);
        const targetPath = filePart.length === 0
          ? sourcePath
          : resolve(dirname(sourcePath), filePart);

        if (!(await targetExists(targetPath))) {
          errors.push(`${relative(absoluteRoot, sourcePath)}:${lineIndex + 1} ${target} targets a missing file`);
          continue;
        }

        if (fragment.length === 0) {
          continue;
        }

        let anchors = anchorsByPath.get(targetPath);

        if (anchors === undefined) {
          const targetContent = await readFile(targetPath, "utf8");
          anchors = collectAnchors(targetContent);
          anchorsByPath.set(targetPath, anchors);
        }

        if (!anchors.has(fragment)) {
          errors.push(`${relative(absoluteRoot, sourcePath)}:${lineIndex + 1} ${target} targets a missing section`);
        }
      }
    }
  }

  return errors;
}

async function main() {
  const errors = await validateMarkdownTree(process.cwd());

  if (errors.length === 0) {
    console.log("All local Markdown links and section anchors are valid.");
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
