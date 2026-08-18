// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Proves progress transition events contain finite committed values.</summary>
public sealed class ProgressValueChangedEventArgsTests
{
    /// <summary>Verifies neither endpoint can publish a non-finite progress value.</summary>
    [Theory]
    [InlineData(double.NaN, 0, "previousValue")]
    [InlineData(0, double.PositiveInfinity, "value")]
    public void Constructor_WhenValueIsNotFinite_Throws(
        double previousValue,
        double value,
        string parameterName)
    {
        var action = () => new ProgressValueChangedEventArgs(previousValue, value);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(parameterName);
    }
}
