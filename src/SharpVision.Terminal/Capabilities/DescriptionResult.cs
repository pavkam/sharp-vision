// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Owns one typed terminal-description result and its redacted diagnostics.</summary>
[PublicAPI]
public sealed class DescriptionResult
{
    /// <summary>Initializes one internally validated result.</summary>
    private DescriptionResult(
        DescriptionLoadStatus status,
        TerminalProfile? profile,
        IReadOnlyList<DescriptionDiagnostic> diagnostics)
    {
        Status = status;
        Profile = profile;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Gets the load classification.</summary>
    public DescriptionLoadStatus Status { get; }

    /// <summary>Gets the owned loaded profile, including an unsuitable profile.</summary>
    public TerminalProfile? Profile { get; }

    /// <summary>Gets the immutable structured diagnostics.</summary>
    public IReadOnlyList<DescriptionDiagnostic> Diagnostics { get; }

    /// <summary>Creates one loaded result.</summary>
    [Pure]
    internal static DescriptionResult Loaded(
        TerminalProfile profile,
        IReadOnlyList<DescriptionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new DescriptionResult(DescriptionLoadStatus.Loaded, profile, diagnostics);
    }

    /// <summary>Creates one result for ncurses' indistinguishable missing-or-generic status.</summary>
    [Pure]
    internal static DescriptionResult MissingOrGeneric(
        IReadOnlyList<DescriptionDiagnostic>? diagnostics = null) => new(
        DescriptionLoadStatus.MissingOrGeneric,
        null,
        diagnostics ?? Array.Empty<DescriptionDiagnostic>());

    /// <summary>Creates one platform-unavailable result.</summary>
    [Pure]
    internal static DescriptionResult PlatformUnavailable(
        IReadOnlyList<DescriptionDiagnostic>? diagnostics = null) => new(
        DescriptionLoadStatus.PlatformUnavailable,
        null,
        diagnostics ?? Array.Empty<DescriptionDiagnostic>());

    /// <summary>Creates one failed-provider result.</summary>
    [Pure]
    internal static DescriptionResult ProviderFailed(
        IReadOnlyList<DescriptionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new DescriptionResult(
            DescriptionLoadStatus.ProviderFailed,
            null,
            diagnostics);
    }
}
