// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

import assert from "node:assert/strict";
import test from "node:test";

import { validatePerformanceAssertions } from "./validate-performance-assertions.mjs";

test("Validate_WhenPerformanceTestAssertsElapsedTime_Fails", () => {
    const source = [
        "public void Write_WhenWarm_AllocatesZeroBytes()",
        "{",
        "    var watch = Stopwatch.StartNew();",
        "    watch.Stop();",
        "    watch.Elapsed.Ticks.ShouldBeLessThan(budget);",
        "}",
        "",
    ].join("\n");

    const errors = validatePerformanceAssertions(source, "Perf.cs");

    assert.equal(errors.length, 1);
    assert.match(errors[0], /must not assert against elapsed time/u);
});

test("Validate_WhenPerformanceTestOnlyReportsElapsedTime_Passes", () => {
    const source = [
        "public void Write_WhenWarm_AllocatesZeroBytes()",
        "{",
        "    var watch = Stopwatch.StartNew();",
        "    watch.Stop();",
        "    minimum.ShouldBe(0);",
        "    Report(\"scenario\", watch.Elapsed, 100_000);",
        "}",
        "",
    ].join("\n");

    assert.deepEqual(validatePerformanceAssertions(source, "Perf.cs"), []);
});

test("Validate_WhenElapsedAssertionSpansMultipleLines_Fails", () => {
    const source = [
        "public void Write_WhenWarm_AllocatesZeroBytes()",
        "{",
        "    watch.Elapsed.Ticks",
        "        .ShouldBeLessThan(budget);",
        "}",
        "",
    ].join("\n");

    const errors = validatePerformanceAssertions(source, "Perf.cs");

    assert.equal(errors.length, 1);
});
