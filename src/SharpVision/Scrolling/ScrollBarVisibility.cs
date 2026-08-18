// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Scrolling;

/// <summary>Selects whether one Container scroll bar is hidden, automatic, or permanent.</summary>
[PublicAPI]
public enum ScrollBarVisibility
{
    /// <summary>Suppress the bar without disabling programmatic scrolling.</summary>
    Hidden,

    /// <summary>Show the bar only when content exceeds the candidate viewport.</summary>
    Auto,

    /// <summary>Always reserve and show the bar.</summary>
    Always
}
