import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { mkdtemp, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { promisify } from "node:util";

import { stageCuratedFonts } from "./package-figlet-fonts.mjs";

const execFileAsync = promisify(execFile);

const createPinnedCheckout = async (prefix) => {
  const root = await mkdtemp(path.join(os.tmpdir(), prefix));
  await execFileAsync("git", ["-C", root, "init", "--quiet"]);
  await execFileAsync("git", ["-C", root, "config", "user.email", "test@example.invalid"]);
  await execFileAsync("git", ["-C", root, "config", "user.name", "Test"]);
  await writeFile(path.join(root, "README"), "seed\n");
  await execFileAsync("git", ["-C", root, "add", "."]);
  await execFileAsync("git", ["-C", root, "commit", "--quiet", "-m", "seed"]);
  const { stdout } = await execFileAsync("git", ["-C", root, "rev-parse", "HEAD"]);

  return { root, commit: stdout.trim() };
};

test("stageCuratedFonts_WhenEitherPinnedCheckoutIsDirty_Rejects", async () => {
  for (const dirtyCheckout of ["official", "classy"]) {
    const official = await createPinnedCheckout("sharpvision-figlet-official-");
    const classy = await createPinnedCheckout("sharpvision-figlet-classy-");
    const output = await mkdtemp(path.join(os.tmpdir(), "sharpvision-figlet-output-"));
    const dirty = dirtyCheckout === "official" ? official : classy;
    await writeFile(path.join(dirty.root, "README"), "tampered\n");

    await assert.rejects(
      stageCuratedFonts(
        official.root,
        classy.root,
        output,
        official.commit,
        classy.commit,
      ),
      /working tree is not clean/iu,
    );
  }
});
