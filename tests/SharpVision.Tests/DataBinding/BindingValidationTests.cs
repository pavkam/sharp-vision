// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies binding declarations fail before publishing invalid relationships.</summary>
public sealed class BindingValidationTests
{
    /// <summary>Verifies method-call paths are rejected.</summary>
    [Fact]
    public void BindProperty_WhenSourceIsMethodCall_Throws()
    {
        var model = new BindingModel();
        var target = new ControlText();

        _ = Should.Throw<ArgumentException>(() => target.BindProperty(
            control => control.Content,
            model,
            source => source.ToString(),
            BindingMode.OneWay));
    }

    /// <summary>Verifies a read-only source cannot accept two-way updates.</summary>
    [Fact]
    public void BindProperty_WhenSourceLeafIsReadOnly_Throws()
    {
        var model = new BindingModel();
        var target = new TextInput();

        _ = Should.Throw<ArgumentException>(() => target.BindProperty(
            control => control.Text,
            model,
            source => source.ReadOnly,
            BindingMode.TwoWay));
    }

    /// <summary>Verifies duplicate target writers are rejected without disturbing the first binding.</summary>
    [Fact]
    public void Bind_WhenTargetPropertyAlreadyBound_ThrowsAndKeepsFirst()
    {
        var first = new BindingModel { Name = "First" };
        var second = new BindingModel { Name = "Second" };
        var target = new ControlText();
        using var binding = target.Bind(first, source => source.Name);

        _ = Should.Throw<ArgumentException>(() => target.Bind(second, source => source.Name));
        first.Name = "Current";

        target.Content.ShouldBe("Current");
    }

    /// <summary>Verifies unknown direction values fail before target mutation.</summary>
    [Fact]
    public void Bind_WhenModeIsUnknown_ThrowsBeforeMutation()
    {
        var model = new BindingModel { Name = "Model" };
        var target = new TextInput { Text = "Target" };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            target.Bind(model, source => source.Name, (BindingMode) 99));

        target.Text.ShouldBe("Target");
    }

    /// <summary>Verifies two-way conversion requires a reverse converter.</summary>
    [Fact]
    public void BindProperty_WhenReverseConverterIsMissing_Throws()
    {
        var model = new BindingModel { Number = 1 };
        var target = new ControlText();

        _ = Should.Throw<ArgumentNullException>(() => target.BindProperty(
            control => control.Content,
            model,
            source => source.Number,
            value => value.ToString(CultureInfo.InvariantCulture),
            convertBack: null,
            BindingMode.TwoWay,
            string.Empty));
    }

    /// <summary>Verifies disposed targets reject declaration before model access.</summary>
    [Fact]
    public void Bind_WhenTargetIsDisposed_Throws()
    {
        var model = new BindingModel { Name = "Model" };
        var target = new ControlText();
        target.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => target.Bind(model, source => source.Name));
    }
}
