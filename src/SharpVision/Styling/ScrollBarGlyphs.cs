// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines scrollbar buttons, tracks, and thumbs for every orientation and fill mode.</summary>
public readonly record struct ScrollBarGlyphs
{
    /// <summary>Initializes the complete scrollbar glyph family.</summary>
    /// <param name="verticalDecrement">The vertical decrement button.</param>
    /// <param name="verticalIncrement">The vertical increment button.</param>
    /// <param name="horizontalDecrement">The horizontal decrement button.</param>
    /// <param name="horizontalIncrement">The horizontal increment button.</param>
    /// <param name="blockTrack">The block-fill track.</param>
    /// <param name="blockThumb">The block-fill thumb.</param>
    /// <param name="horizontalLineTrack">The horizontal line track.</param>
    /// <param name="horizontalLineThumb">The horizontal line thumb.</param>
    /// <param name="verticalLineTrack">The vertical line track.</param>
    /// <param name="verticalLineThumb">The vertical line thumb.</param>
    public ScrollBarGlyphs(
        ThemedGlyph verticalDecrement,
        ThemedGlyph verticalIncrement,
        ThemedGlyph horizontalDecrement,
        ThemedGlyph horizontalIncrement,
        ThemedGlyph blockTrack,
        ThemedGlyph blockThumb,
        ThemedGlyph horizontalLineTrack,
        ThemedGlyph horizontalLineThumb,
        ThemedGlyph verticalLineTrack,
        ThemedGlyph verticalLineThumb)
    {
        VerticalDecrement = verticalDecrement;
        VerticalIncrement = verticalIncrement;
        HorizontalDecrement = horizontalDecrement;
        HorizontalIncrement = horizontalIncrement;
        BlockTrack = blockTrack;
        BlockThumb = blockThumb;
        HorizontalLineTrack = horizontalLineTrack;
        HorizontalLineThumb = horizontalLineThumb;
        VerticalLineTrack = verticalLineTrack;
        VerticalLineThumb = verticalLineThumb;
    }

    /// <summary>Gets the vertical decrement button.</summary>
    public ThemedGlyph VerticalDecrement { get; }
    /// <summary>Gets the vertical increment button.</summary>
    public ThemedGlyph VerticalIncrement { get; }
    /// <summary>Gets the horizontal decrement button.</summary>
    public ThemedGlyph HorizontalDecrement { get; }
    /// <summary>Gets the horizontal increment button.</summary>
    public ThemedGlyph HorizontalIncrement { get; }
    /// <summary>Gets the block-fill track.</summary>
    public ThemedGlyph BlockTrack { get; }
    /// <summary>Gets the block-fill thumb.</summary>
    public ThemedGlyph BlockThumb { get; }
    /// <summary>Gets the horizontal line track.</summary>
    public ThemedGlyph HorizontalLineTrack { get; }
    /// <summary>Gets the horizontal line thumb.</summary>
    public ThemedGlyph HorizontalLineThumb { get; }
    /// <summary>Gets the vertical line track.</summary>
    public ThemedGlyph VerticalLineTrack { get; }
    /// <summary>Gets the vertical line thumb.</summary>
    public ThemedGlyph VerticalLineThumb { get; }
}
