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
                new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Light,
                    Color.Default,
                    Color.Transparent,
                    TerminalAttributes.None),
                default)
        };
        var root = new Overlay { Children = { surface } };
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

            if (surface.ActualBorder.Sides != BorderSide.All || surface.ActualShadow.IsVisible)
            {
                throw new InvalidOperationException("The external surface did not apply its validated style.");
            }

            if (!surface.Dismiss() || surface.IsPresented)
            {
                throw new InvalidOperationException("The external surface did not complete its protected close transaction.");
            }
        });
    }
}
