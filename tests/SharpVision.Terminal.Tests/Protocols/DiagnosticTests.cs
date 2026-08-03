// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies structured protocol diagnostics.
/// </summary>
public sealed class DiagnosticTests
{
    /// <summary>
    /// Verifies that diagnostics describe structure without retaining payloads.
    /// </summary>
    [Fact]
    public void ToString_WhenDiagnosticIsSensitive_DoesNotExposePayload()
    {
        const string secret = "clipboard-password-do-not-log";
        var diagnostic = new Diagnostic(
            DiagnosticCode.StringLimit,
            SequenceKind.Osc,
            42,
            secret.Length);

        var text = diagnostic.ToString();

        text.ShouldContain(nameof(DiagnosticCode.StringLimit));
        text.ShouldContain(nameof(SequenceKind.Osc));
        text.ShouldContain("42");
        text.ShouldContain(secret.Length.ToString(CultureInfo.InvariantCulture));
        text.ShouldNotContain(secret);
    }

    /// <summary>
    /// Verifies that invalid positions and counts cannot enter a diagnostic.
    /// </summary>
    [Fact]
    public void Constructor_WhenPositionIsNegative_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new Diagnostic(DiagnosticCode.Malformed, SequenceKind.Csi, -1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new Diagnostic(DiagnosticCode.Malformed, SequenceKind.Csi, 0, -1));
    }

    /// <summary>Verifies an unknown recovery category cannot enter a structured diagnostic.</summary>
    [Fact]
    public void Constructor_WhenCodeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new Diagnostic((DiagnosticCode) 999, SequenceKind.Csi, 0, 0));

        exception.ParamName.ShouldBe("code");
    }

    /// <summary>Verifies an unknown sequence family cannot enter a structured diagnostic.</summary>
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new Diagnostic(DiagnosticCode.Malformed, (SequenceKind) 999, 0, 0));

        exception.ParamName.ShouldBe("kind");
    }
}
