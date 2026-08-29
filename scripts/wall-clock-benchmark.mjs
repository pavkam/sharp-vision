import {
    appendFile,
    mkdir,
    readFile,
    rename,
    writeFile,
} from "node:fs/promises";
import { dirname } from "node:path";

const schemaVersion = 1;

export function median(samples) {
    if (
        !Array.isArray(samples) ||
        samples.length === 0 ||
        samples.some((sample) => !Number.isFinite(sample) || sample <= 0)
    ) {
        throw new TypeError(
            "Benchmark samples must be a non-empty array of positive finite numbers.",
        );
    }

    const ordered = [...samples].sort((left, right) => left - right);
    const middle = Math.floor(ordered.length / 2);
    return ordered.length % 2 === 0
        ? (ordered[middle - 1] + ordered[middle]) / 2
        : ordered[middle];
}

export function evaluateBenchmarkResults({
    config,
    environment,
    measurements,
    previousState,
    generatedAt,
}) {
    validateConfig(config);
    validateEnvironment(environment);

    const initialize = !isCompatible(
        previousState,
        environment,
        config.scenarios,
    );
    const scenarios = {};
    const nextScenarios = {};

    for (const scenario of config.scenarios) {
        const samples = measurements[scenario.id];
        const currentMilliseconds = median(samples);
        const prior = initialize
            ? undefined
            : previousState.scenarios[scenario.id];
        const baselineMilliseconds =
            prior?.baselineMilliseconds ?? currentMilliseconds;
        const thresholdRatio =
            scenario.maxRegressionRatio ?? config.maxRegressionRatio;
        const ratio = currentMilliseconds / baselineMilliseconds;
        const regressed = !initialize && ratio > thresholdRatio;
        const consecutiveRegressions = regressed
            ? prior.consecutiveRegressions + 1
            : 0;
        const status = initialize
            ? "baseline-initialized"
            : regressed && consecutiveRegressions >= config.confirmationRuns
              ? "confirmed-regression"
              : regressed
                ? "unconfirmed-regression"
                : "passed";
        const nextBaselineMilliseconds = regressed
            ? baselineMilliseconds
            : currentMilliseconds;

        scenarios[scenario.id] = {
            baselineMilliseconds,
            currentMilliseconds,
            name: scenario.name,
            ratio,
            samplesMilliseconds: samples,
            status,
            thresholdRatio,
        };
        nextScenarios[scenario.id] = {
            baselineMilliseconds: nextBaselineMilliseconds,
            consecutiveRegressions,
        };
    }

    const statuses = Object.values(scenarios).map(
        (scenario) => scenario.status,
    );
    const status = initialize
        ? "initialized"
        : statuses.includes("confirmed-regression")
          ? "failed"
          : statuses.includes("unconfirmed-regression")
            ? "warning"
            : "passed";
    const state = {
        environment,
        scenarios: nextScenarios,
        schemaVersion,
        updatedAt: generatedAt,
    };
    const report = {
        environment,
        generatedAt,
        scenarios,
        schemaVersion,
        status,
    };

    return { exitCode: status === "failed" ? 1 : 0, report, state };
}

export async function readJsonIfPresent(path) {
    try {
        return JSON.parse(await readFile(path, "utf8"));
    } catch (error) {
        if (error.code === "ENOENT") {
            return undefined;
        }

        throw error;
    }
}

export async function writeJsonAtomically(path, value) {
    await mkdir(dirname(path), { recursive: true });
    const temporaryPath = `${path}.tmp`;
    await writeFile(
        temporaryPath,
        `${JSON.stringify(value, undefined, 2)}\n`,
        "utf8",
    );
    await rename(temporaryPath, path);
}

export async function appendBenchmarkSummary(path, report) {
    if (!path) {
        return;
    }

    const status = {
        failed: "❌ Confirmed regression",
        initialized: "🆕 Baseline initialized",
        passed: "✅ Passed",
        warning: "⚠️ Regression awaiting confirmation",
    }[report.status];
    const lines = [
        "## ⏱️ Wall-clock benchmark",
        "",
        status,
        "",
        "| Scenario | Current | Baseline | Ratio | Threshold | Status |",
        "| --- | ---: | ---: | ---: | ---: | --- |",
    ];

    for (const scenario of Object.values(report.scenarios)) {
        lines.push(
            `| ${scenario.name} | ${scenario.currentMilliseconds.toFixed(1)} ms | ` +
                `${scenario.baselineMilliseconds.toFixed(1)} ms | ${scenario.ratio.toFixed(3)}× | ` +
                `${scenario.thresholdRatio.toFixed(2)}× | ${scenario.status} |`,
        );
    }

    lines.push(
        "",
        `${report.environment.runnerImage}; ${report.environment.os}; ${report.environment.architecture}; ` +
            `.NET SDK ${report.environment.dotnet}`,
        "",
    );
    await appendFile(path, lines.join("\n"), "utf8");
}

function isCompatible(state, environment, scenarios) {
    return (
        state?.schemaVersion === schemaVersion &&
        state.environment?.architecture === environment.architecture &&
        state.environment?.dotnet === environment.dotnet &&
        state.environment?.os === environment.os &&
        state.environment?.runnerImage === environment.runnerImage &&
        scenarios.every((scenario) => {
            const saved = state.scenarios?.[scenario.id];
            return (
                Number.isFinite(saved?.baselineMilliseconds) &&
                saved.baselineMilliseconds > 0 &&
                Number.isInteger(saved.consecutiveRegressions) &&
                saved.consecutiveRegressions >= 0
            );
        })
    );
}

function validateConfig(config) {
    if (
        !Number.isInteger(config?.confirmationRuns) ||
        config.confirmationRuns < 1
    ) {
        throw new TypeError("confirmationRuns must be a positive integer.");
    }

    if (
        !Number.isFinite(config.maxRegressionRatio) ||
        config.maxRegressionRatio <= 1
    ) {
        throw new TypeError("maxRegressionRatio must be greater than one.");
    }

    if (!Array.isArray(config.scenarios) || config.scenarios.length === 0) {
        throw new TypeError("At least one benchmark scenario is required.");
    }
}

function validateEnvironment(environment) {
    for (const key of ["architecture", "dotnet", "os", "runnerImage"]) {
        if (
            typeof environment?.[key] !== "string" ||
            environment[key].length === 0
        ) {
            throw new TypeError(
                `Benchmark environment ${key} must be a non-empty string.`,
            );
        }
    }
}
