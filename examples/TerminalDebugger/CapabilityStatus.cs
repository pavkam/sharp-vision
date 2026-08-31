// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Owns detected evidence and session verification for one terminal feature.</summary>
internal sealed class CapabilityStatus
{
    /// <summary>Initializes one capability status row.</summary>
    /// <param name="descriptor">The presentation metadata.</param>
    /// <param name="feature">The detected feature evidence.</param>
    internal CapabilityStatus(CapabilityDescriptor descriptor, Feature? feature)
    {
        Descriptor = descriptor;
        Feature = feature ?? new Feature(CapabilitySupport.Supported, Origin.Default);
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

}
