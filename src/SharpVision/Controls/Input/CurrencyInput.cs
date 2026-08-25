// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Edits a nullable monetary value through a transient typed buffer committed on Enter or
/// focus loss, formatted and parsed against a culture's currency-specific globalization
/// data.</summary>
/// <remarks>
/// <para>
/// <see cref="CurrencyInput"/> shares its buffer-then-commit editing model with
/// <see cref="NumberInput"/>, including the same private <see cref="NumericEditBuffer"/>
/// primitive for digit, separator, and sign buffering. The buffer itself stays
/// currency-agnostic: this control configures it with a cloned
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
public sealed class CurrencyInput: InputBase
{
    private static readonly string[] _positivePatterns = ["$n", "n$", "$ n", "n $"];

    private static readonly string[] _negativePatterns =
    [
        "($n)", "-$n", "$-n", "$n-",
        "(n$)", "-n$", "n-$", "n$-",
        "-n $", "-$ n", "n $-", "$ n-",
        "$ -n", "n- $", "($ n)", "(n $)"
    ];

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).ToAppearanceStates();

    private readonly NumericEditBuffer _buffer = new();
    private readonly NumericInputCommitCoordinator _coordinator;
    private decimal? _value;

    /// <summary>Initializes a focusable currency field with no committed value.</summary>
    public CurrencyInput()
    {
        TabNavigation = TabNavigation.None;
        _coordinator = new NumericInputCommitCoordinator(
            _buffer,
            () => _value,
            candidate => SetProperty(ref _value, candidate, InvalidationImpact.Render, nameof(Value)),
            () => Minimum,
            () => Maximum,
            () => Step,
            ResolveCommitRounding,
            () => AllowNull,
            () => IsFocused,
            RefreshBuffer,
            (previous, candidate) => ValueChanged?.Invoke(this, new CurrencyInputValueChangedEventArgs(previous, candidate)));
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<CurrencyInputValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the current value, or null when cleared. Assignment clamps silently
    /// into <see cref="Minimum"/> and <see cref="Maximum"/>; a null assignment is a no-op unless
    /// <see cref="AllowNull"/> is set.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal? Value
    {
        get => _value;
        set
        {
            VerifyMutable();

            if (!value.HasValue)
            {
                if (AllowNull)
                {
                    _ = _coordinator.CommitValue(null);
                }

                return;
            }

            _ = _coordinator.CommitValue(_coordinator.ClampToRange(value.Value));
        }
    }

    /// <summary>Gets or sets whether the value may be cleared to null. Default is true.</summary>
    /// <remarks>Disabling this while the value is already null eagerly reseeds it to zero, clamped
    /// into <see cref="Minimum"/> and <see cref="Maximum"/>, raising <see cref="ValueChanged"/> - the
    /// same eager-reseed rule <see cref="NumberInput.AllowNull"/> applies.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AllowNull
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.None) && !value && _value is null)
            {
                _ = _coordinator.CommitValue(_coordinator.ClampToRange(0m));
            }
        }
    } = true;

    /// <summary>Gets or sets the inclusive lower bound. Default is <see cref="decimal.MinValue"/>.</summary>
    /// <remarks>Endpoints may be equal.</remarks>
    /// <exception cref="ArgumentException">The minimum exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Minimum
    {
        get;
        set
        {
            ArgumentException.ThrowIfAboveMaximum(value, Maximum, nameof(value), "Minimum cannot exceed Maximum.");

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _coordinator.RepairValue();
            }
        }
    } = decimal.MinValue;

    /// <summary>Gets or sets the inclusive upper bound. Default is <see cref="decimal.MaxValue"/>.</summary>
    /// <remarks>Endpoints may be equal.</remarks>
    /// <exception cref="ArgumentException">The maximum is below <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Maximum
    {
        get;
        set
        {
            ArgumentException.ThrowIfBelowMinimum(value, Minimum, nameof(value), "Maximum cannot be less than Minimum.");

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _coordinator.RepairValue();
            }
        }
    } = decimal.MaxValue;

    /// <summary>Gets or sets the positive increment Up and Down apply, and the jump Home and End
    /// commit to <see cref="Minimum"/> and <see cref="Maximum"/> land on directly. Default is
    /// <c>1</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Step
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotAPositiveStep(value, nameof(value));

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = 1m;

    /// <summary>Gets or sets an explicit fractional digit count, or null to derive it from
    /// <see cref="NumberFormatInfo.CurrencyDecimalDigits"/> on
    /// <see cref="Culture"/> every time it is needed, so it automatically tracks a runtime
    /// <see cref="Culture"/> change. Default is null.</summary>
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

    /// <summary>Gets or sets whether the idle and freshly focused display groups digits under
    /// <see cref="Culture"/>'s currency-specific group separator and sizes. Purely a display
    /// concern: a typed or pasted group separator is always accepted and stripped while parsing,
    /// regardless of this setting. Default is true.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AllowGrouping
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    } = true;

    /// <summary>Gets or sets the rounding applied when a typed value commits, using the
    /// three-argument <see cref="Math.Round(decimal, int, MidpointRounding)"/> overload
    /// exclusively - never the two-argument overload, which silently rounds to even. Default is
    /// <see cref="MidpointRounding.AwayFromZero"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public MidpointRounding RoundingMode
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The rounding mode is unknown.");
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = MidpointRounding.AwayFromZero;

    /// <summary>Gets or sets the culture whose currency-specific decimal separator, group
    /// separator, group sizes, sign, positive/negative layout pattern, and default symbol govern
    /// display and parsing. Default is <see cref="CultureInfo.InvariantCulture"/>, matching
    /// <see cref="NumberInput.Culture"/>, so out-of-the-box rendering never depends on the host
    /// operating system's locale.</summary>
    /// <remarks>Changing this mid-edit discards any in-progress transient buffer back to the
    /// committed value's formatting under the new culture and re-derives a null
    /// <see cref="DecimalPlaces"/>, rather than migrating a half-parsed string across the
    /// switch.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher, or
    /// the resulting combination of <see cref="DisplayMode"/>, the new culture, and
    /// <see cref="CurrencyOverride"/> cannot resolve a currency identity.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CultureInfo Culture
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = ResolveCurrencyText(DisplayMode, value, CurrencyOverride);

            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                return;
            }

            if (IsFocused)
            {
                RefreshBuffer();
            }
        }
    } = CultureInfo.InvariantCulture;

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
    /// the resulting combination of the new display mode, <see cref="Culture"/>, and
    /// <see cref="CurrencyOverride"/> cannot resolve a currency identity - including
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

    #region Layout

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inboard of the
    /// border and outboard of the value's own composed text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inboard of the
    /// border and outboard of the value's own composed text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var minimumText = FormatValue(Minimum);
        var maximumText = FormatValue(Maximum);
        var widest = minimumText.Length >= maximumText.Length ? minimumText : maximumText;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        return new Size(MeasureCells(widest) + affixes.StartCells + affixes.EndCells, 1);
    }

    #endregion

    #region Input

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
            return;
        }

        // Keeps the buffer's currency-adapted separator/sign tokens and precision in lockstep with
        // the current Culture and DecimalPlaces even if a caller drives keyboard input without ever
        // having routed an actual focus transition through this control - RefreshBuffer's own
        // Configure call (on focus gain, commit, or a mid-edit Culture change) makes this redundant
        // on the ordinary focused path, but costs nothing to repeat here.
        _buffer.Configure(BuildBufferFormat(), EffectiveDecimalPlaces == 0);

        switch (eventArgs)
        {
            case KeyEventArgs key:
                HandleKey(key);
                break;
            case TextEventArgs text:
                _ = _buffer.Insert(text.Text.Value.ToString());
                text.IsHandled = true;
                Invalidate(InvalidationImpact.Render);
                break;
            case PasteEventArgs paste:
                _ = _buffer.Insert(Encoding.UTF8.GetString(paste.Paste.Utf8.Span));
                paste.IsHandled = true;
                Invalidate(InvalidationImpact.Render);
                break;
            case PointerEventArgs pointer:
                HandlePointer(pointer);
                break;
            default:
                break;
        }

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    private void HandleKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (!eventArgs.IsKeyDown)
        {
            return;
        }

        if (TryGetStepDelta(eventArgs, out var delta))
        {
            _ = _coordinator.ApplyStep(delta);
            eventArgs.IsHandled = true;
            return;
        }

#pragma warning disable IDE0072 // Every unmatched key intentionally remains unhandled.
        var handled = stroke.Code switch
        {
            Code.Home => _coordinator.JumpToBound(minimum: true, EffectiveDecimalPlaces),
            Code.End => _coordinator.JumpToBound(minimum: false, EffectiveDecimalPlaces),
            Code.Enter => _coordinator.CommitBuffer(),
            Code.Escape => _coordinator.RevertBuffer(),
            Code.Backspace => _buffer.Backspace(),
            Code.Delete => _buffer.Delete(),
            Code.Left => _buffer.MovePrevious(extend: false),
            Code.Right => _buffer.MoveNext(extend: false),
            Code.Character when stroke.Character is { } ch => _buffer.Insert(ch.ToString()),
            _ => false
        };
#pragma warning restore IDE0072

        if (handled)
        {
            eventArgs.IsHandled = true;
            Invalidate(InvalidationImpact.Render);
        }
    }

    private void HandlePointer(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Press || (pointer.Buttons & Buttons.Primary) == 0)
        {
            return;
        }

        if (pointer.Cells is not { } cells)
        {
            return;
        }

        var content = ContentBounds;

        if (!content.Contains(cells))
        {
            return;
        }

        if (!IsFocused)
        {
            _ = RequestFocus();
        }

        // The rendered text is the currency-composed display (symbol plus sign plus digits), not
        // the buffer's own raw core, so a click column resolves against the composed text first and
        // is then mapped back into the buffer's own index space. It is also further inboard of any
        // configured StartAffix/EndAffix decoration, which - unlike the currency symbol - is never
        // part of the buffer's editable content.
        var valueBox = DeflateForAffixes(content, MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap()));
        var localX = cells.X - valueBox.X;
        var display = BuildFocusedDisplay();
        var composedIndex = IndexAtColumn(display.Text, localX, CellPolicy.AmbiguousWidth);
        var coreIndex = Math.Clamp(composedIndex - display.CoreStart, 0, display.Magnitude.Length);
        _buffer.SetCaret(display.SignLength + coreIndex);
        Invalidate(InvalidationImpact.Render);
        eventArgs.IsHandled = true;
    }

    #endregion

    #region Commit and buffer synchronization

    /// <summary>Resolves the decimal places and rounding policy a freshly parsed buffer value
    /// commits under, for <see cref="NumericInputCommitCoordinator"/>.</summary>
    [Pure]
    private decimal ResolveCommitRounding(decimal parsed) => Math.Round(parsed, EffectiveDecimalPlaces, RoundingMode);

    private void RefreshBuffer()
    {
        _buffer.Configure(BuildBufferFormat(), EffectiveDecimalPlaces == 0);
        _buffer.Load(_value is { } value ? FormatCoreForBuffer(value) : string.Empty);
    }

    #endregion

    #region Formatting

    private int EffectiveDecimalPlaces => DecimalPlaces ?? Culture.NumberFormat.CurrencyDecimalDigits;

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

    /// <summary>Builds a clone of <see cref="Culture"/>'s <see cref="NumberFormatInfo"/>
    /// whose <see cref="NumberFormatInfo.CurrencySymbol"/> is the resolved
    /// <see cref="DisplayMode"/> identity, so the committed idle display can flow entirely through the
    /// runtime's own currency-pattern-aware <c>"C"</c> formatting instead of a hand-built
    /// template.</summary>
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

    /// <summary>Builds a clone of <see cref="Culture"/>'s <see cref="NumberFormatInfo"/>
    /// with the plain-number decimal separator, group separator, and group sizes replaced by the
    /// culture's currency-specific equivalents, so the currency-agnostic
    /// <see cref="NumericEditBuffer"/> parses and formats against the right tokens without needing
    /// to know currency exists.</summary>
    [Pure]
    private NumberFormatInfo BuildBufferFormat()
    {
        var format = (NumberFormatInfo) Culture.NumberFormat.Clone();
        format.NumberDecimalSeparator = format.CurrencyDecimalSeparator;
        format.NumberGroupSeparator = format.CurrencyGroupSeparator;
        format.NumberGroupSizes = format.CurrencyGroupSizes;
        return format;
    }

    [Pure]
    private string FormatValue(decimal value) =>
        value.ToString("C" + EffectiveDecimalPlaces.ToString(CultureInfo.InvariantCulture), BuildFormatInfo());

    /// <summary>Formats the buffer's editable numeric core - magnitude only, with a literal leading
    /// sign token prepended for a negative value - deliberately never through the culture's plain
    /// <see cref="NumberFormatInfo.NumberNegativePattern"/>, which can insert a
    /// space the buffer's own leading-sign grammar does not expect.</summary>
    [Pure]
    private string FormatCoreForBuffer(decimal value)
    {
        var format = BuildBufferFormat();
        var specifier = (AllowGrouping ? "N" : "F") +
            EffectiveDecimalPlaces.ToString(CultureInfo.InvariantCulture);
        var magnitude = Math.Abs(value).ToString(specifier, format);
        return value < 0m ? format.NegativeSign + magnitude : magnitude;
    }

    #endregion

    #region Affix composition

    private readonly record struct FocusedDisplay(string Text, int CoreStart, string Magnitude, int SignLength);

    [Pure]
    private FocusedDisplay BuildFocusedDisplay()
    {
        var format = Culture.NumberFormat;
        var raw = _buffer.Text;
        var magnitude = StripLeadingSign(raw, format, out var isNegative, out var signLength);
        var pattern = isNegative
            ? _negativePatterns[format.CurrencyNegativePattern]
            : _positivePatterns[format.CurrencyPositivePattern];
        var composed = ComposeTemplate(pattern, ResolveCurrencyText(), magnitude, format.NegativeSign, out var coreStart);
        return new FocusedDisplay(composed, coreStart, magnitude, signLength);
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

    [Pure]
    private static int IndexAtColumn(string text, int column, Ambiguous ambiguousWidth)
    {
        var x = 0;

        foreach (var grapheme in Graphemes.Enumerate(text.AsSpan()))
        {
            var cluster = text.AsSpan(grapheme.Offset, grapheme.Length);
            var width = Terminal.Unicode.Width.Measure(cluster, ambiguousWidth).Cells;

            if (column < x + width)
            {
                return grapheme.Offset;
            }

            x += width;
        }

        return text.Length;
    }

    #endregion

    #region Rendering

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var content = ContentBounds;

        if (content.Width == 0 || content.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());

        // Affixes render against the undeflated content box - not the value box below - so a
        // present affix keeps sitting at the true edge even when the value's own box saturates to
        // zero width.
        RenderAffixes(canvas, content, affixes, StartAffix, EndAffix, style);

        var valueBox = DeflateForAffixes(content, affixes);
        string displayText;
        var caretIndex = 0;

        if (IsFocused)
        {
            var focused = BuildFocusedDisplay();
            displayText = focused.Text;
            var caretInCore = Math.Clamp(_buffer.Selection.Caret - focused.SignLength, 0, focused.Magnitude.Length);
            caretIndex = focused.CoreStart + caretInCore;
        }
        else
        {
            displayText = _value is { } value ? FormatValue(value) : string.Empty;
        }

        var clipped = canvas.Clip(new Rect(valueBox.X, valueBox.Y, valueBox.Width, 1));
        _ = clipped.Draw(displayText.AsSpan(), new Point(valueBox.X, valueBox.Y), style, background: BackgroundMode.Transparent);

        if (!IsFocused)
        {
            return;
        }

        var caretColumn = MeasureCells(displayText.AsSpan(0, Math.Min(caretIndex, displayText.Length)));
        var position = new Point(valueBox.X + caretColumn, valueBox.Y);

        if (valueBox.Contains(position) && canvas.Bounds.Contains(position))
        {
            canvas.SetCursor(position, visible: true, CursorShape.Block);
        }
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (focused)
        {
            RefreshBuffer();
        }
        else
        {
            _ = _coordinator.CommitBuffer();
        }

        Invalidate(InvalidationImpact.Render);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            ValueChanged = null;
        }
    }

    #endregion
}
