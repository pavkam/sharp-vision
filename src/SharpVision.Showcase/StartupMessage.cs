// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase;

/// <summary>
/// Provides a concise startup description for non-interactive hosts.
/// </summary>
internal static class StartupMessage
{
    /// <summary>
    /// Gets a message that describes the interactive gallery.
    /// </summary>
    /// <returns>The message written by the showcase shell.</returns>
    internal static string Get() =>
        "SharpVision interactive showcase: use the sidebar to explore controls. " +
        "Product specifications start at docs/index.md.";
}
