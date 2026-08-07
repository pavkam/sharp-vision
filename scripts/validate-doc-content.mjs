import { readFile, readdir } from "node:fs/promises";
import { basename, extname, join, relative, sep } from "node:path";
import process from "node:process";

const issueIdentifierPattern = /(^|[^\p{L}\p{N}_])(#\d+)\b/gu;
const issueUrlPattern = /https:\/\/github\.com\/[^/\s)]+\/[^/\s)]+\/issues\/\d+\b/giu;

// A durable citation to another project's tracker is exempt. AGENTS.md bans the reference because
// *this* repository's numbers get renumbered and deleted, which is not true of an upstream
// platform bug: `microsoft/codecoverage#232` and a full upstream issue URL both stay resolvable
// and both name the project they belong to, so the reasoning survives without a lookup here.
const qualifiedIdentifierPattern = /[\p{L}\p{N}_.-]+\/[\p{L}\p{N}_.-]+#\d+/gu;
const foreignUrlPattern =
  /https:\/\/github\.com\/(?!pavkam\/sharp-vision\/)[^/\s)]+\/[^/\s)]+\/issues\/\d+\b/giu;

const ignoredDirectories = new Set([
  ".git",
  ".claude",
  ".vs",
  ".worktrees",
  "artifacts",
  "bin",
  "node_modules",
  "obj",
  "TestResults"
]);

const scannedExtensions = new Set([
  ".md",
  ".cs",
  ".mjs",
  ".js",
  ".json",
  ".config",
  ".props",
  ".targets",
  ".csproj",
  ".yml",
  ".yaml"
]);

const scannedFilenames = new Set(["Makefile"]);

/**
 * Reports every banned GitHub issue reference in one file's text.
 *
 * Fenced code blocks are skipped for Markdown, and inline code spans are stripped, so example
 * data and terminfo-style capability syntax do not register. The leading non-word guard already
 * excludes `colors#256`, `co#80`, and `RGB#1`.
 *
 * For source and configuration files, string literals are stripped first. A `#` followed by digits
 * inside a literal is data, not documentation - sixel color-select bytes, hex colors, and the
 * malformed-hex fixtures a color parser is tested against are all indistinguishable from an issue
 * number by shape alone, and a gate that reports them is a gate that gets switched off. The rule
 * this enforces targets prose: code, comments, and XML documentation. C# preprocessor directives
 * are never followed by a digit, so leaving code in scope costs nothing.
 *
 * @param {string} text The file contents.
 * @param {{ fenced?: boolean, literals?: boolean }} [options] Markdown fencing, and whether the
 * text has string literals to strip.
 * @returns {string[]} One message per violation.
 */
export function findGitHubIssueIdentifiers(text, options = {}) {
  const fenced = options.fenced ?? true;
  const literals = options.literals ?? false;
  const errors = [];
  const lines = (literals ? stripStringLiterals(text) : text).split(/\r?\n/u);
  let fence = null;

  for (const [index, line] of lines.entries()) {
    if (fenced) {
      const marker = line.match(/^\s{0,3}(`{3,}|~{3,})/u)?.[1];

      if (marker !== undefined) {
        if (fence === null) {
          fence = marker[0];
        } else if (marker[0] === fence) {
          fence = null;
        }

        continue;
      }

      if (fence !== null) {
        continue;
      }
    }

    const prose = line
      .replace(/`+[^`]*`+/gu, "")
      .replace(qualifiedIdentifierPattern, "")
      .replace(foreignUrlPattern, "");
    const lineNumber = index + 1;

    if (issueUrlPattern.test(prose)) {
      errors.push(`line ${lineNumber}: GitHub issue URL`);
    }

    issueUrlPattern.lastIndex = 0;

    for (const match of prose.matchAll(issueIdentifierPattern)) {
      errors.push(`line ${lineNumber}: GitHub issue identifier ${match[2]}`);
    }
  }

  return errors;
}

/**
 * Blanks the contents of string literals while preserving line structure, so reported line numbers
 * stay accurate. Handles C# raw, verbatim, and ordinary literals plus JavaScript quotes; a lone
 * unterminated quote leaves the rest of its line blanked, which errs toward silence rather than a
 * false report.
 *
 * @param {string} text The source text.
 * @returns {string} The text with literal contents replaced by spaces.
 */
function stripStringLiterals(text) {
  return text
    .replace(/"""[\s\S]*?"""/gu, blank)
    .replace(/@"(?:[^"]|"")*"/gu, blank)
    .replace(/"(?:[^"\\\n]|\\.)*"/gu, blank)
    .replace(/'(?:[^'\\\n]|\\.)*'/gu, blank);
}

/**
 * Replaces one match with spaces, keeping every newline so line numbers do not shift.
 *
 * @param {string} match The matched literal.
 * @returns {string} The blanked replacement.
 */
function blank(match) {
  return match.replace(/[^\n]/gu, " ");
}

/**
 * Collects every scannable file under one root.
 *
 * @param {string} root The directory to walk.
 * @returns {Promise<string[]>} Absolute paths.
 */
export async function scannableFiles(root) {
  const entries = await readdir(root, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const path = join(root, entry.name);

    if (entry.isDirectory()) {
      if (!ignoredDirectories.has(entry.name)) {
        files.push(...(await scannableFiles(path)));
      }
    } else if (
      scannedExtensions.has(extname(entry.name)) ||
      scannedFilenames.has(basename(entry.name))
    ) {
      files.push(path);
    }
  }

  return files;
}

/**
 * Scans one repository root and returns every violation.
 *
 * @param {string} root The repository root.
 * @returns {Promise<string[]>} One message per violation, prefixed by its relative path.
 */
export async function scanRepository(root) {
  const files = await scannableFiles(root);
  const errors = [];

  for (const file of files.sort()) {
    const relativePath = relative(root, file).split(sep).join("/");

    // This scanner's own fixtures and detector are necessarily full of the pattern it bans.
    if (relativePath === "scripts/validate-doc-content.mjs" || relativePath.endsWith(".test.mjs")) {
      continue;
    }

    const text = await readFile(file, "utf8");
    const markdown = extname(file) === ".md";

    for (const error of findGitHubIssueIdentifiers(text, {
      fenced: markdown,
      literals: !markdown
    })) {
      errors.push(`${relativePath}: ${error}`);
    }
  }

  return errors;
}

if (process.argv[1] !== undefined && import.meta.url.endsWith(basename(process.argv[1]))) {
  const root = join(import.meta.dirname, "..");
  const errors = await scanRepository(root);

  if (errors.length !== 0) {
    for (const error of errors) {
      console.error(error);
    }

    console.error(
      `\n${errors.length} GitHub issue reference(s) found. AGENTS.md requires the rationale to be ` +
        "stated directly, because this repository's issue numbers are not a durable citation target."
    );
    process.exit(1);
  }

  console.log("No GitHub issue references found in code, configuration, or documentation.");
}
