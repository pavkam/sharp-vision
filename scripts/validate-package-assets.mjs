import { readFile } from "node:fs/promises";

const [, , path, version] = process.argv;

if (!path || !version) {
  throw new Error("Usage: validate-package-assets.mjs <project.assets.json> <version>");
}

const assets = JSON.parse(await readFile(path, "utf8"));
const libraries = assets.libraries ?? {};

for (const packageId of ["SharpVision", "SharpVision.Terminal"]) {
  const key = `${packageId}/${version}`;
  const library = libraries[key];

  if (!library || library.type !== "package") {
    throw new Error(`${key} did not resolve as an isolated package dependency.`);
  }
}

const projectFrameworks = assets.project?.frameworks ?? {};
const framework = Object.values(projectFrameworks)[0];
const dependencies = framework?.dependencies ?? {};

if (Object.keys(dependencies).length !== 1 || !dependencies.SharpVision) {
  throw new Error("The package consumer must declare exactly one SharpVision dependency.");
}
