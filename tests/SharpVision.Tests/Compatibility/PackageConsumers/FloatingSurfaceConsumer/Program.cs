// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.FloatingSurfaceConsumer;

/// <summary>Runs the derived surface through a public attached application host.</summary>
internal static class Program
{
    private static async Task Main()
    {
        var surface = new ConsumerSurface
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Style = new ConsumerSurfaceStyle(
                new Face(Color.Default, Color.Transparent, TerminalAttributes.None, Underline.None, Color.Default),
                new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Light,
                    Color.Default,
                    Color.Transparent,
                    TerminalAttributes.None),
                default)
        };
        var styledControl = new ConsumerControl { Style = surface.Style };
        var root = new Overlay { Children = { surface, styledControl } };
        var terminal = new ConsumerTerminal();
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync();
        await application.Dispatcher.InvokeAsync(() =>
        {
            surface.Present();

            if (!surface.IsPresented)
            {
                throw new InvalidOperationException("The external surface did not commit its presented state.");
            }

            if (surface.ActualBorder.Sides != BorderSide.All || surface.ActualShadow.Visible)
            {
                throw new InvalidOperationException("The external surface did not apply its validated style.");
            }

            if (styledControl.ActualStyle != surface.ActualStyle || styledControl.ActualBorder.Sides != BorderSide.All)
            {
                throw new InvalidOperationException("The generic external control did not expose or apply its typed style.");
            }

            if (!surface.Dismiss() || surface.IsPresented)
            {
                throw new InvalidOperationException("The external surface did not complete its protected close transaction.");
            }
        });
    }
}
