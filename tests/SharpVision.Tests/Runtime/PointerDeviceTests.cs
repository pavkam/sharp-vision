// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;


/// <summary>Verifies PointerDevice correctly snapshots pointer state.</summary>
public sealed class PointerDeviceTests
{
    /// <summary>Verifies Observe updates position and buttons on move action.</summary>
    [Fact]
    public void Observe_WhenMove_UpdatesPositionAndButtons()
    {
        var device = new PointerDevice(() => null);
        var pointer = new Pointer(
            cells: new Point(4, 2),
            pixels: null,
            buttons: Buttons.Primary,
            action: PointerAction.Move,
            wheelX: 0,
            wheelY: 0,
            modifiers: Modifiers.Shift,
            isMotion: true,
            isCellPositionInferred: false);

        device.Observe(pointer);

        device.Position.ShouldBe(new Point(4, 2));
        device.Buttons.ShouldBe(Buttons.Primary);
        device.Modifiers.ShouldBe(Modifiers.Shift);
        device.LastAction.ShouldBe(PointerAction.Move);
    }

    /// <summary>Verifies Observe clears position on leave action.</summary>
    [Fact]
    public void Observe_WhenLeave_ClearsPosition()
    {
        var device = new PointerDevice(() => null);
        device.Observe(new Pointer(new Point(1, 1), null, Buttons.None, PointerAction.Move, 0, 0, Modifiers.None, true, false));

        device.Observe(new Pointer(null, null, Buttons.None, PointerAction.Leave, 0, 0, Modifiers.None, false, false));

        device.Position.ShouldBeNull();
    }
}
