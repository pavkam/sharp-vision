namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Protocols;

using Shouldly;

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
        text.ShouldContain(secret.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        text.ShouldNotContain(secret);
    }

    /// <summary>
    /// Verifies that invalid positions and counts cannot enter a diagnostic.
    /// </summary>
    [Fact]
    public void Constructor_WhenPositionIsNegative_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Diagnostic(DiagnosticCode.Malformed, SequenceKind.Csi, -1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Diagnostic(DiagnosticCode.Malformed, SequenceKind.Csi, 0, -1));
    }
}
