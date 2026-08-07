// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies framework-owned complete-style slots.</summary>
public sealed class StyleSlotTests
{
    /// <summary>Verifies the generic base owns the standard slot, facade, and callback.</summary>
    [Fact]
    public void Style_WhenGenericControlAssignsAndResets_UsesFrameworkFacade()
    {
        // Arrange
        var control = new GenericStyleProbe();
        var local = ButtonStyle.Filled with { Padding = new Thickness(3) };

        // Act
        control.Style = local;
        var assigned = control.ActualStyle;
        control.Style = null;

        // Assert
        assigned.ShouldBe(local);
        control.ActualStyle.ShouldBe(ButtonStyle.Definition.Resolve(null, ThemeCatalog.Dark));
        control.StyleChanges.ShouldBe(2);
    }

    /// <summary>Verifies local assignment and reset use one framework-owned resolution path.</summary>
    [Fact]
    public void Local_WhenAssignedAndReset_ResolvesLocalThenTheme()
    {
        // Arrange
        var control = new StyleSlotProbe();
        var lightInput = ThemeCatalog.Load("default-light").Input.Normal;
        var local = new ButtonStyle(lightInput.Face, lightInput.Border, lightInput.Shadow, new Thickness(3));

        // Act
        control.Style = local;
        var assigned = control.ActualStyle;
        control.Style = null;
        var reset = control.ActualStyle;

        // Assert
        assigned.ShouldBe(local);
        reset.ShouldBe(ButtonStyle.Definition.Resolve(null, ThemeCatalog.Dark));
    }

    /// <summary>Verifies binding forwards nullable ownership instead of pinning a resolved value.</summary>
    [Fact]
    public void BindStyle_WhenLocalIsAssignedAndReset_ForwardsNullableLocal()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();
        var lightInput = ThemeCatalog.Load("default-light").Input.Normal;
        var local = new ButtonStyle(lightInput.Face, lightInput.Border, lightInput.Shadow, new Thickness(3));

        // Act
        control.ButtonStyle = local;
        var assigned = control.Target.Style;
        control.ButtonStyle = null;

        // Assert
        assigned.ShouldBe(local);
        control.SecondTarget.Style.ShouldBeNull();
        control.Target.Style.ShouldBeNull();
        control.Target.ActualStyle.ShouldBe(ButtonStyle.Definition.Resolve(null, ThemeCatalog.Dark));
    }

    /// <summary>Verifies a bound target cannot acquire a competing local owner.</summary>
    [Fact]
    public void Local_WhenSlotHasUpstreamOwner_Throws()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();

        // Act
        var darkInput = ThemeCatalog.Dark.Input.Normal;
        var exception = Should.Throw<InvalidOperationException>(() =>
            control.Target.Style = new ButtonStyle(darkInput.Face, darkInput.Border, darkInput.Shadow, new Thickness(3)));

        // Assert
        exception.Message.ShouldContain("upstream");
    }

    /// <summary>Verifies one source fans out to every matching retained target.</summary>
    [Fact]
    public void BindStyle_WhenSourceHasMultipleTargets_ForwardsToAll()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();
        var local = ButtonStyle.Filled;

        // Act
        control.ButtonStyle = local;

        // Assert
        control.Target.Style.ShouldBe(local);
        control.SecondTarget.Style.ShouldBe(local);
    }

    /// <summary>Verifies duplicate, mismatched, and cross-tree bindings are rejected before mutation.</summary>
    [Fact]
    public void BindStyle_WhenGraphIsInvalid_Throws()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(control.BindDuplicate);
        _ = Should.Throw<InvalidOperationException>(control.BindMismatchedType);
        _ = Should.Throw<InvalidOperationException>(() => control.BindCrossTree(new StyleSlotProbe()));
    }

    /// <summary>Verifies target disposal releases its upstream binding edge.</summary>
    [Fact]
    public void Dispose_WhenBoundTargetIsRemoved_SourceRemainsMutable()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();
        control.Target.Dispose();

        // Act
        control.ButtonStyle = ButtonStyle.Filled;

        // Assert
        control.ButtonStyle.ShouldBe(ButtonStyle.Filled);
        control.SecondTarget.Style.ShouldBe(ButtonStyle.Filled);
    }

    /// <summary>Verifies repeated resolved reads use one cached value without allocating.</summary>
    [Fact]
    public void Actual_WhenReadRepeatedly_UsesCachedAllocationFreeValue()
    {
        // Arrange
        var control = new StyleSlotProbe();
        _ = control.ActualStyle;
        var minimum = long.MaxValue;

        // Act
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 1_000; index++)
            {
                _ = control.ActualStyle;
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // Assert
        control.CompletionCalls.ShouldBe(1);
        minimum.ShouldBe(0);
    }
}
