// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




/// <summary>Verifies framed terminal window layout, title chrome, and visual shadow behavior.</summary>
public sealed class WindowTests
{
    /// <summary>Verifies Window exposes only the public single-content authoring role.</summary>
    [Fact]
    public void Type_WhenInspected_DerivesFromContentControlWithoutChildCollectionOrAlias()
    {
        var type = typeof(Window);

        type.BaseType.ShouldBe(typeof(ContentControl));
        typeof(Container).IsAssignableFrom(type).ShouldBeFalse();
        type.GetProperty(nameof(Container.Children)).ShouldBeNull();
        type.GetProperty(nameof(Container.AutoScroll)).ShouldBeNull();
        type.GetProperty(nameof(Container.AutoSize)).ShouldBeNull();
        type.GetProperty("Child").ShouldBeNull();
        _ = type.GetProperty(nameof(ContentControl.Content)).ShouldNotBeNull();
        var constructor = type.GetConstructors().ShouldHaveSingleItem();
        constructor.GetParameters().ShouldBeEmpty();
    }

    /// <summary>Verifies inherited content publication and direct disposal expose committed Window ownership.</summary>
    [Fact]
    public void Content_WhenAssignedThenDisposedDirectly_PublishesCommittedWindowOwnership()
    {
        var window = new Window();
        ContentControl owner = window;
        var content = new ProbeControl();
        var observations = new List<(Control? Content, Control? Parent)>();
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                observations.Add((owner.Content, content.Parent));
            }
        };

        owner.Content = content;
        content.Dispose();

        observations.ShouldBe([(content, window), (null, null)]);
        owner.Content.ShouldBeNull();
        content.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies Window disposal clears published content, disposes only its current content, and does so once.</summary>
    [Fact]
    public void Dispose_WhenWindowOwnsReplacement_DisposesCurrentOnceAndPublishesCommittedClear()
    {
        var window = new Window();
        var replaced = new OwnershipObserverControl();
        var current = new OwnershipObserverControl();
        window.Content = replaced;
        window.Content = current;
        var observations = new List<(Control? Content, Control? Parent, bool Disposed, int DisposingCalls)>();
        window.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                observations.Add((window.Content, current.Parent, current.IsDisposed, current.DisposingCalls));
            }
        };

        window.Dispose();
        window.Dispose();

        window.IsDisposed.ShouldBeTrue();
        window.Content.ShouldBeNull();
        current.IsDisposed.ShouldBeTrue();
        current.DisposingCalls.ShouldBe(1);
        observations.ShouldBe([(null, null, false, 1)]);
        replaced.IsDisposed.ShouldBeFalse();
        replaced.Parent.ShouldBeNull();
    }

    /// <summary>Verifies collapsed content contributes no margin and has stale layout state cleared.</summary>
    [Fact]
    public void Layout_WhenContentCollapses_PreservesFrameMinimumAndClearsContentGeometry()
    {
        var content = new ProbeControl(new Size(4, 2))
        {
            Margin = new Thickness(3),
        };
        var window = new Window { Content = content };
        var engine = new Engine();

        engine.Layout(window, new Size(20, 10));
        var measureCalls = content.MeasureConstraints.Count;
        var arrangeCalls = content.ArrangeBounds.Count;
        content.DesiredSize.ShouldNotBe(default);
        content.Bounds.ShouldNotBe(default);

        content.Visibility = Visibility.Collapsed;
        engine.Layout(window, new Size(20, 10));

        window.DesiredSize.ShouldBe(new Size(2, 2));
        content.DesiredSize.ShouldBe(default);
        content.Bounds.ShouldBe(default);
        content.MeasureConstraints.Count.ShouldBe(measureCalls);
        content.ArrangeBounds.Count.ShouldBe(arrangeCalls);
    }

    /// <summary>Verifies a title owns the top edge while content receives the bounded interior box.</summary>
    [Fact]
    public void Render_WhenTitleAndChildArePresent_DrawsFramedChromeAndInterior()
    {
        var child = new ProbeControl(new Size(3, 1)) { Content = "app".AsMemory() };
        var window = new Window()
        {
            Title = "Tools",
            Content = child,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var size = new Size(10, 4);
        new Engine().Layout(window, size);
        using Frame frame = new(size);

        window.Render(frame.Canvas);

        child.Bounds.ShouldBe(new Rect(1, 1, 8, 2));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("s");
        FrameOracle.Get(frame, new Point(7, 0)).ShouldBe(" ");
        FrameOracle.Get(frame, new Point(9, 0)).ShouldBe("╮");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(0, 3)).ShouldBe("╰");
    }

    /// <summary>Verifies window body and border retain semantic resource decorations.</summary>
    [Fact]
    public void Render_WhenStyleUsesModernDecorations_PreservesChromeStyle()
    {
        var style = ThemeTestSupport.OverlayStyle<Window>(
            (State.Normal, new ThemeOverlay(
                attributes: Attributes.Overline,
                underline: Underline.Paired,
                underlineColor: Color.Indexed(6))));
        var window = new Window()
        {
            Bounds = new Rect(0, 0, 4, 3),
            Background = Color.Indexed(0),
            Style = style,
        };
        using Frame frame = new(new Size(4, 3));

        window.Render(frame.Canvas);

        var rendered = frame.GetCell(default).Style;
        rendered.Attributes.ShouldBe(Attributes.Overline);
        rendered.Underline.ShouldBe(Underline.Paired);
        rendered.UnderlineColor.ShouldBe(Color.Indexed(6));
    }

    /// <summary>Verifies the Turbo Vision block shadow occupies only translated cells outside the window body.</summary>
    [Fact]
    public void Render_WhenBlockShadowIsEnabled_DrawsOutsideBodyWithoutCoveringContent()
    {
        var window = new Window()
        {
            Bounds = new Rect(0, 0, 4, 3),
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowOffset = new Point(1, 1),
        };
        using Frame frame = new(new Size(6, 5));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("╯");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 3)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
    }

    /// <summary>Verifies a long title clips inside the top edge without corrupting either frame corner.</summary>
    [Fact]
    public void Render_WhenTitleExceedsFrameWidth_PreservesTopCorners()
    {
        var window = new Window() { Title = "A deliberately long title" };
        var size = new Size(6, 2);
        new Engine().Layout(window, size);
        using Frame frame = new(size);

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("╮");
    }

    /// <summary>Verifies centered and right title placement keep the title inside both corners.</summary>
    [Theory]
    [InlineData(WindowTitlePlacement.Center, 9)]
    [InlineData(WindowTitlePlacement.Right, 16)]
    public void Render_WhenTitlePlacementChanges_AlignsTitleInsideFrame(
        WindowTitlePlacement placement,
        int expectedTitleColumn)
    {
        var window = new Window()
        {
            Bounds = new Rect(0, 0, 20, 3),
            Title = "Hi",
            TitlePlacement = placement,
        };
        using Frame frame = new(new Size(20, 3));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(expectedTitleColumn, 0)).ShouldBe("H");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(19, 0)).ShouldBe("╮");
    }

    /// <summary>Verifies unhandled Enter and Escape invoke the first available default and cancel button inside the window.</summary>
    [Fact]
    public void Dispatch_WhenEnterOrEscapeIsUnhandled_InvokesWindowDefaultOrCancelButton()
    {
        var defaults = 0;
        var cancels = 0;
        var content = new Stack();
        var accept = new Button() { IsDefault = true };
        var cancel = new Button() { IsCancel = true };
        accept.Click += (_, _) => defaults++;
        cancel.Click += (_, _) => cancels++;
        content.Children.Add(accept);
        content.Children.Add(cancel);
        var window = new Window() { Content = content };

        Router.Route(window, Events.Key, Key(Code.Enter));
        Router.Route(window, Events.Key, Key(Code.Escape));

        defaults.ShouldBe(1);
        cancels.ShouldBe(1);
    }

    /// <summary>Verifies default and cancel discovery traverses private slots on non-Container content.</summary>
    [Fact]
    public void Dispatch_WhenButtonsUseNonContainerSlots_InvokesDefaultAndCancel()
    {
        var defaults = 0;
        var cancels = 0;
        var content = new TraversalOwner();
        var branch = new TraversalOwner();
        var accept = new Button { IsDefault = true };
        var cancel = new Button { IsCancel = true };
        accept.Click += (_, _) => defaults++;
        cancel.Click += (_, _) => cancels++;
        content.AddExcluded(branch);
        branch.AddExcluded(accept);
        content.AddPopup(cancel);
        var window = new Window { Content = content };

        Router.Route(window, Events.Key, Key(Code.Enter));
        Router.Route(window, Events.Key, Key(Code.Escape));

        defaults.ShouldBe(1);
        cancels.ShouldBe(1);
    }

    /// <summary>Verifies fallback discovery skips unavailable candidates and activates only the first eligible slot member.</summary>
    [Fact]
    public void Dispatch_WhenEarlierFallbackButtonsAreUnavailable_ActivatesFirstEligibleAcrossSlots()
    {
        var content = new TraversalOwner();
        var disabled = FallbackButton();
        disabled.IsEnabled = false;
        var hidden = FallbackButton();
        hidden.Visibility = Visibility.Hidden;
        var collapsed = FallbackButton();
        collapsed.Visibility = Visibility.Collapsed;
        var firstEligible = FallbackButton();
        var laterEligible = FallbackButton();
        var invocations = new Dictionary<Button, int>
        {
            [disabled] = 0,
            [hidden] = 0,
            [collapsed] = 0,
            [firstEligible] = 0,
            [laterEligible] = 0,
        };

        foreach (var button in invocations.Keys)
        {
            button.Click += (_, _) => invocations[button]++;
        }

        content.AddNormal(disabled);
        content.AddExcluded(hidden);
        content.AddSecondary(collapsed);
        content.AddPopup(firstEligible);
        content.AddPopup(laterEligible);
        var window = new Window { Content = content };

        Router.Route(window, Events.Key, Key(Code.Enter));
        Router.Route(window, Events.Key, Key(Code.Escape));

        invocations[disabled].ShouldBe(0);
        invocations[hidden].ShouldBe(0);
        invocations[collapsed].ShouldBe(0);
        invocations[firstEligible].ShouldBe(2);
        invocations[laterEligible].ShouldBe(0);
    }

    /// <summary>Verifies handled keys and non-press strokes do not invoke Window fallbacks.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsHandledOrNotPress_IgnoresFallbackButton()
    {
        var invocations = 0;
        var button = FallbackButton();
        button.Click += (_, _) => invocations++;
        var window = new Window { Content = button };
        var handled = Key(Code.Enter);
        _ = window.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Stroke is { Code: Code.Enter, Action: KeyAction.Press })
            {
                eventArgs.Handled = true;
            }
        });

        Router.Route(window, Events.Key, handled);
        Router.Route(window, Events.Key, Key(Code.Escape, KeyAction.Release));

        handled.Handled.ShouldBeTrue();
        invocations.ShouldBe(0);
    }

    /// <summary>Verifies CanMove defaults to true and can be disabled.</summary>
    [Fact]
    public void CanMove_WhenDefaulted_IsTrue()
    {
        var window = new Window();

        window.CanMove.ShouldBeTrue();
        window.CanMove = false;
        window.CanMove.ShouldBeFalse();
    }

    private static Button FallbackButton() => new()
    {
        IsDefault = true,
        IsCancel = true,
    };

    private static KeyEventArgs Key(Code code, KeyAction action = KeyAction.Press) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        action));
}
