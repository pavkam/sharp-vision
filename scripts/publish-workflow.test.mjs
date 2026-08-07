import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const workflowPath = new URL("../.github/workflows/sharpvision-publish.yml", import.meta.url);

const expectedFilesVersionVariable = {
  "SharpVision.Terminal": "TERMINAL_VERSION",
  SharpVision: "UI_VERSION",
  "SharpVision.FigletFonts": "FONTS_VERSION",
};

test("publishWorkflow_WhenPackagesAreMissing_PublishesEveryDependencyInOrder", async () => {
  const workflow = await readFile(workflowPath, "utf8");

  for (const packageId of ["SharpVision.Terminal", "SharpVision", "SharpVision.FigletFonts"]) {
    assert.match(workflow, new RegExp(`check_package \\w+ ${packageId.replaceAll(".", "\\.")}`, "u"));
    assert.match(
      workflow,
      new RegExp(`dotnet pack src/${packageId.replace("SharpVision.", "SharpVision.")}/${packageId}\\.csproj`, "u"),
    );

    const versionVariable = expectedFilesVersionVariable[packageId];

    assert.match(workflow, new RegExp(`${packageId}\\.\\$${versionVariable}\\.nupkg`, "u"));
    assert.match(workflow, new RegExp(`${packageId}\\.\\$${versionVariable}\\.snupkg`, "u"));
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

test("publishWorkflow_WhenPackagesHaveDifferentVersions_ReadsAndPublishesEachIndependently", async () => {
  const workflow = await readFile(workflowPath, "utf8");

  // Each package's own Version property, read independently - there is no cross-project
  // agreement check. Reintroducing one would block publishing SharpVision.Terminal, SharpVision,
  // and SharpVision.FigletFonts on their own separate schedules, which is the entire point of
  // each owning an independent version property in Directory.Build.props.
  assert.match(
    workflow,
    /terminal_version="\$\(dotnet msbuild src\/SharpVision\.Terminal\/SharpVision\.Terminal\.csproj -getProperty:Version/u,
  );
  assert.match(
    workflow,
    /ui_version="\$\(dotnet msbuild src\/SharpVision\/SharpVision\.csproj -getProperty:Version/u,
  );
  assert.match(
    workflow,
    /fonts_version="\$\(dotnet msbuild src\/SharpVision\.FigletFonts\/SharpVision\.FigletFonts\.csproj -getProperty:Version/u,
  );
  assert.doesNotMatch(workflow, /disagree/u);
  assert.doesNotMatch(workflow, /OverallVersion/u);

  assert.match(workflow, /echo "terminal_version=\$terminal_version" >> "\$GITHUB_OUTPUT"/u);
  assert.match(workflow, /echo "ui_version=\$ui_version" >> "\$GITHUB_OUTPUT"/u);
  assert.match(workflow, /echo "fonts_version=\$fonts_version" >> "\$GITHUB_OUTPUT"/u);
});
