// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Tests.Styling;

/// <summary>Verifies a theme swap publishes <c>ActualStyle</c> whenever the value a viewer would
/// see actually changed - which is not the same question as whether the two style records compare
/// equal.
///
/// <para>A raw record comparison sees symbolic references. Two themes that map the same
/// <c>SemanticColor</c> to different literals produce style values that compare equal and render
/// differently, so the slot resolves each side against its own theme first. That reasoning was
/// applied to <c>ControlColor</c> and stopped there - while <c>ControlDecoration</c> has the
/// identical semantic/literal split, and <c>Face</c>, <c>Border</c>, and <c>Shadow</c> attributes
/// are all <c>ControlDecoration</c> that every bundled theme authors symbolically. Rendering stayed
/// correct, because invalidation goes through <c>Compare</c>, which resolves properly; only the
/// binding was left stale, indefinitely.</para>
/// </summary>
public sealed class ThemeResolvedStyleNotificationTests
{
    /// <summary>The regression this file exists to pin: two themes differing only in what their
    /// attributes table maps a symbolic decoration to must publish.</summary>
    [Fact]
    public void PropagateTheme_WhenOnlyTheAttributesTableDiffers_PublishesActualStyle()
    {
        using var probe = new StyleSlotProbe();
        var notifications = Observe(probe);
        probe.PropagateTheme(ThemeWithNormalTextAttributes("[]"));
        notifications.Clear();

        probe.PropagateTheme(ThemeWithNormalTextAttributes("\"bold\""));

        notifications.ShouldContain(nameof(StyleSlotProbe.ActualStyle));
    }

    /// <summary>The counter-case: two themes with identical tables must not publish, so this did not
    /// simply make every swap notify.</summary>
    [Fact]
    public void PropagateTheme_WhenTheAttributesTableMatches_DoesNotPublish()
    {
        using var probe = new StyleSlotProbe();
        var notifications = Observe(probe);
        probe.PropagateTheme(ThemeWithNormalTextAttributes("\"bold\""));
        notifications.Clear();

        probe.PropagateTheme(ThemeWithNormalTextAttributes("\"bold\""));

        notifications.ShouldNotContain(nameof(StyleSlotProbe.ActualStyle));
    }

    /// <summary>Verifies the color half - the case that was already handled - still works, since the
    /// decoration branch sits directly above it and shares the recursive walk.</summary>
    [Fact]
    public void PropagateTheme_WhenOnlyThePaletteDiffers_StillPublishesActualStyle()
    {
        using var probe = new StyleSlotProbe();
        var notifications = Observe(probe);
        probe.PropagateTheme(ThemeCatalog.Parse(ThemeJson.Create(foreground: "#e0e0e0")));
        notifications.Clear();

        probe.PropagateTheme(ThemeCatalog.Parse(ThemeJson.Create(foreground: "#c0c0c0")));

        notifications.ShouldContain(nameof(StyleSlotProbe.ActualStyle));
    }

    /// <summary>Verifies an <c>ImmutableArray</c> member with equal contents stops reporting a
    /// change. Boxed, it compares by the underlying array reference, so two structurally identical
    /// resolutions of <c>SpinnerStyle.Frames</c> looked different every single time - the
    /// over-notifying mirror of the two gaps above.</summary>
    [Fact]
    public void PropagateTheme_WhenAFrameArrayResolvesEqually_DoesNotPublish()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create(extraStyles: _spinnerFrames));
        var current = ThemeCatalog.Parse(ThemeJson.Create(extraStyles: _spinnerFrames));

        SpinnerStyle.Definition.Resolve(null, previous).Frames
            .ShouldBe(SpinnerStyle.Definition.Resolve(null, current).Frames);
        ReportsThemeChange(previous, current).ShouldBeFalse();
    }

    /// <summary>The counter-case for that member: genuinely different frames must still report a
    /// change, so the sequence comparison did not silence the notification altogether.</summary>
    [Fact]
    public void PropagateTheme_WhenAFrameArrayDiffers_ReportsAChange()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create(extraStyles: _spinnerFrames));
        var current = ThemeCatalog.Parse(ThemeJson.Create(
            extraStyles: """, "spinner": { "normal": { "frames": ["x", "y", "z"] } } """));

        ReportsThemeChange(previous, current).ShouldBeTrue();
    }

    private const string _spinnerFrames = """, "spinner": { "normal": { "frames": ["a", "b", "c"] } } """;

    private static bool ReportsThemeChange(Theme previous, Theme current)
    {
        using var spinner = new Spinner();
        var notifications = Observe(spinner);
        spinner.PropagateTheme(previous);
        notifications.Clear();

        spinner.PropagateTheme(current);

        return notifications.Contains(nameof(Spinner.ActualStyle));
    }

    private static List<string?> Observe(ControlBase control)
    {
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        return notifications;
    }

    // Differs from its sibling ONLY in the attributes table. Every styles.* section keeps referring
    // to "normalText" symbolically, which is exactly the shape that compared equal.
    private static Theme ThemeWithNormalTextAttributes(string attributes) =>
        ThemeCatalog.Parse(ThemeJson.Create().Replace("\"normalText\":[]", $"\"normalText\":{attributes}", StringComparison.Ordinal));
}
