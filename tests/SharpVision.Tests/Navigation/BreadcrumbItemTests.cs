// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

using Moq;

/// <summary>Verifies breadcrumb-item validation, presentation, and detached activation.</summary>
public sealed class BreadcrumbItemTests
{
    /// <summary>Verifies the item starts empty and non-current.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasDocumentedDefaults()
    {
        var item = new BreadcrumbItem();

        item.Text.ShouldBe(string.Empty);
        item.IsCurrent.ShouldBeFalse();
        item.Style.ShouldBeNull();
    }

    /// <summary>Verifies null is rejected before the caption changes.</summary>
    [Fact]
    public void Text_WhenAssignedNull_ThrowsWithoutMutation()
    {
        var item = new BreadcrumbItem { Text = "Root" };

        _ = Should.Throw<ArgumentNullException>(() => item.Text = null!);

        item.Text.ShouldBe("Root");
    }

    /// <summary>Verifies terminal controls cannot enter retained caption text.</summary>
    [Fact]
    public void Text_WhenAssignedTerminalControl_ThrowsWithoutMutation()
    {
        var item = new BreadcrumbItem { Text = "Root" };

        _ = Should.Throw<ArgumentException>(() => item.Text = "Bad\u001bText");

        item.Text.ShouldBe("Root");
    }

    /// <summary>Verifies detached programmatic activation follows event then captured-command order.</summary>
    [Fact]
    public void PerformInvoke_WhenDetached_RaisesEventBeforeCommand()
    {
        List<string> order = [];
        var command = new Mock<System.Windows.Input.ICommand>();
        _ = command.Setup(candidate => candidate.CanExecute(null)).Returns(true);
        _ = command.Setup(candidate => candidate.Execute(null)).Callback(() => order.Add("command"));
        var item = new BreadcrumbItem { Command = command.Object };
        item.Invoked += (_, _) => order.Add("event");

        item.PerformInvoke();

        order.ShouldBe(["event", "command"]);
    }

    /// <summary>Verifies attachment during the detached event retires detached command authority.</summary>
    [Fact]
    public void PerformInvoke_WhenDetachedHandlerAttachesItem_DoesNotExecuteDetachedCommand()
    {
        var command = new Mock<System.Windows.Input.ICommand>();
        _ = command.Setup(candidate => candidate.CanExecute(null)).Returns(true);
        var item = new BreadcrumbItem { Command = command.Object };
        var breadcrumb = new Breadcrumb();
        item.Invoked += (_, _) => breadcrumb.Items.Add(item);

        item.PerformInvoke();

        breadcrumb.Items.ShouldBe([item]);
        command.Verify(candidate => candidate.Execute(null), Times.Never);
    }
}
