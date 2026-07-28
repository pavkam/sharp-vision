#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const controlSourceSegments = [
  "/src/SharpVision/Controls/",
  "/src/SharpVision/Dialogs/",
  "/src/SharpVision/Menus/",
  "/src/SharpVision/Navigation/",
  "/src/SharpVision/Popups/",
  "/src/SharpVision/Windows/",
];

const sharedControlSourceFiles = new Set([
  "/src/SharpVision/Input/AccessKeyText.cs",
  "/src/SharpVision/Input/CurrentItemNavigator.cs",
  "/src/SharpVision/Input/DragBehavior.cs",
  "/src/SharpVision/Input/PressBehavior.cs",
  "/src/SharpVision/Layout/LayoutMath.cs",
  "/src/SharpVision/Layout/ResolvedAxes.cs",
  "/src/SharpVision/Styling/BorderGlyphs.cs",
  "/src/SharpVision/Styling/CellGlyphResolver.cs",
  "/src/SharpVision/Styling/ChromeRenderOptions.cs",
  "/src/SharpVision/Styling/ControlAppearance.cs",
  "/src/SharpVision/Styling/ControlChrome.cs",
  "/src/SharpVision/Styling/DecorationResolver.cs",
  "/src/SharpVision/Styling/ResolvedAppearance.cs",
  "/src/SharpVision/Styling/ShadowMode.cs",
]);

const isControlSource = (filename) =>
  controlSourceSegments.some((segment) => filename.includes(segment)) ||
  [...sharedControlSourceFiles].some((source) => filename.endsWith(source));

const attribute = (attributes, name) => {
  const match = attributes.match(new RegExp(`\\b${name}="([^"]*)"`));
  return match?.[1];
};

export const parseControlCoverage = (xml) => {
  const classes = [...xml.matchAll(/<class\b([^>]*)>([\s\S]*?)<\/class>/g)];

  if (classes.length === 0) {
    throw new Error("Coverage report does not contain Cobertura class entries.");
  }

  let covered = 0;
  let total = 0;

  for (const [, attributes, body] of classes) {
    const source = attribute(attributes, "filename")?.replaceAll("\\", "/");
    const filename = source?.startsWith("/") ? source : `/${source}`;

    if (!filename || !isControlSource(filename)) {
      continue;
    }

    const methodsEnd = body.lastIndexOf("</methods>");
    const classLines = methodsEnd < 0 ? body : body.slice(methodsEnd + "</methods>".length);

    for (const line of classLines.matchAll(/<line\b([^>]*)\/?\s*>/g)) {
      const hits = Number.parseInt(attribute(line[1], "hits") ?? "", 10);

      if (!Number.isInteger(hits) || hits < 0) {
        throw new Error("Coverage report contains a control line with invalid hits.");
      }

      total++;
      covered += hits > 0 ? 1 : 0;
    }
  }

  if (total === 0) {
    throw new Error(
      "Coverage report does not contain instrumented SharpVision UI control lines.",
    );
  }

  return { covered, total, rate: covered / total };
};

export const assertControlCoverage = (xml, minimum) => {
  if (!Number.isFinite(minimum) || minimum < 0 || minimum > 1) {
    throw new RangeError("Coverage minimum must be a finite ratio from zero through one.");
  }

  const coverage = parseControlCoverage(xml);

  if (coverage.rate < minimum) {
    throw new Error(
      `Control line coverage ${(coverage.rate * 100).toFixed(2)}% ` +
        `(${coverage.covered}/${coverage.total}) is below ` +
        `${(minimum * 100).toFixed(2)}%.`,
    );
  }

  return coverage;
};

const option = (name, fallback) => {
  const index = process.argv.indexOf(name);
  return index < 0 ? fallback : process.argv[index + 1];
};

const run = () => {
  const results = option("--results");
  const minimum = Number.parseFloat(option("--minimum", "0.90"));
  const maxAgeSeconds = Number.parseInt(option("--max-age-seconds", "1800"), 10);

  if (!results) {
    throw new Error("Coverage results directory is required via --results.");
  }

  const reports = fs
    .readdirSync(results, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith(".cobertura.xml"))
    .map((entry) => {
      const filename = path.join(results, entry.name);
      return { filename, modified: fs.statSync(filename).mtimeMs };
    })
    .sort((left, right) => right.modified - left.modified);

  if (reports.length === 0) {
    throw new Error(`No Cobertura report exists under ${results}.`);
  }

  const report = reports[0];
  const ageSeconds = (Date.now() - report.modified) / 1000;

  if (!Number.isFinite(maxAgeSeconds) || maxAgeSeconds <= 0 || ageSeconds > maxAgeSeconds) {
    throw new Error(
      `Latest Cobertura report is ${ageSeconds.toFixed(0)} seconds old; ` +
        `maximum is ${maxAgeSeconds}.`,
    );
  }

  const coverage = assertControlCoverage(
    fs.readFileSync(report.filename, "utf8"),
    minimum,
  );
  console.log(
    `SharpVision UI control line coverage ${(coverage.rate * 100).toFixed(2)}% ` +
      `(${coverage.covered}/${coverage.total}) meets ` +
      `${(minimum * 100).toFixed(2)}%.`,
  );
};

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    run();
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
