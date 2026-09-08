// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using TextSelection = Text.Selection;

/// <summary>Edits a nullable monetary value through a transient typed buffer committed on Enter or
/// focus loss, formatted and parsed against a culture's currency-specific globalization
/// data.</summary>
/// <remarks>
/// <para>
/// <see cref="CurrencyInput"/> shares its buffer-then-commit editing model with
/// <see cref="NumberInput"/> through their common <see cref="NumericInputBase"/>, including the
/// same <see cref="NumericEditBehavior"/> routed lifecycle and nullable range state. The buffer
/// itself stays currency-agnostic: this control configures it with a cloned
/// <see cref="NumberFormatInfo"/> whose plain-number decimal separator, group
/// separator, and group sizes are replaced with the culture's <c>Currency*</c> equivalents before
/// every commit and refresh, rather than modifying the shared buffer type to understand currency
/// concepts.
/// </para>
/// <para>
/// The currency symbol or code is never part of the edit buffer. While focused, the rendered text
/// composes the resolved currency identity around the buffered numeric core using the culture's
/// resolved <see cref="NumberFormatInfo.CurrencyPositivePattern"/> or
/// <see cref="NumberFormatInfo.CurrencyNegativePattern"/> layout template -
/// never a hand-built "symbol, space, sign, number" assumption. A pattern that wraps a negative
/// amount in parentheses (the accounting convention several cultures, including
/// <see cref="CultureInfo.InvariantCulture"/>, use by default) renders that
/// way even though the buffer itself still holds a literal typed sign character.
/// </para>
/// <para>
/// Localized native digit glyphs (<see cref="NumberFormatInfo.NativeDigits"/>)
/// are outside this control's input contract: only ASCII digits are accepted and rendered.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class CurrencyInput: NumericInputBase
{
    private static readonly string[] _positivePatterns = ["$n", "n$", "$ n", "n $"];

    private static readonly string[] _negativePatterns =
    [
        "($n)", "-$n", "$-n", "$n-",
        "(n$)", "-n$", "n-$", "n$-",
        "-n $", "-$ n", "n $-", "$ n-",
        "$ -n", "n- $", "($ n)", "(n $)",
        "$- n"
    ];

    /// <summary>Initializes a focusable currency field with no committed value.</summary>
    public CurrencyInput()
    {
    }

    /// <summary>Gets or sets an explicit fractional digit count, or null to derive it from
    /// <see cref="NumberFormatInfo.CurrencyDecimalDigits"/> on
    /// <see cref="NumericInputBase.Culture"/> every time it is needed, so it automatically tracks a
    /// runtime <see cref="NumericInputBase.Culture"/> change. Default is null.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int? DecimalPlaces
    {
        get;
        set
        {
            if (value is { } places)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(places);
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets how the currency identity is resolved and composed around the
    /// formatted number. Default is <see cref="CurrencyDisplayMode.Symbol"/>.</summary>
    /// <remarks>
    /// <see cref="CurrencyDisplayMode.IsoCode"/> and <see cref="CurrencyDisplayMode.Name"/> resolve
    /// through <see cref="RegionInfo"/> when <see cref="CurrencyOverride"/> is not set, because
    /// <see cref="NumberFormatInfo"/> does not reliably expose an ISO code or a
    /// localized currency name. <see cref="RegionInfo"/> has no entry for
    /// <see cref="CultureInfo.InvariantCulture"/> or other region-less cultures, so this setter
    /// validates the resulting combination immediately and throws rather than silently rendering
    /// the generic currency sign.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher, or
    /// the resulting combination of the new display mode, <see cref="NumericInputBase.Culture"/>,
    /// and <see cref="CurrencyOverride"/> cannot resolve a currency identity - including
    /// <see cref="CurrencyDisplayMode.Custom"/> while <see cref="CurrencyOverride"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CurrencyDisplayMode DisplayMode
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The display mode is unknown.");
            _ = ResolveCurrencyText(value, Culture, CurrencyOverride);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = CurrencyDisplayMode.Symbol;

    /// <summary>Gets or sets caller-supplied currency identity text that takes precedence over
    /// every <see cref="DisplayMode"/> resolution rule, and is the only source
    /// <see cref="CurrencyDisplayMode.Custom"/> accepts. Default is null.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher, or
    /// clearing this while <see cref="DisplayMode"/> is <see cref="CurrencyDisplayMode.Custom"/>
    /// would leave the currency identity unresolvable.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string? CurrencyOverride
    {
        get;
        set
        {
            _ = ResolveCurrencyText(DisplayMode, Culture, value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    #region Numeric editing seams

    /// <inheritdoc/>
    protected override int EffectiveDecimalPlaces => DecimalPlaces ?? Culture.NumberFormat.CurrencyDecimalDigits;

    /// <inheritdoc/>
    protected override bool IsIntegerOnly => EffectiveDecimalPlaces == 0;

    /// <inheritdoc/>
    protected override void ValidateCulture(CultureInfo culture) =>
        _ = ResolveCurrencyText(DisplayMode, culture, CurrencyOverride);

    /// <inheritdoc/>
    [Pure]
    protected override decimal ResolveCommitRounding(decimal parsed) =>
        NumericInputCommitCoordinator.RoundAtAcceptedPrecision(parsed, EffectiveDecimalPlaces, RoundingMode);

    /// <summary>Builds a clone of <see cref="NumericInputBase.Culture"/>'s
    /// <see cref="NumberFormatInfo"/> with the plain-number decimal separator, group separator, and
    /// group sizes replaced by the culture's currency-specific equivalents, so the currency-agnostic
    /// <see cref="NumericEditBuffer"/> parses and formats against the right tokens without needing
    /// to know currency exists.</summary>
    /// <inheritdoc/>
    [Pure]
    protected override NumberFormatInfo BuildBufferFormat()
    {
        var format = (NumberFormatInfo) Culture.NumberFormat.Clone();
        format.NumberDecimalSeparator = format.CurrencyDecimalSeparator;
        format.NumberGroupSeparator = format.CurrencyGroupSeparator;
        format.NumberGroupSizes = format.CurrencyGroupSizes;
        return format;
    }

    /// <inheritdoc/>
    [Pure]
    protected override string FormatValue(decimal value) =>
        value.ToString(
            "C" + NumericInputCommitCoordinator.RepresentableDecimalPlaces(EffectiveDecimalPlaces)
                .ToString(CultureInfo.InvariantCulture),
            BuildFormatInfo());

    /// <summary>Formats the buffer's editable numeric core - magnitude only, with a literal leading
    /// sign token prepended for a negative value - deliberately never through the culture's plain
    /// <see cref="NumberFormatInfo.NumberNegativePattern"/>, which can insert a
    /// space the buffer's own leading-sign grammar does not expect.</summary>
    /// <inheritdoc/>
    [Pure]
    protected override string FormatBufferValue(decimal value)
    {
        var format = BuildBufferFormat();
        var specifier = (AllowGrouping ? "N" : "F") +
            NumericInputCommitCoordinator.RepresentableDecimalPlaces(EffectiveDecimalPlaces)
                .ToString(CultureInfo.InvariantCulture);
        var magnitude = Math.Abs(value).ToString(specifier, format);
        return value < 0m ? format.NegativeSign + magnitude : magnitude;
    }

    /// <inheritdoc/>
    [Pure]
    protected override int ResolveBufferIndexAtColumn(int column)
    {
        var display = BuildFocusedDisplay();
        var composedIndex = NumericEditBuffer.IndexAtColumn(display.Text, column, CellPolicy.AmbiguousWidth);
        var coreIndex = Math.Clamp(composedIndex - display.CoreStart, 0, display.Magnitude.Length);
        return display.SignLength + coreIndex;
    }

    /// <inheritdoc/>
    [Pure]
    protected override NumericFocusedDisplay ProjectFocusedDisplay()
    {
        if (_buffer.IsEmpty && Placeholder is { Length: > 0 })
        {
            return default;
        }

        var focused = BuildFocusedDisplay();
        var selection = ProjectSelection(focused, _buffer.Selection);
        return new NumericFocusedDisplay(focused.Text, selection, selection.Caret);
    }

    #endregion

    #region Formatting

    [Pure]
    private string ResolveCurrencyText() => ResolveCurrencyText(DisplayMode, Culture, CurrencyOverride);

    [Pure]
    private static string ResolveCurrencyText(CurrencyDisplayMode mode, CultureInfo culture, string? overrideText)
    {
        if (mode == CurrencyDisplayMode.Custom)
        {
            return overrideText ??
                throw new InvalidOperationException(
                    "CurrencyDisplayMode.Custom requires CurrencyOverride to be set.");
        }

        if (overrideText is not null)
        {
            return overrideText;
        }

#pragma warning disable IDE0072 // Every defined mode is matched explicitly; Custom is handled above.
        return mode switch
        {
            CurrencyDisplayMode.Symbol => culture.NumberFormat.CurrencySymbol,
            CurrencyDisplayMode.IsoCode => ResolveRegion(culture).ISOCurrencySymbol,
            CurrencyDisplayMode.Name => ResolveRegion(culture).CurrencyNativeName,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The display mode is unknown.")
        };
#pragma warning restore IDE0072
    }

    [Pure]
    private static RegionInfo ResolveRegion(CultureInfo culture)
    {
        try
        {
            return new RegionInfo(culture.Name);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"Cannot resolve a currency identity for culture '{culture.Name}' under " +
                "CurrencyDisplayMode.IsoCode or CurrencyDisplayMode.Name; set CurrencyOverride instead.",
                ex);
        }
    }

    /// <summary>Builds a clone of <see cref="NumericInputBase.Culture"/>'s
    /// <see cref="NumberFormatInfo"/> whose <see cref="NumberFormatInfo.CurrencySymbol"/> is the
    /// resolved <see cref="DisplayMode"/> identity, so the committed idle display can flow entirely
    /// through the runtime's own currency-pattern-aware <c>"C"</c> formatting instead of a
    /// hand-built template.</summary>
    [Pure]
    private NumberFormatInfo BuildFormatInfo()
    {
        var format = (NumberFormatInfo) Culture.NumberFormat.Clone();
        format.CurrencySymbol = ResolveCurrencyText();

        if (!AllowGrouping)
        {
            format.CurrencyGroupSeparator = string.Empty;
        }

        return format;
    }

    #endregion

    #region Affix composition

    [Pure]
    private static TextSelection ProjectSelection(CurrencyInputFocusedDisplay display, TextSelection selection) => new(
        display.CoreStart + Math.Clamp(selection.Anchor - display.SignLength, 0, display.Magnitude.Length),
        display.CoreStart + Math.Clamp(selection.Caret - display.SignLength, 0, display.Magnitude.Length));

    [Pure]
    private CurrencyInputFocusedDisplay BuildFocusedDisplay()
    {
        var format = Culture.NumberFormat;
        var raw = _buffer.Text;
        var magnitude = StripLeadingSign(raw, format, out var isNegative, out var signLength);
        var pattern = isNegative
            ? _negativePatterns[format.CurrencyNegativePattern]
            : _positivePatterns[format.CurrencyPositivePattern];
        var composed = ComposeTemplate(pattern, ResolveCurrencyText(), magnitude, format.NegativeSign, out var coreStart);
        return new CurrencyInputFocusedDisplay(composed, coreStart, magnitude, signLength);
    }

    [Pure]
    private static string StripLeadingSign(string raw, NumberFormatInfo format, out bool isNegative, out int signLength)
    {
        if (TryStripToken(raw, format.NegativeSign, out var rest, out signLength) ||
            TryStripToken(raw, "-", out rest, out signLength))
        {
            isNegative = true;
            return rest;
        }

        if (TryStripToken(raw, format.PositiveSign, out rest, out signLength) ||
            TryStripToken(raw, "+", out rest, out signLength))
        {
            isNegative = false;
            return rest;
        }

        isNegative = false;
        signLength = 0;
        return raw;
    }

    [Pure]
    private static bool TryStripToken(string text, string token, out string rest, out int length)
    {
        if (!string.IsNullOrEmpty(token) && text.StartsWith(token, StringComparison.Ordinal))
        {
            rest = text[token.Length..];
            length = token.Length;
            return true;
        }

        rest = text;
        length = 0;
        return false;
    }

    [Pure]
    private static string ComposeTemplate(
        string template,
        string symbol,
        string magnitude,
        string negativeSign,
        out int coreStart)
    {
        var builder = new StringBuilder(template.Length + symbol.Length + magnitude.Length);
        coreStart = 0;

        foreach (var ch in template)
        {
            switch (ch)
            {
                case '$':
                    _ = builder.Append(symbol);
                    break;
                case 'n':
                    coreStart = builder.Length;
                    _ = builder.Append(magnitude);
                    break;
                case '-':
                    _ = builder.Append(negativeSign);
                    break;
                default:
                    _ = builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }

    #endregion
}
