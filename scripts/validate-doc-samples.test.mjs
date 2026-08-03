import assert from "node:assert/strict";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  classifyBlock,
  extractCSharpBlocks,
  renderCompilationUnit,
  splitProgramBlock,
  stripUsingLines,
  validateDocSamples,
} from "./validate-doc-samples.mjs";

test("validateDocSamples_WhenDotnetRootIsUnset_DiscoversPathSdk", async () => {
  const root = await mkdtemp(join(tmpdir(), "sharpvision-doc-samples-test-"));
  const previousDotnetRoot = process.env.DOTNET_ROOT;
  const previousHome = process.env.HOME;

  try {
    delete process.env.DOTNET_ROOT;
    process.env.HOME = root;

    const result = await validateDocSamples(root, []);

    assert.deepEqual(result, { errors: [], totalBlocks: 0 });
  } finally {
    if (previousDotnetRoot === undefined) {
      delete process.env.DOTNET_ROOT;
    } else {
      process.env.DOTNET_ROOT = previousDotnetRoot;
    }

    if (previousHome === undefined) {
      delete process.env.HOME;
    } else {
      process.env.HOME = previousHome;
    }

    await rm(root, { recursive: true, force: true });
  }
});

test("extractCSharpBlocks_WhenFenceIsCSharp_ReturnsBodyAndStartLine", () => {
  const content = [
    "# Title",
    "",
    "```csharp",
    "var x = 1;",
    "```",
    "",
    "```bash",
    "dotnet build",
    "```",
  ].join("\n");

  const blocks = extractCSharpBlocks(content);

  assert.equal(blocks.length, 1);
  assert.equal(blocks[0].code, "var x = 1;");
  assert.equal(blocks[0].startLine, 3);
});

test("extractCSharpBlocks_WhenNoFencesPresent_ReturnsEmpty", () => {
  assert.deepEqual(extractCSharpBlocks("# Title\n\nSome prose.\n"), []);
});

test("stripUsingLines_WhenUsingLinesPresent_RemovesOnlyThose", () => {
  const code = ["using System;", "using SharpVision.Controls;", "var x = 1;"].join("\n");

  assert.equal(stripUsingLines(code), "var x = 1;");
});

test("classifyBlock_WhenBodyStartsWithTypeKeyword_ReturnsDeclaration", () => {
  const body = "public sealed class CommandTile : Control\n{\n}\n";

  assert.equal(classifyBlock(body), "declaration");
});

test("classifyBlock_WhenBodyStartsWithMemberModifier_ReturnsMember", () => {
  const body = "public ColorValue Fill { get; set; } = ThemeColor.Accent;";

  assert.equal(classifyBlock(body), "member");
});

test("classifyBlock_WhenBodyIsPlainStatements_ReturnsFragment", () => {
  const body = "var result = await MessageBox.ShowAsync(owner, \"Delete?\");";

  assert.equal(classifyBlock(body), "fragment");
});

test("classifyBlock_WhenOnlyBlankOrComments_ReturnsFragment", () => {
  assert.equal(classifyBlock("\n// nothing here\n"), "fragment");
});

test("classifyBlock_WhenStatementsPrecedeTrailingTypeDeclaration_ReturnsProgram", () => {
  const body = [
    "var status = await ConsoleApplication.RunAsync(new HelloScreen());",
    "return status == ConsoleRunStatus.Failed ? 1 : 0;",
    "",
    "internal sealed class HelloScreen : Screen",
    "{",
    "}",
  ].join("\n");

  assert.equal(classifyBlock(body), "program");
});

test("splitProgramBlock_WhenGivenProgramBody_SeparatesStatementsFromDeclaration", () => {
  const body = [
    "var status = await ConsoleApplication.RunAsync(new HelloScreen());",
    "return status == ConsoleRunStatus.Failed ? 1 : 0;",
    "",
    "internal sealed class HelloScreen : Screen",
    "{",
    "}",
  ].join("\n");

  const { statements, declaration } = splitProgramBlock(body);

  assert.match(statements, /ConsoleApplication\.RunAsync/u);
  assert.doesNotMatch(statements, /internal sealed class/u);
  assert.match(declaration, /internal sealed class HelloScreen/u);
});

test("renderCompilationUnit_WhenBlockIsProgramKind_WrapsStatementsInStaticMethodNotTopLevel", () => {
  const blocks = [
    {
      code: [
        "var status = await ConsoleApplication.RunAsync(new HelloScreen());",
        "return status == ConsoleRunStatus.Failed ? 1 : 0;",
        "",
        "internal sealed class HelloScreen : Screen",
        "{",
        "}",
      ].join("\n"),
      startLine: 10,
    },
  ];

  const { source } = renderCompilationUnit("walkthroughs/first-application.md", blocks, new Set(), new Set());

  // Top-level statements are illegal in a /target:library compilation
  // (CS8805); the generated source must wrap them in a static method inside
  // the namespace instead of emitting them before it.
  assert.match(source, /internal static class __Program_0/u);
  assert.match(source, /internal sealed class HelloScreen : Screen/u);

  const namespaceIndex = source.indexOf("namespace ");
  const statementIndex = source.indexOf("ConsoleApplication.RunAsync");
  assert.ok(namespaceIndex >= 0 && statementIndex > namespaceIndex);
});

test("renderCompilationUnit_WhenDeclarationBlockPresent_EmitsItAtNamespaceScope", () => {
  const blocks = [
    {
      code: "public sealed class CommandTile : Control\n{\n}\n",
      startLine: 3,
    },
  ];

  const { source, lineMap } = renderCompilationUnit("concepts/theming.md", blocks, new Set(), new Set());

  assert.match(source, /public sealed class CommandTile : Control/u);
  assert.ok(lineMap.size > 0);
});

test("renderCompilationUnit_WhenStubNamesGiven_UsesKnownStubOverDynamic", () => {
  const blocks = [{ code: "input.Focus();", startLine: 5 }];

  const { source } = renderCompilationUnit(
    "controls/text-input.md",
    blocks,
    new Set(["input"]),
    new Set(),
  );

  assert.match(source, /TextInput input = new TextInput\(\);/u);
});
