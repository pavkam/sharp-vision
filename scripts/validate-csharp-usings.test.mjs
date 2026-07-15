import assert from "node:assert/strict";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { validateCSharpUsings } from "./validate-csharp-usings.mjs";

test("validateCSharpUsings_WhenNamespacePrecedesUniqueUsing_ReturnsNoErrors", async () => {
  const root = await createRoot();

  try {
    await writeFile(join(root, "GlobalUsings.cs"), "global using System;\n");
    await writeFile(
      join(root, "Widget.cs"),
      "namespace Example;\n\nusing System.Text;\n\npublic sealed class Widget {}\n",
    );

    const errors = await validateCSharpUsings(root);

    assert.deepEqual(errors, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateCSharpUsings_WhenUsingPrecedesNamespace_ReportsPlacement", async () => {
  const root = await createRoot();

  try {
    await writeFile(join(root, "GlobalUsings.cs"), "global using System;\n");
    await writeFile(
      join(root, "Widget.cs"),
      "using System.Text;\n\nnamespace Example;\n\npublic sealed class Widget {}\n",
    );

    const errors = await validateCSharpUsings(root);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /Widget\.cs.*namespace declaration must precede using directives/iu);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateCSharpUsings_WhenLocalUsingDuplicatesGlobalUsing_ReportsDuplicate", async () => {
  const root = await createRoot();

  try {
    await writeFile(join(root, "GlobalUsings.cs"), "global using System.Text;\n");
    await writeFile(
      join(root, "Widget.cs"),
      "namespace Example;\n\nusing System.Text;\n\npublic sealed class Widget {}\n",
    );

    const errors = await validateCSharpUsings(root);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /Widget\.cs.*System\.Text.*already declared globally/iu);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateCSharpUsings_WhenAliasReferencesItself_ReportsAlias", async () => {
  const root = await createRoot();

  try {
    await writeFile(
      join(root, "GlobalUsings.cs"),
      "global using Widget = Example.Widget;\n",
    );
    await writeFile(
      join(root, "WidgetTests.cs"),
      "namespace Example.Tests;\n\nusing Widget = Widget;\n\npublic sealed class WidgetTests {}\n",
    );

    const errors = await validateCSharpUsings(root);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /WidgetTests\.cs.*Widget.*alias cannot reference itself/iu);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateCSharpUsings_WhenAssemblyAttributeNeedsTopLevelUsing_AllowsIt", async () => {
  const root = await createRoot();

  try {
    await writeFile(join(root, "GlobalUsings.cs"), "global using System;\n");
    await writeFile(
      join(root, "AssemblyMarker.cs"),
      [
        "using System.Runtime.CompilerServices;",
        "",
        '[assembly: InternalsVisibleTo("Example.Tests")] ',
        "",
        "namespace Example;",
        "",
        "internal sealed class AssemblyMarker;",
        "",
      ].join("\n"),
    );

    const errors = await validateCSharpUsings(root);

    assert.deepEqual(errors, []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("validateCSharpUsings_WhenTopLevelProgramHasLocalUsing_ReportsPlacement", async () => {
  const root = await createRoot();

  try {
    await writeFile(join(root, "GlobalUsings.cs"), "global using System;\n");
    await writeFile(
      join(root, "Program.cs"),
      "using System.Text;\n\n_ = new StringBuilder();\n",
    );

    const errors = await validateCSharpUsings(root);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /Program\.cs.*move top-level imports to GlobalUsings\.cs/iu);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

async function createRoot() {
  return await mkdtemp(join(tmpdir(), "sharpvision-csharp-usings-"));
}
