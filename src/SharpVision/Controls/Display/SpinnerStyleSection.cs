// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "spinner" theme-file style section (see #155).</summary>
internal sealed class SpinnerStyleSection
{
    /// <summary>Gets or sets the ordered one-Rune animation frames.</summary>
    [JsonPropertyName("frames")]
    public List<string>? Frames { get; set; }
}
