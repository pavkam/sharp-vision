// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Terminal.Capabilities;

using GraphicsImage = Terminal.Graphics.ImageSource;
using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents backend-neutral image fitting and deterministic terminal-cell fallback.</summary>
internal sealed class ImagePane: CompositeControlBase
{
    /// <summary>Initializes the retained responsive image reference page.</summary>
    internal ImagePane()
    {
        ContainSample = CreateSample(CreateRgba(), "RGBA contain", ImageStretch.Contain);
        CoverSample = CreateSample(CreatePng(), "PNG cover", ImageStretch.Cover);
        StretchSample = CreateSample(CreateRgba(), "RGBA stretch", ImageStretch.Stretch);
        ForcedFallback = CreateSample(CreateRgba(), "Fallback: terminal cells", ImageStretch.Contain);
        OverlapBadge = new Text(" TOP ")
        {
            Width = Length.Cells(5),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        FallbackOccluder = new Text(" CELL FALLBACK ")
        {
            Width = Length.Cells(15),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        Samples = [ContainSample, CoverSample, StretchSample, ForcedFallback];
        SampleGrid = CreateGrid();
        Status = new Text { Overflow = Overflow.Wrap };
        UpdateStatus(Capabilities);
        InitializeContent(CreateContent());
    }

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Image";

    /// <summary>Gets the responsive two-column sample grid.</summary>
    internal Grid SampleGrid { get; }

    /// <summary>Gets all retained specimens in reading order.</summary>
    internal IReadOnlyList<Image> Samples { get; }

    /// <summary>Gets the RGBA contain specimen.</summary>
    internal Image ContainSample { get; }

    /// <summary>Gets the PNG cover specimen.</summary>
    internal Image CoverSample { get; }

    /// <summary>Gets the RGBA stretch specimen.</summary>
    internal Image StretchSample { get; }

    /// <summary>Gets the source-backed specimen deliberately occluded by later semantic cells.</summary>
    internal Image ForcedFallback { get; }

    /// <summary>Gets the semantic cell badge painted after and above the contain specimen.</summary>
    internal Text OverlapBadge { get; }

    /// <summary>Gets the later semantic cell surface that suppresses the fallback specimen's placement.</summary>
    internal Text FallbackOccluder { get; }

    /// <summary>Gets the capability and fallback evidence readout.</summary>
    internal Text Status { get; }

    /// <inheritdoc/>
    protected override void OnCapabilitiesChanged(
        TerminalCapabilities previous,
        TerminalCapabilities current)
    {
        base.OnCapabilitiesChanged(previous, current);
        UpdateStatus(current);
    }

    private DocPage CreateContent() => new DocPage(
        Title,
        "<info>Image</info> displays a borrowed immutable source through negotiated terminal graphics while preserving readable cell output everywhere.",
        new DocSection(
            "🖼️",
            "Fitting and fallback",
            "Contain, cover, and stretch share the same protocol-neutral control. Switch to another pane to verify that retained raster content leaves with this page; the final card forces the unsupported-terminal path without changing application code.",
            new DocExample(
                "Responsive image grid",
                "Resize the page to compare fitting. Exact cell metrics guide intrinsic size; the semantic shade and alternate text remain the portable evidence layer.",
                new DocColumn(Status, SampleGrid),
                "var image = new Image\n{\n    Source = source,\n    AlternateText = \"Preview\",\n    Stretch = ImageStretch.Contain,\n};")));

    private Grid CreateGrid()
    {
        var grid = new Grid { ColumnSpacing = 1, RowSpacing = 1 };
        grid.Columns.Add(Track.Star(1, minimum: Length.Cells(8)));
        grid.Columns.Add(Track.Star(1, minimum: Length.Cells(8)));
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Auto());

        var overlap = new Overlay { Height = Length.Cells(3), Children = { ContainSample, OverlapBadge } };
        AddCard(grid, "Contain + cell badge", overlap, 0, 0);
        AddCard(grid, "Cover", CoverSample, 0, 1);
        AddCard(grid, "Stretch", StretchSample, 1, 0);
        var fallback = new Overlay
        {
            Height = Length.Cells(3),
            Children = { ForcedFallback, FallbackOccluder }
        };
        AddCard(grid, "Forced fallback", fallback, 1, 1);
        return grid;
    }

    private void UpdateStatus(TerminalCapabilities capabilities)
    {
        Status.Content =
            $"Inherited evidence — Kitty {Evidence(capabilities.KittyGraphics)}; " +
            $"sixel {Evidence(capabilities.Sixel)}; iTerm2 {Evidence(capabilities.ItermImages)}. " +
            "Backend selection remains host-owned; every sample keeps its cell fallback.";
    }

    private static string Evidence(Feature feature) => $"{feature.State} ({feature.Origin})";

    private static void AddCard(Grid grid, string label, ControlBase sample, int row, int column)
    {
        var card = new DocCard(new DocColumn(new Text(label), sample));
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private static Image CreateSample(
        GraphicsImage? source,
        string alternateText,
        ImageStretch stretch) => new()
        {
            Source = source,
            AlternateText = alternateText,
            Stretch = stretch,
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

    private static GraphicsImage CreateRgba()
    {
        var size = new Size(6, 4);
        var bytes = new byte[checked(size.Width * size.Height * 4)];

        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var offset = ((y * size.Width) + x) * 4;
                bytes[offset] = (byte) (40 + (x * 32));
                bytes[offset + 1] = (byte) (70 + (y * 38));
                bytes[offset + 2] = (byte) (210 - (x * 18));
                bytes[offset + 3] = 255;
            }
        }

        return GraphicsImage.FromRgba(size, bytes);
    }

    private static GraphicsImage CreatePng() => GraphicsImage.FromPng(Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEUlEQVR4nGP4z8DA8B+MgBgAHfAD/dPQfSYAAAAASUVORK5CYII="));
}
