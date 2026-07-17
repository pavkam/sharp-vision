// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Provides direct layout and chrome configuration for <see cref="Control"/>.</summary>
public abstract partial class Control
{
    /// <summary>Gets or sets outer non-collapsing cell edges.</summary>
    public Thickness Margin { get; set => _ = SetProperty(ref field, value, ChangeImpact.Measure); }

    /// <summary>Gets or sets inner cell edges around content.</summary>
    public Thickness Padding { get; set => _ = SetProperty(ref field, value, ChangeImpact.Measure); }

    /// <summary>Gets or sets the optional foreground UI color.</summary>
    public ThemeColor? Foreground { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets the optional background UI color; null preserves destination cells.</summary>
    public ThemeColor? Background { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets text attributes.</summary>
    public TerminalAttributes? Attributes
    {
        get;
        set
        {
            Decoration.Validate(value, null, null);
            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets the underline form.</summary>
    public Underline? Underline { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets the optional underline UI color.</summary>
    public ThemeColor? UnderlineColor { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets independently enabled one-cell border edges.</summary>
    public Thickness BorderThickness
    {
        get;
        set
        {
            if (value.Left > 1 || value.Top > 1 || value.Right > 1 || value.Bottom > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Every border edge must be zero or one cell.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    }

    /// <summary>Gets or sets the border glyph family.</summary>
    public Glyphs BorderGlyphs
    {
        get;
        set
        {
            if (value.TopLeft.Value == 0)
            {
                throw new ArgumentException("A complete printable border glyph family is required.", nameof(value));
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = Glyphs.Default;

    /// <summary>Gets or sets the optional border UI color.</summary>
    public ThemeColor? BorderColor { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets border text attributes.</summary>
    public TerminalAttributes? BorderAttributes
    {
        get;
        set
        {
            Decoration.Validate(value, null, null);
            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets whether translated shadow chrome is rendered.</summary>
    public bool HasShadow { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets the shadow composition mode.</summary>
    public ShadowMode ShadowMode
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The shadow mode is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = ShadowMode.Composite;

    /// <summary>Gets or sets the signed shadow translation in cells.</summary>
    public Point ShadowOffset { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets the shadow glyph.</summary>
    public Rune ShadowGlyph
    {
        get;
        set
        {
            Span<char> buffer = stackalloc char[2];
            var length = value.EncodeToUtf16(buffer);
            var measurement = Terminal.Unicode.Width.Measure(buffer[..length]);

            if (measurement.Cells != 1 || measurement.Controls != 0)
            {
                throw new ArgumentException("The shadow glyph must be printable and one cell wide.", nameof(value));
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = new('▓');

    /// <summary>Gets or sets the optional shadow foreground UI color.</summary>
    public ThemeColor? ShadowForeground { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets the optional shadow background UI color.</summary>
    public ThemeColor? ShadowBackground { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets shadow text attributes.</summary>
    public TerminalAttributes? ShadowAttributes
    {
        get;
        set
        {
            Decoration.Validate(value, null, null);
            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    }
}
