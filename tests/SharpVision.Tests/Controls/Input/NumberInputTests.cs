// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves the detached public NumberInput contract, clamping, rounding, and culture-aware
/// formatting.</summary>
public sealed class NumberInputTests
{
    /// <summary>Verifies both endpoint setters repair the committed numeric value after a throwing observer.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bounds_WhenPropertyObserverThrows_StillClampValue(bool minimum)
    {
        // Arrange
        using var control = new NumberInput { Value = minimum ? 10m : 90m };
        var propertyName = minimum ? nameof(NumberInput.Minimum) : nameof(NumberInput.Maximum);
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
        using var control = new NumberInput();

        // Assert
        control.Value.ShouldBeNull();
        control.AllowNull.ShouldBeTrue();
        control.Minimum.ShouldBe(decimal.MinValue);
        control.Maximum.ShouldBe(decimal.MaxValue);
        control.Step.ShouldBe(1m);
        control.Mode.ShouldBe(NumberInputMode.Decimal);
        control.DecimalPlaces.ShouldBe(2);
        control.AllowGrouping.ShouldBeTrue();
        control.RoundingMode.ShouldBe(MidpointRounding.AwayFromZero);
        control.Culture.ShouldBeSameAs(CultureInfo.InvariantCulture);
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
        using var control = new NumberInput();

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
        using var control = new NumberInput { Minimum = 0m, Maximum = 10m };

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
        using var control = new NumberInput { Value = 5m };

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
        using var control = new NumberInput { Value = 5m, AllowNull = false };

        // Act
        control.Value = null;

        // Assert
        control.Value.ShouldBe(5m);
    }

    /// <summary>Verifies value when fractional and mode is integer throws.</summary>
    [Fact]
    public void Value_WhenFractionalAndModeIsInteger_Throws()
    {
        // Arrange
        using var control = new NumberInput { Mode = NumberInputMode.Integer };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Value = 1.5m);
    }

    /// <summary>Verifies allow null when disabled while value is null eagerly reseeds to clamped zero and raises event.</summary>
    [Fact]
    public void AllowNull_WhenDisabledWhileValueIsNull_EagerlyReseedsToClampedZeroAndRaisesEvent()
    {
        // Arrange
        using var control = new NumberInput { Minimum = 5m, Maximum = 10m };
        NumberInputValueChangedEventArgs? observed = null;
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
        using var control = new NumberInput { Value = 3m };

        // Act
        control.AllowNull = false;

        // Assert
        control.Value.ShouldBe(3m);
    }

    #endregion

    #region Bounds

    /// <summary>Verifies minimum when exceeds maximum throws and leaves the prior bound untouched.</summary>
    [Fact]
    public void Minimum_WhenExceedsMaximum_ThrowsAndPreservesPreviousMinimum()
    {
        // Arrange
        using var control = new NumberInput { Minimum = 0m, Maximum = 10m };

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
        using var control = new NumberInput { Minimum = 10m, Maximum = 20m };

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
        using var control = new NumberInput();

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
        using var control = new NumberInput { Value = 3m };
        NumberInputValueChangedEventArgs? change = null;
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
        using var control = new NumberInput { Value = 8m };
        NumberInputValueChangedEventArgs? change = null;
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
        using var control = new NumberInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Step = 0m);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Step = -1m);
    }

    #endregion

    #region Mode and DecimalPlaces

    /// <summary>Verifies decimal places when assigned negative throws.</summary>
    [Fact]
    public void DecimalPlaces_WhenAssignedNegative_Throws()
    {
        // Arrange
        using var control = new NumberInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.DecimalPlaces = -1);
    }

    /// <summary>Verifies mode when switched to integer with fractional committed value rounds and raises event.</summary>
    [Fact]
    public void Mode_WhenSwitchedToIntegerWithFractionalCommittedValue_RoundsAndRaisesEvent()
    {
        // Arrange
        using var control = new NumberInput { Value = 2.5m, RoundingMode = MidpointRounding.AwayFromZero };
        NumberInputValueChangedEventArgs? observed = null;
        control.ValueChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        control.Mode = NumberInputMode.Integer;

        // Assert
        control.Value.ShouldBe(3m);
        _ = observed.ShouldNotBeNull();
        observed.Value.ShouldBe(3m);
    }

    /// <summary>Verifies mode when switched to integer with whole committed value does not raise event.</summary>
    [Fact]
    public void Mode_WhenSwitchedToIntegerWithWholeCommittedValue_DoesNotRaiseEvent()
    {
        // Arrange
        using var control = new NumberInput { Value = 4m };
        var raised = 0;
        control.ValueChanged += (_, _) => raised++;

        // Act
        control.Mode = NumberInputMode.Integer;

        // Assert
        raised.ShouldBe(0);
        control.Value.ShouldBe(4m);
    }

    /// <summary>Verifies mode when assigned undefined value throws.</summary>
    [Fact]
    public void Mode_WhenAssignedUndefinedValue_Throws()
    {
        // Arrange
        using var control = new NumberInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Mode = (NumberInputMode) 99);
    }

    /// <summary>Verifies rounding mode when assigned undefined value throws before mutation.</summary>
    [Fact]
    public void RoundingMode_WhenAssignedUndefinedValue_ThrowsBeforeMutation()
    {
        // Arrange
        using var control = new NumberInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.RoundingMode = (MidpointRounding) 99);
        control.RoundingMode.ShouldBe(MidpointRounding.AwayFromZero);
    }

    #endregion

    #region Typed buffer commit and rounding

    /// <summary>Verifies commit when typed value is at a midpoint at zero decimal places rounds per configured mode.</summary>
    [Theory]
    [InlineData(MidpointRounding.AwayFromZero, "2.5", 3)]
    [InlineData(MidpointRounding.ToEven, "2.5", 2)]
    [InlineData(MidpointRounding.AwayFromZero, "3.5", 4)]
    [InlineData(MidpointRounding.ToEven, "3.5", 4)]
    public void Commit_WhenTypedValueIsAtAMidpointAtZeroDecimalPlaces_RoundsPerConfiguredMode(
        MidpointRounding mode,
        string typed,
        int expected)
    {
        // Arrange - "2.5" typed digit-by-digit then committed with Enter, pinning the AwayFromZero
        // vs ToEven divergence. DecimalPlaces is zero while Mode stays Decimal so the decimal
        // separator keystroke is still admitted; rounding to zero places happens only at commit.
        using var control = new NumberInput { DecimalPlaces = 0, RoundingMode = mode };

        foreach (var ch in typed)
        {
            TypeCharacter(control, ch);
        }

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(expected);
    }

    /// <summary>Verifies mode when switched to integer with fractional committed value and to even rounding rounds to even.</summary>
    [Fact]
    public void Mode_WhenSwitchedToIntegerWithFractionalCommittedValueAndToEvenRounding_RoundsToEven()
    {
        // Arrange
        using var control = new NumberInput { Value = 2.5m, RoundingMode = MidpointRounding.ToEven };

        // Act
        control.Mode = NumberInputMode.Integer;

        // Assert
        control.Value.ShouldBe(2m);
    }

    /// <summary>Verifies commit when typed text is grouped and decimal parses and rounds to configured places.</summary>
    [Fact]
    public void Commit_WhenTypedTextIsGroupedAndDecimal_ParsesAndRoundsToConfiguredPlaces()
    {
        // Arrange
        using var control = new NumberInput { DecimalPlaces = 1 };
        foreach (var ch in "1,234.56")
        {
            TypeCharacter(control, ch);
        }

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        control.Value.ShouldBe(1234.6m);
    }

    /// <summary>Verifies commit when decimal separator is typed under integer mode never appears in the buffer.</summary>
    [Fact]
    public void Commit_WhenDecimalSeparatorIsTypedUnderIntegerMode_NeverAppearsInTheBuffer()
    {
        // Arrange
        using var control = new NumberInput { Mode = NumberInputMode.Integer };
        TypeCharacter(control, '1');

        // Act
        TypeCharacter(control, '.');
        TypeCharacter(control, '2');

        // Assert
        _ = Router.Route(control, Events.Key, Key(Code.Enter));
        control.Value.ShouldBe(12m);
    }

    /// <summary>Verifies commit when buffer overflows decimal range rejects and keeps prior value.</summary>
    [Fact]
    public void Commit_WhenBufferOverflowsDecimalRange_RejectsAndKeepsPriorValue()
    {
        // Arrange
        using var control = new NumberInput { Value = 5m };
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

    /// <summary>Verifies commit when buffer is empty and allow null is false reverts to prior value.</summary>
    [Fact]
    public void Commit_WhenBufferIsEmptyAndAllowNullIsFalse_RevertsToPriorValue()
    {
        // Arrange
        using var control = new NumberInput { Value = 4m, AllowNull = false };
        _ = Router.Route(control, Events.Key, Key(Code.Backspace));

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
        using var control = new NumberInput { Value = 4m };
        TypeCharacter(control, '9');
        var raised = 0;
        control.ValueChanged += (_, _) => raised++;

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Escape));

        // Assert
        control.Value.ShouldBe(4m);
        raised.ShouldBe(0);
    }

    #endregion

    #region Stepping and jumping

    /// <summary>Verifies step when up is pressed increments by configured step and clamps.</summary>
    [Fact]
    public void Step_WhenUpIsPressed_IncrementsByConfiguredStepAndClamps()
    {
        // Arrange
        using var control = new NumberInput { Value = 5m, Step = 2.5m, Maximum = 6m };

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
        using var control = new NumberInput { Value = 5m, Step = 1m };

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
        using var control = new NumberInput { Value = 5m, Minimum = 0m, Maximum = 10m };

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
        using var control = new NumberInput { Value = 5m, Minimum = 0m, Maximum = 10m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.End));

        // Assert
        control.Value.ShouldBe(10m);
    }

    /// <summary>Verifies jump to Minimum under the default unbounded decimal.MinValue does not
    /// overflow scaling that bound to isolate whole units at the configured decimal places -
    /// decimal.MinValue multiplied by a power of ten to round it exceeds Decimal's own range.</summary>
    [Fact]
    public void Jump_WhenHomeIsPressedUnderDefaultUnboundedMinimum_CommitsMinValueWithoutOverflow()
    {
        // Arrange
        using var control = new NumberInput { Value = 5m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Home));

        // Assert
        control.Value.ShouldBe(decimal.MinValue);
    }

    /// <summary>Verifies jump to Maximum under the default unbounded decimal.MaxValue does not
    /// overflow scaling that bound to isolate whole units at the configured decimal places -
    /// decimal.MaxValue multiplied by a power of ten to round it exceeds Decimal's own range.</summary>
    [Fact]
    public void Jump_WhenEndIsPressedUnderDefaultUnboundedMaximum_CommitsMaxValueWithoutOverflow()
    {
        // Arrange
        using var control = new NumberInput { Value = 5m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.End));

        // Assert
        control.Value.ShouldBe(decimal.MaxValue);
    }

    /// <summary>Verifies jump to Minimum under the default unbounded decimal.MinValue does not throw
    /// when DecimalPlaces exceeds the 28 significant digits Decimal itself can represent -
    /// isolating whole units at that precision needs a scaling power of ten that itself exceeds
    /// Decimal's own range before the bound is ever multiplied by it, so the scale must stop
    /// growing, and the jump must recover, the same way the already-scaled multiplication does one
    /// step later.</summary>
    [Fact]
    public void Jump_WhenHomeIsPressedWithDecimalPlacesBeyondDecimalRange_CommitsMinValueWithoutOverflow()
    {
        // Arrange
        using var control = new NumberInput { Value = 5m, DecimalPlaces = 29 };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Home));

        // Assert
        control.Value.ShouldBe(decimal.MinValue);
    }

    /// <summary>Verifies step when the configured step carries more fractional digits than
    /// DecimalPlaces rounds the stepped candidate to the configured precision - the same rounding
    /// an Enter-commit of the equivalent typed text ("0.333") would apply, instead of leaving the
    /// extra digit of precision the bare arithmetic would otherwise produce.</summary>
    [Fact]
    public void Step_WhenStepHasMoreFractionalDigitsThanDecimalPlaces_RoundsToConfiguredPrecision()
    {
        // Arrange
        using var control = new NumberInput { Step = 0.333m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Up));

        // Assert
        control.Value.ShouldBe(0.33m);
    }

    /// <summary>Verifies jump to Minimum when the bound carries more fractional digits than
    /// DecimalPlaces rounds up to the nearest value at the configured precision that is still
    /// within range, rather than naively rounding via RoundingMode and clamping back - the naive
    /// approach would push a rounded Minimum straight back to the unrounded bound it just moved
    /// away from, since ClampToRange's own floor IS that unrounded Minimum, making the rounding a
    /// no-op and leaving Value at a precision the control's own typing path can never produce.
    /// </summary>
    [Fact]
    public void Jump_WhenMinimumHasMoreFractionalDigitsThanDecimalPlaces_RoundsUpToNearestInRangeValue()
    {
        // Arrange
        using var control = new NumberInput { Minimum = 0.001m, Maximum = 10m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Home));

        // Assert
        control.Value.ShouldBe(0.01m);
    }

    /// <summary>Verifies jump to Maximum when the bound carries more fractional digits than
    /// DecimalPlaces rounds down to the nearest value at the configured precision that is still
    /// within range - the mirror image of the Minimum case, rounding away from the bound rather
    /// than toward it.</summary>
    [Fact]
    public void Jump_WhenMaximumHasMoreFractionalDigitsThanDecimalPlaces_RoundsDownToNearestInRangeValue()
    {
        // Arrange
        using var control = new NumberInput { Minimum = 0m, Maximum = 9.999m };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.End));

        // Assert
        control.Value.ShouldBe(9.99m);
    }

    #endregion

    #region Culture

    /// <summary>Verifies culture when constructed defaults to invariant.</summary>
    [Fact]
    public void Culture_WhenConstructed_DefaultsToInvariant()
    {
        // Arrange
        using var control = new NumberInput();

        // Assert
        control.Culture.ShouldBeSameAs(CultureInfo.InvariantCulture);
    }

    /// <summary>Verifies culture when assigned null throws.</summary>
    [Fact]
    public void Culture_WhenAssignedNull_Throws()
    {
        // Arrange
        using var control = new NumberInput();

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => control.Culture = null!);
    }

    #endregion

    #region Commit event ordering

    /// <summary>Verifies commit when value changes raises value changed exactly once.</summary>
    [Fact]
    public void Commit_WhenValueChanges_RaisesValueChangedExactlyOnce()
    {
        // Arrange
        using var control = new NumberInput { Value = 1m };
        var raised = 0;
        NumberInputValueChangedEventArgs? observed = null;
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
        using var control = new NumberInput { Value = 1m };
        var raised = 0;
        control.ValueChanged += (_, _) => raised++;

        // Act
        control.Value = 1m;

        // Assert
        raised.ShouldBe(0);
    }

    #endregion

    #region Measurement

    /// <summary>Verifies measure when bounds are large widens to fit the widest formatted bound.</summary>
    [Fact]
    public void Measure_WhenBoundsAreLarge_WidensToFitTheWidestFormattedBound()
    {
        // Arrange
        using var narrow = new NumberInput { Minimum = 0m, Maximum = 9m, DecimalPlaces = 0, AllowGrouping = false };
        using var wide = new NumberInput { Minimum = -999999m, Maximum = 999999m, DecimalPlaces = 0, AllowGrouping = false };
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
        using var grouped = new NumberInput { Minimum = 0m, Maximum = 999999m, DecimalPlaces = 0 };
        using var ungrouped = new NumberInput { Minimum = 0m, Maximum = 999999m, DecimalPlaces = 0, AllowGrouping = false };

        // Act
        new LayoutEngine().Layout(grouped, new Size(80, 3));
        new LayoutEngine().Layout(ungrouped, new Size(80, 3));

        // Assert
        grouped.DesiredSize.Width.ShouldBeGreaterThan(ungrouped.DesiredSize.Width);
    }

    #endregion

    #region Affixes

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus the
    /// shared theme gap, over an equivalent affixless NumberInput.</summary>
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
        using var baseline = new NumberInput { Minimum = 0m, Maximum = 9m, DecimalPlaces = 0, AllowGrouping = false };
        new LayoutEngine().Layout(baseline, new Size(80, 3));
        using var control = new NumberInput
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
        using var control = new NumberInput { Value = 5m, DecimalPlaces = 0 };
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
        using var control = new NumberInput { Value = 5m, DecimalPlaces = 0, EndAffix = new Affix("|") };
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
        using var control = new NumberInput { Value = 5m, DecimalPlaces = 0, StartAffix = new Affix("!") };
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
        using var control = new NumberInput { Value = 5m, DecimalPlaces = 0, StartAffix = affix };
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
        using var control = new NumberInput { IsEnabled = false };

        // Assert
        control.EffectiveIsEnabled.ShouldBeFalse();
        control.CanFocus.ShouldBeFalse();
    }

    #endregion

    #region Helpers

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static void TypeCharacter(NumberInput control, char character) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Character,
                new Rune(character),
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

    #endregion
}
