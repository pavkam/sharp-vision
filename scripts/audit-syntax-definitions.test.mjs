import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  classifyLicense,
  createManifest,
  firstPartyDefinitions,
  firstPartySource,
  upstreamSource,
  validateManifest,
} from "./audit-syntax-definitions.mjs";

const definition = (name, extensions, license, extra = "") =>
  `<?xml version="1.0" encoding="UTF-8"?>\n<!DOCTYPE language>\n` +
  `<language name="${name}" section="Sources" extensions="${extensions}" version="1" ` +
  `kateversion="5.0" author="Someone" license="${license}"${extra}>\n` +
  `  <highlighting>\n    <contexts>\n      <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>\n    </contexts>\n` +
  `    <itemDatas>\n      <itemData name="Normal Text" defStyleNum="dsNormal"/>\n    </itemDatas>\n  </highlighting>\n</language>\n`;

const spdxHeader = (license) =>
  `<?xml version="1.0" encoding="UTF-8"?>\n<!--\n    SPDX-License-Identifier: ${license}\n-->\n`;

test("classifyLicense_WhenAttributeIsMit_ReturnsMit", () => {
  assert.equal(classifyLicense(definition("Demo", "*.demo", "MIT")), "MIT");
});

test("classifyLicense_WhenAttributeIsGpl_ReturnsNull", () => {
  assert.equal(classifyLicense(definition("Demo", "*.demo", "GPL")), null);
});

test("classifyLicense_WhenAttributeIsEmpty_ReturnsNull", () => {
  assert.equal(classifyLicense(definition("Demo", "*.demo", "")), null);
});

test("classifyLicense_WhenAttributeIsMissing_ReturnsNull", () => {
  const text = definition("Demo", "*.demo", "MIT").replace(' license="MIT"', "");
  assert.equal(classifyLicense(text), null);
});

test("classifyLicense_WhenAttributeIsAmbiguousBsd_ReturnsNull", () => {
  assert.equal(classifyLicense(definition("Demo", "*.demo", "BSD")), null);
});

test("classifyLicense_WhenAttributeIsNewBsdLicense_ReturnsBsd3Clause", () => {
  assert.equal(classifyLicense(definition("Demo", "*.demo", "New BSD License")), "BSD-3-Clause");
});

test("classifyLicense_WhenAttributeIsPublicDomain_ReturnsPublicDomain", () => {
  assert.equal(classifyLicense(definition("Demo", "*.demo", "Public Domain")), "Public-Domain");
});

test("classifyLicense_WhenSpdxHeaderIsMit_OverridesMissingAttribute", () => {
  const text = spdxHeader("MIT") + definition("Demo", "*.demo", "MIT").replace(' license="MIT"', "");
  assert.equal(classifyLicense(text), "MIT");
});

test("classifyLicense_WhenSpdxHeaderIsGpl_ReturnsNull", () => {
  const text = spdxHeader("GPL-3.0-or-later") + definition("Demo", "*.demo", "GPL");
  assert.equal(classifyLicense(text), null);
});

const createCuratedFolder = async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-audit-"));
  await mkdir(root, { recursive: true });
  await writeFile(path.join(root, "demo.xml"), definition("Demo", "*.demo", "MIT"));
  await writeFile(path.join(root, "other.xml"), definition("Other", "*.other", "BSD-3-Clause"));
  return root;
};

test("createManifest_WhenDefinitionsAreCurated_RecordsResourcesAndLicenses", async () => {
  const root = await createCuratedFolder();

  const manifest = await createManifest(root);
  const demo = manifest.definitions.find(({ name }) => name === "Demo");

  assert.equal(manifest.schema, 2);
  assert.equal(manifest.count, 2);
  assert.equal(demo.license, "MIT");
  assert.equal(demo.resource, "SharpVision.SyntaxHighlighting.Resources.Syntax.demo.xml");
  assert.equal(demo.sourceRepository, upstreamSource.repository);
  assert.equal(demo.sourceCommit, upstreamSource.commit);
  assert.match(demo.sha256, /^[0-9a-f]{64}$/u);
});

test("createManifest_WhenDefinitionDeclaresStyleAndHidden_RecordsLazyInventoryMetadata", async () => {
  const root = await createCuratedFolder();
  await writeFile(
    path.join(root, "hidden.xml"),
    definition("Hidden", "*.hidden", "MIT", ' style="haskell" hidden="1"'),
  );

  const manifest = await createManifest(root);
  const hidden = manifest.definitions.find(({ name }) => name === "Hidden");

  assert.equal(hidden.style, "haskell");
  assert.equal(hidden.hidden, true);
});

test("createManifest_WhenAFileIsNotCurated_Rejects", async () => {
  const root = await createCuratedFolder();
  await writeFile(path.join(root, "copyleft.xml"), definition("Copyleft", "*.cl", "GPL"));

  await assert.rejects(createManifest(root), /curated permissive license/iu);
});

test("validateManifest_WhenContentDrifts_Rejects", async () => {
  const root = await createCuratedFolder();
  const manifest = await createManifest(root);
  manifest.definitions[0].sha256 = "0".repeat(64);

  await assert.rejects(validateManifest(root, manifest), /does not match/iu);
});

test("firstPartyDefinitions_WhenInspected_ContainsExactlyCSharp", () => {
  assert.deepEqual([...firstPartyDefinitions], ["csharp.xml"]);
});

test("createManifest_WhenFileIsFirstParty_RecordsThisRepositoryWithNoCommit", async () => {
  const root = await createCuratedFolder();
  await writeFile(path.join(root, "csharp.xml"), definition("C#", "*.cs", "MIT"));

  const manifest = await createManifest(root);
  const csharp = manifest.definitions.find(({ name }) => name === "C#");

  assert.equal(csharp.sourceRepository, firstPartySource.repository);
  assert.equal(csharp.sourceCommit, "");
});

test("createManifest_WhenFileIsNotFirstParty_RecordsTheUpstreamPin", async () => {
  const root = await createCuratedFolder();

  const manifest = await createManifest(root);
  const demo = manifest.definitions.find(({ name }) => name === "Demo");

  assert.equal(demo.sourceRepository, upstreamSource.repository);
  assert.equal(demo.sourceCommit, upstreamSource.commit);
});
