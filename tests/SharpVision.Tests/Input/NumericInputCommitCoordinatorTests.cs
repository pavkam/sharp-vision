// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared buffer-then-commit orchestration NumberInput and CurrencyInput both
/// drive through <see cref="NumericInputCommitCoordinator"/> - directly against the coordinator,
/// mirroring <see cref="NumericEditBufferTests"/>'s direct-construction style. A small in-test fake
/// supplies the owner callbacks while the coordinator remains authoritative for numeric state.</summary>
public sealed class NumericInputCommitCoordinatorTests
{
    private sealed class Fake
    {
        private decimal? _initialValue;
        private bool _initialAllowNull = true;
        private decimal _initialMinimum = decimal.MinValue;
        private decimal _initialMaximum = decimal.MaxValue;
        private decimal _initialStep = 1m;

        public NumericEditBuffer Buffer { get; } = new();
        public NumericInputCommitCoordinator? Coordinator { get; private set; }
        public decimal? Value
        {
            get => Coordinator is null ? _initialValue : Coordinator.Value;
            set
            {
                if (Coordinator is null)
                {
                    _initialValue = value;
                }
                else
                {
                    _ = Coordinator.SetValue(value);
                }
            }
        }
        public bool AllowNull
        {
            get => Coordinator?.AllowNull ?? _initialAllowNull;
            set
            {
                if (Coordinator is null)
                {
                    _initialAllowNull = value;
                }
                else
                {
                    _ = Coordinator.SetAllowNull(value);
                }
            }
        }
        public bool Focused { get; set; }
        public int RefreshCount { get; private set; }
        public List<(decimal? Previous, decimal? Current)> Raised { get; } = [];
        public decimal Minimum
        {
            get => Coordinator?.Minimum ?? _initialMinimum;
            set
            {
                if (Coordinator is null)
                {
                    _initialMinimum = value;
                }
                else
                {
                    _ = Coordinator.SetMinimum(value);
                }
            }
        }
        public decimal Maximum
        {
            get => Coordinator?.Maximum ?? _initialMaximum;
            set
            {
                if (Coordinator is null)
                {
                    _initialMaximum = value;
                }
                else
                {
                    _ = Coordinator.SetMaximum(value);
                }
            }
        }
        public decimal Step
        {
            get => Coordinator?.Step ?? _initialStep;
            set
            {
                if (Coordinator is null)
                {
                    _initialStep = value;
                }
                else
                {
                    _ = Coordinator.SetStep(value);
                }
            }
        }
        public int DecimalPlaces { get; set; }

        public NumericInputCommitCoordinator CreateCoordinator()
        {
            Buffer.Configure(NumberFormatInfo.InvariantInfo, integerOnly: false);

            var initialValue = _initialValue;
            var initialAllowNull = _initialAllowNull;
            var initialMinimum = _initialMinimum;
            var initialMaximum = _initialMaximum;
            var initialStep = _initialStep;
            Coordinator = new NumericInputCommitCoordinator(
                Buffer,
                () => { },
                (_, _) => { },
                parsed => Math.Round(parsed, DecimalPlaces, MidpointRounding.AwayFromZero),
                () => Focused,
                () => RefreshCount++,
                (previous, current) => Raised.Add((previous, current)));

            _ = Coordinator.SetMinimum(initialMinimum);
            _ = Coordinator.SetMaximum(initialMaximum);
            _ = Coordinator.SetStep(initialStep);
            _ = Coordinator.SetValue(initialValue);
            _ = Coordinator.SetAllowNull(initialAllowNull);
            Raised.Clear();
            RefreshCount = 0;
            return Coordinator;
        }
    }

    #region Construction validation

    /// <summary>Verifies construct when the buffer is null throws.</summary>
    [Fact]
    public void Construct_WhenBufferIsNull_Throws()
    {
        // Arrange
        var fake = new Fake();

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => new NumericInputCommitCoordinator(
            null!,
            () => { },
            (_, _) => { },
            parsed => parsed,
            () => false,
            () => { },
            (_, _) => { }));
    }

    /// <summary>Verifies construct when the raise-value-changed callback is null throws.</summary>
    [Fact]
    public void Construct_WhenRaiseValueChangedIsNull_Throws()
    {
        // Arrange
        var buffer = new NumericEditBuffer();

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => new NumericInputCommitCoordinator(
            buffer,
            () => { },
            (_, _) => { },
            parsed => parsed,
            () => false,
            () => { },
            null!));
    }

    #endregion

    #region CommitBuffer

    /// <summary>Verifies commit buffer when the buffer holds a parseable value rounds through the
    /// caller's own formula, clamps into range, and commits.</summary>
    [Fact]
    public void CommitBuffer_WhenBufferHoldsAParseableValue_RoundsClampsAndCommits()
    {
        // Arrange
        var fake = new Fake { Minimum = 0m, Maximum = 100m, DecimalPlaces = 0 };
        var coordinator = fake.CreateCoordinator();
        fake.Buffer.Load("12.6");

        // Act
        var handled = coordinator.CommitBuffer();

        // Assert
        handled.ShouldBeTrue();
        fake.Value.ShouldBe(13m);
        fake.Raised.Count.ShouldBe(1);
        fake.Raised[0].Previous.ShouldBeNull();
        fake.Raised[0].Current.ShouldBe(13m);
    }

    /// <summary>Verifies commit buffer when the rounded parsed value sits outside range clamps into
    /// it instead of committing the raw parse.</summary>
    [Fact]
    public void CommitBuffer_WhenRoundedValueIsOutsideRange_ClampsIntoRange()
    {
        // Arrange
        var fake = new Fake { Minimum = 0m, Maximum = 10m, DecimalPlaces = 0 };
        var coordinator = fake.CreateCoordinator();
        fake.Buffer.Load("99");

        // Act
        _ = coordinator.CommitBuffer();

        // Assert
        fake.Value.ShouldBe(10m);
    }

    /// <summary>Verifies commit buffer when the buffer is empty and null is allowed clears the
    /// previously committed value.</summary>
    [Fact]
    public void CommitBuffer_WhenBufferIsEmptyAndAllowNullIsTrue_CommitsNull()
    {
        // Arrange
        var fake = new Fake { Value = 5m };
        var coordinator = fake.CreateCoordinator();
        fake.Buffer.Load(string.Empty);

        // Act
        var handled = coordinator.CommitBuffer();

        // Assert
        handled.ShouldBeTrue();
        fake.Value.ShouldBeNull();
        fake.Raised.Count.ShouldBe(1);
        fake.Raised[0].Previous.ShouldBe(5m);
        fake.Raised[0].Current.ShouldBeNull();
    }

    /// <summary>Verifies commit buffer when the buffer is empty and null is disallowed leaves the
    /// committed value untouched and raises nothing.</summary>
    [Fact]
    public void CommitBuffer_WhenBufferIsEmptyAndAllowNullIsFalse_LeavesValueUntouched()
    {
        // Arrange
        var fake = new Fake { Value = 5m, AllowNull = false };
        var coordinator = fake.CreateCoordinator();
        fake.Buffer.Load(string.Empty);

        // Act
        var handled = coordinator.CommitBuffer();

        // Assert
        handled.ShouldBeTrue();
        fake.Value.ShouldBe(5m);
        fake.Raised.ShouldBeEmpty();
    }

    /// <summary>Verifies commit buffer when the buffer overflows decimal range rejects the parse and
    /// leaves the committed value untouched.</summary>
    [Fact]
    public void CommitBuffer_WhenBufferOverflowsDecimalRange_LeavesValueUntouched()
    {
        // Arrange
        var fake = new Fake { Value = 5m };
        var coordinator = fake.CreateCoordinator();
        var overflowing = decimal.MaxValue.ToString("F0", CultureInfo.InvariantCulture) + "9";
        fake.Buffer.Load(overflowing);

        // Act
        _ = coordinator.CommitBuffer();

        // Assert
        fake.Value.ShouldBe(5m);
        fake.Raised.ShouldBeEmpty();
    }

    /// <summary>Verifies commit buffer always reloads the buffer afterward, regardless of which
    /// branch committed.</summary>
    [Fact]
    public void CommitBuffer_Always_RefreshesTheBufferAfterward()
    {
        // Arrange
        var fake = new Fake();
        var coordinator = fake.CreateCoordinator();
        fake.Buffer.Load("5");

        // Act
        _ = coordinator.CommitBuffer();

        // Assert
        fake.RefreshCount.ShouldBe(1);
    }

    /// <summary>Verifies commit buffer when the typed text is a sign-negative zero literal (e.g.
    /// "-0" or "-0.00") stores a Value that is not <see cref="decimal.IsNegative"/> - the parsed
    /// <c>-0m</c> must be normalized at commit rather than stored with its sign bit intact.</summary>
    [Theory]
    [InlineData("-0")]
    [InlineData("-0.00")]
    public void CommitBuffer_WhenTypedTextIsNegativeZero_StoresNormalizedPositiveZero(string text)
    {
        // Arrange
        var fake = new Fake { DecimalPlaces = 2 };
        var coordinator = fake.CreateCoordinator();
        fake.Buffer.Load(text);

        // Act
        _ = coordinator.CommitBuffer();

        // Assert
        fake.Value.ShouldBe(0m);
        decimal.IsNegative(fake.Value!.Value).ShouldBeFalse();
    }

    /// <summary>Verifies commit buffer when a small negative value rounds to zero at the accepted
    /// precision (mirroring <see cref="NumericInputCommitCoordinator.RoundAtAcceptedPrecision"/>,
    /// which every caller's own <c>resolveCommitRounding</c> formula funnels through) still stores
    /// a normalized positive zero rather than the sign-preserved <c>-0m</c> that
    /// <see cref="Math.Round(decimal, int, MidpointRounding)"/> itself produces for such an
    /// input.</summary>
    [Fact]
    public void CommitBuffer_WhenSmallNegativeValueRoundsToZeroAtAcceptedPrecision_StoresNormalizedPositiveZero()
    {
        // Arrange
        var fake = new Fake { DecimalPlaces = 0 };
        var coordinator = fake.CreateCoordinator();
        fake.Buffer.Load("-0.001");

        // Precondition: Math.Round itself preserves the sign bit for this input, so the
        // regression only exists because the coordinator's own rounding formula reaches it -
        // confirming this keeps the scenario honest rather than accidentally vacuous.
        decimal.IsNegative(Math.Round(-0.001m, 0, MidpointRounding.AwayFromZero)).ShouldBeTrue();

        // Act
        _ = coordinator.CommitBuffer();

        // Assert
        fake.Value.ShouldBe(0m);
        decimal.IsNegative(fake.Value!.Value).ShouldBeFalse();
    }

    #endregion

    #region RevertBuffer

    /// <summary>Verifies revert buffer reloads the buffer from the still-committed value without
    /// touching it or raising anything.</summary>
    [Fact]
    public void RevertBuffer_Always_RefreshesWithoutCommitting()
    {
        // Arrange
        var fake = new Fake { Value = 5m };
        var coordinator = fake.CreateCoordinator();
        fake.Buffer.Load("999");

        // Act
        var handled = coordinator.RevertBuffer();

        // Assert
        handled.ShouldBeTrue();
        fake.Value.ShouldBe(5m);
        fake.Raised.ShouldBeEmpty();
        fake.RefreshCount.ShouldBe(1);
    }

    #endregion

    #region CommitValue

    /// <summary>Verifies commit value when the candidate equals the current value returns false and
    /// raises nothing, matching every other SetProperty-backed member.</summary>
    [Fact]
    public void CommitValue_WhenCandidateEqualsCurrentValue_ReturnsFalseAndDoesNotRaise()
    {
        // Arrange
        var fake = new Fake { Value = 5m };
        var coordinator = fake.CreateCoordinator();

        // Act
        var committed = coordinator.CommitValue(5m);

        // Assert
        committed.ShouldBeFalse();
        fake.Raised.ShouldBeEmpty();
    }

    /// <summary>Verifies commit value while focused refreshes the buffer after committing.</summary>
    [Fact]
    public void CommitValue_WhenFocused_RefreshesBufferAfterCommitting()
    {
        // Arrange
        var fake = new Fake { Focused = true };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.CommitValue(5m);

        // Assert
        fake.RefreshCount.ShouldBe(1);
    }

    /// <summary>Verifies commit value while not focused never touches the buffer.</summary>
    [Fact]
    public void CommitValue_WhenNotFocused_DoesNotRefreshTheBuffer()
    {
        // Arrange
        var fake = new Fake { Focused = false };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.CommitValue(5m);

        // Assert
        fake.RefreshCount.ShouldBe(0);
    }

    #endregion

    #region RepairValue

    /// <summary>Verifies tightening a bound through the authoritative state owner clamps the
    /// committed value and raises exactly one repaired transition.</summary>
    [Fact]
    public void SetMaximum_WhenCommittedValueIsAboveNewBound_ClampsAndRaises()
    {
        // Arrange
        var fake = new Fake { Value = 20m };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.SetMaximum(10m);

        // Assert
        fake.Value.ShouldBe(10m);
        fake.Raised.Count.ShouldBe(1);
        fake.Raised[0].Previous.ShouldBe(20m);
        fake.Raised[0].Current.ShouldBe(10m);
    }

    /// <summary>Verifies repair value when the committed value is already inside range is a no-op.</summary>
    [Fact]
    public void RepairValue_WhenCommittedValueIsAlreadyInRange_DoesNotRaise()
    {
        // Arrange
        var fake = new Fake { Value = 5m, Minimum = 0m, Maximum = 10m };
        var coordinator = fake.CreateCoordinator();

        // Act
        coordinator.RepairValue();

        // Assert
        fake.Value.ShouldBe(5m);
        fake.Raised.ShouldBeEmpty();
    }

    /// <summary>Verifies repair value when there is no committed value is a no-op.</summary>
    [Fact]
    public void RepairValue_WhenValueIsNull_IsNoOp()
    {
        // Arrange
        var fake = new Fake();
        var coordinator = fake.CreateCoordinator();

        // Act
        coordinator.RepairValue();

        // Assert
        fake.Value.ShouldBeNull();
        fake.Raised.ShouldBeEmpty();
    }

    #endregion

    #region ClampToRange

    /// <summary>Verifies clamp to range when the value falls inside Minimum/Maximum returns it
    /// unchanged.</summary>
    [Fact]
    public void ClampToRange_WhenValueIsInsideRange_ReturnsItUnchanged()
    {
        // Arrange
        var fake = new Fake { Minimum = 0m, Maximum = 10m };
        var coordinator = fake.CreateCoordinator();

        // Act and assert
        coordinator.ClampToRange(5m).ShouldBe(5m);
    }

    /// <summary>Verifies clamp to range when the value falls outside Minimum/Maximum pulls it to the
    /// nearest bound.</summary>
    [Fact]
    public void ClampToRange_WhenValueIsOutsideRange_PullsToTheNearestBound()
    {
        // Arrange
        var fake = new Fake { Minimum = 0m, Maximum = 10m };
        var coordinator = fake.CreateCoordinator();

        // Act and assert
        coordinator.ClampToRange(-5m).ShouldBe(0m);
        coordinator.ClampToRange(15m).ShouldBe(10m);
    }

    /// <summary>Verifies clamp to range when the input is a sign-negative zero normalizes the
    /// result to ordinary positive zero rather than preserving the sign bit - <c>-0m</c> and
    /// <c>0m</c> compare equal, so the regression must inspect <see cref="decimal.IsNegative"/>
    /// directly rather than <c>ShouldBe</c>.</summary>
    [Fact]
    public void ClampToRange_WhenValueIsNegativeZero_NormalizesToPositiveZero()
    {
        // Arrange
        var fake = new Fake { Minimum = decimal.MinValue, Maximum = decimal.MaxValue };
        var coordinator = fake.CreateCoordinator();

        // Act
        var result = coordinator.ClampToRange(-0.0m);

        // Assert
        decimal.IsNegative(result).ShouldBeFalse();
    }

    /// <summary>Verifies clamp to range when an ordinary negative value clamps exactly to the
    /// Minimum bound of zero produces a positive, not negative, zero.</summary>
    [Fact]
    public void ClampToRange_WhenNegativeValueClampsToZeroMinimum_ProducesPositiveZero()
    {
        // Arrange
        var fake = new Fake { Minimum = 0m, Maximum = 10m };
        var coordinator = fake.CreateCoordinator();

        // Act
        var result = coordinator.ClampToRange(-5m);

        // Assert
        decimal.IsNegative(result).ShouldBeFalse();
    }

    #endregion

    #region ApplyStep

    /// <summary>Verifies apply step when there is no committed value steps from zero.</summary>
    [Fact]
    public void ApplyStep_WhenValueIsNull_StepsFromZero()
    {
        // Arrange
        var fake = new Fake { Step = 2m };
        var coordinator = fake.CreateCoordinator();

        // Act
        var handled = coordinator.ApplyStep(1);

        // Assert
        handled.ShouldBeTrue();
        fake.Value.ShouldBe(2m);
    }

    /// <summary>Verifies apply step when stepping down applies the negative direction against the
    /// committed value.</summary>
    [Fact]
    public void ApplyStep_WhenDirectionIsNegative_StepsDown()
    {
        // Arrange
        var fake = new Fake { Value = 5m, Step = 2m };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.ApplyStep(-1);

        // Assert
        fake.Value.ShouldBe(3m);
    }

    /// <summary>Verifies apply step when the stepped result overflows Decimal saturates to the
    /// direction's extreme instead of throwing.</summary>
    [Fact]
    public void ApplyStep_WhenResultOverflows_SaturatesToTheExtreme()
    {
        // Arrange
        var fake = new Fake { Value = decimal.MaxValue - 1m, Step = decimal.MaxValue };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.ApplyStep(1);

        // Assert
        fake.Value.ShouldBe(decimal.MaxValue);
    }

    /// <summary>Verifies apply step when the stepped result sits outside range clamps into it.</summary>
    [Fact]
    public void ApplyStep_WhenResultIsOutsideRange_ClampsIntoRange()
    {
        // Arrange
        var fake = new Fake { Value = 9m, Step = 5m, Minimum = 0m, Maximum = 10m };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.ApplyStep(1);

        // Assert
        fake.Value.ShouldBe(10m);
    }

    #endregion

    #region JumpToBound

    /// <summary>Verifies jump to bound when jumping to the minimum commits Minimum directly.</summary>
    [Fact]
    public void JumpToBound_WhenJumpingToMinimum_CommitsMinimum()
    {
        // Arrange
        var fake = new Fake { Value = 5m, Minimum = 0m, Maximum = 10m };
        var coordinator = fake.CreateCoordinator();

        // Act
        var handled = coordinator.JumpToBound(minimum: true, places: 0);

        // Assert
        handled.ShouldBeTrue();
        fake.Value.ShouldBe(0m);
    }

    /// <summary>Verifies jump to bound when jumping to the maximum commits Maximum directly.</summary>
    [Fact]
    public void JumpToBound_WhenJumpingToMaximum_CommitsMaximum()
    {
        // Arrange
        var fake = new Fake { Value = 5m, Minimum = 0m, Maximum = 10m };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.JumpToBound(minimum: false, places: 0);

        // Assert
        fake.Value.ShouldBe(10m);
    }

    /// <summary>Verifies jump to bound when Minimum carries more precision than the requested places
    /// rounds up toward the interior of the range instead of down past it.</summary>
    [Fact]
    public void JumpToBound_WhenMinimumHasExcessPrecision_RoundsUpTowardTheInterior()
    {
        // Arrange
        var fake = new Fake { Minimum = 0.1m, Maximum = 10m };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.JumpToBound(minimum: true, places: 0);

        // Assert
        fake.Value.ShouldBe(1m);
    }

    /// <summary>Verifies jump to bound when Maximum carries more precision than the requested places
    /// rounds down toward the interior of the range instead of up past it.</summary>
    [Fact]
    public void JumpToBound_WhenMaximumHasExcessPrecision_RoundsDownTowardTheInterior()
    {
        // Arrange
        var fake = new Fake { Minimum = 0m, Maximum = 9.9m };
        var coordinator = fake.CreateCoordinator();

        // Act
        _ = coordinator.JumpToBound(minimum: false, places: 0);

        // Assert
        fake.Value.ShouldBe(9m);
    }

    #endregion
}
