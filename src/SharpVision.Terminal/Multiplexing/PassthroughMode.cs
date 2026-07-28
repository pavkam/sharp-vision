// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Multiplexing;

/// <summary>Describes the explicitly configured multiplexer passthrough gate.</summary>
[PublicAPI]
public enum PassthroughMode
{
    /// <summary>Passthrough is disabled.</summary>
    Disabled,

    /// <summary>Passthrough is allowed only while the originating pane is visible.</summary>
    Visible,

    /// <summary>Passthrough is allowed for visible and hidden panes.</summary>
    All
}
