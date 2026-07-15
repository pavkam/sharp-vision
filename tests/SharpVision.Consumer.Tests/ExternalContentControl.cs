// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

/// <summary>Provides an externally authored single-content component using only the public role API.</summary>
public sealed class ExternalContentControl: ContentControl
{
    /// <summary>Initializes an empty external content control.</summary>
    public ExternalContentControl()
    {
    }

    /// <summary>Gets the number of committed content callbacks.</summary>
    public int ContentChangeCount { get; private set; }

    /// <summary>Gets the previous control observed by the latest callback, or null.</summary>
    public Control? PreviousContent { get; private set; }

    /// <summary>Gets the current control observed by the latest callback, or null.</summary>
    public Control? CurrentContent { get; private set; }

    /// <summary>Gets whether the latest callback observed the complete committed structure.</summary>
    public bool CallbackObservedCommittedStructure { get; private set; }

    /// <inheritdoc/>
    protected override void OnContentChanged(Control? previous, Control? current)
    {
        ContentChangeCount++;
        PreviousContent = previous;
        CurrentContent = current;
        CallbackObservedCommittedStructure =
            ReferenceEquals(Content, current) &&
            (previous is null || previous.Parent is null) &&
            (current is null || ReferenceEquals(current.Parent, this));
    }
}
