// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Detects an attached coverage profiler so wall-clock budgets can opt out of it.</summary>
internal static class CoverageInstrumentation
{
    /// <summary>
    /// Gets whether a CLR profiler is instrumenting this process.
    /// </summary>
    /// <remarks>
    /// Dynamic code-coverage instrumentation hooks every managed method, which inflates absolute
    /// timings by roughly an order of magnitude. A wall-clock budget measured under it gates the
    /// profiler rather than the product, so absolute budgets skip while it is attached. Budgets
    /// expressed as a ratio between two measurements taken in the same process stay valid, because
    /// both sides carry the same overhead, and are deliberately left running. The uninstrumented
    /// performance pass in <c>make test-ci</c> keeps the absolute budgets enforced in CI.
    /// </remarks>
    internal static bool IsProfilerAttached { get; } = string.Equals(
        Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING"),
        "1",
        StringComparison.Ordinal);
}
