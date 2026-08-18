// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.FigletFontsConsumer;

using SharpVision.Fonts;

/// <summary>Loads and renders an optional bundled font through only NuGet dependencies.</summary>
internal static class Program
{
    private static void Main()
    {
        var catalog = FigletCatalog.Default;
        var rendered = catalog.Load("Classy").Render("A");

        if (catalog.Names.Count != 19 || string.IsNullOrWhiteSpace(rendered))
        {
            throw new InvalidOperationException("The packed optional font catalog did not render correctly.");
        }
    }
}
