// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

using System.Reflection;

/// <summary>Verifies raw intrinsic chrome is public only on approved layout hosts.</summary>
public sealed class ChromeAccessibilityTests
{
    /// <summary>Verifies specialized controls do not publish raw chrome authoring members.</summary>
    /// <param name="type">The specialized public control type.</param>
    [Theory]
    [InlineData(typeof(Button))]
    [InlineData(typeof(TextInput))]
    [InlineData(typeof(ListView))]
    [InlineData(typeof(GroupBox))]
    [InlineData(typeof(Expander))]
    public void Chrome_WhenControlIsSpecialized_IsNotPublic(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        type.GetProperty("Border", flags).ShouldBeNull();
        type.GetProperty("Shadow", flags).ShouldBeNull();
        type.GetMethod("ResetBorder", flags).ShouldBeNull();
        type.GetMethod("ResetShadow", flags).ShouldBeNull();
        type.GetMethod("SetAppearance", flags).ShouldBeNull();
        _ = type.GetProperty("ActualBorder", flags).ShouldNotBeNull();
        _ = type.GetProperty("ActualShadow", flags).ShouldNotBeNull();
    }

    /// <summary>Verifies approved layout hosts republish complete raw chrome authoring.</summary>
    /// <param name="type">The approved public host type.</param>
    [Theory]
    [InlineData(typeof(Dock))]
    [InlineData(typeof(Grid))]
    [InlineData(typeof(Stack))]
    [InlineData(typeof(Overlay))]
    [InlineData(typeof(Window))]
    [InlineData(typeof(Popup))]
    public void Chrome_WhenControlIsApprovedHost_IsPublic(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        _ = type.GetProperty("Border", flags).ShouldNotBeNull();
        _ = type.GetProperty("Shadow", flags).ShouldNotBeNull();
        _ = type.GetMethod("ResetBorder", flags).ShouldNotBeNull();
        _ = type.GetMethod("ResetShadow", flags).ShouldNotBeNull();
        type.GetMethod("SetAppearance", flags).ShouldBeNull();
    }
}
