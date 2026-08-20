// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TreeViewChildDescription's constructor validation, property round-trips, and
/// documented defaults.</summary>
public sealed class TreeViewChildDescriptionTests
{
    /// <summary>Verifies a null key is rejected.</summary>
    [Fact]
    public void Constructor_WhenKeyIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => new TreeViewChildDescription(null!, "Header"));

    /// <summary>Verifies a null header is rejected.</summary>
    [Fact]
    public void Constructor_WhenHeaderIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => new TreeViewChildDescription("key", null!));

    /// <summary>Verifies a freshly constructed description defaults to non-checkable, no initial
    /// check state, and MayHaveChildren presence.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var description = new TreeViewChildDescription("key", "Header");

        description.Key.ShouldBe("key");
        description.Header.ShouldBe("Header");
        description.IsCheckable.ShouldBeFalse();
        description.InitialCheckState.ShouldBeNull();
        description.Presence.ShouldBe(TreeViewChildPresence.MayHaveChildren);
    }

    /// <summary>Verifies every init-only property round-trips a caller-assigned value.</summary>
    [Fact]
    public void Properties_WhenAssignedThroughObjectInitializer_RoundTrip()
    {
        var description = new TreeViewChildDescription("key", "Header")
        {
            IsCheckable = true,
            InitialCheckState = true,
            Presence = TreeViewChildPresence.Leaf,
        };

        description.IsCheckable.ShouldBeTrue();
        description.InitialCheckState.ShouldBe(true);
        description.Presence.ShouldBe(TreeViewChildPresence.Leaf);
    }
}
