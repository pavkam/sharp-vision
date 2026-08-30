// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Owns detected evidence and session verification for one terminal feature.</summary>
internal sealed class CapabilityStatus
{
    /// <summary>Initializes one capability status row.</summary>
    /// <param name="descriptor">The presentation metadata.</param>
    /// <param name="feature">The detected feature evidence.</param>
    internal CapabilityStatus(CapabilityDescriptor descriptor, Feature feature)
    {
        Descriptor = descriptor;
        Feature = feature;
    }

    /// <summary>Gets the presentation metadata.</summary>
    internal CapabilityDescriptor Descriptor { get; }

    /// <summary>Gets the detected evidence.</summary>
    internal Feature Feature { get; private set; }

    /// <summary>Gets the live verification state.</summary>
    internal VerificationState Verification { get; private set; }

    /// <summary>Gets the verification detail.</summary>
    internal string VerificationDetail { get; private set; } = "No matching event or test has been observed.";

    /// <summary>Updates detected evidence after capability refinement.</summary>
    /// <param name="feature">The replacement immutable evidence.</param>
    internal void UpdateFeature(Feature feature) => Feature = feature;

    /// <summary>Updates live verification for the current session.</summary>
    /// <param name="verification">The new verification state.</param>
    /// <param name="detail">The non-empty explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="verification"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="detail"/> is empty.</exception>
    internal void SetVerification(VerificationState verification, string detail)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(verification);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        Verification = verification;
        VerificationDetail = detail;
    }

    /// <summary>Gets the compact row text without relying on color alone.</summary>
    internal string RowText =>
        $"{SupportGlyph(Feature.State)} {Descriptor.Label,-22} {Feature.State,-11} {Feature.Origin,-11} {VerificationGlyph(Verification)} {VerificationLabel(Verification)}";

    /// <inheritdoc/>
    public override string ToString() => RowText;

    private static string SupportGlyph(CapabilitySupport support) => support switch
    {
        CapabilitySupport.Supported => "✓",
        CapabilitySupport.Unsupported => "×",
        CapabilitySupport.Tentative => "~",
        CapabilitySupport.Unknown => "?",
        _ => throw new ArgumentOutOfRangeException(nameof(support), support, "The capability support state is unknown.")
    };

    private static string VerificationGlyph(VerificationState verification) => verification switch
    {
        VerificationState.Observed or VerificationState.Passed => "●",
        VerificationState.Failed => "!",
        VerificationState.NotRun => "○",
        _ => throw new ArgumentOutOfRangeException(nameof(verification), verification, "The verification state is unknown.")
    };

    private static string VerificationLabel(VerificationState verification) => verification switch
    {
        VerificationState.NotRun => "Not run",
        VerificationState.Observed => "Observed",
        VerificationState.Passed => "Passed",
        VerificationState.Failed => "Failed",
        _ => throw new ArgumentOutOfRangeException(nameof(verification), verification, "The verification state is unknown.")
    };
}
