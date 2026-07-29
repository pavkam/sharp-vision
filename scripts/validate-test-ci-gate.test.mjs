// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

import assert from "node:assert/strict";
import test from "node:test";

import {
    validatePublishResultGate,
    validateTestCiGate,
} from "./validate-test-ci-gate.mjs";

test("validateTestCiGate_WhenRecipeMasksAFailingCommand_ReportsTheLine", () => {
    const makefile = [
        "test-ci: build",
        "\t@dotnet test --project tests/SharpVision.Terminal.Tests || echo \"warning\"",
        "\t@dotnet test --project tests/SharpVision.Tests",
        "\t@dotnet test --project tests/SharpVision.Compatibility.Tests",
        "",
        "test-binding-coverage: build",
        "\t@echo unrelated",
        "",
    ].join("\n");

    const errors = validateTestCiGate(makefile);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /masks a failing command/u);
});

test("validateTestCiGate_WhenACommandIsMaskedWithOrTrue_ReportsTheLine", () => {
    const makefile = [
        "test-ci: build",
        "\t@dotnet test --project tests/SharpVision.Terminal.Tests || true",
        "\t@dotnet test --project tests/SharpVision.Tests",
        "\t@dotnet test --project tests/SharpVision.Compatibility.Tests",
        "",
    ].join("\n");

    const errors = validateTestCiGate(makefile);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /masks a failing command/u);
});

test("validateTestCiGate_WhenCompatibilityTestsAreMissing_ReportsTheGap", () => {
    const makefile = [
        "test-ci: build",
        "\t@dotnet test --project tests/SharpVision.Terminal.Tests",
        "\t@dotnet test --project tests/SharpVision.Tests",
        "",
    ].join("\n");

    const errors = validateTestCiGate(makefile);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /Compatibility\.Tests/u);
});

test("validateTestCiGate_WhenTargetIsMissing_ReportsIt", () => {
    const errors = validateTestCiGate("build: restore\n\t@echo hi\n");

    assert.equal(errors.length, 1);
    assert.match(errors[0], /no 'test-ci' target/u);
});

test("validateTestCiGate_WhenRecipeRunsEveryProjectWithoutMasking_ReturnsNoErrors", () => {
    const makefile = [
        "test-ci: build",
        "\t@dotnet test --project tests/SharpVision.Terminal.Tests --coverage",
        "\t@dotnet test --project tests/SharpVision.Tests --coverage",
        "\t@dotnet test --project tests/SharpVision.Compatibility.Tests",
        "\t@node scripts/validate-control-coverage.mjs",
        "",
    ].join("\n");

    assert.deepEqual(validateTestCiGate(makefile), []);
});

test("validatePublishResultGate_WhenActionFailIsMissing_ReportsIt", () => {
    const yaml = [
        "runs:",
        "  using: composite",
        "  steps:",
        "    - uses: EnricoMi/publish-unit-test-result-action@deadbeef",
        "      with:",
        "        files: \"**/*.trx\"",
        "",
    ].join("\n");

    const errors = validatePublishResultGate(yaml);

    assert.equal(errors.length, 1);
    assert.match(errors[0], /action_fail: true/u);
});

test("validatePublishResultGate_WhenActionFailIsTrue_ReturnsNoErrors", () => {
    const yaml = [
        "runs:",
        "  using: composite",
        "  steps:",
        "    - uses: EnricoMi/publish-unit-test-result-action@deadbeef",
        "      with:",
        "        files: \"**/*.trx\"",
        "        action_fail: true",
        "",
    ].join("\n");

    assert.deepEqual(validatePublishResultGate(yaml), []);
});

test("validatePublishResultGate_WhenStepIsMissing_ReportsIt", () => {
    const errors = validatePublishResultGate("runs:\n  using: composite\n");

    assert.equal(errors.length, 1);
    assert.match(errors[0], /no publish-unit-test-result-action step/u);
});
