import { execFile } from "node:child_process";
import { copyFile, mkdir, readFile, readdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { promisify } from "node:util";
import { pathToFileURL } from "node:url";

import { classifyLicense, firstPartyDefinitions, upstreamSource } from "./audit-syntax-definitions.mjs";

const execFileAsync = promisify(execFile);

const verifyCommit = async (root, expected) => {
  const { stdout } = await execFileAsync("git", ["-C", root, "rev-parse", "HEAD"]);

  if (stdout.trim() !== expected) {
    throw new Error(`Source checkout ${root} is not pinned to ${expected}.`);
  }
};

/**
 * Copies every permissively licensed syntax definition from a pinned upstream checkout's
 * `data/syntax` directory into `outputRoot`, replacing any previous contents except this
 * project's own first-party definitions (see {@link firstPartyDefinitions}), which this function
 * always preserves exactly as they were found in `outputRoot` before the refresh - a fresh
 * upstream checkout has no bearing on a file that was never sourced from it.
 *
 * @param {string} checkoutRoot the pinned `KDE/syntax-highlighting` checkout root.
 * @param {string} outputRoot the destination directory to replace.
 * @param {string} expectedCommit the commit `checkoutRoot` must be pinned to; defaults to the
 *   real upstream pin and is overridable only so tests can exercise this without a network clone.
 * @returns {Promise<{staged: number, excluded: number, preserved: number}>} counts for the
 *   operator to review.
 */
export const stageCuratedSyntaxDefinitions = async (
  checkoutRoot,
  outputRoot,
  expectedCommit = upstreamSource.commit,
) => {
  await verifyCommit(checkoutRoot, expectedCommit);

  const preservedFiles = new Map();

  for (const file of firstPartyDefinitions) {
    try {
      preservedFiles.set(file, await readFile(path.join(outputRoot, file)));
    } catch (error) {
      if (error.code !== "ENOENT") {
        throw error;
      }
    }
  }

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

  for (const [file, contents] of preservedFiles) {
    await writeFile(path.join(outputRoot, file), contents);
  }

  return { staged, excluded, preserved: preservedFiles.size };
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

  const { staged, excluded, preserved } = await stageCuratedSyntaxDefinitions(checkoutRoot, outputRoot);
  console.log(
    `Staged ${staged} permissively licensed definitions; excluded ${excluded}; preserved ${preserved} first-party definition(s).`,
  );
};

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  await main();
}
