// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using SharpVision.Runtime;

/// <summary>Provides a focusable clipboard-copy source with observable invocation state.</summary>
internal sealed class ClipboardCopySourceProbe: Container, IClipboardCopySource
{
    private string _copyText;

    /// <summary>Initializes a source with owned copy text and an optional owned child.</summary>
    /// <param name="copyText">The non-null text returned by <see cref="CopySelection"/>.</param>
    /// <param name="child">The optional detached child owned by the source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="copyText"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="child"/> is already owned or attached.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="child"/> is disposed.</exception>
    internal ClipboardCopySourceProbe(string copyText, ControlBase? child = null) : base(capacity: 1)
    {
        ArgumentNullException.ThrowIfNull(copyText);

        _copyText = copyText;
        IsFocusable = true;
        IsTabStop = true;

        if (child is not null)
        {
            Children.Add(child);
        }
    }

    /// <summary>Gets or sets the owned text returned by the next copy request.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    internal string CopyText
    {
        get => _copyText;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _copyText = value;
        }
    }

    /// <summary>Gets how many copy requests reached this exact source.</summary>
    internal int CopyCalls { get; private set; }

    /// <inheritdoc/>
    public string CopySelection()
    {
        CopyCalls++;
        return CopyText;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) =>
        Children.Count == 0 ? default : MeasureChild(Children[0], constraint);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Children.Count > 0)
        {
            ArrangeChild(Children[0], bounds, ResolvedAxes.Both);
        }
    }
}
