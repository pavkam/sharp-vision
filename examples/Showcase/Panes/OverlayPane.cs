// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Overlay layering, absolute positioning, clipping, and input order.</summary>
internal sealed class OverlayPane: CompositeControlBase
{
    internal OverlayPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Overlay";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var fixedStage = Stage();
        var fixedCard = Card("fixed 2,1", BorderGlyphStyle.Light);
        ShowcasePaneHelpers.Place(fixedCard, 2, 1);
        fixedStage.Children.Add(fixedCard);

        var percentStage = Stage();
        var percentCard = Card("50%,50%", BorderGlyphStyle.Heavy);
        Overlay.SetLeft(percentCard, Length.Percent(50));
        Overlay.SetTop(percentCard, Length.Percent(50));
        percentStage.Children.Add(percentCard);

        var edgeStage = Stage();
        var edgeCard = Card("Right 2 / Bottom 1", BorderGlyphStyle.Paired);
        Overlay.SetRight(edgeCard, Length.Cells(2));
        Overlay.SetBottom(edgeCard, Length.Cells(1));
        edgeStage.Children.Add(edgeCard);
        var widthCard = Card("40% wide", BorderGlyphStyle.Rounded);
        widthCard.Width = Length.Percent(40);
        ShowcasePaneHelpers.Place(widthCard, 1, 1);
        edgeStage.Children.Add(widthCard);

        var constraintStage = Stage();
        var stretched = Card("Stretched", BorderGlyphStyle.Rounded);
        Overlay.SetLeft(stretched, Length.Cells(2));
        Overlay.SetRight(stretched, Length.Cells(2));
        Overlay.SetTop(stretched, Length.Cells(1));
        Overlay.SetBottom(stretched, Length.Cells(1));
        constraintStage.Children.Add(stretched);

        var explicitStage = Stage();
        var explicitSize = Card("12 cells", BorderGlyphStyle.Heavy);
        explicitSize.Width = Length.Cells(12);
        Overlay.SetLeft(explicitSize, Length.Cells(2));
        Overlay.SetRight(explicitSize, Length.Cells(2));
        Overlay.SetTop(explicitSize, Length.Cells(2));
        explicitStage.Children.Add(explicitSize);

        var intrinsicStage = new Overlay { ClipToBounds = true };
        var intrinsicLeft = Card("Union starts here", BorderGlyphStyle.Light);
        ShowcasePaneHelpers.Place(intrinsicLeft, 1, 1);
        intrinsicStage.Children.Add(intrinsicLeft);
        var intrinsicRight = Card("and ends here", BorderGlyphStyle.Paired);
        ShowcasePaneHelpers.Place(intrinsicRight, 22, 3);
        intrinsicStage.Children.Add(intrinsicRight);

        var negativeStage = ShowcasePaneHelpers.OverlayStage(24, 6);
        var oversized = Card("Larger than host", BorderGlyphStyle.Ascii);
        oversized.Width = Length.Cells(30);
        Overlay.SetRight(oversized, Length.Cells(2));
        Overlay.SetTop(oversized, Length.Cells(1));
        negativeStage.Children.Add(oversized);

        var layerStage = Stage();
        var positionedBack = Card("Back", BorderGlyphStyle.Light);
        ShowcasePaneHelpers.Place(positionedBack, 2, 1);
        layerStage.Children.Add(positionedBack);
        var positionedFront = Card("Front", BorderGlyphStyle.Heavy);
        ShowcasePaneHelpers.Place(positionedFront, 6, 2);
        layerStage.Children.Add(positionedFront);
        var positionedClipped = Card("clipped", BorderGlyphStyle.Ascii);
        ShowcasePaneHelpers.Place(positionedClipped, 29, 5);
        layerStage.Children.Add(positionedClipped);

        Overlay zOrder = new() { Width = Length.Cells(32), Height = Length.Cells(7), ClipToBounds = true };
        Text back = new("Background layer") { Padding = new Thickness(1) };
        Overlay.SetZIndex(back, -1);
        zOrder.Children.Add(back);

        Dock middle = new()
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Heavy,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Padding = new Thickness(1, 0),
            Margin = new Thickness(4, 2, 4, 2),
            Children = { new Text("Middle layer") }
        };
        zOrder.Children.Add(middle);

        Text front = new("Front layer")
        {
            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        Overlay.SetZIndex(front, 10);
        zOrder.Children.Add(front);

        Overlay alignment = new() { Width = Length.Cells(32), Height = Length.Cells(7), ClipToBounds = true };
        alignment.Children.Add(new Text("Top-left")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        });
        alignment.Children.Add(new Text("Top-right")
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        });
        alignment.Children.Add(new Text("Center")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        alignment.Children.Add(new Text("Bottom-left")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom
        });
        alignment.Children.Add(new Text("Bottom-right")
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        });

        // ClipToBounds affects hit testing — a pointer press outside the clipped
        // Overlay returns null; the unclipped Overlay still reports the child.
        // Each child's explicit Width exceeds the box, mirroring the "Negative origin and
        // clipping" specimen above, so the clipped Overlay actually clips and the unclipped one
        // actually overflows instead of rendering identically.
        var clippedStatus = new Text("Hit: —");
        var clippedChild = new Button { Text = "Overflowing button", UseMnemonic = false, Width = Length.Cells(24) };
        clippedChild.Click += (_, _) => clippedStatus.Content = "Hit: inside clipped";
        Overlay clipped = new()
        {
            Width = Length.Cells(18),
            Height = Length.Cells(3),
            ClipToBounds = true,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
        };
        Overlay.SetLeft(clippedChild, Length.Cells(2));
        Overlay.SetTop(clippedChild, Length.Cells(0));
        clipped.Children.Add(clippedChild);

        var unclippedChild = new Button { Text = "Overflowing button", UseMnemonic = false, Width = Length.Cells(24) };
        unclippedChild.Click += (_, _) => clippedStatus.Content = "Hit: inside unclipped";
        Overlay unclipped = new()
        {
            Width = Length.Cells(18),
            Height = Length.Cells(3),
            ClipToBounds = false,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
        };
        Overlay.SetLeft(unclippedChild, Length.Cells(2));
        Overlay.SetTop(unclippedChild, Length.Cells(0));
        unclipped.Children.Add(unclippedChild);

        var equalTies = new Overlay { Width = Length.Cells(30), Height = Length.Cells(4) };
        var tieFirst = new Text("First at z=5") { HorizontalAlignment = HorizontalAlignment.Left };
        var tieSecond = new Text("Second at z=5") { HorizontalAlignment = HorizontalAlignment.Right };
        Overlay.SetZIndex(tieFirst, 5);
        Overlay.SetZIndex(tieSecond, 5);
        equalTies.Children.Add(tieFirst);
        equalTies.Children.Add(tieSecond);

        var pointerStatus = new Text("Pointer: waiting");
        var underlying = new Button { Text = "&Underlying action" };
        underlying.Click += (_, eventArgs) => pointerStatus.Content = $"Pointer: action received ({eventArgs.Cause})";
        ShowcasePaneHelpers.Place(underlying, 2, 1);
        var decoration = new Text("Decorative overlay")
        {
            HitTestVisible = false,
            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default)
        };
        ShowcasePaneHelpers.Place(decoration, 3, 2);
        var transparent = Stage();
        transparent.Children.Add(underlying);
        Overlay.SetZIndex(decoration, 10);
        transparent.Children.Add(decoration);

        var percent = new Overlay { Width = Length.Cells(32), Height = Length.Cells(6) };
        percent.Children.Add(new Dock
        {
            Width = Length.Percent(60),
            Height = Length.Percent(50),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Children = { new Text("60% × 50% centered") }
        });

        var notification = new Overlay { Width = Length.Cells(36), Height = Length.Cells(6) };
        notification.Children.Add(new Text("Editor content remains first in focus order."));
        var banner = new DocCard(new Text("Saved successfully"))
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        Overlay.SetZIndex(banner, 20);
        notification.Children.Add(banner);

        return new DocPage(
            Title,
            "<info>Overlay</info> owns overlapping controls, optional absolute offsets, and stable <info>ZIndex</info> order for rendering and hit testing.",
            new DocSection(
                "📐",
                "Absolute placement",
                "Left, Top, Right, and Bottom accept cell or percentage offsets while unpositioned children continue to share the complete content box.",
                new DocExample(
                    "Fixed placement",
                    "Cell offsets keep the child two cells from the left and one from the top as the host changes size.",
                    Frame(fixedStage),
                    "var overlay = new Overlay();\nOverlay.SetLeft(card, Length.Cells(2));\nOverlay.SetTop(card, Length.Cells(1));\noverlay.Children.Add(card);"),
                new DocExample(
                    "Percentage placement",
                    "Percentage offsets resolve against the committed Overlay content box.",
                    Frame(percentStage)),
                new DocExample(
                    "Trailing-edge placement",
                    "Right and Bottom anchor to trailing edges while a sibling resolves percentage width against the same host.",
                    Frame(edgeStage))),
            new DocSection(
                "📐",
                "Position constraints",
                "Opposing offsets stretch an automatic child; an explicit extent keeps its size and gives Left or Top origin precedence.",
                new DocExample(
                    "Automatic opposing-edge stretch",
                    "Left 2, Right 2, Top 1, and Bottom 1 define the complete automatic child slot.",
                    Frame(constraintStage),
                    "Overlay.SetLeft(child, Length.Cells(2));\nOverlay.SetRight(child, Length.Cells(2));"),
                new DocExample(
                    "Explicit extent precedence",
                    "This twelve-cell child keeps its width even though both horizontal edges are supplied.",
                    Frame(explicitStage)),
                new DocExample(
                    "Intrinsic positioned union",
                    "With no explicit Overlay size, fixed positioned children contribute to one finite desired-size union.",
                    Frame(intrinsicStage)),
                new DocExample(
                    "Negative origin and clipping",
                    "An oversized right-anchored child may begin before the host and is clipped safely.",
                    Frame(negativeStage))),
            new DocSection(
                "🧩",
                "Layering",
                "All children share one content box while attached z-order controls paint and pointer priority.",
                new DocExample(
                    "Negative, default, and high z",
                    "Background paints at -1, the framed middle at 0, and the front label at 10.",
                    zOrder,
                    "var overlay = new Overlay();\nOverlay.SetZIndex(status, 10);\noverlay.Children.Add(status);"),
                new DocExample(
                    "Positioned overlap and clipping",
                    "Fixed offsets overlap two cards while a third deliberately crosses the clipped right edge.",
                    Frame(layerStage))),
            new DocSection(
                "🧩",
                "Stable ties",
                "Equal z-index values preserve collection order so rendering remains deterministic.",
                new DocExample(
                    "Two children at z=5",
                    "First remains before Second until their collection order or z-index changes.",
                    equalTies)),
            new DocSection(
                "🧩",
                "Pointer transparency",
                "Decorative layers may render above interactive content without becoming pointer targets.",
                new DocExample(
                    "Input passes through decoration",
                    "Click the visible overlap. Decorative overlay is not hit-test-visible, so the underlying Button receives activation.",
                    new DocColumn(transparent, pointerStatus),
                    "decoration.HitTestVisible = false;")),
            new DocSection(
                "🧩",
                "Alignment and sizing",
                "Each child independently resolves length and alignment against the shared box.",
                new DocExample(
                    "Five alignments",
                    "The labels occupy four corners and center without absolute coordinates.",
                    alignment),
                new DocExample(
                    "Percentage card",
                    "The centered card resolves sixty percent width and half height whenever the host resizes.",
                    percent)),
            new DocSection(
                "🧩",
                "Clipping",
                "<info>ClipToBounds</info> controls hit testing beyond the Overlay boundary. When true, pointer input outside the bounds is ignored. When false, children remain interactive past the visual edge.",
                new DocExample(
                    "Clipped and unclipped",
                    "Both Overlays host a button wider than the box, so it visibly overflows the right edge. Click the visible portion of each to confirm hit testing. <info>ClipToBounds</info> also affects rendering of visual overflow like shadows.",
                    new DocColumn(
                        new DocRow(
                            new DocColumn(new Text("Clipped"), clipped),
                            new DocColumn(new Text("Unclipped"), unclipped)),
                        clippedStatus))),
            new DocSection(
                "🧩",
                "Notification composition",
                "High visual priority does not rewrite collection-based focus traversal.",
                new DocExample(
                    "Non-modal saved banner",
                    "The banner paints above editor content while ordinary ownership and focus order remain unchanged.",
                    notification)));
    }

    private static Overlay Stage() => ShowcasePaneHelpers.OverlayStage(36, 7);

    private static Dock Frame(ControlBase child) => ShowcasePaneHelpers.Frame(child);

    private static Dock Card(string content, BorderGlyphStyle glyphs) =>
        ShowcasePaneHelpers.Card(content, glyphs, new Thickness(1, 0));
}
