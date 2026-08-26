// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves the detached public CurrencyInput contract, clamping, rounding, currency
/// identity resolution, and culture-aware currency formatting and parsing.</summary>
public sealed class CurrencyInputTests
{
    /// <summary>Verifies reentrant value publication suppresses the superseded typed event.</summary>
    [Fact]
    public void Value_WhenPropertyObserverCommitsNewerValue_SuppressesStaleTypedEvent()
    {
        using var input = new CurrencyInput();
        var observations = new List<(decimal? EventValue, decimal? LiveValue)>();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CurrencyInput.Value) && input.Value == 10m)
            {
                input.Value = 20m;
            }
        };
        input.ValueChanged += (_, eventArgs) => observations.Add((eventArgs.Value, input.Value));

        input.Value = 10m;

        observations.ShouldBe([(20m, 20m)]);
    }

    /// <summary>Verifies reentrant AllowNull restoration prevents obsolete zero seeding.</summary>
    [Fact]
    public void AllowNull_WhenPropertyObserverRestoresTrue_PreservesNullValue()
    {
        using var input = new CurrencyInput();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CurrencyInput.AllowNull) && !input.AllowNull)
            {
                input.AllowNull = true;
            }
        };

        input.AllowNull = false;

        input.AllowNull.ShouldBeTrue();
        input.Value.ShouldBeNull();
    }

    /// <summary>Verifies both currency endpoints repair the committed value after a throwing observer.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bounds_WhenPropertyObserverThrows_StillClampValue(bool minimum)
    {
        // Arrange
        using var control = new CurrencyInput { Value = minimum ? 10m : 90m };
        var propertyName = minimum ? nameof(CurrencyInput.Minimum) : nameof(CurrencyInput.Maximum);
        control.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == propertyName)
            {
                throw new InvalidOperationException("observer failure");
            }
        };

        // Act
        _ = Should.Throw<InvalidOperationException>(() =>
        {
            if (minimum)
            {
                control.Minimum = 20m;
            }
            else
            {
                control.Maximum = 80m;
            }
        });

        // Assert
        control.Value.ShouldBe(minimum ? 20m : 80m);
    }

    #region Defaults

    /// <summary>Verifies construct when created has documented defaults.</summary>
    [Fact]
    public void Construct_WhenCreated_HasDocumentedDefaults()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Assert
        control.Value.ShouldBeNull();
        control.AllowNull.ShouldBeTrue();
        control.Minimum.ShouldBe(decimal.MinValue);
        control.Maximum.ShouldBe(decimal.MaxValue);
        control.Step.ShouldBe(1m);
        control.DecimalPlaces.ShouldBeNull();
        control.AllowGrouping.ShouldBeTrue();
        control.RoundingMode.ShouldBe(MidpointRounding.AwayFromZero);
        control.Culture.ShouldBeSameAs(CultureInfo.InvariantCulture);
        control.DisplayMode.ShouldBe(CurrencyDisplayMode.Symbol);
        control.CurrencyOverride.ShouldBeNull();
        control.IsFocusable.ShouldBeTrue();
        control.IsTabStop.ShouldBeTrue();
    }

    #endregion

    #region Value and AllowNull

    /// <summary>Verifies value when assigned inside bounds commits.</summary>
    [Fact]
    public void Value_WhenAssignedInsideBounds_Commits()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act
        control.Value = 12.5m;

        // Assert
        control.Value.ShouldBe(12.5m);
    }

    /// <summary>Verifies value when assigned outside bounds clamps silently.</summary>
    [Fact]
    public void Value_WhenAssignedOutsideBounds_ClampsSilently()
    {
        // Arrange
        using var control = new CurrencyInput { Minimum = 0m, Maximum = 10m };

        // Act
        control.Value = 99m;

        // Assert
        control.Value.ShouldBe(10m);
    }

    /// <summary>Verifies value when assigned null and allow null is true clears.</summary>
    [Fact]
    public void Value_WhenAssignedNullAndAllowNullIsTrue_Clears()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m };

        // Act
        control.Value = null;

        // Assert
        control.Value.ShouldBeNull();
    }

    /// <summary>Verifies value when assigned null and allow null is false is no op.</summary>
    [Fact]
    public void Value_WhenAssignedNullAndAllowNullIsFalse_IsNoOp()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m, AllowNull = false };

        // Act
        control.Value = null;

        // Assert
        control.Value.ShouldBe(5m);
    }

    /// <summary>Verifies allow null when disabled while value is null eagerly reseeds to clamped zero and raises event.</summary>
    [Fact]
    public void AllowNull_WhenDisabledWhileValueIsNull_EagerlyReseedsToClampedZeroAndRaisesEvent()
    {
        // Arrange
        using var control = new CurrencyInput { Minimum = 5m, Maximum = 10m };
        CurrencyInputValueChangedEventArgs? observed = null;
        control.ValueChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        control.AllowNull = false;

        // Assert
        control.Value.ShouldBe(5m);
        _ = observed.ShouldNotBeNull();
        observed.PreviousValue.ShouldBeNull();
        observed.Value.ShouldBe(5m);
    }

    /// <summary>Verifies allow null when disabled while value is already set does not change value.</summary>
    [Fact]
    public void AllowNull_WhenDisabledWhileValueIsAlreadySet_DoesNotChangeValue()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 3m };

        // Act
        control.AllowNull = false;

        // Assert
        control.Value.ShouldBe(3m);
    }

    #endregion

    #region Bounds and step

    /// <summary>Verifies minimum when exceeds maximum throws and leaves the prior bound untouched.</summary>
    [Fact]
    public void Minimum_WhenExceedsMaximum_ThrowsAndPreservesPreviousMinimum()
    {
        // Arrange
        using var control = new CurrencyInput { Minimum = 0m, Maximum = 10m };

        // Act
        var exception = Should.Throw<ArgumentException>(() => control.Minimum = 20m);

        // Assert
        exception.ParamName.ShouldBe("value");
        control.Minimum.ShouldBe(0m);
        control.Maximum.ShouldBe(10m);
    }

    /// <summary>Verifies maximum when below minimum throws and leaves the prior bound untouched.</summary>
    [Fact]
    public void Maximum_WhenBelowMinimum_ThrowsAndPreservesPreviousMaximum()
    {
        // Arrange
        using var control = new CurrencyInput { Minimum = 10m, Maximum = 20m };

        // Act
        var exception = Should.Throw<ArgumentException>(() => control.Maximum = 5m);

        // Assert
        exception.ParamName.ShouldBe("value");
        control.Maximum.ShouldBe(20m);
        control.Minimum.ShouldBe(10m);
    }

    /// <summary>Verifies minimum when equal to maximum is accepted.</summary>
    [Fact]
    public void Minimum_WhenEqualToMaximum_IsAccepted()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        Should.NotThrow(() =>
        {
            control.Minimum = 5m;
            control.Maximum = 5m;
        });
        control.Minimum.ShouldBe(5m);
        control.Maximum.ShouldBe(5m);
    }

    /// <summary>Verifies minimum when raised above committed value repairs by clamping and raises
    /// ValueChanged with the exact previous and new committed values.</summary>
    [Fact]
    public void Minimum_WhenRaisedAboveCommittedValue_RepairsByClampingAndRaisesValueChanged()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 3m };
        CurrencyInputValueChangedEventArgs? change = null;
        control.ValueChanged += (_, args) => change = args;

        // Act
        control.Minimum = 5m;

        // Assert
        control.Value.ShouldBe(5m);
        var raised = change.ShouldNotBeNull();
        raised.PreviousValue.ShouldBe(3m);
        raised.Value.ShouldBe(5m);
    }

    /// <summary>Verifies maximum when lowered below committed value repairs by clamping and raises
    /// ValueChanged with the exact previous and new committed values.</summary>
    [Fact]
    public void Maximum_WhenLoweredBelowCommittedValue_RepairsByClampingAndRaisesValueChanged()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 8m };
        CurrencyInputValueChangedEventArgs? change = null;
        control.ValueChanged += (_, args) => change = args;

        // Act
        control.Maximum = 5m;

        // Assert
        control.Value.ShouldBe(5m);
        var raised = change.ShouldNotBeNull();
        raised.PreviousValue.ShouldBe(8m);
        raised.Value.ShouldBe(5m);
    }

    /// <summary>Verifies step when assigned zero or negative throws.</summary>
    [Fact]
    public void Step_WhenAssignedZeroOrNegative_Throws()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Step = 0m);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Step = -1m);
    }

    /// <summary>Verifies step when up is pressed increments by configured step and clamps.</summary>
    [Fact]
    public void Step_WhenUpIsPressed_IncrementsByConfiguredStepAndClamps()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m, Step = 2.5m, Maximum = 6m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Up));

        // Assert
        control.Value.ShouldBe(6m);
    }

    /// <summary>Verifies step when down is pressed decrements by configured step.</summary>
    [Fact]
    public void Step_WhenDownIsPressed_DecrementsByConfiguredStep()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m, Step = 1m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Down));

        // Assert
        control.Value.ShouldBe(4m);
    }

    /// <summary>Verifies jump when home is pressed commits minimum.</summary>
    [Fact]
    public void Jump_WhenHomeIsPressed_CommitsMinimum()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m, Minimum = 0m, Maximum = 10m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Home));

        // Assert
        control.Value.ShouldBe(0m);
    }

    /// <summary>Verifies jump when end is pressed commits maximum.</summary>
    [Fact]
    public void Jump_WhenEndIsPressed_CommitsMaximum()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m, Minimum = 0m, Maximum = 10m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.End));

        // Assert
        control.Value.ShouldBe(10m);
    }

    /// <summary>Verifies jump to Minimum under the default unbounded decimal.MinValue does not
    /// overflow scaling that bound to isolate whole units at the effective decimal places -
    /// decimal.MinValue multiplied by a power of ten to round it exceeds Decimal's own range.</summary>
    [Fact]
    public void Jump_WhenHomeIsPressedUnderDefaultUnboundedMinimum_CommitsMinValueWithoutOverflow()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Home));

        // Assert
        control.Value.ShouldBe(decimal.MinValue);
    }

    /// <summary>Verifies jump to Maximum under the default unbounded decimal.MaxValue does not
    /// overflow scaling that bound to isolate whole units at the effective decimal places -
    /// decimal.MaxValue multiplied by a power of ten to round it exceeds Decimal's own range.</summary>
    [Fact]
    public void Jump_WhenEndIsPressedUnderDefaultUnboundedMaximum_CommitsMaxValueWithoutOverflow()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.End));

        // Assert
        control.Value.ShouldBe(decimal.MaxValue);
    }

    /// <summary>Verifies step when the configured step carries more fractional digits than the
    /// culture's effective decimal places rounds the stepped candidate to that precision - the same
    /// rounding an Enter-commit would apply. Under a yen culture EffectiveDecimalPlaces is zero, so
    /// the edit buffer's integer-only policy makes a typed fractional amount unreachable; stepping
    /// must not reach it either.</summary>
    [Fact]
    public void Step_WhenStepHasMoreFractionalDigitsThanEffectiveDecimalPlaces_RoundsToConfiguredPrecision()
    {
        // Arrange
        using var control = new CurrencyInput { Culture = new CultureInfo("ja-JP"), Step = 0.4m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Up));

        // Assert
        control.Value.ShouldBe(0m);
    }

    /// <summary>Verifies jump to Minimum when the bound carries more fractional digits than the
    /// culture's effective decimal places rounds up to the nearest in-range value at that
    /// precision, rather than naively rounding via RoundingMode and clamping back - the naive
    /// approach would push a rounded Minimum straight back to the unrounded bound it just moved
    /// away from, since ClampToRange's own floor IS that unrounded Minimum, making the rounding a
    /// no-op and leaving Value at a precision the control's own typing path (zero-decimal-place
    /// yen) can never produce.</summary>
    [Fact]
    public void Jump_WhenMinimumHasMoreFractionalDigitsThanEffectiveDecimalPlaces_RoundsUpToNearestInRangeValue()
    {
        // Arrange
        using var control = new CurrencyInput { Culture = new CultureInfo("ja-JP"), Minimum = 0.4m, Maximum = 10m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Home));

        // Assert
        control.Value.ShouldBe(1m);
    }

    /// <summary>Verifies jump to Maximum when the bound carries more fractional digits than the
    /// culture's effective decimal places rounds down to the nearest in-range value - the mirror
    /// image of the Minimum case, rounding away from the bound rather than toward it.</summary>
    [Fact]
    public void Jump_WhenMaximumHasMoreFractionalDigitsThanEffectiveDecimalPlaces_RoundsDownToNearestInRangeValue()
    {
        // Arrange
        using var control = new CurrencyInput { Culture = new CultureInfo("ja-JP"), Minimum = 0m, Maximum = 9.6m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.End));

        // Assert
        control.Value.ShouldBe(9m);
    }

    /// <summary>Verifies jump to Minimum when the whole range is narrower than one unit at the
    /// configured precision falls back to Maximum, rather than committing an out-of-range value -
    /// rounding Minimum up to the nearest in-range step can overshoot Maximum itself in a
    /// sufficiently narrow range, and the trailing clamp exists precisely to catch that.</summary>
    [Fact]
    public void Jump_WhenRangeIsNarrowerThanOneUnitAtEffectiveDecimalPlaces_ClampsToMaximum()
    {
        // Arrange
        using var control = new CurrencyInput { Culture = new CultureInfo("ja-JP"), Minimum = 0.4m, Maximum = 0.6m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Home));

        // Assert
        control.Value.ShouldBe(0.6m);
    }

    #endregion

    #region DecimalPlaces derivation

    /// <summary>Verifies decimal places when assigned negative throws.</summary>
    [Fact]
    public void DecimalPlaces_WhenAssignedNegative_Throws()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.DecimalPlaces = -1);
    }

    /// <summary>Verifies decimal places when null and culture is japanese rejects the decimal
    /// separator keystroke, because CurrencyDecimalDigits is zero for yen.</summary>
    [Fact]
    public void DecimalPlaces_WhenNullAndCultureIsJapanese_DerivesZeroAndRejectsDecimalSeparator()
    {
        // Arrange
        using var control = new CurrencyInput { Culture = new CultureInfo("ja-JP") };

        // Act - the decimal separator keystroke never reaches the buffer once places derive to
        // zero, so "1" and "2" concatenate into a single integer instead of parsing as "1.2".
        TypeCharacter(control, '1');
        TypeCharacter(control, '.');
        TypeCharacter(control, '2');
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(12m);
    }

    /// <summary>Verifies decimal places when explicitly set overrides the culture-derived value.</summary>
    [Fact]
    public void DecimalPlaces_WhenExplicitlySet_OverridesCultureDerivedValue()
    {
        // Arrange - ja-JP alone would derive zero places and reject the decimal separator.
        using var control = new CurrencyInput { Culture = new CultureInfo("ja-JP"), DecimalPlaces = 2 };

        // Act
        TypeCharacter(control, '1');
        TypeCharacter(control, '.');
        TypeCharacter(control, '5');
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(1.5m);
    }

    /// <summary>Verifies direct character editing accepts only text-entry modifier states and
    /// leaves command chords unhandled without contaminating the transient buffer.</summary>
    [Theory]
    [InlineData(Modifiers.None, true)]
    [InlineData(Modifiers.Shift, true)]
    [InlineData(Modifiers.CapsLock | Modifiers.NumLock, true)]
    [InlineData(Modifiers.Control, false)]
    [InlineData(Modifiers.Alt, false)]
    [InlineData(Modifiers.Super, false)]
    [InlineData(Modifiers.Hyper, false)]
    [InlineData(Modifiers.Meta, false)]
    public void Input_WhenCharacterCarriesModifiers_EditsOnlyForTextEntryState(
        Modifiers modifiers,
        bool expectedEdit)
    {
        // Arrange
        using var control = new CurrencyInput { Value = 4m, DecimalPlaces = 0 };
        TypeCharacter(control, '5');
        var key = CharacterKey('9', modifiers);

        // Act
        _ = Router.Route(control, Events.Key, key);
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(expectedEdit ? 59m : 5m);
        key.IsHandled.ShouldBe(expectedEdit);
    }

    /// <summary>Verifies culture when changed after construction re-derives the effective decimal
    /// places used by the next commit, without needing an explicit DecimalPlaces assignment.</summary>
    [Fact]
    public void Culture_WhenChangedAfterConstruction_ReDerivesEffectiveDecimalPlacesForTheNextCommit()
    {
        // Arrange - invariant derives two places by default.
        using var control = new CurrencyInput();

        // Act - switching to a culture whose currency has no minor unit before any typing happens.
        control.Culture = new CultureInfo("ja-JP");
        TypeCharacter(control, '1');
        TypeCharacter(control, '.');
        TypeCharacter(control, '2');
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(12m);
    }

    #endregion

    #region RoundingMode

    /// <summary>Verifies rounding mode when assigned undefined value throws before mutation.</summary>
    [Fact]
    public void RoundingMode_WhenAssignedUndefinedValue_ThrowsBeforeMutation()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.RoundingMode = (MidpointRounding) 99);
        control.RoundingMode.ShouldBe(MidpointRounding.AwayFromZero);
    }

    #endregion

    #region Typed buffer commit and rounding

    /// <summary>Verifies commit when a typed value sits at a rounding midpoint at one decimal place
    /// rounds per the configured mode, pinning the AwayFromZero vs ToEven divergence.</summary>
    [Theory]
    [InlineData(MidpointRounding.AwayFromZero, "2.25", "2.3")]
    [InlineData(MidpointRounding.ToEven, "2.25", "2.2")]
    [InlineData(MidpointRounding.AwayFromZero, "2.35", "2.4")]
    [InlineData(MidpointRounding.ToEven, "2.35", "2.4")]
    public void Commit_WhenTypedValueIsAtAMidpointAtOneDecimalPlace_RoundsPerConfiguredMode(
        MidpointRounding mode,
        string typed,
        string expected)
    {
        // Arrange
        using var control = new CurrencyInput { DecimalPlaces = 1, RoundingMode = mode };

        foreach (var ch in typed)
        {
            TypeCharacter(control, ch);
        }

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    /// <summary>Verifies commit when typed text is grouped and decimal parses and rounds to
    /// configured places, using the invariant culture's currency separators.</summary>
    [Fact]
    public void Commit_WhenTypedTextIsGroupedAndDecimal_ParsesAndRoundsToConfiguredPlaces()
    {
        // Arrange
        using var control = new CurrencyInput { DecimalPlaces = 1 };

        foreach (var ch in "1,234.56")
        {
            TypeCharacter(control, ch);
        }

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(1234.6m);
    }

    /// <summary>Verifies commit when buffer overflows decimal range rejects and keeps prior value.</summary>
    [Fact]
    public void Commit_WhenBufferOverflowsDecimalRange_RejectsAndKeepsPriorValue()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m };
        var overflowing = decimal.MaxValue.ToString("F0", CultureInfo.InvariantCulture) + "9";

        foreach (var ch in overflowing)
        {
            TypeCharacter(control, ch);
        }

        // Act
        _ = Should.NotThrow(() => Router.Route(control, Events.Key, Key(Code.Enter)));

        // Assert
        control.Value.ShouldBe(5m);
    }

    /// <summary>Verifies commit when buffer is empty and allow null is false leaves the value
    /// untouched.</summary>
    [Fact]
    public void Commit_WhenBufferIsEmptyAndAllowNullIsFalse_LeavesValueUntouched()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 4m, AllowNull = false };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(4m);
    }

    /// <summary>Verifies escape when buffer has uncommitted edits reverts without committing.</summary>
    [Fact]
    public void Escape_WhenBufferHasUncommittedEdits_RevertsWithoutCommitting()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 4m };
        TypeCharacter(control, '9');
        var raised = 0;
        control.ValueChanged += (_, _) => raised++;

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Escape));

        // Assert
        control.Value.ShouldBe(4m);
        raised.ShouldBe(0);
    }

    /// <summary>Verifies command-modified Escape leaves the pending currency edit available to commit.</summary>
    [Fact]
    public void Escape_WhenCommandModified_LeavesPendingEditUnhandled()
    {
        using var control = new CurrencyInput { Value = 4m };
        TypeCharacter(control, '9');
        var escape = Key(Code.Escape, Modifiers.Control);

        _ = Router.Route(control, Events.Key, escape);
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        escape.IsHandled.ShouldBeFalse();
        control.Value.ShouldBe(9m);
    }

    /// <summary>Verifies commit when only a sign is typed does not commit garbage, because a lone
    /// sign is a valid partial edit state but never a complete parseable number.</summary>
    [Fact]
    public void Commit_WhenOnlyASignIsTyped_DoesNotCommit()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 4m };
        TypeCharacter(control, '-');

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(4m);
    }

    /// <summary>Verifies commit when the ASCII hyphen-minus is typed accepts it as the negative
    /// sign even under a culture whose own NegativeSign is a different glyph.</summary>
    [Fact]
    public void Commit_WhenAsciiHyphenIsTypedUnderSwedishCulture_AcceptsItAsNegative()
    {
        // Arrange - sv-SE's own NegativeSign is U+2212 (minus sign), not the ASCII hyphen-minus.
        var culture = new CultureInfo("sv-SE");
        culture.NumberFormat.NegativeSign.ShouldBe("−");
        using var control = new CurrencyInput { Culture = culture, DecimalPlaces = 0 };

        // Act
        TypeCharacter(control, '-');
        TypeCharacter(control, '5');
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(-5m);
    }

    /// <summary>Verifies commit when the culture's own negative sign glyph is typed accepts it.</summary>
    [Fact]
    public void Commit_WhenCultureNegativeSignGlyphIsTypedUnderSwedishCulture_AcceptsIt()
    {
        // Arrange
        var culture = new CultureInfo("sv-SE");
        using var control = new CurrencyInput { Culture = culture, DecimalPlaces = 0 };

        // Act
        TypeCharacter(control, '−');
        TypeCharacter(control, '5');
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(-5m);
    }

    #endregion

    #region Paste

    /// <summary>Verifies paste when payload is valid accepts and commits it.</summary>
    [Fact]
    public void Paste_WhenPayloadIsValid_AcceptsAndCommits()
    {
        // Arrange
        using var control = new CurrencyInput { DecimalPlaces = 0 };

        // Act
        _ = Router.Route(control, Events.Paste, new PasteEventArgs(new Paste("456"u8)));
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(456m);
    }

    /// <summary>Verifies paste when payload is invalid rejects the whole paste, without silently
    /// stripping the offending characters, leaving prior typed text intact.</summary>
    [Fact]
    public void Paste_WhenPayloadIsInvalid_RejectsWholePaste()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 1m, DecimalPlaces = 0 };
        TypeCharacter(control, '9');

        // Act
        _ = Router.Route(control, Events.Paste, new PasteEventArgs(new Paste("12a34"u8)));
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert - the invalid paste never applied, so the typed "9" commits unchanged.
        control.Value.ShouldBe(9m);
    }

    #endregion

    #region Culture matrix

    /// <summary>Verifies culture when constructed defaults to invariant.</summary>
    [Fact]
    public void Culture_WhenConstructed_DefaultsToInvariant()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Assert
        control.Culture.ShouldBeSameAs(CultureInfo.InvariantCulture);
    }

    /// <summary>Verifies culture when assigned null throws.</summary>
    [Fact]
    public void Culture_WhenAssignedNull_Throws()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => control.Culture = null!);
    }

    /// <summary>Verifies commit when typed with a culture's currency-specific group and decimal
    /// separators parses correctly - the buffer reads Currency* NumberFormatInfo members, never the
    /// plain-number ones, across en-US, de-DE, sv-SE (U+00A0 grouping), de-CH (negative pattern 2),
    /// and en-IN ([3,2] lakh/crore grouping).</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    [InlineData("de-CH")]
    [InlineData("en-IN")]
    public void Commit_WhenTypedWithCultureCurrencySeparators_ParsesUsingCurrencySpecificTokens(string cultureName)
    {
        // Arrange - oracle-derived typed text: built from the culture's own CurrencyGroupSeparator
        // and CurrencyDecimalSeparator rather than a hardcoded literal.
        var culture = new CultureInfo(cultureName);
        using var control = new CurrencyInput { Culture = culture };
        var format = culture.NumberFormat;
        var typed = "1" + format.CurrencyGroupSeparator + "234" + format.CurrencyDecimalSeparator + "56";

        foreach (var ch in typed)
        {
            TypeCharacter(control, ch);
        }

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(1234.56m);
    }

    /// <summary>Verifies commit when typed with French currency formatting parses correctly. French
    /// groups currency digits with U+202F NARROW NO-BREAK SPACE, a distinct code point from the more
    /// common U+00A0 NO-BREAK SPACE other space-grouping cultures use; both are single UTF-16 code
    /// units.</summary>
    [Fact]
    public void Commit_WhenTypedWithFrenchNarrowNoBreakSpaceGroupSeparator_ParsesCorrectly()
    {
        // Arrange
        var culture = new CultureInfo("fr-FR");
        culture.NumberFormat.CurrencyGroupSeparator.ShouldBe(" ");
        using var control = new CurrencyInput { Culture = culture };
        var format = culture.NumberFormat;
        var typed = "1" + format.CurrencyGroupSeparator + "234" + format.CurrencyDecimalSeparator + "56";

        foreach (var ch in typed)
        {
            TypeCharacter(control, ch);
        }

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(1234.56m);
    }

    #endregion

    #region DisplayMode and CurrencyOverride resolution

    /// <summary>Verifies display mode when symbol under invariant culture never throws, because
    /// NumberFormatInfo.CurrencySymbol is always resolvable.</summary>
    [Fact]
    public void DisplayMode_WhenSymbolUnderInvariantCulture_DoesNotThrow()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.NotThrow(() => control.DisplayMode = CurrencyDisplayMode.Symbol);
    }

    /// <summary>Verifies display mode when iso code and currency override is set resolves from the
    /// override instead of RegionInfo, even under invariant culture.</summary>
    [Fact]
    public void DisplayMode_WhenIsoCodeAndCurrencyOverrideIsSet_DoesNotThrowUnderInvariantCulture()
    {
        // Arrange
        using var control = new CurrencyInput { CurrencyOverride = "XAU" };

        // Act and assert
        _ = Should.NotThrow(() => control.DisplayMode = CurrencyDisplayMode.IsoCode);
    }

    /// <summary>Verifies display mode when name and currency override is set resolves from the
    /// override, even under invariant culture.</summary>
    [Fact]
    public void DisplayMode_WhenNameAndCurrencyOverrideIsSet_DoesNotThrowUnderInvariantCulture()
    {
        // Arrange
        using var control = new CurrencyInput { CurrencyOverride = "Gold ounce" };

        // Act and assert
        _ = Should.NotThrow(() => control.DisplayMode = CurrencyDisplayMode.Name);
    }

    /// <summary>Verifies display mode when iso code and culture is german resolves through
    /// RegionInfo without an override or a thrown exception.</summary>
    [Fact]
    public void DisplayMode_WhenIsoCodeAndCultureIsGerman_ResolvesThroughRegionInfoWithoutThrowing()
    {
        // Arrange
        using var control = new CurrencyInput { Culture = new CultureInfo("de-DE") };

        // Act and assert
        _ = Should.NotThrow(() => control.DisplayMode = CurrencyDisplayMode.IsoCode);
    }

    /// <summary>Verifies display mode when iso code and culture is invariant without an override
    /// throws at set time rather than silently rendering the generic currency sign.</summary>
    [Fact]
    public void DisplayMode_WhenIsoCodeAndCultureIsInvariantWithoutOverride_ThrowsAtSetTime()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => control.DisplayMode = CurrencyDisplayMode.IsoCode);
        control.DisplayMode.ShouldBe(CurrencyDisplayMode.Symbol);
    }

    /// <summary>Verifies display mode when name and culture is invariant without an override throws
    /// at set time.</summary>
    [Fact]
    public void DisplayMode_WhenNameAndCultureIsInvariantWithoutOverride_ThrowsAtSetTime()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => control.DisplayMode = CurrencyDisplayMode.Name);
    }

    /// <summary>Verifies display mode when custom without a currency override throws.</summary>
    [Fact]
    public void DisplayMode_WhenCustomWithoutOverride_Throws()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => control.DisplayMode = CurrencyDisplayMode.Custom);
    }

    /// <summary>Verifies display mode when custom with an override does not throw.</summary>
    [Fact]
    public void DisplayMode_WhenCustomWithOverride_DoesNotThrow()
    {
        // Arrange
        using var control = new CurrencyInput { CurrencyOverride = "MyCoin" };

        // Act and assert
        _ = Should.NotThrow(() => control.DisplayMode = CurrencyDisplayMode.Custom);
    }

    /// <summary>Verifies display mode when assigned an undefined value throws.</summary>
    [Fact]
    public void DisplayMode_WhenAssignedUndefinedValue_Throws()
    {
        // Arrange
        using var control = new CurrencyInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.DisplayMode = (CurrencyDisplayMode) 99);
    }

    /// <summary>Verifies currency override when cleared while display mode is custom throws, rather
    /// than leaving the currency identity unresolvable.</summary>
    [Fact]
    public void CurrencyOverride_WhenClearedWhileDisplayModeIsCustom_Throws()
    {
        // Arrange
        using var control = new CurrencyInput { CurrencyOverride = "MyCoin", DisplayMode = CurrencyDisplayMode.Custom };

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => control.CurrencyOverride = null);
        control.CurrencyOverride.ShouldBe("MyCoin");
    }

    /// <summary>Verifies culture when changed to invariant while display mode is iso code without an
    /// override throws at set time, leaving the prior culture in place.</summary>
    [Fact]
    public void Culture_WhenChangedToInvariantWhileDisplayModeIsIsoCodeWithoutOverride_Throws()
    {
        // Arrange
        using var control = new CurrencyInput { Culture = new CultureInfo("de-DE"), DisplayMode = CurrencyDisplayMode.IsoCode };

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => control.Culture = CultureInfo.InvariantCulture);
        control.Culture.Name.ShouldBe("de-DE");
    }

    #endregion

    #region Commit event ordering

    /// <summary>Verifies commit when value changes raises value changed exactly once.</summary>
    [Fact]
    public void Commit_WhenValueChanges_RaisesValueChangedExactlyOnce()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 1m };
        var raised = 0;
        CurrencyInputValueChangedEventArgs? observed = null;
        control.ValueChanged += (_, eventArgs) =>
        {
            raised++;
            observed = eventArgs;
        };

        // Act
        control.Value = 2m;

        // Assert
        raised.ShouldBe(1);
        _ = observed.ShouldNotBeNull();
        observed.PreviousValue.ShouldBe(1m);
        observed.Value.ShouldBe(2m);
    }

    /// <summary>Verifies commit when value is unchanged does not raise value changed.</summary>
    [Fact]
    public void Commit_WhenValueIsUnchanged_DoesNotRaiseValueChanged()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 1m };
        var raised = 0;
        control.ValueChanged += (_, _) => raised++;

        // Act
        control.Value = 1m;

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies commit when multiple digits are typed before enter raises value changed
    /// exactly once, not once per keystroke.</summary>
    [Fact]
    public void Commit_WhenMultipleDigitsAreTypedBeforeEnter_RaisesValueChangedExactlyOnce()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 1m, DecimalPlaces = 0 };
        var raised = 0;
        control.ValueChanged += (_, _) => raised++;

        // Act
        TypeCharacter(control, '2');
        TypeCharacter(control, '3');
        TypeCharacter(control, '4');
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        raised.ShouldBe(1);
        control.Value.ShouldBe(234m);
    }

    #endregion

    #region Measurement

    /// <summary>Verifies measure when bounds are large widens to fit the widest formatted bound.</summary>
    [Fact]
    public void Measure_WhenBoundsAreLarge_WidensToFitTheWidestFormattedBound()
    {
        // Arrange
        using var narrow = new CurrencyInput { Minimum = 0m, Maximum = 9m, DecimalPlaces = 0, AllowGrouping = false };
        using var wide = new CurrencyInput { Minimum = -999999m, Maximum = 999999m, DecimalPlaces = 0, AllowGrouping = false };
        new LayoutEngine().Layout(narrow, new Size(80, 3));
        new LayoutEngine().Layout(wide, new Size(80, 3));

        // Assert
        wide.DesiredSize.Width.ShouldBeGreaterThan(narrow.DesiredSize.Width);
    }

    /// <summary>Verifies grouped digit display measures wider than ungrouped display for the same
    /// bound, proving AllowGrouping actually reaches the widest-formatted-bound measurement it
    /// documents rather than only affecting a currently-unmeasured idle display string.</summary>
    [Fact]
    public void Measure_WhenAllowGroupingIsTrue_WidensToFitGroupedDigits()
    {
        // Arrange
        using var grouped = new CurrencyInput { Minimum = 0m, Maximum = 999999m, DecimalPlaces = 0 };
        using var ungrouped = new CurrencyInput { Minimum = 0m, Maximum = 999999m, DecimalPlaces = 0, AllowGrouping = false };

        // Act
        new LayoutEngine().Layout(grouped, new Size(80, 3));
        new LayoutEngine().Layout(ungrouped, new Size(80, 3));

        // Assert
        grouped.DesiredSize.Width.ShouldBeGreaterThan(ungrouped.DesiredSize.Width);
    }

    #endregion

    #region Affixes

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus the
    /// shared theme gap, over an equivalent affixless CurrencyInput.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedExtraWidth)
    {
        // Arrange
        using var baseline = new CurrencyInput { Minimum = 0m, Maximum = 9m, DecimalPlaces = 0, AllowGrouping = false };
        new LayoutEngine().Layout(baseline, new Size(80, 3));
        using var control = new CurrencyInput
        {
            Minimum = 0m,
            Maximum = 9m,
            DecimalPlaces = 0,
            AllowGrouping = false,
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };

        // Act
        new LayoutEngine().Layout(control, new Size(80, 3));

        // Assert
        control.DesiredSize.Width.ShouldBe(baseline.DesiredSize.Width + expectedExtraWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure - the
    /// reserved width changes between zero and non-zero cells.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m, DecimalPlaces = 0 };
        control.Measure(new Constraint(10, 3));
        control.Arrange(new Rect(0, 0, 10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);
        control.Clear(Invalidation.All);

        // Act
        control.StartAffix = new Affix("!");

        // Assert
        control.Pending.ShouldBe(Invalidation.All);
        control.Clear(Invalidation.All);

        // Act
        control.StartAffix = null;

        // Assert
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only.</summary>
    [Fact]
    public void EndAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m, DecimalPlaces = 0, EndAffix = new Affix("|") };
        control.Measure(new Constraint(10, 3));
        control.Arrange(new Rect(0, 0, 10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);
        control.Clear(Invalidation.All);

        // Act
        control.EndAffix = new Affix("/");

        // Assert
        control.Pending.ShouldBe(Invalidation.Render);
        control.Clear(Invalidation.All);

        // Act
        control.EndAffix = new Affix("/", "?", SemanticColor.Warning);

        // Assert
        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change invalidates Measure again, not just Render, even
    /// though both affix values are non-null.</summary>
    [Fact]
    public void StartAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        // Arrange
        using var control = new CurrencyInput { Value = 5m, DecimalPlaces = 0, StartAffix = new Affix("!") };
        control.Measure(new Constraint(10, 3));
        control.Arrange(new Rect(0, 0, 10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);
        control.Clear(Invalidation.All);

        // Act - U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        control.StartAffix = new Affix("世");

        // Assert
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op, matching every other
    /// SetProperty-backed member.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        // Arrange
        var affix = new Affix("!");
        using var control = new CurrencyInput { Value = 5m, DecimalPlaces = 0, StartAffix = affix };
        control.Measure(new Constraint(10, 3));
        control.Arrange(new Rect(0, 0, 10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);
        control.Clear(Invalidation.All);

        // Act
        control.StartAffix = affix;

        // Assert
        control.Pending.ShouldBe(Invalidation.None);
    }

    #endregion

    #region IsEnabled contract

    /// <summary>Verifies enabled when control is disabled refuses focus and reports ineffective enabled.</summary>
    [Fact]
    public void Enabled_WhenControlIsDisabled_RefusesFocusAndReportsIneffectiveEnabled()
    {
        // Arrange
        using var control = new CurrencyInput { IsEnabled = false };

        // Assert
        control.EffectiveIsEnabled.ShouldBeFalse();
        control.CanFocus.ShouldBeFalse();
    }

    #endregion

    #region Helpers

    private static KeyEventArgs Key(Code code, Modifiers modifiers = Modifiers.None) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    private static void TypeCharacter(CurrencyInput control, char character) =>
        Router.Route(
            control,
            Events.Key,
            CharacterKey(character, Modifiers.None));

    private static KeyEventArgs CharacterKey(char character, Modifiers modifiers) => new(new Stroke(
        Code.Character,
        new Rune(character),
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    #endregion
}
