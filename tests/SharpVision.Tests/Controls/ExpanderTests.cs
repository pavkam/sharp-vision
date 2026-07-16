// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Expander validation, retained composition, layout, events, and ownership.</summary>
public sealed class ExpanderTests
{
    /// <summary>Verifies defaults and invalid header assignments preserve committed state.</summary>
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidHeader_PreserveDefaults()
    {
        var expander = new Expander();

        expander.Header.ShouldBeEmpty();
        expander.IsExpanded.ShouldBeTrue();
        expander.Content.ShouldBeNull();
        _ = expander.HeaderPart.ShouldNotBeNull();
        expander.HeaderPart.Content.ShouldBeOfType<ControlText>().Content.ShouldBe("▼");

        _ = Should.Throw<ArgumentNullException>(() => expander.Header = null!);
        _ = Should.Throw<ArgumentException>(() => expander.Header = "bad\nheader");

        expander.Header.ShouldBeEmpty();
    }

    /// <summary>Verifies collapse excludes content geometry without releasing caller ownership.</summary>
    [Fact]
    public void Layout_WhenExpansionChanges_ExcludesAndRestoresRetainedContent()
    {
        var content = new ProbeControl(new Size(4, 2));
        var expander = new Expander
        {
            Header = "Details",
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var engine = new Engine();

        engine.Layout(expander, new Size(20, 8));

        expander.DesiredSize.ShouldBe(new Size(9, 3));
        expander.HeaderPart.Bounds.ShouldBe(new Rect(0, 0, 9, 1));
        content.Bounds.ShouldBe(new Rect(0, 1, 9, 2));

        expander.IsExpanded = false;
        engine.Layout(expander, new Size(20, 8));

        expander.DesiredSize.ShouldBe(new Size(9, 1));
        expander.HeaderPart.Bounds.ShouldBe(new Rect(0, 0, 9, 1));
        content.Bounds.ShouldBe(default);
        content.Parent.ShouldBeSameAs(expander);
        expander.HeaderPart.Content.ShouldBeOfType<ControlText>().Content.ShouldBe("▶ Details");
    }

    /// <summary>Verifies changed expansion publishes committed state once and identical assignment is a no-op.</summary>
    [Fact]
    public void IsExpanded_WhenValueChanges_RaisesOnePostCommitEvent()
    {
        var expander = new Expander();
        var states = new List<bool>();
        expander.ExpandedChanged += (_, _) => states.Add(expander.IsExpanded);

        expander.IsExpanded = false;
        expander.IsExpanded = false;
        expander.IsExpanded = true;

        states.ShouldBe([false, true]);
    }

    /// <summary>Verifies replacing collapsed content releases the previous child and retains only the replacement.</summary>
    [Fact]
    public void Content_WhenReplacedWhileCollapsed_TransfersOwnershipWithoutExpanding()
    {
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var expander = new Expander
        {
            IsExpanded = false,
            Content = first,
        };

        expander.Content = second;

        expander.IsExpanded.ShouldBeFalse();
        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(expander);
        expander.Content.ShouldBeSameAs(second);
    }
}
