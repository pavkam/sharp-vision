// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using GraphicsImage = Terminal.Graphics.ImageSource;
using PlacementMode = Terminal.Graphics.PlacementMode;

/// <summary>Verifies Image ownership, validation, intrinsic geometry, fallback, and semantic placement.</summary>
public sealed class ImageTests
{
    /// <summary>Verifies stable borrowed-source, alternate-text, and fitting defaults.</summary>
    [ComponentUnitEvidence(typeof(Image))]
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var image = new Image();

        image.Source.ShouldBeNull();
        image.AlternateText.ShouldBe(string.Empty);
        image.Stretch.ShouldBe(ImageStretch.Contain);
        image.Focusable.ShouldBeFalse();
    }

    /// <summary>Verifies invalid text and stretch values preserve state and publish nothing.</summary>
    [Fact]
    public void Properties_WhenValueIsInvalid_RejectBeforeMutationOrNotification()
    {
        var image = new Image { AlternateText = "preview", Stretch = ImageStretch.Cover };
        var notifications = new List<string?>();
        image.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        _ = Should.Throw<ArgumentNullException>(() => image.AlternateText = null!);
        _ = Should.Throw<ArgumentException>(() => image.AlternateText = "line\nbreak");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => image.Stretch = (ImageStretch) int.MaxValue);

        image.AlternateText.ShouldBe("preview");
        image.Stretch.ShouldBe(ImageStretch.Cover);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies source, alternate text, and stretch invalidate only their required phases.</summary>
    [Fact]
    public void Properties_WhenChanged_InvalidateMeasureOrRenderPrecisely()
    {
        var image = new Image();
        var engine = new LayoutEngine();
        engine.Layout(image, new Size(8, 3));
        using (var frame = new Frame(new Size(8, 3)))
        {
            image.Render(frame.Canvas);
        }

        image.Stretch = ImageStretch.Stretch;
        image.Pending.ShouldBe(Invalidation.Render);
        using (var frame = new Frame(new Size(8, 3)))
        {
            image.Render(frame.Canvas);
        }

        image.AlternateText = "alt";
        image.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
        engine.Layout(image, new Size(8, 3));
        using (var frame = new Frame(new Size(8, 3)))
        {
            image.Render(frame.Canvas);
        }

        image.Source = Rgba(new Size(2, 2));
        image.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
        image.Source = null;
        image.Source.ShouldBeNull();
    }

    /// <summary>Verifies exact metrics determine intrinsic size and metric changes request remeasure.</summary>
    [Fact]
    public void Measure_WhenMetricsChange_UsesExactUnevenPixelGrid()
    {
        var image = new Image
        {
            Source = Rgba(new Size(7, 5)),
            AlternateText = "preview"
        };
        var engine = new LayoutEngine();

        engine.Layout(image, new Size(20, 10));
        image.DesiredSize.ShouldBe(new Size(7, 1));
        image.SetCellMetrics(new CellMetrics(new Size(3, 2), new Size(10, 5)));
        image.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
        engine.Layout(image, new Size(20, 10));

        image.DesiredSize.ShouldBe(new Size(2, 2));
        image.SetCellMetrics(new CellMetrics(2, 5));
        engine.Layout(image, new Size(20, 10));
        image.DesiredSize.ShouldBe(new Size(4, 1));
    }

    /// <summary>Verifies absent geometry falls back to alternate text or one deterministic preview cell.</summary>
    [Fact]
    public void Measure_WhenSourceGeometryIsUnavailable_UsesAlternateTextOrPreviewCell()
    {
        var image = new Image { AlternateText = "界x" };
        var engine = new LayoutEngine();

        engine.Layout(image, new Size(10, 2));
        image.DesiredSize.ShouldBe(new Size(3, 1));
        image.Source = Rgba(new Size(1, 1));
        image.AlternateText = string.Empty;
        engine.Layout(image, new Size(10, 2));

        image.DesiredSize.ShouldBe(new Size(1, 1));
    }

    /// <summary>Verifies an empty source and alternate text form a zero-sized byte-quiet display.</summary>
    [Fact]
    public void Render_WhenSourceAndAlternateTextAreEmpty_DesiresNothingAndPaintsNothing()
    {
        var image = new Image();
        new LayoutEngine().Layout(image, new Size(2, 1));
        using var frame = new Frame(new Size(2, 1));

        image.Render(frame.Canvas);

        image.DesiredSize.ShouldBe(default);
        frame.PlacementCount.ShouldBe(0);
        FrameOracle.Get(frame, default).ShouldBe(string.Empty);
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe(string.Empty);
    }

    /// <summary>Verifies complete fallback paint precedes placement and later cell paint occludes it.</summary>
    [Fact]
    public void Render_WhenSourceExists_PaintsFullFallbackThenRecordsPlacement()
    {
        var image = new Image
        {
            Source = Rgba(new Size(2, 2)),
            AlternateText = "ALT",
            Stretch = ImageStretch.Cover,
            Width = Length.Cells(4),
            Height = Length.Cells(2)
        };
        new LayoutEngine().Layout(image, new Size(4, 2));
        using var frame = new Frame(new Size(4, 2));

        image.Render(frame.Canvas);

        Row(frame, 0).ShouldBe("ALT░");
        Row(frame, 1).ShouldBe("░░░░");
        frame.PlacementCount.ShouldBe(1);
        frame.GetPlacement(0).Image.ShouldBeSameAs(image.Source);
        frame.GetPlacement(0).Destination.ShouldBe(image.ContentBounds);
        frame.GetPlacement(0).Mode.ShouldBe(PlacementMode.Cover);
        frame.IsPlacementEffective(0).ShouldBeTrue();

        _ = frame.Canvas.Draw("X", new Point(1, 0));

        frame.IsPlacementEffective(0).ShouldBeFalse();
    }

    /// <summary>Verifies a later image placement remains eligible above earlier ordinary cells.</summary>
    [Fact]
    public void Render_WhenCellsAlreadyExist_RecordsImageAfterItsCompleteUnderlay()
    {
        var image = new Image
        {
            Source = Rgba(new Size(1, 1)),
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        new LayoutEngine().Layout(image, new Size(2, 1));
        using var frame = new Frame(new Size(2, 1));
        _ = frame.Canvas.Draw("XX", default);

        image.Render(frame.Canvas);

        frame.IsPlacementEffective(0).ShouldBeTrue();
        Row(frame, 0).ShouldBe("░░");
    }

    /// <summary>Verifies a later ordinary window invalidates the overlapping image placement.</summary>
    [Fact]
    public void Render_WhenWindowOverlapsImage_WindowOccludesPlacement()
    {
        var image = new Image { Source = Rgba(new Size(1, 1)) };
        var window = new Window
        {
            Width = Length.Cells(4),
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Header = "Top"
        };
        var root = new Overlay { Children = { image, window } };
        var size = new Size(8, 4);
        new LayoutEngine().Layout(root, size);
        using var frame = new Frame(size);

        root.Render(frame.Canvas);

        frame.PlacementCount.ShouldBe(1);
        frame.IsPlacementEffective(0).ShouldBeFalse();
        FrameOracle.Get(frame, default).ShouldBe("╔");
    }

    /// <summary>Verifies an elevated popup invalidates an earlier image despite later ordinary siblings.</summary>
    [Fact]
    public void Render_WhenPopupOverlapsImage_PopupOccludesPlacement()
    {
        var image = new Image { Source = Rgba(new Size(1, 1)) };
        var anchor = new ControlText("A")
        {
            Width = Length.Cells(1),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("POP"),
            IsOpen = true
        };
        var cover = new Dock();
        var root = new Overlay { Children = { image, anchor, popup, cover } };
        var size = new Size(8, 4);
        new LayoutEngine().Layout(root, size);
        using var frame = new Frame(size);

        root.Render(frame.Canvas);

        frame.PlacementCount.ShouldBe(1);
        frame.IsPlacementEffective(0).ShouldBeFalse();
        FrameOracle.Get(frame, new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Y)).ShouldBe("╭");
    }

    /// <summary>Verifies source replacement and control disposal never dispose or mutate borrowed images.</summary>
    [Fact]
    public void Source_WhenReplacedOrControlDisposed_RemainsCallerOwned()
    {
        var first = Rgba(new Size(1, 1));
        var second = Rgba(new Size(1, 1));
        var image = new Image { Source = first };

        image.Source = second;
        image.Dispose();
        var firstBytes = new byte[first.ByteCount];
        var secondBytes = new byte[second.ByteCount];

        first.CopyTo(firstBytes).ShouldBe(first.ByteCount);
        second.CopyTo(secondBytes).ShouldBe(second.ByteCount);
    }

    /// <summary>Verifies attached setters enforce dispatcher affinity.</summary>
    [Fact]
    public async Task Properties_WhenAttachedOffDispatcher_ThrowInvalidOperationExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var image = new Image();
        await dispatcher.InvokeAsync(() => image.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => image.Source = Rgba(new Size(1, 1)));
        _ = Should.Throw<InvalidOperationException>(() => image.AlternateText = "alt");
        _ = Should.Throw<InvalidOperationException>(() => image.Stretch = ImageStretch.Stretch);

        await dispatcher.InvokeAsync(image.Dispose, TestContext.Current.CancellationToken);
    }

    private static GraphicsImage Rgba(Size size) => GraphicsImage.FromRgba(
        size,
        new byte[checked(size.Width * size.Height * 4)]);

    private static string Row(Frame frame, int row)
    {
        var value = new StringBuilder();

        for (var column = 0; column < frame.Size.Width; column++)
        {
            _ = value.Append(FrameOracle.Get(frame, new Point(column, row)));
        }

        return value.ToString();
    }
}
