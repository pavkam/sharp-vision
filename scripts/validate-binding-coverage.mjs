// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

import { readFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";

function attributes(fragment) {
  return new Map([...fragment.matchAll(/([\w-]+)="([^"]*)"/g)].map((match) => [match[1], match[2]]));
}

function isBindingFile(filename) {
  const normalized = filename.replaceAll("\\", "/");
  return normalized.includes("/src/SharpVision/DataBinding/") ||
    normalized.endsWith("/src/SharpVision/Controls/Control.Bindings.cs");
}

export function summarizeBindingCoverage(xml) {
  const lines = new Map();
  const branches = new Map();
  let matchedFiles = 0;

  for (const classMatch of xml.matchAll(/<class\b([^>]*)>([\s\S]*?)<\/class>/g)) {
    const classAttributes = attributes(classMatch[1]);
    const filename = classAttributes.get("filename");

    if (!filename || !isBindingFile(filename)) {
      continue;
    }

    matchedFiles++;

    for (const lineMatch of classMatch[2].matchAll(/<line\b([^>]*)\/?>(?:<\/line>)?/g)) {
      const lineAttributes = attributes(lineMatch[1]);
      const number = lineAttributes.get("number");
      const hits = Number.parseInt(lineAttributes.get("hits") ?? "0", 10);

      if (!number) {
        continue;
      }

      const key = `${filename.replaceAll("\\", "/")}:${number}`;
      lines.set(key, Math.max(lines.get(key) ?? 0, hits));
      const condition = lineAttributes.get("condition-coverage")?.match(/\((\d+)\/(\d+)\)/);

      if (condition) {
        const covered = Number.parseInt(condition[1], 10);
        const total = Number.parseInt(condition[2], 10);
        const previous = branches.get(key) ?? { covered: 0, total };
        branches.set(key, {
          covered: Math.max(previous.covered, covered),
          total: Math.max(previous.total, total),
        });
      }
    }
  }

  if (matchedFiles === 0 || lines.size === 0) {
    throw new Error("Coverage report does not contain SharpVision binding files.");
  }

  const lineCovered = [...lines.values()].filter((hits) => hits > 0).length;
  const branchCovered = [...branches.values()].reduce((sum, value) => sum + value.covered, 0);
  const branchTotal = [...branches.values()].reduce((sum, value) => sum + value.total, 0);

  return {
    branchCovered,
    branchRate: branchTotal === 0 ? 100 : (branchCovered * 100) / branchTotal,
    branchTotal,
    lineCovered,
    lineRate: (lineCovered * 100) / lines.size,
    lineTotal: lines.size,
  };
}

export function assertBindingCoverage(summary, minimumLineRate, minimumBranchRate) {
  const failures = [];

  if (summary.lineRate < minimumLineRate) {
    failures.push(`line ${summary.lineRate.toFixed(2)}% < ${minimumLineRate.toFixed(2)}%`);
  }

  if (summary.branchRate < minimumBranchRate) {
    failures.push(`branch ${summary.branchRate.toFixed(2)}% < ${minimumBranchRate.toFixed(2)}%`);
  }

  if (failures.length > 0) {
    throw new Error(`Binding coverage failed: ${failures.join("; ")}.`);
  }
}

async function main() {
  const [reportPath, lineText = "95", branchText = "90"] = process.argv.slice(2);

  if (!reportPath) {
    throw new Error("Usage: validate-binding-coverage.mjs <cobertura.xml> [line%] [branch%]");
  }

  const minimumLineRate = Number.parseFloat(lineText);
  const minimumBranchRate = Number.parseFloat(branchText);

  if (!Number.isFinite(minimumLineRate) || !Number.isFinite(minimumBranchRate) ||
      minimumLineRate < 0 || minimumLineRate > 100 ||
      minimumBranchRate < 0 || minimumBranchRate > 100) {
    throw new Error("Coverage thresholds must be finite percentages from 0 through 100.");
  }

  const summary = summarizeBindingCoverage(await readFile(reportPath, "utf8"));
  assertBindingCoverage(summary, minimumLineRate, minimumBranchRate);
  process.stdout.write(
    `Binding coverage: line ${summary.lineRate.toFixed(2)}% ` +
      `(${summary.lineCovered}/${summary.lineTotal}), branch ${summary.branchRate.toFixed(2)}% ` +
      `(${summary.branchCovered}/${summary.branchTotal}).\n`,
  );
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  await main();
}
