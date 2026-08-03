import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  classySource,
  createManifest,
  officialFontFiles,
  officialSource,
  validateManifest,
} from "./audit-figlet-fonts.mjs";

const font = (notice) => `flf2a$ 1 1 8 0 1 0\n${notice}\n`;

const createCuratedFolder = async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-font-audit-"));
  await mkdir(root, { recursive: true });

  for (const file of officialFontFiles) {
    await writeFile(path.join(root, file), font(`BSD font ${file}`));
  }

  await writeFile(path.join(root, "Classy.flf"), font("This font is free to use / MIT License"));
  return root;
};

test("createManifest_WhenCuratedFontsExist_RecordsResourcesHashesAndLicenses", async () => {
  const root = await createCuratedFolder();

  const manifest = await createManifest(root);
  const classy = manifest.fonts.find(({ name }) => name === "Classy");
  const standard = manifest.fonts.find(({ name }) => name === "standard");

  assert.equal(manifest.schema, 2);
  assert.equal(manifest.count, 19);
  assert.equal(classy.license, "MIT");
  assert.equal(classy.sourceRepository, classySource.repository);
  assert.equal(standard.license, "BSD-3-Clause");
  assert.equal(standard.sourceCommit, officialSource.commit);
  assert.equal(
    standard.resource,
    "SharpVision.FigletFonts.Resources.Fonts.standard.flf",
  );
  assert.match(standard.sha256, /^[0-9a-f]{64}$/u);
});

test("createManifest_WhenUnapprovedFontExists_RejectsCollection", async () => {
  const root = await createCuratedFolder();
  await writeFile(path.join(root, "unapproved.flf"), font("Unknown license"));

  await assert.rejects(createManifest(root), /allowlist/iu);
});

test("validateManifest_WhenHashDrifts_RejectsAudit", async () => {
  const root = await createCuratedFolder();
  const manifest = await createManifest(root);
  manifest.fonts[0].sha256 = "0".repeat(64);

  await assert.rejects(validateManifest(root, manifest), /does not match/iu);
});
