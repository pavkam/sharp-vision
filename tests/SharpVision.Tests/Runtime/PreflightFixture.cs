// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Owns one complete deterministic cross-layer preflight arrangement.</summary>
internal sealed class PreflightFixture
{
    /// <summary>Initializes one arrangement from non-null collaborators and owned logs.</summary>
    internal PreflightFixture(
        ConsoleApplicationBuilder builder,
        ProbeScreen screen,
        ConsoleApplicationTransport transport,
        ConsoleApplicationResizeSource resize,
        ConsoleApplicationRestoreLease restore,
        CrossLayerDescriptionProvider provider,
        List<string> disposalOrder,
        List<string> messages)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ArgumentNullException.ThrowIfNull(restore);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(disposalOrder);
        ArgumentNullException.ThrowIfNull(messages);
        Builder = builder;
        Screen = screen;
        Transport = transport;
        Resize = resize;
        Restore = restore;
        Provider = provider;
        DisposalOrder = disposalOrder;
        Messages = messages;
    }

    /// <summary>Gets the configured builder.</summary>
    internal ConsoleApplicationBuilder Builder { get; }

    /// <summary>Gets the detached screen.</summary>
    internal ProbeScreen Screen { get; }

    /// <summary>Gets the recording transport.</summary>
    internal ConsoleApplicationTransport Transport { get; }

    /// <summary>Gets the recording resize source.</summary>
    internal ConsoleApplicationResizeSource Resize { get; }

    /// <summary>Gets the recording restore lease.</summary>
    internal ConsoleApplicationRestoreLease Restore { get; }

    /// <summary>Gets the deterministic provider.</summary>
    internal CrossLayerDescriptionProvider Provider { get; }

    /// <summary>Gets the shared disposal-order log.</summary>
    internal List<string> DisposalOrder { get; }

    /// <summary>Gets plain host messages.</summary>
    internal List<string> Messages { get; }
}
