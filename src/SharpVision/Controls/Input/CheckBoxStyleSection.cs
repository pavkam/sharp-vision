// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "checkBox" theme-file style section (see #155). Named enum
/// values are parsed by hand, matching every other theme-JSON enum field in this codebase.</summary>
internal sealed class CheckBoxStyleSection
{
    /// <summary>Gets or sets the named <see cref="CheckBoxMarkStyle"/> value ("square", "brackets", or "tick").</summary>
    [JsonPropertyName("markStyle")]
    public string? MarkStyle { get; set; }
}
