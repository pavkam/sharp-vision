import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { evaluateBenchmarkResults, median } from "./wall-clock-benchmark.mjs";

const environment = {
    architecture: "x64",
    dotnet: "10.0.100",
    os: "linux",
    runnerImage: "ubuntu-24.04",
};
const config = {
    confirmationRuns: 2,
    maxRegressionRatio: 1.2,
    scenarios: [
        { id: "ui", name: "UI performance suite" },
        { id: "terminal", name: "Terminal performance suite" },
    ],
};

test("median_WhenSamplesAreOrderedOrUnordered_ReturnsTheMiddleValue", () => {
    assert.equal(median([30, 10, 20]), 20);
    assert.equal(median([40, 10, 30, 20]), 25);
});

test("evaluateBenchmarkResults_WhenNoBaselineExists_InitializesWithoutFailing", () => {
    const result = evaluateBenchmarkResults({
        config,
        environment,
        measurements: measurements(100, 200),
        previousState: undefined,
        generatedAt: "2026-08-29T14:00:00.000Z",
    });

    assert.equal(result.report.status, "initialized");
    assert.equal(result.state.scenarios.ui.baselineMilliseconds, 100);
    assert.equal(result.state.scenarios.terminal.baselineMilliseconds, 200);
    assert.equal(result.exitCode, 0);
});

test("evaluateBenchmarkResults_WhenRunnerIdentityChanges_ReinitializesTheBaseline", () => {
    const previousState = baselineState();
    previousState.environment.dotnet = "10.0.101";

    const result = evaluateBenchmarkResults({
        config,
        environment,
        measurements: measurements(150, 300),
        previousState,
        generatedAt: "2026-08-29T14:00:00.000Z",
    });

    assert.equal(result.report.status, "initialized");
    assert.equal(result.state.scenarios.ui.baselineMilliseconds, 150);
    assert.equal(result.state.scenarios.ui.consecutiveRegressions, 0);
    assert.equal(result.exitCode, 0);
});

test("evaluateBenchmarkResults_WhenFirstRegressionExceedsThreshold_WarnsAndKeepsBaseline", () => {
    const result = evaluateBenchmarkResults({
        config,
        environment,
        measurements: measurements(125, 200),
        previousState: baselineState(),
        generatedAt: "2026-08-29T14:00:00.000Z",
    });

    assert.equal(result.report.status, "warning");
    assert.equal(result.report.scenarios.ui.status, "unconfirmed-regression");
    assert.equal(result.state.scenarios.ui.baselineMilliseconds, 100);
    assert.equal(result.state.scenarios.ui.consecutiveRegressions, 1);
    assert.equal(result.exitCode, 0);
});

test("evaluateBenchmarkResults_WhenRegressionRepeats_FailsTheLane", () => {
    const previousState = baselineState();
    previousState.scenarios.ui.consecutiveRegressions = 1;

    const result = evaluateBenchmarkResults({
        config,
        environment,
        measurements: measurements(125, 200),
        previousState,
        generatedAt: "2026-08-29T14:00:00.000Z",
    });

    assert.equal(result.report.status, "failed");
    assert.equal(result.report.scenarios.ui.status, "confirmed-regression");
    assert.equal(result.state.scenarios.ui.consecutiveRegressions, 2);
    assert.equal(result.exitCode, 1);
});

test("evaluateBenchmarkResults_WhenPerformanceRecovers_RefreshesBaselineAndClearsStreak", () => {
    const previousState = baselineState();
    previousState.scenarios.ui.consecutiveRegressions = 1;

    const result = evaluateBenchmarkResults({
        config,
        environment,
        measurements: measurements(110, 190),
        previousState,
        generatedAt: "2026-08-29T14:00:00.000Z",
    });

    assert.equal(result.report.status, "passed");
    assert.equal(result.state.scenarios.ui.baselineMilliseconds, 110);
    assert.equal(result.state.scenarios.ui.consecutiveRegressions, 0);
    assert.equal(result.exitCode, 0);
});

test("benchmarkWorkflow_WhenScheduled_UsesPinnedRunnerAndPersistsEvidence", async () => {
    const workflow = await readFile(
        new URL(
            "../.github/workflows/sharpvision-benchmark.yml",
            import.meta.url,
        ),
        "utf8",
    );

    assert.match(workflow, /schedule:/u);
    assert.match(workflow, /cron:/u);
    assert.match(workflow, /workflow_dispatch:/u);
    assert.match(workflow, /runs-on: ubuntu-24\.04/u);
    assert.match(workflow, /group: sharpvision-wall-clock-benchmark/u);
    assert.match(workflow, /cancel-in-progress: false/u);
    assert.match(workflow, /actions\/cache\/restore@[0-9a-f]{40}/u);
    assert.match(workflow, /actions\/cache\/save@[0-9a-f]{40}/u);
    assert.match(workflow, /make benchmark/u);
    assert.match(workflow, /actions\/upload-artifact@[0-9a-f]{40}/u);
    assert.match(workflow, /if:.*always\(\)/u);
    assert.doesNotMatch(workflow, /pull_request:/u);
});

function measurements(ui, terminal) {
    return {
        ui: [ui - 5, ui, ui + 5],
        terminal: [terminal + 10, terminal - 10, terminal],
    };
}

function baselineState() {
    return {
        environment: { ...environment },
        scenarios: {
            terminal: { baselineMilliseconds: 200, consecutiveRegressions: 0 },
            ui: { baselineMilliseconds: 100, consecutiveRegressions: 0 },
        },
        schemaVersion: 1,
        updatedAt: "2026-08-22T14:00:00.000Z",
    };
}
