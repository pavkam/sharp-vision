// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Hosts one detached control under the ordinary semantic application screen.</summary>
internal sealed class HostedControlScreen: Screen
{
    /// <summary>Initializes a screen that owns the supplied detached content.</summary>
    /// <param name="content">The non-null detached control to host.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    internal HostedControlScreen(ControlBase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        InitializeContent(new Overlay { Children = { content } });
    }
}
