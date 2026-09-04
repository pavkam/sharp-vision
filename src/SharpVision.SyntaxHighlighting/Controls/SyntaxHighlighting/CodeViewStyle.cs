// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.SyntaxHighlighting;

using System.Diagnostics.CodeAnalysis;

using SharpVision.SyntaxHighlighting;

/// <summary>
/// Defines one complete immutable <see cref="CodeView"/> presentation: one
/// <see cref="ControlColor"/> per <see cref="SyntaxDefaultStyle"/> role, plus selection and fold
/// gutter colors. This style declares no theme section of its own: it falls back to
/// <see cref="ContainerStyle"/>'s focusable "container" role presentation, resolves its
/// syntax-role colors from semantic colors, and is themeable only through that fallback and a
/// locally assigned <see cref="CodeView.Style"/>.
/// </summary>
/// <remarks>
/// Every role defaults to one of the library's existing <see cref="SemanticColor"/> roles rather
/// than a new one: adding syntax-specific global theme roles would ripple into every built-in
/// theme's own required color set well beyond this optional package's boundary. A theme, or a
/// control instance's own local <see cref="CodeView.Style"/>, can still repaint any
/// individual role directly - the default merely reuses an existing semantic bucket rather than
/// inventing a new one.
/// </remarks>
[PublicAPI]
public sealed record CodeViewStyle: ContainerStyle
{
    /// <summary>Gets the primary code-view style definition.</summary>
    /// <remarks>
    /// Falls back through the public <see cref="Theme.GetFocusableContainerStyleSet"/>, preserving
    /// container geometry while contributing the standard focused border cue.
    /// </remarks>
    internal static StyleDefinition<CodeViewStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetFocusableContainerStyleSet(),
        Complete,
        static (previous, previousTheme, current, currentTheme) => Compare(previous, previousTheme, current, currentTheme));

    // Deliberately never short-circuits on a raw `previous == current` record match: the framework
    // invokes this same delegate for a pure Theme swap with the identical style value on both sides
    // (StyleSlot.GetThemeImpact / ControlBase.GetStyleThemeImpact resolve the SAME LocalValue
    // against the previous and current Theme), specifically so a symbolic role that keeps its
    // record-equal declared value - e.g. every color role above still literally reads
    // SemanticColor.Info - but resolves to a different literal Color under the new theme is caught
    // below. A raw equality fast path here would make that exact case - the common one, since most
    // theme swaps repaint every symbolic color without ever touching a control's own declared style
    // - silently return None, leaving stale colors on screen until some unrelated invalidation
    // happened to repaint the surface. See DocumentStyle.Compare for the same pattern.
    [Pure]
    private static InvalidationImpact Compare(
        CodeViewStyle previous,
        Theme? previousTheme,
        CodeViewStyle current,
        Theme? currentTheme)
    {
        // Glyphs are compared directly - never symbolic, so theme-relative resolution does not
        // apply - the same way DocumentStyle.Compare checks its own Glyphs before any face role.
        if (previous.CollapsedGlyph != current.CollapsedGlyph || previous.ExpandedGlyph != current.ExpandedGlyph)
        {
            return InvalidationImpact.Render;
        }

        foreach (var role in Enum.GetValues<SyntaxDefaultStyle>())
        {
            if (Resolve(previous.ColorFor(role), previousTheme) != Resolve(current.ColorFor(role), currentTheme))
            {
                return InvalidationImpact.Render;
            }
        }

        return Resolve(previous.SelectedTextColor, previousTheme) != Resolve(current.SelectedTextColor, currentTheme) ||
               Resolve(previous.SelectedBackground, previousTheme) != Resolve(current.SelectedBackground, currentTheme) ||
               Resolve(previous.GutterColor, previousTheme) != Resolve(current.GutterColor, currentTheme)
            ? InvalidationImpact.Render
            : InvalidationImpact.None;
    }

    /// <summary>
    /// Resolves one <see cref="ControlColor"/> against an optional theme, the same two-branch rule
    /// <c>ControlBase.ResolveColor</c> applies internally: a literal color resolves to itself, and
    /// a semantic color resolves through the theme, or to <see cref="Color.Default"/> with no
    /// theme. Reimplemented locally because that core helper is <c>protected internal</c> and this
    /// style is declared outside the core assembly and outside <c>ControlBase</c>'s hierarchy.
    /// </summary>
    [Pure]
    private static Color Resolve(ControlColor value, Theme? theme) =>
        value.IsLiteral ? value.Literal : theme?.ResolveColor(value.SemanticColor) ?? Color.Default;

    /// <summary>
    /// Rejects a literal transparent color, the same rule <c>ControlColor.ValidatePaint</c>
    /// applies internally. Reimplemented locally for the same cross-assembly reason as
    /// <see cref="Resolve"/>.
    /// </summary>
    private static void ValidatePaint(ControlColor value, string paramName)
    {
        if (value.IsLiteral && value.Literal.IsTransparent)
        {
            throw new ArgumentException("Transparent is valid only for background composition.", paramName);
        }
    }

    private static CodeViewStyle Complete(ContainerStyle container, VisualState state, Theme theme) =>
        new(
            container.Face,
            container.Border,
            container.Shadow,
            normalColor: SemanticColor.ControlText,
            keywordColor: SemanticColor.Magenta,
            functionColor: SemanticColor.Blue,
            variableColor: SemanticColor.ControlText,
            controlFlowColor: SemanticColor.Magenta,
            operatorColor: SemanticColor.ControlText,
            builtInColor: SemanticColor.Cyan,
            extensionColor: SemanticColor.Cyan,
            preprocessorColor: SemanticColor.Yellow,
            attributeColor: SemanticColor.Cyan,
            charColor: SemanticColor.Green,
            // Cyan, not Green: an escape sequence must stay visible against the surrounding Green
            // string it is nested within, matching base16's dedicated support/escape slot.
            // specialStringColor below follows the same rule for a string that is itself a
            // special/support token rather than ordinary string content.
            specialCharColor: SemanticColor.Cyan,
            stringColor: SemanticColor.Green,
            verbatimStringColor: SemanticColor.Green,
            specialStringColor: SemanticColor.Cyan,
            importColor: SemanticColor.Magenta,
            dataTypeColor: SemanticColor.Cyan,
            decimalValueColor: SemanticColor.Yellow,
            baseNColor: SemanticColor.Yellow,
            floatColor: SemanticColor.Yellow,
            constantColor: SemanticColor.Yellow,
            commentColor: SemanticColor.Muted,
            documentationColor: SemanticColor.Muted,
            // Magenta, not Muted: a Doxygen-style tag (e.g. @param) must pop against the surrounding
            // Muted comment prose it is embedded in, the Kate highlighting convention.
            annotationColor: SemanticColor.Magenta,
            commentVariableColor: SemanticColor.Muted,
            regionMarkerColor: SemanticColor.Muted,
            informationColor: SemanticColor.Info,
            warningColor: SemanticColor.Warning,
            alertColor: SemanticColor.Warning,
            othersColor: SemanticColor.ControlText,
            errorColor: SemanticColor.Error,
            selectedTextColor: SemanticColor.SelectedText,
            selectedBackground: SemanticColor.SelectedControl,
            gutterColor: SemanticColor.Muted,
            collapsedGlyph: new Rune('▶'),
            expandedGlyph: new Rune('▼'));

    /// <summary>Initializes a complete code-view presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="normalColor">The non-transparent <see cref="SyntaxDefaultStyle.Normal"/> foreground.</param>
    /// <param name="keywordColor">The non-transparent <see cref="SyntaxDefaultStyle.Keyword"/> foreground.</param>
    /// <param name="functionColor">The non-transparent <see cref="SyntaxDefaultStyle.Function"/> foreground.</param>
    /// <param name="variableColor">The non-transparent <see cref="SyntaxDefaultStyle.Variable"/> foreground.</param>
    /// <param name="controlFlowColor">The non-transparent <see cref="SyntaxDefaultStyle.ControlFlow"/> foreground.</param>
    /// <param name="operatorColor">The non-transparent <see cref="SyntaxDefaultStyle.Operator"/> foreground.</param>
    /// <param name="builtInColor">The non-transparent <see cref="SyntaxDefaultStyle.BuiltIn"/> foreground.</param>
    /// <param name="extensionColor">The non-transparent <see cref="SyntaxDefaultStyle.Extension"/> foreground.</param>
    /// <param name="preprocessorColor">The non-transparent <see cref="SyntaxDefaultStyle.Preprocessor"/> foreground.</param>
    /// <param name="attributeColor">The non-transparent <see cref="SyntaxDefaultStyle.Attribute"/> foreground.</param>
    /// <param name="charColor">The non-transparent <see cref="SyntaxDefaultStyle.Char"/> foreground.</param>
    /// <param name="specialCharColor">The non-transparent <see cref="SyntaxDefaultStyle.SpecialChar"/> foreground.</param>
    /// <param name="stringColor">The non-transparent <see cref="SyntaxDefaultStyle.String"/> foreground.</param>
    /// <param name="verbatimStringColor">The non-transparent <see cref="SyntaxDefaultStyle.VerbatimString"/> foreground.</param>
    /// <param name="specialStringColor">The non-transparent <see cref="SyntaxDefaultStyle.SpecialString"/> foreground.</param>
    /// <param name="importColor">The non-transparent <see cref="SyntaxDefaultStyle.Import"/> foreground.</param>
    /// <param name="dataTypeColor">The non-transparent <see cref="SyntaxDefaultStyle.DataType"/> foreground.</param>
    /// <param name="decimalValueColor">The non-transparent <see cref="SyntaxDefaultStyle.DecimalValue"/> foreground.</param>
    /// <param name="baseNColor">The non-transparent <see cref="SyntaxDefaultStyle.BaseN"/> foreground.</param>
    /// <param name="floatColor">The non-transparent <see cref="SyntaxDefaultStyle.Float"/> foreground.</param>
    /// <param name="constantColor">The non-transparent <see cref="SyntaxDefaultStyle.Constant"/> foreground.</param>
    /// <param name="commentColor">The non-transparent <see cref="SyntaxDefaultStyle.Comment"/> foreground.</param>
    /// <param name="documentationColor">The non-transparent <see cref="SyntaxDefaultStyle.Documentation"/> foreground.</param>
    /// <param name="annotationColor">The non-transparent <see cref="SyntaxDefaultStyle.Annotation"/> foreground.</param>
    /// <param name="commentVariableColor">The non-transparent <see cref="SyntaxDefaultStyle.CommentVariable"/> foreground.</param>
    /// <param name="regionMarkerColor">The non-transparent <see cref="SyntaxDefaultStyle.RegionMarker"/> foreground.</param>
    /// <param name="informationColor">The non-transparent <see cref="SyntaxDefaultStyle.Information"/> foreground.</param>
    /// <param name="warningColor">The non-transparent <see cref="SyntaxDefaultStyle.Warning"/> foreground.</param>
    /// <param name="alertColor">The non-transparent <see cref="SyntaxDefaultStyle.Alert"/> foreground.</param>
    /// <param name="othersColor">The non-transparent <see cref="SyntaxDefaultStyle.Others"/> foreground.</param>
    /// <param name="errorColor">The non-transparent <see cref="SyntaxDefaultStyle.Error"/> foreground.</param>
    /// <param name="selectedTextColor">The non-transparent selected-text foreground.</param>
    /// <param name="selectedBackground">The non-transparent selected-text background.</param>
    /// <param name="gutterColor">The non-transparent fold-gutter glyph foreground.</param>
    /// <param name="collapsedGlyph">The printable one-cell collapsed-fold arrow.</param>
    /// <param name="expandedGlyph">The printable one-cell expanded-fold arrow.</param>
    /// <exception cref="ArgumentException">A configured color is transparent, or a glyph is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public CodeViewStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor normalColor,
        ControlColor keywordColor,
        ControlColor functionColor,
        ControlColor variableColor,
        ControlColor controlFlowColor,
        ControlColor operatorColor,
        ControlColor builtInColor,
        ControlColor extensionColor,
        ControlColor preprocessorColor,
        ControlColor attributeColor,
        ControlColor charColor,
        ControlColor specialCharColor,
        ControlColor stringColor,
        ControlColor verbatimStringColor,
        ControlColor specialStringColor,
        ControlColor importColor,
        ControlColor dataTypeColor,
        ControlColor decimalValueColor,
        ControlColor baseNColor,
        ControlColor floatColor,
        ControlColor constantColor,
        ControlColor commentColor,
        ControlColor documentationColor,
        ControlColor annotationColor,
        ControlColor commentVariableColor,
        ControlColor regionMarkerColor,
        ControlColor informationColor,
        ControlColor warningColor,
        ControlColor alertColor,
        ControlColor othersColor,
        ControlColor errorColor,
        ControlColor selectedTextColor,
        ControlColor selectedBackground,
        ControlColor gutterColor,
        Rune collapsedGlyph,
        Rune expandedGlyph) : base(face, border, shadow)
    {
        NormalColor = normalColor;
        KeywordColor = keywordColor;
        FunctionColor = functionColor;
        VariableColor = variableColor;
        ControlFlowColor = controlFlowColor;
        OperatorColor = operatorColor;
        BuiltInColor = builtInColor;
        ExtensionColor = extensionColor;
        PreprocessorColor = preprocessorColor;
        AttributeColor = attributeColor;
        CharColor = charColor;
        SpecialCharColor = specialCharColor;
        StringColor = stringColor;
        VerbatimStringColor = verbatimStringColor;
        SpecialStringColor = specialStringColor;
        ImportColor = importColor;
        DataTypeColor = dataTypeColor;
        DecimalValueColor = decimalValueColor;
        BaseNColor = baseNColor;
        FloatColor = floatColor;
        ConstantColor = constantColor;
        CommentColor = commentColor;
        DocumentationColor = documentationColor;
        AnnotationColor = annotationColor;
        CommentVariableColor = commentVariableColor;
        RegionMarkerColor = regionMarkerColor;
        InformationColor = informationColor;
        WarningColor = warningColor;
        AlertColor = alertColor;
        OthersColor = othersColor;
        ErrorColor = errorColor;
        SelectedTextColor = selectedTextColor;
        SelectedBackground = selectedBackground;
        GutterColor = gutterColor;
        CollapsedGlyph = collapsedGlyph.ValidateSingleCell(nameof(collapsedGlyph));
        ExpandedGlyph = expandedGlyph.ValidateSingleCell(nameof(expandedGlyph));
    }

    /// <summary>Gets the standard code-view presentation.</summary>
    // A bare Theme, not ThemeCatalog.Dark: Complete never reads the theme it is given, so any
    // valid instance resolves identically.
    public static new CodeViewStyle Default => Complete(ContainerStyle.Default, VisualState.Normal, new Theme());

    /// <summary>Gets the foreground for one default style role.</summary>
    /// <param name="role">The role to resolve.</param>
    /// <returns>The configured, possibly theme-relative, color for <paramref name="role"/>.</returns>
    [Pure]
    public ControlColor ColorFor(SyntaxDefaultStyle role) => role switch
    {
        SyntaxDefaultStyle.Normal => NormalColor,
        SyntaxDefaultStyle.Keyword => KeywordColor,
        SyntaxDefaultStyle.Function => FunctionColor,
        SyntaxDefaultStyle.Variable => VariableColor,
        SyntaxDefaultStyle.ControlFlow => ControlFlowColor,
        SyntaxDefaultStyle.Operator => OperatorColor,
        SyntaxDefaultStyle.BuiltIn => BuiltInColor,
        SyntaxDefaultStyle.Extension => ExtensionColor,
        SyntaxDefaultStyle.Preprocessor => PreprocessorColor,
        SyntaxDefaultStyle.Attribute => AttributeColor,
        SyntaxDefaultStyle.Char => CharColor,
        SyntaxDefaultStyle.SpecialChar => SpecialCharColor,
        SyntaxDefaultStyle.String => StringColor,
        SyntaxDefaultStyle.VerbatimString => VerbatimStringColor,
        SyntaxDefaultStyle.SpecialString => SpecialStringColor,
        SyntaxDefaultStyle.Import => ImportColor,
        SyntaxDefaultStyle.DataType => DataTypeColor,
        SyntaxDefaultStyle.DecimalValue => DecimalValueColor,
        SyntaxDefaultStyle.BaseN => BaseNColor,
        SyntaxDefaultStyle.Float => FloatColor,
        SyntaxDefaultStyle.Constant => ConstantColor,
        SyntaxDefaultStyle.Comment => CommentColor,
        SyntaxDefaultStyle.Documentation => DocumentationColor,
        SyntaxDefaultStyle.Annotation => AnnotationColor,
        SyntaxDefaultStyle.CommentVariable => CommentVariableColor,
        SyntaxDefaultStyle.RegionMarker => RegionMarkerColor,
        SyntaxDefaultStyle.Information => InformationColor,
        SyntaxDefaultStyle.Warning => WarningColor,
        SyntaxDefaultStyle.Alert => AlertColor,
        SyntaxDefaultStyle.Others => OthersColor,
        SyntaxDefaultStyle.Error => ErrorColor,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown default style role."),
    };

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Normal"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor NormalColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Keyword"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor KeywordColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Function"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor FunctionColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Variable"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor VariableColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.ControlFlow"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ControlFlowColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Operator"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor OperatorColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.BuiltIn"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor BuiltInColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Extension"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ExtensionColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Preprocessor"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor PreprocessorColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Attribute"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor AttributeColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Char"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor CharColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.SpecialChar"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SpecialCharColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.String"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor StringColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.VerbatimString"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor VerbatimStringColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.SpecialString"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SpecialStringColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Import"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ImportColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.DataType"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor DataTypeColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.DecimalValue"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor DecimalValueColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.BaseN"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor BaseNColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Float"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor FloatColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Constant"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ConstantColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Comment"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor CommentColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Documentation"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor DocumentationColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Annotation"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor AnnotationColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.CommentVariable"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor CommentVariableColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.RegionMarker"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor RegionMarkerColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Information"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor InformationColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Warning"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor WarningColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Alert"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor AlertColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Others"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor OthersColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the <see cref="SyntaxDefaultStyle.Error"/> foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ErrorColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the selected-text foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SelectedTextColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the selected-text background.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SelectedBackground
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the fold-gutter glyph foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor GutterColor
    {
        get;
        init
        {
            ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the arrow drawn for a collapsed fold range.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CollapsedGlyph { get; init => field = value.ValidateSingleCell(nameof(value)); }

    /// <summary>Gets the arrow drawn for an expanded fold range's start line.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune ExpandedGlyph { get; init => field = value.ValidateSingleCell(nameof(value)); }
}
