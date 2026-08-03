// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "radioButton" theme-file style section (see #155). Named enum
/// values are parsed by hand, matching every other theme-JSON enum field in this codebase.</summary>
internal sealed class RadioButtonStyleSection
{
    /// <summary>Gets or sets the named <see cref="RadioButtonMarkStyle"/> value ("circle" or "parentheses").</summary>
    [JsonPropertyName("markStyle")]
    public string? MarkStyle { get; set; }
}
