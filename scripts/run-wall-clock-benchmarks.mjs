import { spawn } from "node:child_process";
import { performance } from "node:perf_hooks";
import { readFile } from "node:fs/promises";

import {
    appendBenchmarkSummary,
    evaluateBenchmarkResults,
    readJsonIfPresent,
    writeJsonAtomically,
} from "./wall-clock-benchmark.mjs";

const options = parseOptions(process.argv.slice(2));
const generatedAt = new Date().toISOString();
const environment = {
    architecture: process.arch,
    dotnet: (await capture("dotnet", ["--version"])).trim(),
    os: process.platform,
    runnerImage: process.env.SHARPVISION_BENCHMARK_RUNNER ?? "local",
};

try {
    const config = JSON.parse(await readFile(options.config, "utf8"));
    const previousState = await readJsonIfPresent(options.baseline);
    const measurements = {};

    for (const scenario of config.scenarios) {
        for (let index = 0; index < config.warmupRuns; index++) {
            await runScenario(
                scenario,
                options.configuration,
                `warm-up ${index + 1}/${config.warmupRuns}`,
            );
        }

        measurements[scenario.id] = [];
        for (let index = 0; index < config.repetitions; index++) {
            const elapsed = await runScenario(
                scenario,
                options.configuration,
                `measurement ${index + 1}/${config.repetitions}`,
            );
            measurements[scenario.id].push(elapsed);
        }
    }

    const result = evaluateBenchmarkResults({
        config,
        environment,
        generatedAt,
        measurements,
        previousState,
    });
    await writeJsonAtomically(options.baseline, result.state);
    await writeJsonAtomically(options.output, result.report);
    await appendBenchmarkSummary(options.summary, result.report);
    printResult(result.report);
    process.exitCode = result.exitCode;
} catch (error) {
    const report = {
        environment,
        error: error instanceof Error ? error.message : String(error),
        generatedAt,
        scenarios: {},
        schemaVersion: 1,
        status: "error",
    };
    await writeJsonAtomically(options.output, report);
    process.stderr.write(`Benchmark lane failed: ${report.error}\n`);
    process.exitCode = 1;
}

async function runScenario(scenario, configuration, phase) {
    process.stdout.write(`⏱️  ${scenario.name}: ${phase}\n`);
    const started = performance.now();
    await execute("dotnet", [
        "test",
        "--project",
        scenario.project,
        "--configuration",
        configuration,
        "--no-build",
        "--minimum-expected-tests",
        String(scenario.minimumExpectedTests),
        "--timeout",
        scenario.timeout,
        "--parallel",
        "none",
        "--filter-class",
        scenario.filterClass,
    ]);
    return performance.now() - started;
}

function execute(command, arguments_) {
    return new Promise((resolve, reject) => {
        const child = spawn(command, arguments_, { stdio: "inherit" });
        child.once("error", reject);
        child.once("exit", (code, signal) => {
            if (code === 0) {
                resolve();
                return;
            }

            reject(
                new Error(
                    `${command} exited with ${code ?? `signal ${signal}`}.`,
                ),
            );
        });
    });
}

function capture(command, arguments_) {
    return new Promise((resolve, reject) => {
        let output = "";
        const child = spawn(command, arguments_, {
            stdio: ["ignore", "pipe", "inherit"],
        });
        child.stdout.setEncoding("utf8");
        child.stdout.on("data", (chunk) => {
            output += chunk;
        });
        child.once("error", reject);
        child.once("exit", (code, signal) => {
            if (code === 0) {
                resolve(output);
                return;
            }

            reject(
                new Error(
                    `${command} exited with ${code ?? `signal ${signal}`}.`,
                ),
            );
        });
    });
}

function parseOptions(arguments_) {
    const options = {
        baseline: "artifacts/benchmarks/wall-clock-baseline.json",
        config: "benchmarks/wall-clock-thresholds.json",
        configuration: "Release",
        output: "artifacts/benchmarks/wall-clock-report.json",
        summary: process.env.GITHUB_STEP_SUMMARY,
    };

    for (let index = 0; index < arguments_.length; index += 2) {
        const key = arguments_[index];
        const value = arguments_[index + 1];
        const property = {
            "--baseline": "baseline",
            "--config": "config",
            "--configuration": "configuration",
            "--output": "output",
            "--summary": "summary",
        }[key];

        if (!property || value === undefined) {
            throw new TypeError(
                `Unknown or incomplete benchmark option: ${key ?? "<missing>"}.`,
            );
        }

        options[property] = value;
    }

    return options;
}

function printResult(report) {
    process.stdout.write(`\nBenchmark status: ${report.status}\n`);
    for (const scenario of Object.values(report.scenarios)) {
        process.stdout.write(
            `${scenario.name}: ${scenario.currentMilliseconds.toFixed(1)} ms / ` +
                `${scenario.baselineMilliseconds.toFixed(1)} ms = ${scenario.ratio.toFixed(3)}× ` +
                `(${scenario.status})\n`,
        );
    }
}
