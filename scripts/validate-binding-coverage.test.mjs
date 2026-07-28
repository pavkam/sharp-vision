// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

import assert from "node:assert/strict";
import test from "node:test";

import { assertBindingCoverage, summarizeBindingCoverage } from "./validate-binding-coverage.mjs";

const report = `
<coverage>
  <class filename="/repo/src/SharpVision/DataBinding/Binding.cs">
    <line number="10" hits="1" branch="False" />
    <line number="11" hits="0" branch="True" condition-coverage="50% (1/2)" />
  </class>
  <class filename="/repo/src/SharpVision/DataBinding/Binding.cs">
    <line number="11" hits="1" branch="True" condition-coverage="100% (2/2)" />
  </class>
  <class filename="/repo/src/SharpVision/Controls/Control.Bindings.cs">
    <line number="8" hits="1" branch="False" />
  </class>
  <class filename="/repo/src/SharpVision/Controls/Button.cs">
    <line number="8" hits="0" branch="False" />
  </class>
</coverage>`;

test("summarizeBindingCoverage merges generated classes and excludes unrelated files", () => {
  assert.deepEqual(summarizeBindingCoverage(report), {
    branchCovered: 2,
    branchRate: 100,
    branchTotal: 2,
    lineCovered: 3,
    lineRate: 100,
    lineTotal: 3,
  });
});

test("summarizeBindingCoverage rejects a report without binding production files", () => {
  assert.throws(
    () => summarizeBindingCoverage('<class filename="/repo/src/SharpVision/Controls/Button.cs" />'),
    /does not contain SharpVision binding files/,
  );
});

test("assertBindingCoverage reports exact failed thresholds", () => {
  assert.throws(
    () => assertBindingCoverage({
      branchCovered: 8,
      branchRate: 80,
      branchTotal: 10,
      lineCovered: 9,
      lineRate: 90,
      lineTotal: 10,
    }, 95, 90),
    /line 90.00% < 95.00%; branch 80.00% < 90.00%/,
  );
});
