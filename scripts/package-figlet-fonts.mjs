import { execFile } from "node:child_process";
import { copyFile, mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { promisify } from "node:util";
import { pathToFileURL } from "node:url";

import {
  classySource,
  officialFontFiles,
  officialSource,
} from "./audit-figlet-fonts.mjs";

const execFileAsync = promisify(execFile);

const verifyCommit = async (root, expected) => {
  const { stdout } = await execFileAsync("git", ["-C", root, "rev-parse", "HEAD"]);

  if (stdout.trim() !== expected) {
    throw new Error(`Source checkout ${root} is not pinned to ${expected}.`);
  }
};

export const stageCuratedFonts = async (officialRoot, classyRoot, outputRoot) => {
  await verifyCommit(officialRoot, officialSource.commit);
  await verifyCommit(classyRoot, classySource.commit);
  await rm(outputRoot, { recursive: true, force: true });
  await mkdir(outputRoot, { recursive: true });

  for (const file of officialFontFiles) {
    await copyFile(path.join(officialRoot, "fonts", file), path.join(outputRoot, file));
  }

  await copyFile(path.join(classyRoot, "fonts", "Classy.flf"), path.join(outputRoot, "Classy.flf"));
};

const argument = (name) => {
  const index = process.argv.indexOf(name);
  return index < 0 ? undefined : process.argv[index + 1];
};

const main = async () => {
  const officialRoot = argument("--official-source");
  const classyRoot = argument("--classy-source");
  const outputRoot = argument("--output");

  if (!officialRoot || !classyRoot || !outputRoot) {
    throw new Error(
      "Usage: --official-source <checkout> --classy-source <checkout> --output <font-folder>",
    );
  }

  await stageCuratedFonts(officialRoot, classyRoot, outputRoot);
};

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  await main();
}
