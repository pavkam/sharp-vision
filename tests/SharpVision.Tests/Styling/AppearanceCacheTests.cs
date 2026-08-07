// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies bounded per-control resolved-appearance caching and invalidation.</summary>
public sealed class AppearanceCacheTests
{
    /// <summary>Verifies repeated reads share one cached resolution until the theme changes.</summary>
    [Fact]
    public void ActualFace_WhenReadRepeatedly_UsesStateCacheUntilThemeChanges()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        _ = control.ActualFace;
        _ = control.ActualFace;

        control.UncachedAppearanceResolutionCount.ShouldBe(1);

        control.SetTheme(ThemeCatalog.White);
        _ = control.ActualFace;

        control.UncachedAppearanceResolutionCount.ShouldBe(2);
    }

    /// <summary>Verifies exact visual-state flag sets occupy independent cache entries.</summary>
    [Fact]
    public void GetActualFace_WhenStatesDiffer_CachesEachExactState()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        _ = control.GetActualFace(VisualState.Normal);
        _ = control.GetActualFace(VisualState.PointerOver);
        _ = control.GetActualFace(VisualState.PointerOver);

        control.UncachedAppearanceResolutionCount.ShouldBe(2);
    }

    /// <summary>Verifies the sparse cache grows past its small inline capacity and still resolves
    /// every distinct state exactly once — the cache starts at 4 slots rather than the full
    /// 512-combination VisualState space.</summary>
    [Fact]
    public void GetActualFace_WhenMoreStatesThanInlineCapacityAreUsed_StillCachesEachExactly()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);
        VisualState[] states =
        [
            VisualState.Normal,
            VisualState.PointerOver,
            VisualState.Focused,
            VisualState.Selected,
            VisualState.Checked,
            VisualState.Pressed,
            VisualState.Disabled
        ];

        foreach (var state in states)
        {
            _ = control.GetActualFace(state);
        }

        control.UncachedAppearanceResolutionCount.ShouldBe(states.Length);

        foreach (var state in states)
        {
            _ = control.GetActualFace(state);
        }

        control.UncachedAppearanceResolutionCount.ShouldBe(states.Length);
    }

    /// <summary>Verifies changing a local state set clears previously resolved entries.</summary>
    [Fact]
    public void SetAppearance_WhenCacheExists_ClearsResolvedEntries()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);
        _ = control.ActualBorder;

        control.SetStateAppearance(
            VisualState.PointerOver,
            new AppearanceOverlay(border: new BorderOverlay(foreground: Color.Rgb(1, 2, 3))));
        _ = control.ActualBorder;

        control.UncachedAppearanceResolutionCount.ShouldBe(2);
    }

    /// <summary>Verifies a complete developer face remains authoritative over ambient inheritance,
    /// regardless of its own transparency — unlike Normal or a state overlay, a LocalFace is a
    /// complete override commonly authored with its own foreground and a left-default transparent
    /// background (e.g. FigletText, Spinner), so it keeps opting out of inheritance entirely.</summary>
    [Fact]
    public void ActualFace_WhenLocalTransparentFaceIsSet_PreservesDeveloperForeground()
    {
        var parent = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3))
        };
        var child = new ProbeControl
        {
            Face = AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6))
        };
        parent.Children.Add(child);
        parent.SetTheme(ThemeCatalog.Dark);

        child.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
    }

    /// <summary>Verifies a partial developer state face is applied after ambient inheritance.</summary>
    [Fact]
    public void GetActualFace_WhenLocalStateFaceIsSet_PreservesDeveloperForeground()
    {
        var parent = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3))
        };
        var child = new ProbeControl();
        child.SetStateAppearance(
            VisualState.PointerOver,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(4, 5, 6))));
        parent.Children.Add(child);
        parent.SetTheme(ThemeCatalog.Dark);

        child.GetActualFace(VisualState.PointerOver).Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
    }
}
