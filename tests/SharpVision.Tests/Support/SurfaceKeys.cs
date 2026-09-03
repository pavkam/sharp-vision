// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Emits the Kitty keyboard encodings the shared component keyboard driver does not
/// model yet - Control chords over printable characters and modified editing keys.</summary>
internal static class SurfaceKeys
{
    /// <summary>Presses one printable character with the Control modifier through the Kitty
    /// <c>CSI code;modifiers u</c> encoding, so the application sees a real Control+letter stroke
    /// rather than the legacy C0 control byte.</summary>
    /// <param name="surface">The mounted surface.</param>
    /// <param name="character">The ASCII letter to press.</param>
    public static Task ControlAsync(this ComponentSurface surface, char character) =>
        surface.SendAsync(
            Encoding.ASCII.GetBytes(FormattableString.Invariant($"\u001b[{(int) character};5u")),
            $"press Control+{character}");

    /// <summary>Presses Delete with the Control modifier (<c>CSI 3;5~</c>).</summary>
    /// <param name="surface">The mounted surface.</param>
    public static Task ControlDeleteAsync(this ComponentSurface surface) =>
        surface.SendAsync("\u001b[3;5~"u8.ToArray(), "press Control+Delete");

    /// <summary>Presses Delete with the Shift modifier (<c>CSI 3;2~</c>).</summary>
    /// <param name="surface">The mounted surface.</param>
    public static Task ShiftDeleteAsync(this ComponentSurface surface) =>
        surface.SendAsync("\u001b[3;2~"u8.ToArray(), "press Shift+Delete");

    /// <summary>Presses PageUp with the Shift modifier (<c>CSI 5;2~</c>).</summary>
    /// <param name="surface">The mounted surface.</param>
    public static Task ShiftPageUpAsync(this ComponentSurface surface) =>
        surface.SendAsync("\u001b[5;2~"u8.ToArray(), "press Shift+PageUp");

    /// <summary>Repeats one printable character through a Kitty repeat action, so the routed key
    /// carries <see cref="KeyAction.Repeat"/> rather than <see cref="KeyAction.Press"/>.</summary>
    /// <param name="surface">The mounted surface.</param>
    /// <param name="character">The ASCII character to repeat.</param>
    public static Task RepeatCharacterAsync(this ComponentSurface surface, char character) =>
        surface.SendAsync(
            Encoding.ASCII.GetBytes(FormattableString.Invariant($"\u001b[{(int) character};1:2u")),
            $"repeat {character}");

    /// <summary>Focuses one owned control directly on the application dispatcher.</summary>
    /// <param name="surface">The mounted surface.</param>
    /// <param name="control">The focusable control to focus.</param>
    public static Task FocusAsync(this ComponentSurface surface, ControlBase control) =>
        surface.UpdateAsync(
            () => surface.Application.Focus.Focus(control).ShouldBeTrue(),
            $"focus {control.GetType().Name}");

    /// <summary>Reads one dispatcher-affine value from the mounted tree.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="surface">The mounted surface.</param>
    /// <param name="read">The read to run on the dispatcher.</param>
    public static async Task<T> ReadAsync<T>(this ComponentSurface surface, Func<T> read) =>
        await surface.Application.Dispatcher.InvokeAsync(read, TestContext.Current.CancellationToken);

    /// <summary>Asserts every listed cell carries (or lacks) the reverse-video selection attribute.</summary>
    /// <param name="surface">The mounted surface.</param>
    /// <param name="y">The surface row.</param>
    /// <param name="selected">The zero-based columns expected to be reversed.</param>
    /// <param name="unselected">The zero-based columns expected to be plain.</param>
    public static void ShouldReverse(this ComponentSurface surface, int y, int[] selected, int[] unselected)
    {
        foreach (var x in selected)
        {
            (surface.Cell(new Point(x, y)).Style.Attributes & TerminalAttributes.Reverse)
                .ShouldBe(TerminalAttributes.Reverse, $"cell ({x},{y}) should be selected");
        }

        foreach (var x in unselected)
        {
            (surface.Cell(new Point(x, y)).Style.Attributes & TerminalAttributes.Reverse)
                .ShouldBe(TerminalAttributes.None, $"cell ({x},{y}) should not be selected");
        }
    }
}
