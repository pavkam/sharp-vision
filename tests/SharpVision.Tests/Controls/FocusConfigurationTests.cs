// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies configured versus effective focus participation.</summary>
public sealed class FocusConfigurationTests
{
    /// <summary>Verifies a configured focusable control becomes ineligible when hidden.</summary>
    [Fact]
    public void CanFocus_WhenFocusableControlIsHidden_IsFalse()
    {
        var control = new ProbeControl { Focusable = true };

        control.CanFocus.ShouldBeTrue();
        control.Visibility = Visibility.Hidden;

        control.CanFocus.ShouldBeFalse();
    }

    /// <summary>Verifies tab participation depends on both configuration and effective focus eligibility.</summary>
    [Fact]
    public void IsTabStop_WhenFocusableAndTabStopConfigured_IsTrue()
    {
        var control = new ProbeControl { Focusable = true, TabStop = true };

        control.CanTabStop.ShouldBeTrue();
        control.Enabled = false;
        control.CanTabStop.ShouldBeFalse();
    }
}
