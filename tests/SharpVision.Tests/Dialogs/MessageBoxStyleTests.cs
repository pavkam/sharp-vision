// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies the immutable MessageBox aggregate presentation record: its declared one-hop
/// fallback to <see cref="WindowStyle"/>'s "window" key (including the shared
/// ActiveBorder-on-FocusWithin default every Window-derived style resolves), and its invalidation
/// policy.</summary>
public sealed class MessageBoxStyleTests
{
    /// <summary>Verifies Default carries WindowStyle's own Face/Border/Shadow, a message face
    /// equal to that Face, and the established content geometry.</summary>
    [Fact]
    public void Default_ResolvesThemeWindowStyleDefaultsWithEstablishedGeometry()
    {
        MessageBoxStyle.Default.Face.ShouldBe(WindowStyle.Default.Face);
        MessageBoxStyle.Default.Border.ShouldBe(WindowStyle.Default.Border);
        MessageBoxStyle.Default.Shadow.ShouldBe(WindowStyle.Default.Shadow);
        MessageBoxStyle.Default.MessageFace.ShouldBe(WindowStyle.Default.Face);
        MessageBoxStyle.Default.MessageMargin.ShouldBe(new Thickness(1, 2, 1, 0));
        MessageBoxStyle.Default.ActionBarMargin.ShouldBe(new Thickness(1, 0));
    }

    /// <summary>Verifies equality compares every record member structurally, the free record behavior.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var equivalent = new MessageBoxStyle(
            MessageBoxStyle.Default.Face,
            MessageBoxStyle.Default.Border,
            MessageBoxStyle.Default.Shadow,
            MessageBoxStyle.Default.MessageFace,
            MessageBoxStyle.Default.MessageMargin,
            MessageBoxStyle.Default.ActionBarMargin);

        equivalent.ShouldBe(MessageBoxStyle.Default);
        (equivalent == MessageBoxStyle.Default).ShouldBeTrue();
        equivalent.ShouldNotBe(MessageBoxStyle.Default with { ActionBarMargin = new Thickness(2, 0) });
    }

    /// <summary>Verifies an unauthored theme resolves to the Window fallback appearance, including
    /// its ActiveBorder-on-FocusWithin patch.</summary>
    [Fact]
    public void Definition_Resolve_WhenNoLocalAndThemeDoesNotAuthorMessageBox_FallsBackToWindow()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var resolved = MessageBoxStyle.Definition.Resolve(null, theme);
        var window = theme.GetWindowStyleSet();

        resolved.Border.ShouldBe(window.Normal.Border);
        resolved.MessageFace.ShouldBe(window.Normal.Face);
    }

    /// <summary>Verifies a local override always wins over both the theme and the fallback.</summary>
    [Fact]
    public void Definition_Resolve_WhenLocalIsSupplied_LocalWins()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());
        var local = MessageBoxStyle.Default with { ActionBarMargin = new Thickness(4, 2) };

        var resolved = MessageBoxStyle.Definition.Resolve(local, theme);

        resolved.ShouldBe(local);
    }

    /// <summary>Verifies a content-geometry change is classified as a measure-affecting invalidation.</summary>
    [Fact]
    public void Definition_Compare_WhenActionBarMarginChanges_IsMeasure()
    {
        var previous = MessageBoxStyle.Default;
        var current = previous with { ActionBarMargin = new Thickness(2, 0) };

        MessageBoxStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies a change that touches only Face is non-invalidating here, because the
    /// appearance pipeline already covers Face/Border/Shadow. Compare is responsible for everything
    /// else.</summary>
    [Fact]
    public void Definition_Compare_WhenOnlyTheOwnFaceChanges_IsNone()
    {
        var previous = MessageBoxStyle.Default;
        var current = previous with { Face = previous.Face with { Foreground = SemanticColor.Accent } };

        MessageBoxStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.None);
    }

    /// <summary>Verifies a MessageFace change is NOT irrelevant.
    ///
    /// <para>This test previously asserted the opposite, and its name said the assumption out loud:
    /// MessageFace was treated as "nothing relevant". It is applied to the retained message Text
    /// from MeasureOverride, so a Compare that returns None means the pass which would apply it
    /// never runs and the caption keeps its old face.</para>
    /// </summary>
    [Fact]
    public void Definition_Compare_WhenTheMessageFaceChanges_IsMeasure()
    {
        var previous = MessageBoxStyle.Default;
        var current = previous with { MessageFace = previous.MessageFace with { Foreground = SemanticColor.Accent } };

        MessageBoxStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }
}
