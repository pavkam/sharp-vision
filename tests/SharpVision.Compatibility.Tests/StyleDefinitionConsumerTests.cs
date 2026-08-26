// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Compatibility.Tests;

using SharpVision.Controls.Display;
using SharpVision.Controls.Input;

/// <summary>Verifies external composites can use built-in forwarding definitions without internals.</summary>
public sealed class StyleDefinitionConsumerTests
{
    /// <summary>Verifies public definitions bind and forward all built-in part styles.</summary>
    [Fact]
    public void BuiltInForwardingDefinitions_WhenUsedExternally_BindToRetainedControls()
    {
        using var probe = new BuiltInPartStyleProbe
        {
            ForwardedButtonStyle = ButtonStyle.Filled,
            ForwardedCheckBoxStyle = CheckBoxStyle.Tick,
            ForwardedSeparatorStyle = SeparatorStyle.Default
        };

        probe.Button.Style.ShouldBe(ButtonStyle.Filled);
        probe.CheckBox.Style.ShouldBe(CheckBoxStyle.Tick);
        probe.Separator.Style.ShouldBe(SeparatorStyle.Default);
    }
}
