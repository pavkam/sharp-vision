// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves the shared range, commit, event, and measurement contract
/// <see cref="NumericInputBase"/> owns, independent of any concrete field's own formatting
/// policy.</summary>
public sealed class NumericInputBaseTests
{
    /// <summary>Verifies value when set outside range clamps.</summary>
    [Fact]
    public void Value_WhenSetOutsideRange_Clamps()
    {
        // Arrange
        using var probe = new NumericInputBaseProbe { Minimum = 0m, Maximum = 10m };

        // Act
        probe.Value = 99m;

        // Assert
        probe.Value.ShouldBe(10m);
    }

    /// <summary>Verifies value changed when committed reports previous and current.</summary>
    [Fact]
    public void ValueChanged_WhenCommitted_ReportsPreviousAndCurrent()
    {
        // Arrange
        using var probe = new NumericInputBaseProbe { Value = 1m };
        NumericValueChangedEventArgs? observed = null;
        probe.ValueChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        probe.Value = 2m;

        // Assert
        var raised = observed.ShouldNotBeNull();
        raised.Previous.ShouldBe(1m);
        raised.Current.ShouldBe(2m);
    }

    /// <summary>Verifies measure override when minimum is widest reserves its width plus caret.</summary>
    [Fact]
    public void MeasureOverride_WhenMinimumIsWidest_ReservesItsWidthPlusCaret()
    {
        // Arrange
        using var probe = new NumericInputBaseProbe { Minimum = -999999m, Maximum = 1m };
        var widest = (-999999m).ToString(CultureInfo.InvariantCulture);

        // Act
        new LayoutEngine().Layout(probe, new Size(80, 3));

        // Assert - independent of whatever border chrome the active theme resolves, the content
        // box beyond that chrome must be exactly the widest bound plus one caret cell.
        var border = probe.ActualBorder;
        var horizontalInset =
            ((border.Sides & BorderSide.Left) != 0 ? 1 : 0) + ((border.Sides & BorderSide.Right) != 0 ? 1 : 0);
        var verticalInset =
            ((border.Sides & BorderSide.Top) != 0 ? 1 : 0) + ((border.Sides & BorderSide.Bottom) != 0 ? 1 : 0);
        probe.DesiredSize.ShouldBe(
            new Size(probe.MeasureCells(widest) + 1 + horizontalInset, 1 + verticalInset));
    }
}
