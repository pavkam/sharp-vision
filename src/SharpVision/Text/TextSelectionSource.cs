// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Captures one embedded selectable source's identity and semantic contribution.</summary>
[PublicAPI]
public sealed class TextSelectionSource
{
    /// <summary>Initializes one source captured during a document layout.</summary>
    /// <param name="source">The live source whose snapshot supplied the text.</param>
    /// <param name="viewport">The same source's optional selectable viewport.</param>
    /// <param name="range">The source snapshot's range in the complete document stream.</param>
    /// <param name="text">The independently owned snapshot text used by the projection.</param>
    /// <param name="invalidationVersion">The source's invalidation generation after snapshot capture.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="text"/> is null.
    /// </exception>
    public TextSelectionSource(
        ISelectableTextSource source,
        ISelectableTextViewport? viewport,
        Selection range,
        string text,
        ulong invalidationVersion)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);

        Source = source;
        Viewport = viewport;
        Range = range;
        Text = text;
        InvalidationVersion = invalidationVersion;
    }

    /// <summary>Gets the live selectable source identity.</summary>
    public ISelectableTextSource Source { get; }

    /// <summary>Gets the source's optional selection-driven viewport.</summary>
    public ISelectableTextViewport? Viewport { get; }

    /// <summary>Gets the source's UTF-16 range in the complete document stream.</summary>
    public Selection Range { get; }

    /// <summary>Gets the captured semantic text used to detect source mutation.</summary>
    public string Text { get; }

    /// <summary>Gets the source invalidation generation matching the captured semantic snapshot.</summary>
    public ulong InvalidationVersion { get; private set; }

    /// <summary>Commits a newer generation after an exact text-equivalent geometry refresh.</summary>
    /// <param name="value">The source generation observed after snapshot capture.</param>
    public void UpdateInvalidationVersion(ulong value) => InvalidationVersion = value;
}
