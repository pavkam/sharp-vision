// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides focused immutable themes for behavior tests that intentionally omit visual chrome.</summary>
internal static class TestThemes
{
    /// <summary>Gets a theme whose input role has transparent face and no intrinsic chrome.</summary>
    internal static Theme BorderlessInput { get; } = CreateBorderlessInput();

    /// <summary>Gets a theme whose container role has transparent face and no intrinsic chrome.</summary>
    internal static Theme BorderlessContainer { get; } = CreateBorderlessContainer();

    /// <summary>Creates a frozen copy of one Theme with only its input chrome removed.</summary>
    /// <param name="source">The Theme whose values and control styles are copied.</param>
    /// <returns>A frozen Theme suitable for geometry tests that isolate input content.</returns>
    internal static Theme WithoutInputChrome(Theme source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Create(source, ControlStyleProfiles.WithoutChrome(source.Input), source.Container);
    }

    private static Theme CreateBorderlessInput()
    {
        var source = Themes.Dark;
        return WithoutInputChrome(source);
    }

    private static Theme CreateBorderlessContainer()
    {
        var source = Themes.Dark;
        return Create(source, source.Input, ControlStyleProfiles.WithoutChrome(source.Container));
    }

    private static Theme Create(Theme source, ThemeProfile input, ThemeProfile container)
    {
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);
        foreach (var color in Enum.GetValues<ThemeColor>())
        {
            theme.SetColor(color, source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<ThemeDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        theme.SetStatusColors(Enum.GetValues<StatusColor>()
            .ToDictionary(status => status, source.ResolveStatusColor));
        theme.SetProfiles(
            source.Control,
            input,
            container,
            source.Window,
            source.Popup);

        theme.Freeze();
        return theme;
    }
}
