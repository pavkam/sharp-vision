// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies typed forward/reverse conversion and fallback behavior.</summary>
public sealed class BindingConversionTests
{
    /// <summary>Verifies a one-way converter projects a differently typed source value.</summary>
    [Fact]
    public void BindProperty_WhenTypesDiffer_ConvertsToTarget()
    {
        var model = new BindingModel { Number = 12 };
        var target = new ControlText();
        using var binding = target.BindProperty(
            control => control.Content,
            model,
            source => source.Number,
            value => value.ToString(CultureInfo.InvariantCulture),
            convertBack: null,
            BindingMode.OneWay,
            "missing");

        target.Content.ShouldBe("12");

        model.Number = 25;

        target.Content.ShouldBe("25");
    }

    /// <summary>Verifies a two-way converter parses committed target values back into the model.</summary>
    [Fact]
    public void BindProperty_WhenTwoWay_UsesReverseConverter()
    {
        var model = new BindingModel { Number = 12 };
        var target = new TextInput();
        using var binding = target.BindProperty(
            control => control.Text,
            model,
            source => source.Number,
            value => value.ToString(CultureInfo.InvariantCulture),
            value => int.Parse(value, CultureInfo.InvariantCulture),
            BindingMode.TwoWay,
            "0");

        target.Text.ShouldBe("12");

        target.Text = "34";

        model.Number.ShouldBe(34);
    }

    /// <summary>Verifies a target property that validates rather than clamps (e.g. Slider.Value
    /// outside its Minimum/Maximum) falls back to the declared fallback value instead of
    /// letting the write's own exception escape ApplySourceToTarget uncaught.</summary>
    [Fact]
    public void BindProperty_WhenTargetWriteThrows_AppliesFallback()
    {
        var model = new BindingModel { Number = 25 };
        var target = new Slider { Maximum = 50 };
        using var binding = target.BindProperty(
            control => control.Value,
            model,
            source => source.Number,
            value => value,
            convertBack: null,
            BindingMode.OneWay,
            0);

        target.Value.ShouldBe(25);

        _ = Should.NotThrow(() => model.Number = 999);

        target.Value.ShouldBe(0);
    }

    /// <summary>Verifies an unavailable nested path uses only the declared fallback.</summary>
    [Fact]
    public void BindProperty_WhenPathIsUnavailable_UsesFallback()
    {
        var model = new BindingModel();
        var target = new ControlText();
        using var binding = target.BindProperty(
            control => control.Content,
            model,
            source => source.Address!.City,
            value => value ?? "null leaf",
            convertBack: null,
            BindingMode.OneWay,
            "missing branch");

        target.Content.ShouldBe("missing branch");
    }

    /// <summary>Verifies a throwing converter applies the declared fallback value.</summary>
    [Fact]
    public void BindProperty_WhenConverterThrows_AppliesFallback()
    {
        var model = new BindingModel { Number = 1 };
        var target = new ControlText();
        using var binding = target.BindProperty(
            control => control.Content,
            model,
            source => source.Number,
            _ => throw new InvalidOperationException("boom"),
            convertBack: null,
            BindingMode.OneWay,
            "fallback");

        target.Content.ShouldBe("fallback");
    }

    /// <summary>Verifies a throwing converter during update still applies the fallback.</summary>
    [Fact]
    public void BindProperty_WhenConverterThrowsDuringUpdate_AppliesFallback()
    {
        var callCount = 0;
        var model = new BindingModel { Number = 1 };
        var target = new ControlText();
        using var binding = target.BindProperty(
            control => control.Content,
            model,
            source => source.Number,
            value =>
            {
                callCount++;
                return callCount == 1
                    ? value.ToString(CultureInfo.InvariantCulture)
                    : throw new InvalidOperationException("boom");
            },
            convertBack: null,
            BindingMode.OneWay,
            "fallback");

        target.Content.ShouldBe("1");

        model.Number = 2;

        target.Content.ShouldBe("fallback");
    }
}
