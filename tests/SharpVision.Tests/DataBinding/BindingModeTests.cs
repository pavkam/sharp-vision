// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies scalar binding direction and committed event ordering.</summary>
public sealed class BindingModeTests
{
    /// <summary>Verifies TextInput defaults to two-way and updates the model before its semantic event.</summary>
    [Fact]
    public void Bind_WhenTextInputChanges_UpdatesModelBeforeTextChanged()
    {
        var model = new BindingModel { Name = "Before" };
        var input = new TextInput();
        using var binding = input.Bind(model, source => source.Name);
        string? observed = null;
        input.TextChanged += (_, _) => observed = model.Name;

        input.Text = "After";

        binding.Mode.ShouldBe(BindingMode.TwoWay);
        model.Name.ShouldBe("After");
        observed.ShouldBe("After");
    }

    /// <summary>Verifies explicit one-way-to-source initializes the model from the retained target.</summary>
    [Fact]
    public void Bind_WhenModeIsOneWayToSource_InitializesSourceOnly()
    {
        var model = new BindingModel { Name = "Model" };
        var input = new TextInput { Text = "Target" };
        using var binding = input.Bind(model, source => source.Name, BindingMode.OneWayToSource);

        model.Name.ShouldBe("Target");

        model.Name = "Ignored";

        input.Text.ShouldBe("Target");
    }

    /// <summary>Verifies an unrelated target notification avoids an equal source write.</summary>
    [Fact]
    public void Bind_WhenTargetPublishesUnrelatedProperty_DoesNotRewriteEqualSource()
    {
        var model = new BindingModel { Name = "Equal" };
        var input = new TextInput();
        using var binding = input.Bind(model, source => source.Name);

        input.SetTheme(Themes.Dark);

        model.Name.ShouldBe("Equal");
        input.Text.ShouldBe("Equal");
    }
}
