// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using TerminalCanvas = Terminal.Rendering.Canvas;

/// <summary>Paints a small filled swatch of one semantic <see cref="ColorRole"/> from the active theme.</summary>
/// <remarks>
/// Reads the color through <see cref="Control.TryGetThemeColor"/> on every render, so it always reflects
/// the live theme context rather than a snapshot taken at construction time.
/// </remarks>
internal sealed class RoleSwatch: Control
{
    /// <summary>Initializes a fixed-footprint swatch tracking one semantic color role.</summary>
    /// <param name="role">The defined semantic color role to paint.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is unknown.</exception>
    internal RoleSwatch(ColorRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "The color role is unknown.");
        }

        Role = role;
        Width = Length.Cells(6);
        Height = Length.Cells(1);
    }

    /// <summary>Gets the semantic color role this swatch tracks.</summary>
    internal ColorRole Role { get; }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        Color color = TryGetThemeColor(Role, out Color resolved) ? resolved : Color.Default;
        canvas.Clear(Bounds, new CellStyle(background: color));
    }
}
