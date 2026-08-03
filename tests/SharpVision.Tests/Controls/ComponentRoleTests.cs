// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Reflection;

/// <summary>Guards the public inheritance roles exposed by interactive controls.</summary>
public sealed class ComponentRoleTests
{
    /// <summary>Verifies raw pointer-target resolution is framework-internal.</summary>
    [Fact]
    public void Control_WhenInspected_DoesNotExposeHitTestTargetResolution()
    {
        var publicHitTest = typeof(ControlBase).GetMethod(
            nameof(ControlBase.HitTest),
            BindingFlags.Public | BindingFlags.Instance);
        var internalHitTest = typeof(ControlBase).GetMethod(
            nameof(ControlBase.HitTest),
            BindingFlags.NonPublic | BindingFlags.Instance);

        publicHitTest.ShouldBeNull();
        _ = internalHitTest.ShouldNotBeNull();
        internalHitTest.IsAssembly.ShouldBeTrue();
        internalHitTest.IsVirtual.ShouldBeTrue();
    }

    /// <summary>Verifies every concrete public-child container must define its layout algorithm.</summary>
    [Fact]
    public void Container_WhenInspected_RequiresMeasureAndArrangeOverrides()
    {
        var measure = typeof(Container).GetMethod(
            "MeasureOverride",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var arrange = typeof(Container).GetMethod(
            "ArrangeOverride",
            BindingFlags.NonPublic | BindingFlags.Instance);

        _ = measure.ShouldNotBeNull();
        measure.DeclaringType.ShouldBe(typeof(Container));
        measure.IsAbstract.ShouldBeTrue();
        _ = arrange.ShouldNotBeNull();
        arrange.DeclaringType.ShouldBe(typeof(Container));
        arrange.IsAbstract.ShouldBeTrue();
    }

    /// <summary>Verifies every single-face pressable uses the single-content authoring role.</summary>
    [Fact]
    public void Type_WhenInspected_UsesSingleContentPressableRole()
    {
        var pressable = typeof(PressableBase);
        Type[] concrete =
        [
            typeof(Button),
            typeof(CheckBox),
            typeof(RadioButton),
            typeof(MenuItem),
            typeof(ListItem),
            typeof(ProbePressable)
        ];

        pressable.BaseType.ShouldBe(typeof(ContentControl));
        pressable.GetProperty(nameof(ContentControl.Content),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ShouldBeNull();
        pressable.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance).ShouldBeNull();
        pressable.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .ShouldAllBe(constructor => constructor.GetParameters().Length == 0);
        pressable.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .ShouldNotContain(field => field.FieldType == typeof(PointerManager));

        foreach (var type in concrete)
        {
            typeof(ContentControl).IsAssignableFrom(type).ShouldBeTrue(type.FullName);
            typeof(Container).IsAssignableFrom(type).ShouldBeFalse(type.FullName);
            type.GetProperty(nameof(ContentControl.Content),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .ShouldBeNull(type.FullName);
            type.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance).ShouldBeNull(type.FullName);
        }
    }

    /// <summary>Verifies ComboBox is a composed primitive with no public access to private presentation parts.</summary>
    [Fact]
    public void ComboBox_WhenInspected_IsDirectControlWithoutLeakedPartsOrHiddenMembers()
    {
        var type = typeof(ComboBox);

        type.BaseType.ShouldBe(typeof(ControlBase));
        type.GetProperty(nameof(ContentControl.Content), BindingFlags.Public | BindingFlags.Instance).ShouldBeNull();
        type.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance).ShouldBeNull();
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(property => property.PropertyType == typeof(Popup) ||
                             property.PropertyType == typeof(ListView))
            .ShouldBeFalse();
    }

    /// <summary>Verifies the typed table API does not leak its private cell presenter as a panel contract.</summary>
    [Fact]
    public void Table_WhenInspected_UsesPrivateItemsPresentationRole()
    {
        var type = typeof(Table);

        type.BaseType.ShouldBe(typeof(ItemsControl));
        typeof(Container).IsAssignableFrom(type).ShouldBeFalse();
        type.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance).ShouldBeNull();
        _ = type.GetProperty(nameof(Table.Rows)).ShouldNotBeNull();
        _ = type.GetProperty(nameof(Table.Columns)).ShouldNotBeNull();
    }

    /// <summary>Verifies menu separators are a distinct non-pressable control role.</summary>
    [Fact]
    public void MenuSeparator_WhenInspected_IsNotAMenuItemKind()
    {
        var separator = typeof(Menu).Assembly.GetType("SharpVision.Menus.MenuSeparator").ShouldNotBeNull();

        typeof(ControlBase).IsAssignableFrom(separator).ShouldBeTrue();
        typeof(PressableBase).IsAssignableFrom(separator).ShouldBeFalse();
        Enum.GetNames<MenuItemKind>().ShouldNotContain("Separator");
    }
}
