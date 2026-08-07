import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const workflowPath = new URL("../.github/workflows/sharpvision-publish.yml", import.meta.url);

test("publishWorkflow_WhenPackagesAreMissing_PublishesEveryDependencyInOrder", async () => {
  const workflow = await readFile(workflowPath, "utf8");

  for (const packageId of ["SharpVision.Terminal", "SharpVision", "SharpVision.FigletFonts"]) {
    assert.match(workflow, new RegExp(`check_package \\w+ ${packageId.replaceAll(".", "\\.")}`, "u"));
    assert.match(
      workflow,
      new RegExp(`dotnet pack src/${packageId.replace("SharpVision.", "SharpVision.")}/${packageId}\\.csproj`, "u"),
    );
    assert.match(workflow, new RegExp(`${packageId}\\.\\$VERSION\\.nupkg`, "u"));
    assert.match(workflow, new RegExp(`${packageId}\\.\\$VERSION\\.snupkg`, "u"));
  }

  const terminalPush = workflow.indexOf(
    'dotnet nuget push "artifacts/package/SharpVision.Terminal.${VERSION}.nupkg"',
  );
  const uiPush = workflow.indexOf(
    'dotnet nuget push "artifacts/package/SharpVision.${VERSION}.nupkg"',
  );
  const fontsPush = workflow.indexOf(
    'dotnet nuget push "artifacts/package/SharpVision.FigletFonts.${VERSION}.nupkg"',
  );

  assert.ok(terminalPush >= 0, "Terminal package push is missing.");
  assert.ok(terminalPush < uiPush, "Terminal must publish before SharpVision.");
  assert.ok(uiPush < fontsPush, "SharpVision must publish before FigletFonts.");
});

test("publishWorkflow_WhenOnePackageExists_TracksEveryPackageIndependently", async () => {
  const workflow = await readFile(workflowPath, "utf8");

  assert.match(workflow, /terminal_deployed:.*published-versions\.outputs\.terminal_deployed/u);
  assert.match(workflow, /ui_deployed:.*published-versions\.outputs\.ui_deployed/u);
  assert.match(workflow, /fonts_deployed:.*published-versions\.outputs\.fonts_deployed/u);
  assert.match(workflow, /terminal_deployed == 'no'/u);
  assert.match(workflow, /ui_deployed == 'no'/u);
  assert.match(workflow, /fonts_deployed == 'no'/u);
});
