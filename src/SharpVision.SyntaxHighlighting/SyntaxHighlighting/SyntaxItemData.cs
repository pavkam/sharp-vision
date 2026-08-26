// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Maps one syntax definition's named <c>&lt;itemData&gt;</c> attribute to its default style role;
/// the default value has an empty name and the default style enumeration value.
/// </summary>
/// <remarks>
/// The KDE XML format also allows an <c>&lt;itemData&gt;</c> to override its role with a literal
/// color, weight, and decoration. SharpVision deliberately does not read those overrides: every
/// token is colored purely by <see cref="DefaultStyle"/> against the active theme, so a theme
/// swap restyles every definition uniformly. See <c>docs/concepts/syntax-highlighting.md</c> for
/// the full rationale.
/// </remarks>
[PublicAPI]
public readonly record struct SyntaxItemData
{
#pragma warning disable IDE0032 // Default structs need null-coalescing getters over nullable backing storage.
    private readonly string? _name;
#pragma warning restore IDE0032

    /// <summary>Initializes a fully specified item-data mapping.</summary>
    /// <param name="name">
    /// The non-null, non-empty name a <see cref="SyntaxContext"/> or <see cref="SyntaxRule"/>
    /// references through its attribute name.
    /// </param>
    /// <param name="defaultStyle">The resolved default style role.</param>
    internal SyntaxItemData(string name, SyntaxDefaultStyle defaultStyle)
    {
        _name = name;
        DefaultStyle = defaultStyle;
    }

    /// <summary>Gets the item-data name, as referenced by an <c>attribute="…"</c> value.</summary>
    public string Name => _name ?? string.Empty;

    /// <summary>Gets the resolved default style role.</summary>
    public SyntaxDefaultStyle DefaultStyle { get; }
}
