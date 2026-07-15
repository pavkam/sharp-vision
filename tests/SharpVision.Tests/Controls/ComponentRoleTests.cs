// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Reflection;

/// <summary>Guards the public inheritance roles exposed by interactive controls.</summary>
public sealed class ComponentRoleTests
{
    /// <summary>Verifies every single-face pressable uses the single-content authoring role.</summary>
    [Fact]
    public void Type_WhenInspected_UsesSingleContentPressableRole()
    {
        var pressable = typeof(Pressable);
        Type[] concrete =
        [
            typeof(Button),
            typeof(CheckBox),
            typeof(RadioButton),
            typeof(MenuItem),
            typeof(ListItem),
            typeof(ProbePressable),
        ];

        pressable.BaseType.ShouldBe(typeof(ContentControl));
        pressable.GetProperty(nameof(ContentControl.Content), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ShouldBeNull();
        pressable.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance).ShouldBeNull();
        pressable.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .ShouldAllBe(constructor => constructor.GetParameters().Length == 0);
        pressable.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .ShouldNotContain(field => field.FieldType == typeof(CaptureManager));

        foreach (var type in concrete)
        {
            typeof(ContentControl).IsAssignableFrom(type).ShouldBeTrue(type.FullName);
            typeof(Container).IsAssignableFrom(type).ShouldBeFalse(type.FullName);
            type.GetProperty(nameof(ContentControl.Content), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .ShouldBeNull(type.FullName);
            type.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance).ShouldBeNull(type.FullName);
        }
    }

    /// <summary>Verifies ComboBox is a composed primitive with no public access to private presentation parts.</summary>
    [Fact]
    public void ComboBox_WhenInspected_IsDirectControlWithoutLeakedPartsOrHiddenMembers()
    {
        var type = typeof(ComboBox);

        type.BaseType.ShouldBe(typeof(Control));
        type.GetProperty(nameof(ContentControl.Content), BindingFlags.Public | BindingFlags.Instance).ShouldBeNull();
        type.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance).ShouldBeNull();
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(property => property.PropertyType == typeof(Popup) ||
                property.PropertyType == typeof(SharpVision.Controls.List))
            .ShouldBeFalse();
    }

    /// <summary>Verifies menu separators are a distinct non-pressable control role.</summary>
    [Fact]
    public void MenuSeparator_WhenInspected_IsNotAMenuItemKind()
    {
        var separator = typeof(Menu).Assembly.GetType("SharpVision.Controls.MenuSeparator").ShouldNotBeNull();

        typeof(Control).IsAssignableFrom(separator).ShouldBeTrue();
        typeof(Pressable).IsAssignableFrom(separator).ShouldBeFalse();
        Enum.GetNames<MenuItemKind>().ShouldNotContain("Separator");
    }
}
