import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { mkdir, mkdtemp, readdir, readFile, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { promisify } from "node:util";

import { stageCuratedSyntaxDefinitions } from "./package-syntax-definitions.mjs";

const execFileAsync = promisify(execFile);

const definition = (name, license) =>
  `<?xml version="1.0" encoding="UTF-8"?>\n<!DOCTYPE language>\n` +
  `<language name="${name}" section="Sources" extensions="*.${name.toLowerCase()}" version="1" ` +
  `kateversion="5.0" author="Someone" license="${license}">\n` +
  `  <highlighting>\n    <contexts>\n      <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>\n    </contexts>\n` +
  `    <itemDatas>\n      <itemData name="Normal Text" defStyleNum="dsNormal"/>\n    </itemDatas>\n  </highlighting>\n</language>\n`;

const createPinnedCheckout = async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-checkout-"));
  const syntaxRoot = path.join(root, "data", "syntax");
  await mkdir(syntaxRoot, { recursive: true });
  await writeFile(path.join(syntaxRoot, "permissive.xml"), definition("Permissive", "MIT"));
  await writeFile(path.join(syntaxRoot, "copyleft.xml"), definition("Copyleft", "GPL"));

  await execFileAsync("git", ["-C", root, "init", "--quiet"]);
  await execFileAsync("git", ["-C", root, "config", "user.email", "test@example.invalid"]);
  await execFileAsync("git", ["-C", root, "config", "user.name", "Test"]);
  await execFileAsync("git", ["-C", root, "add", "."]);
  await execFileAsync("git", ["-C", root, "commit", "--quiet", "-m", "seed"]);
  const { stdout } = await execFileAsync("git", ["-C", root, "rev-parse", "HEAD"]);

  return { root, commit: stdout.trim() };
};

test("stageCuratedSyntaxDefinitions_WhenCheckoutIsUnpinned_Rejects", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-unpinned-"));
  await mkdir(path.join(root, "data", "syntax"), { recursive: true });
  await execFileAsync("git", ["-C", root, "init", "--quiet"]);
  await execFileAsync("git", ["-C", root, "config", "user.email", "test@example.invalid"]);
  await execFileAsync("git", ["-C", root, "config", "user.name", "Test"]);
  await writeFile(path.join(root, "README"), "seed");
  await execFileAsync("git", ["-C", root, "add", "."]);
  await execFileAsync("git", ["-C", root, "commit", "--quiet", "-m", "seed"]);

  const output = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-output-"));
  await assert.rejects(
    stageCuratedSyntaxDefinitions(root, output, "0".repeat(40)),
    /not pinned/iu,
  );
});

test("stageCuratedSyntaxDefinitions_WhenPinned_StagesOnlyPermissiveFiles", async () => {
  const { root: checkout, commit } = await createPinnedCheckout();
  const output = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-output-"));

  const result = await stageCuratedSyntaxDefinitions(checkout, output, commit);
  const staged = await readdir(output);

  assert.equal(result.staged, 1);
  assert.equal(result.excluded, 1);
  assert.equal(result.preserved, 0);
  assert.deepEqual(staged, ["permissive.xml"]);
});

test("stageCuratedSyntaxDefinitions_WhenPinnedCheckoutIsDirty_Rejects", async () => {
  const { root: checkout, commit } = await createPinnedCheckout();
  const output = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-output-"));
  await writeFile(
    path.join(checkout, "data", "syntax", "permissive.xml"),
    definition("Tampered", "MIT"),
  );

  await assert.rejects(
    stageCuratedSyntaxDefinitions(checkout, output, commit),
    /working tree is not clean/iu,
  );
});

test("stageCuratedSyntaxDefinitions_WhenPinnedCheckoutHasUntrackedDefinition_Rejects", async () => {
  const { root: checkout, commit } = await createPinnedCheckout();
  const output = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-output-"));
  await writeFile(
    path.join(checkout, "data", "syntax", "untracked.xml"),
    definition("Untracked", "MIT"),
  );

  await assert.rejects(
    stageCuratedSyntaxDefinitions(checkout, output, commit),
    /working tree is not clean/iu,
  );
});

test("stageCuratedSyntaxDefinitions_WhenAFirstPartyFileAlreadyExists_PreservesItUnchanged", async () => {
  const { root: checkout, commit } = await createPinnedCheckout();
  const output = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-output-"));
  const csharpContent = definition("C#", "MIT");
  await writeFile(path.join(output, "csharp.xml"), csharpContent);

  const result = await stageCuratedSyntaxDefinitions(checkout, output, commit);
  const staged = await readdir(output);
  const preservedContent = await readFile(path.join(output, "csharp.xml"), "utf8");

  assert.equal(result.preserved, 1);
  assert.deepEqual(staged.sort(), ["csharp.xml", "permissive.xml"]);
  assert.equal(preservedContent, csharpContent);
});

test("stageCuratedSyntaxDefinitions_WhenNoFirstPartyFileExistsYet_DoesNotFabricateOne", async () => {
  const { root: checkout, commit } = await createPinnedCheckout();
  const output = await mkdtemp(path.join(os.tmpdir(), "sharpvision-syntax-output-"));

  const result = await stageCuratedSyntaxDefinitions(checkout, output, commit);
  const staged = await readdir(output);

  assert.equal(result.preserved, 0);
  assert.ok(!staged.includes("csharp.xml"));
});
