import assert from "node:assert/strict";
import { mkdtemp, readFile, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  createManifest,
  validateManifest,
} from "./audit-figlet-fonts.mjs";
import { createArchive } from "./package-figlet-fonts.mjs";

const font = (notice = "Example font by Tester") =>
  `flf2a$ 1 1 8 0 1 0\n${notice}\n`;

test("createManifest_WhenFontsExist_RecordsSortedHashesAndClassifications", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-font-audit-"));
  await writeFile(path.join(root, "z.tlf"), font("Public domain"));
  await writeFile(path.join(root, "a.flf"), font("Copyright Example"));

  const manifest = await createManifest(root, {
    repository: "https://example.invalid/fonts",
    commit: "abc123",
  });

  assert.deepEqual(
    manifest.fonts.map((entry) => entry.file),
    ["a.flf", "z.tlf"],
  );
  assert.equal(manifest.fonts[0].license, "attribution-only");
  assert.equal(manifest.fonts[1].license, "public-domain");
  assert.match(manifest.fonts[0].sha256, /^[0-9a-f]{64}$/);
});

test("validateManifest_WhenHashDrifts_RejectsAudit", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-font-audit-"));
  await writeFile(path.join(root, "a.flf"), font());
  const manifest = await createManifest(root, {
    repository: "https://example.invalid/fonts",
    commit: "abc123",
  });
  manifest.fonts[0].sha256 = "0".repeat(64);

  await assert.rejects(validateManifest(root, manifest), /hash/i);
});

test("createArchive_WhenRepeated_ProducesIdenticalCompressedBytes", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-font-package-"));
  const first = path.join(root, "first.zip");
  const second = path.join(root, "second.zip");
  await writeFile(path.join(root, "a.flf"), font("Public domain"));
  await writeFile(path.join(root, "b.tlf"), font("Permission is hereby granted"));

  await createArchive(root, first);
  await createArchive(root, second);

  assert.deepEqual(await readFile(first), await readFile(second));
});
