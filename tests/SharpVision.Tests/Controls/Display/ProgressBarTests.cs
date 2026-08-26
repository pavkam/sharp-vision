// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies ProgressBar range, layout, and rendering contracts.</summary>
public sealed class ProgressBarTests
{
    /// <summary>Verifies documented range, presentation, alignment, and interaction defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var bar = new ProgressBar();

        // Assert
        bar.Minimum.ShouldBe(0);
        bar.Maximum.ShouldBe(1);
        bar.Value.ShouldBe(0);
        bar.IsIndeterminate.ShouldBeFalse();
        bar.Orientation.ShouldBe(Orientation.Horizontal);
        bar.UseSubCellResolution.ShouldBeFalse();
        bar.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        bar.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        bar.CanFocus.ShouldBeFalse();
        bar.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies finite values clamp while invalid numbers fail before mutation.</summary>
    [Fact]
    public void Value_WhenAssigned_ClampsFiniteValuesAndRejectsNonFiniteValues()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 20, Minimum = 10 };
        bar.Value.ShouldBe(10);

        // Act and assert
        bar.Value = 25;
        bar.Value.ShouldBe(20);
        bar.Value = 5;
        bar.Value.ShouldBe(10);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Value = double.NaN);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Value = double.PositiveInfinity);
        bar.Value.ShouldBe(10);
    }

    /// <summary>Verifies overflow-safe normalization preserves the midpoint and endpoints of the
    /// widest accepted finite range.</summary>
    [Theory]
    [InlineData(-1.7976931348623157E+308, 0)]
    [InlineData(0, 0.5)]
    [InlineData(1.7976931348623157E+308, 1)]
    public void ResolveRatio_WhenFiniteRangeSpanOverflows_MapsExactPosition(double value, double expected)
    {
        // Act
        var ratio = ProgressBar.ResolveRatio(-double.MaxValue, double.MaxValue, value);

        // Assert
        ratio.ShouldBe(expected);
    }

    /// <summary>Verifies both orientations and both resolution modes consume the same stable
    /// midpoint ratio for an overflowing finite span.</summary>
    [Theory]
    [InlineData(Orientation.Horizontal, false)]
    [InlineData(Orientation.Horizontal, true)]
    [InlineData(Orientation.Vertical, false)]
    [InlineData(Orientation.Vertical, true)]
    public void Render_WhenFiniteRangeSpanOverflows_FillsHalf(
        Orientation orientation,
        bool useSubCellResolution)
    {
        // Arrange
        var bar = new ProgressBar
        {
            Minimum = -double.MaxValue,
            Maximum = double.MaxValue,
            Value = 0,
            Orientation = orientation,
            UseSubCellResolution = useSubCellResolution
        };
        new LayoutEngine().Layout(bar, new Size(4, 4));
        using Frame frame = new(new Size(4, 4));

        // Act
        bar.Render(frame.Canvas);

        // Assert
        var filled = orientation == Orientation.Horizontal
            ? FrameOracle.Get(frame, new Point(1, 0))
            : FrameOracle.Get(frame, new Point(0, 2));
        var empty = orientation == Orientation.Horizontal
            ? FrameOracle.Get(frame, new Point(2, 0))
            : FrameOracle.Get(frame, new Point(0, 1));
        filled.ShouldNotBe(empty);
    }

    /// <summary>Verifies endpoint changes clamp before ordered property notifications.</summary>
    [Fact]
    public void Minimum_WhenRaisedAboveValue_ClampsBeforeNotifications()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 50 };
        List<string?> properties = [];
        bar.PropertyChanged += (_, eventArgs) =>
        {
            bar.Value.ShouldBe(60);
            properties.Add(eventArgs.PropertyName);
        };

        // Act
        bar.Minimum = 60;

        // Assert
        properties.ShouldBe([nameof(ProgressBar.Minimum), nameof(ProgressBar.Value)]);
    }

    /// <summary>Verifies a Minimum assignment far smaller in magnitude than <see
    /// cref="float.Epsilon"/> - the tolerance the no-op guard compares against, even though
    /// <see cref="ProgressBar.Minimum"/> and <see cref="ProgressBar.Value"/> are <see
    /// cref="double"/> - still commits, instead of the guard misreading two distinct doubles as
    /// equal and silently discarding the assignment.</summary>
    [Fact]
    public void Minimum_WhenAssignedValueSmallerThanFloatEpsilon_Commits()
    {
        // Arrange
        var bar = new ProgressBar();
        bar.Minimum.ShouldBe(0);

        // Act
        bar.Minimum = 1e-300;

        // Assert
        bar.Minimum.ShouldBe(1e-300);
        bar.Value.ShouldBe(1e-300);
    }

    /// <summary>Verifies a Maximum reassignment to a distinct value less than <see
    /// cref="float.Epsilon"/> away from the current Maximum - the tolerance the no-op guard
    /// compares against, even though <see cref="ProgressBar.Maximum"/> is a <see cref="double"/>
    /// - still commits, instead of the guard misreading two distinct doubles as equal and
    /// silently discarding the assignment.</summary>
    [Fact]
    public void Maximum_WhenReassignedValueWithinFloatEpsilonOfCurrent_Commits()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 1e-300 };
        bar.Maximum.ShouldBe(1e-300);

        // Act
        bar.Maximum = 2e-300;

        // Assert
        bar.Maximum.ShouldBe(2e-300);
    }

    /// <summary>Verifies lowering the maximum clamps before ordered property notifications.</summary>
    [Fact]
    public void Maximum_WhenLoweredBelowValue_ClampsBeforeNotifications()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 80 };
        List<string?> properties = [];
        bar.PropertyChanged += (_, eventArgs) =>
        {
            bar.Value.ShouldBe(60);
            properties.Add(eventArgs.PropertyName);
        };

        // Act
        bar.Maximum = 60;

        // Assert
        properties.ShouldBe([nameof(ProgressBar.Maximum), nameof(ProgressBar.Value)]);
    }

    /// <summary>Verifies invalid endpoints and orientation fail without changing valid state.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        var bar = new ProgressBar();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Minimum = double.NegativeInfinity);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Maximum = double.NaN);
        _ = Should.Throw<ArgumentException>(() => bar.Minimum = 1);
        _ = Should.Throw<ArgumentException>(() => bar.Maximum = 0);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Orientation = (Orientation) 99);

        // Assert
        bar.Minimum.ShouldBe(0);
        bar.Maximum.ShouldBe(1);
        bar.Orientation.ShouldBe(Orientation.Horizontal);
        bar.UseSubCellResolution.ShouldBeFalse();
    }

    /// <summary>Verifies ValueChanged fires with correct previous and new values.</summary>
    [Fact]
    public void ValueChanged_WhenValueChanges_FiresWithPreviousAndNewValues()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 25 };
        var previousValue = double.NaN;
        var newValue = double.NaN;
        var raised = 0;
        bar.ValueChanged += (_, eventArgs) =>
        {
            previousValue = eventArgs.PreviousValue;
            newValue = eventArgs.Value;
            raised++;
        };

        // Act
        bar.Value = 75;

        // Assert
        raised.ShouldBe(1);
        previousValue.ShouldBe(25);
        newValue.ShouldBe(75);
    }

    /// <summary>Verifies ValueChanged does not fire when the clamped value is unchanged.</summary>
    [Fact]
    public void ValueChanged_WhenValueUnchanged_DoesNotFire()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 50 };
        var raised = 0;
        bar.ValueChanged += (_, _) => raised++;

        // Act
        bar.Value = 50;

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies sub-cell rendering uses deterministic eighth-cell blocks.</summary>
    [Fact]
    public void Render_WhenSubCellResolutionIsEnabled_UsesFractionalBlock()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Value = 0.5,
            UseSubCellResolution = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        new LayoutEngine().Layout(bar, new Size(3, 1));
        using Frame frame = new(new Size(3, 1));

        // Act
        bar.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, default).ShouldBe("█");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▌");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("░");
    }

    /// <summary>Verifies ValueChanged fires when Minimum clamping actually changes the value through an endpoint shift.</summary>
    [Fact]
    public void ValueChanged_WhenMinimumClampsValue_FiresWithPreviousAndClampedValues()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 30 };
        var previousValue = double.NaN;
        var newValue = double.NaN;
        var raised = 0;
        bar.ValueChanged += (_, eventArgs) =>
        {
            previousValue = eventArgs.PreviousValue;
            newValue = eventArgs.Value;
            raised++;
        };

        // Act
        bar.Minimum = 50;

        // Assert
        raised.ShouldBe(1);
        previousValue.ShouldBe(30);
        newValue.ShouldBe(50);
        bar.Value.ShouldBe(50);
    }

    /// <summary>Verifies ValueChanged fires when Maximum clamping actually changes the value through an endpoint shift.</summary>
    [Fact]
    public void ValueChanged_WhenMaximumClampsValue_FiresWithPreviousAndClampedValues()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 80 };
        var previousValue = double.NaN;
        var newValue = double.NaN;
        var raised = 0;
        bar.ValueChanged += (_, eventArgs) =>
        {
            previousValue = eventArgs.PreviousValue;
            newValue = eventArgs.Value;
            raised++;
        };

        // Act
        bar.Maximum = 60;

        // Assert
        raised.ShouldBe(1);
        previousValue.ShouldBe(80);
        newValue.ShouldBe(60);
        bar.Value.ShouldBe(60);
    }

    /// <summary>Verifies PropertyChanged(Value) and ValueChanged report the same clamped value
    /// when Minimum clamping changes it, keeping both notification channels consistent.</summary>
    [Fact]
    public void ValueChanged_WhenMinimumClampsValue_MatchesPropertyChangedValue()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 30 };
        double? propertyChangedValue = null;
        double? valueChangedValue = null;
        bar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ProgressBar.Value))
            {
                propertyChangedValue = bar.Value;
            }
        };
        bar.ValueChanged += (_, eventArgs) => valueChangedValue = eventArgs.Value;

        // Act
        bar.Minimum = 50;

        // Assert
        propertyChangedValue.ShouldBe(50);
        valueChangedValue.ShouldBe(50);
    }

    /// <summary>Verifies ValueChanged does not fire when endpoint changes leave the current value in range.</summary>
    [Fact]
    public void ValueChanged_WhenEndpointChangesPreserveValue_DoesNotFire()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 50 };
        var raised = 0;
        bar.ValueChanged += (_, _) => raised++;

        // Act
        bar.Minimum = 10;
        bar.Maximum = 90;

        // Assert
        raised.ShouldBe(0);
        bar.Value.ShouldBe(50);
    }

    /// <summary>Verifies Minimum's own PropertyChanged fires before ValueChanged, and
    /// that Value already reports its coherent clamped result inside both callbacks.</summary>
    [Fact]
    public void ValueChanged_WhenMinimumClampsValue_FiresAfterMinimumPropertyChangedWithCoherentValue()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 30 };
        List<string> order = [];
        bar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ProgressBar.Minimum))
            {
                order.Add($"Minimum:{bar.Value}");
            }
        };
        bar.ValueChanged += (_, eventArgs) => order.Add($"ValueChanged:{eventArgs.Value}");

        // Act
        bar.Minimum = 50;

        // Assert
        order.ShouldBe(["Minimum:50", "ValueChanged:50"]);
    }

    /// <summary>Verifies horizontal and vertical measure uses a 10-cell extent along the main axis.</summary>
    [Theory]
    [InlineData(Orientation.Horizontal, 10, 1)]
    [InlineData(Orientation.Vertical, 1, 10)]
    public void MeasureOverride_WhenOrientationIsSet_ReturnsExpectedDesiredSize(
        Orientation orientation,
        int expectedWidth,
        int expectedHeight)
    {
        // Arrange
        var bar = new ProgressBar
        {
            Orientation = orientation,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        new LayoutEngine().Layout(bar, new Size(100, 100));

        // Assert
        bar.Bounds.Width.ShouldBe(expectedWidth);
        bar.Bounds.Height.ShouldBe(expectedHeight);
    }

    /// <summary>Verifies assigning a Style built from a transparent part color throws and leaves
    /// the previous local Style untouched, since colors now live on ProgressBarStyle.</summary>
    [Fact]
    public void Style_WhenAssignedStyleHasTransparentPartColor_ThrowsBeforeMutation()
    {
        // Arrange
        var bar = new ProgressBar();
        var baseline = ProgressBarStyle.Default;

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => bar.Style = new ProgressBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            Color.Transparent,
            baseline.TrackColor,
            baseline.IndeterminateColor,
            baseline.Glyphs));
        bar.Style.ShouldBeNull();
    }

    /// <summary>Verifies a custom Style's glyph family overrides defaults, and clearing Style restores them.</summary>
    [Fact]
    public void Style_WhenGlyphsAreCustomized_OverrideDefaultsAndClearingRestores()
    {
        // Arrange
        var bar = new ProgressBar();
        var defaultGlyphs = bar.ActualStyle.Glyphs;
        var baseline = ProgressBarStyle.Default;

        // Act
        bar.Style = new ProgressBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.FillColor,
            baseline.TrackColor,
            baseline.IndeterminateColor,
            new ProgressBarGlyphs(new Rune('#'), new Rune('.'), new Rune('?')));

        // Assert custom
        bar.ActualStyle.Glyphs.Fill.ShouldBe(new Rune('#'));
        bar.ActualStyle.Glyphs.Track.ShouldBe(new Rune('.'));
        bar.ActualStyle.Glyphs.Indeterminate.ShouldBe(new Rune('?'));

        // Act reset
        bar.Style = null;

        // Assert restored
        bar.ActualStyle.Glyphs.ShouldBe(defaultGlyphs);
    }

    /// <summary>Verifies disposing the progress bar prevents further mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsValueMutation()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 50 };

        // Act
        bar.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() => bar.Value = 80);
    }

    /// <summary>Verifies minimum and maximum at equal values are rejected.</summary>
    [Fact]
    public void Endpoints_WhenEqual_ThrowArgumentException()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100 };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => bar.Minimum = 100);
        _ = Should.Throw<ArgumentException>(() => bar.Maximum = 0);
    }

    /// <summary>Verifies IsIndeterminate round-trips and its rendering replaces the value-driven
    /// fill/track bar with a uniform indeterminate glyph across every content cell.</summary>
    [Fact]
    public void Render_WhenIsIndeterminateIsTrue_FillsEveryCellWithIndeterminateGlyph()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Value = 0,
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        new LayoutEngine().Layout(bar, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        // Act
        bar.IsIndeterminate.ShouldBeTrue();
        bar.Render(frame.Canvas);

        // Assert
        for (var x = 0; x < 5; x++)
        {
            FrameOracle.Get(frame, new Point(x, 0)).ShouldBe(bar.ActualStyle.Glyphs.Indeterminate.ToString());
        }
    }

    /// <summary>Verifies rendering at Value = 0 produces a fully empty bar.</summary>
    [Fact]
    public void Render_WhenValueIsZero_ProducesEmptyBar()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Value = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        new LayoutEngine().Layout(bar, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        // Act
        bar.Render(frame.Canvas);

        // Assert
        for (var x = 0; x < 5; x++)
        {
            FrameOracle.Get(frame, new Point(x, 0)).ShouldBe("░");
        }
    }

    /// <summary>Verifies rendering at Value = Maximum produces a fully filled bar.</summary>
    [Fact]
    public void Render_WhenValueIsMaximum_ProducesFullBar()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Value = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        new LayoutEngine().Layout(bar, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        // Act
        bar.Render(frame.Canvas);

        // Assert
        for (var x = 0; x < 5; x++)
        {
            FrameOracle.Get(frame, new Point(x, 0)).ShouldBe("█");
        }
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes compute the effective
    /// disabled state ProgressBar's mounted disabled contract depends on.</summary>
    [Fact]
    public void IsEnabled_WhenDisabledDirectlyOrByAncestor_ComputesEffectiveState()
    {
        var bar = new ProgressBar();
        bar.EffectiveIsEnabled.ShouldBeTrue();

        bar.IsEnabled = false;
        bar.EffectiveIsEnabled.ShouldBeFalse();

        bar.IsEnabled = true;
        var stack = new Stack { Children = { bar } };
        bar.EffectiveIsEnabled.ShouldBeTrue();

        stack.IsEnabled = false;
        bar.EffectiveIsEnabled.ShouldBeFalse();
    }
}
