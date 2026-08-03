// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "scrollBar" theme-file style section (see #155). Named enum
/// values are parsed by hand, matching every other theme-JSON enum field in this codebase.</summary>
internal sealed class ScrollBarStyleSection
{
    /// <summary>Gets or sets the named <see cref="ScrollBarChrome"/> value ("thin" or "full").</summary>
    [JsonPropertyName("chrome")]
    public string? Chrome { get; set; }

    /// <summary>Gets or sets the named <see cref="ScrollBarFill"/> value ("line" or "block").</summary>
    [JsonPropertyName("fill")]
    public string? Fill { get; set; }
}
