// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

/// <summary>
/// Verifies <see cref="DescriptionEnvironment"/> bounds the observed environment before a native
/// ncurses lookup ever runs.
/// </summary>
public sealed class DescriptionEnvironmentTests
{
    /// <summary>
    /// Verifies a <c>hex:</c>-prefixed <c>TERMINFO</c> value is accepted even when it exceeds
    /// <see cref="DescriptionLimits.MaxDescriptionPathBytes"/>, as long as the complete observed
    /// environment still fits under <see cref="DescriptionLimits.MaxDescriptionSnapshotBytes"/>.
    /// An encoded terminfo blob is not a filesystem path, so the per-path length policy that
    /// ordinary <c>TERMINFO</c> path values are held to does not apply to it by design.
    /// </summary>
    [Fact]
    public void TryCapture_WhenTerminfoIsHexEncodedAndExceedsPathLimit_Succeeds()
    {
        var limits = DescriptionLimits.Default with { MaxDescriptionPathBytes = 8 };
        var terminfo = "hex:" + new string('a', 100);

        var succeeded = DescriptionEnvironment.TryCapture(
            name => name == "TERMINFO" ? terminfo : null,
            "xterm",
            limits,
            out var environment,
            out var diagnostic);

        succeeded.ShouldBeTrue();
        _ = environment.ShouldNotBeNull();
        diagnostic.ShouldBe(default);
    }

    /// <summary>
    /// Verifies the same <c>hex:</c>-prefixed bypass of the per-path length policy is still
    /// bounded by the aggregate <see cref="DescriptionLimits.MaxDescriptionSnapshotBytes"/> cap:
    /// skipping the per-field check does not exempt the value from every limit.
    /// </summary>
    [Fact]
    public void TryCapture_WhenTerminfoIsHexEncodedAndExceedsSnapshotLimit_Fails()
    {
        var limits = DescriptionLimits.Default with
        {
            MaxDescriptionPathBytes = 8,
            MaxDescriptionSnapshotBytes = 32
        };
        var terminfo = "hex:" + new string('a', 100);

        var succeeded = DescriptionEnvironment.TryCapture(
            name => name == "TERMINFO" ? terminfo : null,
            "xterm",
            limits,
            out var environment,
            out var diagnostic);

        succeeded.ShouldBeFalse();
        environment.ShouldBeNull();
        diagnostic.Code.ShouldBe(DescriptionDiagnosticCode.EnvironmentLimit);
    }

    /// <summary>
    /// Verifies an ordinary (non-encoded) <c>TERMINFO</c> path value is still held to the
    /// per-path length policy, unlike the <c>hex:</c>/<c>b64:</c> encoded forms.
    /// </summary>
    [Fact]
    public void TryCapture_WhenTerminfoIsPlainPathAndExceedsPathLimit_Fails()
    {
        var limits = DescriptionLimits.Default with { MaxDescriptionPathBytes = 8 };
        var terminfo = "/" + new string('a', 100);

        var succeeded = DescriptionEnvironment.TryCapture(
            name => name == "TERMINFO" ? terminfo : null,
            "xterm",
            limits,
            out var environment,
            out var diagnostic);

        succeeded.ShouldBeFalse();
        environment.ShouldBeNull();
        diagnostic.Code.ShouldBe(DescriptionDiagnosticCode.EnvironmentLimit);
    }
}
