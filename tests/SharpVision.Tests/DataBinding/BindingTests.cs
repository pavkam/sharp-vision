// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies the public scalar data-binding lifetime and update contract.</summary>
public sealed class BindingTests
{
    /// <summary>Verifies a Text binding initializes and observes one notifying model property.</summary>
    [Fact]
    public void Bind_WhenSourceChanges_UpdatesTextOnce()
    {
        // Arrange
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText("unused");

        // Act
        using var binding = target.Bind(model, source => source.Name);
        model.Name = "After";

        // Assert
        target.Content.ShouldBe("After");
        binding.Mode.ShouldBe(BindingMode.OneWay);
        binding.Target.ShouldBeSameAs(target);
        binding.Disposed.ShouldBeFalse();
    }

    /// <summary>Verifies explicit disposal stops later source notifications.</summary>
    [Fact]
    public void Dispose_WhenCalled_StopsSourceUpdates()
    {
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        var binding = target.Bind(model, source => source.Name);

        binding.Dispose();
        model.Name = "After";

        binding.Disposed.ShouldBeTrue();
        target.Content.ShouldBe("Before");
    }

    /// <summary>Verifies plain objects still participate in deterministic initial synchronization.</summary>
    [Fact]
    public void Bind_WhenSourceDoesNotNotify_InitializesOnce()
    {
        var model = new { Name = "Plain" };
        var target = new ControlText();
        using var binding = target.Bind(model, source => source.Name);

        target.Content.ShouldBe("Plain");
    }

    /// <summary>Verifies target disposal owns and releases its live bindings.</summary>
    [Fact]
    public void Dispose_WhenTargetIsDisposed_DisposesBindingAndStopsUpdates()
    {
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        var binding = target.Bind(model, source => source.Name);

        target.Dispose();
        model.Name = "After";

        binding.Disposed.ShouldBeTrue();
    }
}
