// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "button" theme-file style section (see #155).</summary>
internal sealed class ButtonStyleSection
{
    /// <summary>Gets or sets the non-negative left/right content padding in cells.</summary>
    [JsonPropertyName("horizontalPadding")]
    public int? HorizontalPadding { get; set; }

    /// <summary>Gets or sets the non-negative top/bottom content padding in cells.</summary>
    [JsonPropertyName("verticalPadding")]
    public int? VerticalPadding { get; set; }
}
