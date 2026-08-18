// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using Terminfo.Ncurses;
using Terminfo.Windows;

/// <summary>Selects one owned terminal profile from explicit or established platform evidence.</summary>
internal sealed class DescriptionLoader
{
    private readonly IDescriptionProvider _unixProvider;
    private readonly IDescriptionProvider _windowsProvider;

    /// <summary>Initializes a loader with the platform providers used by interactive console hosting.</summary>
    public DescriptionLoader() : this(new Provider(), new WindowsVtProvider())
    {
    }

    /// <summary>Initializes a loader with deterministic platform-provider seams.</summary>
    /// <param name="unixProvider">The non-null Unix ncurses provider.</param>
    /// <param name="windowsProvider">The non-null Windows VT provider.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public DescriptionLoader(
        IDescriptionProvider unixProvider,
        IDescriptionProvider windowsProvider)
    {
        ArgumentNullException.ThrowIfNull(unixProvider);
        ArgumentNullException.ThrowIfNull(windowsProvider);

        _unixProvider = unixProvider;
        _windowsProvider = windowsProvider;
    }

    /// <summary>Resolves one request without silently substituting terminal command programs.</summary>
    /// <param name="request">The non-null immutable resolution request.</param>
    /// <returns>A typed owned profile result or ordinary absence/failure result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public DescriptionResult Load(DescriptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ExplicitProfile is { } explicitProfile)
        {
            return DescriptionResult.Loaded(explicitProfile, Array.Empty<DescriptionDiagnostic>());
        }

        var result = request.Platform switch
        {
            DescriptionPlatform.Unix => _unixProvider.Load(request),
            DescriptionPlatform.Windows when request.WindowsVirtualTerminal => _windowsProvider.Load(request),
            DescriptionPlatform.Windows => DescriptionResult.PlatformUnavailable(),
            _ => throw new UnreachableException()
        };

        return MayUseAnsiFallback(request, result)
            ? CreateAnsiFallback(result.Diagnostics)
            : result;
    }

    private static bool MayUseAnsiFallback(
        DescriptionRequest request,
        DescriptionResult result) =>
        request.Platform == DescriptionPlatform.Unix &&
        request.AllowAnsiFallback &&
        result.Status == DescriptionLoadStatus.PlatformUnavailable;

    private static DescriptionResult CreateAnsiFallback(
        IReadOnlyList<DescriptionDiagnostic> diagnostics)
    {
        var retained = new DescriptionDiagnostic[diagnostics.Count + 1];

        for (var index = 0; index < diagnostics.Count; index++)
        {
            retained[index] = diagnostics[index];
        }

        retained[^1] = new DescriptionDiagnostic(DescriptionDiagnosticCode.AnsiFallback);

        return DescriptionResult.Loaded(
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            retained);
    }
}
