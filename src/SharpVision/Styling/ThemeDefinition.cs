// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Validated data extracted from one versioned theme document.</summary>
internal sealed class ThemeDefinition
{
    /// <summary>Gets or sets the supported schema version.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the stable catalog slug.</summary>
    public string? Slug { get; set; }

    /// <summary>Gets or sets the color-scheme token (<c>dark</c> or <c>light</c>).</summary>
    public string? ColorScheme { get; set; }

    /// <summary>Gets or sets the deterministic catalog sort key.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets the attribution author.</summary>
    public string? Author { get; set; }

    /// <summary>Gets or sets the license identifier.</summary>
    public string? License { get; set; }

    /// <summary>Gets or sets the source URL.</summary>
    public string? Source { get; set; }

    /// <summary>Gets or sets the named palette (name to color-value string).</summary>
    public Dictionary<string, string>? Palette { get; set; }

    /// <summary>Gets or sets the semantic role map (role name to color-value or palette key).</summary>
    public Dictionary<string, string>? Roles { get; set; }
}
