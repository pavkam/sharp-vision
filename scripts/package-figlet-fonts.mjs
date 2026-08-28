import { copyFile, mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

import {
  classySource,
  officialFontFiles,
  officialSource,
} from "./audit-figlet-fonts.mjs";
import { verifyPinnedCheckout } from "./verify-pinned-checkout.mjs";

export const stageCuratedFonts = async (
  officialRoot,
  classyRoot,
  outputRoot,
  expectedOfficialCommit = officialSource.commit,
  expectedClassyCommit = classySource.commit,
) => {
  await verifyPinnedCheckout(officialRoot, expectedOfficialCommit);
  await verifyPinnedCheckout(classyRoot, expectedClassyCommit);
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
