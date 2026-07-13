// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Unicode;

public abstract partial class Control
{
    /// <summary>Identifies the external margin style property.</summary>
    public static StyleProperty<Thickness> MarginProperty { get; } =
        StyleProperty<Thickness>.Register<Control>("margin", default, Impact.Measure);

    /// <summary>Identifies the internal padding style property.</summary>
    public static StyleProperty<Thickness> PaddingProperty { get; } =
        StyleProperty<Thickness>.Register<Control>("padding", default, Impact.Measure);

    /// <summary>Identifies the foreground style property.</summary>
    public static StyleProperty<Color?> ForegroundProperty { get; } =
        StyleProperty<Color?>.Register<Control>("foreground", null, Impact.Render);

    /// <summary>Identifies the background style property.</summary>
    public static StyleProperty<Color?> BackgroundProperty { get; } =
        StyleProperty<Color?>.Register<Control>("background", null, Impact.Render);

    /// <summary>Identifies the text-attribute style property.</summary>
    public static StyleProperty<TerminalAttributes?> AttributesProperty { get; } =
        StyleProperty<TerminalAttributes?>.Register<Control>(
            "attributes",
            null,
            Impact.Render,
            ValidateThemeAttributes);

    /// <summary>Identifies the typed underline style property.</summary>
    public static StyleProperty<Underline?> UnderlineProperty { get; } =
        StyleProperty<Underline?>.Register<Control>("underline", null, Impact.Render);

    /// <summary>Identifies the underline color style property.</summary>
    public static StyleProperty<Color?> UnderlineColorProperty { get; } =
        StyleProperty<Color?>.Register<Control>("underline-color", null, Impact.Render);

    /// <summary>Identifies the body fill mode style property.</summary>
    public static StyleProperty<FillMode> FillModeProperty { get; } =
        StyleProperty<FillMode>.Register<Control>("fill-mode", FillMode.Transparent, Impact.Render);

    /// <summary>Identifies the border thickness style property.</summary>
    public static StyleProperty<Thickness> BorderThicknessProperty { get; } =
        StyleProperty<Thickness>.Register<Control>(
            "border-thickness",
            default,
            Impact.Measure,
            ValidateThemeBorderThickness);

    /// <summary>Identifies the border glyph style property.</summary>
    public static StyleProperty<Glyphs> BorderStyleProperty { get; } =
        StyleProperty<Glyphs>.Register<Control>("border-glyphs", Glyphs.Default, Impact.Render);

    /// <summary>Identifies the border color style property.</summary>
    public static StyleProperty<Color?> BorderColorProperty { get; } =
        StyleProperty<Color?>.Register<Control>("border-color", null, Impact.Render);

    /// <summary>Identifies the border attribute style property.</summary>
    public static StyleProperty<TerminalAttributes?> BorderAttributesProperty { get; } =
        StyleProperty<TerminalAttributes?>.Register<Control>(
            "border-attributes",
            null,
            Impact.Render,
            ValidateThemeAttributes);

    /// <summary>Identifies the shadow visibility style property.</summary>
    public static StyleProperty<bool> HasShadowProperty { get; } =
        StyleProperty<bool>.Register<Control>("has-shadow", false, Impact.Render);

    /// <summary>Identifies the shadow mode style property.</summary>
    public static StyleProperty<ShadowMode> ShadowModeProperty { get; } =
        StyleProperty<ShadowMode>.Register<Control>(
            "shadow-mode",
            ShadowMode.Composite,
            Impact.Render,
            ValidateThemeShadowMode);

    /// <summary>Identifies the shadow offset style property.</summary>
    public static StyleProperty<Point> ShadowOffsetProperty { get; } =
        StyleProperty<Point>.Register<Control>("shadow-offset", default, Impact.Render);

    /// <summary>Identifies the shadow glyph style property.</summary>
    public static StyleProperty<Rune> ShadowGlyphProperty { get; } =
        StyleProperty<Rune>.Register<Control>(
            "shadow-glyph",
            new Rune('▓'),
            Impact.Render,
            ValidateThemeShadowGlyph);

    /// <summary>Identifies the shadow foreground style property.</summary>
    public static StyleProperty<Color?> ShadowForegroundProperty { get; } =
        StyleProperty<Color?>.Register<Control>("shadow-foreground", null, Impact.Render);

    /// <summary>Identifies the shadow background style property.</summary>
    public static StyleProperty<Color?> ShadowBackgroundProperty { get; } =
        StyleProperty<Color?>.Register<Control>("shadow-background", null, Impact.Render);

    /// <summary>Identifies the shadow attribute style property.</summary>
    public static StyleProperty<TerminalAttributes?> ShadowAttributesProperty { get; } =
        StyleProperty<TerminalAttributes?>.Register<Control>(
            "shadow-attributes",
            null,
            Impact.Render,
            ValidateThemeAttributes);

    /// <summary>Gets or sets external non-collapsing cell edges.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Thickness Margin
    {
        get => GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>Gets or sets internal cell edges around content.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>Gets or sets the optional terminal foreground.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the optional terminal background.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Gets or sets the optional complete text-attribute set.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TerminalAttributes? Attributes
    {
        get => GetValue(AttributesProperty);
        set => SetValue(AttributesProperty, value);
    }

    /// <summary>Gets or sets the optional typed underline variant.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Underline? Underline
    {
        get => GetValue(UnderlineProperty);
        set => SetValue(UnderlineProperty, value);
    }

    /// <summary>Gets or sets the optional semantic underline color.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color? UnderlineColor
    {
        get => GetValue(UnderlineColorProperty);
        set => SetValue(UnderlineColorProperty, value);
    }

    /// <summary>Gets or sets whether the body fill preserves or replaces existing cells.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public FillMode FillMode
    {
        get => GetValue(FillModeProperty);
        set => SetValue(FillModeProperty, value);
    }

    /// <summary>Gets or sets independently enabled zero-or-one-cell border edges.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>Gets or sets the validated physical glyph family used for border edges.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Glyphs BorderStyle
    {
        get => GetValue(BorderStyleProperty);
        set => SetValue(BorderStyleProperty, value);
    }

    /// <summary>Gets or sets the optional border color.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color? BorderColor
    {
        get => GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>Gets or sets the optional border attribute overlay.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TerminalAttributes? BorderAttributes
    {
        get => GetValue(BorderAttributesProperty);
        set => SetValue(BorderAttributesProperty, value);
    }

    /// <summary>Gets or sets whether a compact translated shadow is rendered outside the body.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool HasShadow
    {
        get => GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }

    /// <summary>Gets or sets how the visual shadow changes overflow cells.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ShadowMode ShadowMode
    {
        get => GetValue(ShadowModeProperty);
        set => SetValue(ShadowModeProperty, value);
    }

    /// <summary>Gets or sets the signed terminal-cell translation applied to the shadow.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Point ShadowOffset
    {
        get => GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

    /// <summary>Gets or sets the printable one-cell-wide shadow glyph.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune ShadowGlyph
    {
        get => GetValue(ShadowGlyphProperty);
        set => SetValue(ShadowGlyphProperty, value);
    }

    /// <summary>Gets or sets the optional shadow foreground.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color? ShadowForeground
    {
        get => GetValue(ShadowForegroundProperty);
        set => SetValue(ShadowForegroundProperty, value);
    }

    /// <summary>Gets or sets the optional shadow background.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color? ShadowBackground
    {
        get => GetValue(ShadowBackgroundProperty);
        set => SetValue(ShadowBackgroundProperty, value);
    }

    /// <summary>Gets or sets the optional shadow attribute overlay.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TerminalAttributes? ShadowAttributes
    {
        get => GetValue(ShadowAttributesProperty);
        set => SetValue(ShadowAttributesProperty, value);
    }

    private static void ValidateThemeAttributes(TerminalAttributes? value) =>
        Decoration.Validate(value, null, null);

    private static void ValidateThemeBorderThickness(Thickness value)
    {
        if (value.Left > 1 || value.Top > 1 || value.Right > 1 || value.Bottom > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Every border edge must be zero or one cell.");
        }
    }

    private static void ValidateThemeShadowGlyph(Rune value)
    {
        Span<char> buffer = stackalloc char[2];
        int length = value.EncodeToUtf16(buffer);
        Measurement measurement = Terminal.Unicode.Width.Measure(buffer[..length]);

        if (measurement.Cells != 1 || measurement.Controls != 0)
        {
            throw new ArgumentException(
                "A shadow glyph must be printable and exactly one cell wide.",
                nameof(value));
        }
    }

    private static void ValidateThemeShadowMode(ShadowMode value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The shadow mode is unknown.");
        }
    }
}
