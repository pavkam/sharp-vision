import { execFile } from "node:child_process";
import { copyFile, mkdir, readFile, readdir, rm } from "node:fs/promises";
import path from "node:path";
import { promisify } from "node:util";
import { pathToFileURL } from "node:url";

import { classifyLicense, upstreamSource } from "./audit-syntax-definitions.mjs";

const execFileAsync = promisify(execFile);

const verifyCommit = async (root, expected) => {
  const { stdout } = await execFileAsync("git", ["-C", root, "rev-parse", "HEAD"]);

  if (stdout.trim() !== expected) {
    throw new Error(`Source checkout ${root} is not pinned to ${expected}.`);
  }
};

/**
 * Copies every permissively licensed syntax definition from a pinned upstream checkout's
 * `data/syntax` directory into `outputRoot`, replacing any previous contents.
 *
 * @param {string} checkoutRoot the pinned `KDE/syntax-highlighting` checkout root.
 * @param {string} outputRoot the destination directory to replace.
 * @param {string} expectedCommit the commit `checkoutRoot` must be pinned to; defaults to the
 *   real upstream pin and is overridable only so tests can exercise this without a network clone.
 * @returns {Promise<{staged: number, excluded: number}>} counts for the operator to review.
 */
export const stageCuratedSyntaxDefinitions = async (
  checkoutRoot,
  outputRoot,
  expectedCommit = upstreamSource.commit,
) => {
  await verifyCommit(checkoutRoot, expectedCommit);
  await rm(outputRoot, { recursive: true, force: true });
  await mkdir(outputRoot, { recursive: true });

  const syntaxRoot = path.join(checkoutRoot, "data", "syntax");
  const entries = (await readdir(syntaxRoot, { withFileTypes: true }))
    .filter((entry) => entry.isFile() && /\.xml$/iu.test(entry.name))
    .map((entry) => entry.name);

  let staged = 0;
  let excluded = 0;

  for (const file of entries) {
    const text = await readFile(path.join(syntaxRoot, file), "utf8");

    if (classifyLicense(text)) {
      await copyFile(path.join(syntaxRoot, file), path.join(outputRoot, file));
      staged += 1;
    } else {
      excluded += 1;
    }
  }

  return { staged, excluded };
};

const argument = (name) => {
  const index = process.argv.indexOf(name);
  return index < 0 ? undefined : process.argv[index + 1];
};

const main = async () => {
  const checkoutRoot = argument("--source");
  const outputRoot = argument("--output");

  if (!checkoutRoot || !outputRoot) {
    throw new Error("Usage: --source <syntax-highlighting checkout> --output <syntax-folder>");
  }

  const { staged, excluded } = await stageCuratedSyntaxDefinitions(checkoutRoot, outputRoot);
  console.log(`Staged ${staged} permissively licensed definitions; excluded ${excluded}.`);
};

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  await main();
}
