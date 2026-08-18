// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines loading and failed one-cell status indicators.</summary>
internal readonly record struct StatusGlyphs
{
    /// <summary>Initializes the complete status indicator family.</summary>
    /// <param name="loading">The in-flight-request indicator.</param>
    /// <param name="failed">The failed-request indicator.</param>
    public StatusGlyphs(ControlGlyph loading, ControlGlyph failed)
    {
        Loading = loading;
        Failed = failed;
    }

    /// <summary>Gets the in-flight-request indicator.</summary>
    public ControlGlyph Loading { get; }

    /// <summary>Gets the failed-request indicator.</summary>
    public ControlGlyph Failed { get; }
}
