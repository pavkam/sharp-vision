// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using DisplayText = Display.Text;

/// <summary>Defines a mutable control that owns zero or one publicly replaceable content control
/// plus zero or one publicly replaceable header control.</summary>
/// <remarks>
/// The header follows the same ownership, replacement, and disposal rules as
/// <see cref="ContentControl.Content"/>. A specialized owner retains full control over how the
/// header is arranged and rendered alongside its own chrome; this role only owns the header's
/// lifecycle and the string convenience over it.
/// </remarks>
[PublicAPI]
public abstract class HeaderedContentControl: ContentControl
{
    private readonly OwnedControlSlot _headerSlot;
    private ControlBase? _publishedHeader;

    /// <summary>Initializes an empty headered content control.</summary>
    protected HeaderedContentControl()
    {
        _headerSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.Header,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: null,
                InvalidationImpact.Measure),
            capacity: 1);
        _headerSlot.Changed += OnHeaderSlotChanged;
    }

    /// <summary>Gets or sets the single publicly replaceable header control.</summary>
    /// <remarks>
    /// Null clears the slot. A successful change commits structure, publishes lifecycle callbacks,
    /// requests measure invalidation, invokes <see cref="OnHeaderChanged"/>, and then raises
    /// <see cref="ControlBase.PropertyChanged"/> while guarded publication remains active. Assigning
    /// the identical control is a no-op after dispatcher and lifetime validation.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The assigned control already belongs to a tree or would create an ownership cycle.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The attached owner is accessed off-dispatcher or an ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or assigned control is disposed.</exception>
    public ControlBase? Header
    {
        get => _headerSlot.Count == 0 ? null : _headerSlot[0];
        set => _headerSlot.ReplaceAll(value is null ? [] : [value]);
    }

    /// <summary>Gets or sets the header as plain text.</summary>
    /// <remarks>
    /// Reading returns the header's text when it is a <see cref="DisplayText"/>, and an empty string
    /// otherwise. Assigning mutates an existing <see cref="DisplayText"/> header in place; any other
    /// header, including none, is replaced by a newly materialized <see cref="DisplayText"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string HeaderText
    {
        get => Header is DisplayText text ? text.Content : string.Empty;
        set
        {
            VerifyMutable();
            ArgumentNullException.ThrowIfNull(value);

            if (string.Equals(HeaderText, value, StringComparison.Ordinal))
            {
                return;
            }

            if (Header is DisplayText text)
            {
                text.Content = value;
            }
            else
            {
                Header = new DisplayText(value);
            }

            NotifyPropertyChanged(nameof(HeaderText), InvalidationImpact.Measure);
        }
    }

    /// <summary>Responds after the header ownership change is structurally committed.</summary>
    /// <param name="previous">The detached previous header, or null.</param>
    /// <param name="current">The committed current header, or null.</param>
    /// <remarks>
    /// The callback runs before the public property notification. Its exception is retained until
    /// that notification is attempted; an earlier structural callback failure remains authoritative
    /// for the complete ownership transaction. It runs while guarded structural publication remains
    /// active, so changing owned slots or disposing the owner or either affected header control is
    /// rejected as reentrant. The committed ownership change is never rolled back.
    /// </remarks>
    protected virtual void OnHeaderChanged(ControlBase? previous, ControlBase? current)
    {
    }

    /// <inheritdoc/>
    protected override string? AccessKeyText => Header is IAccessKeyCaption caption ? caption.Text : null;

    /// <summary>Gets whether <paramref name="candidate"/> is this control's own header control.</summary>
    /// <param name="candidate">The control to test.</param>
    internal bool OwnsCaption(ControlBase candidate) => ReferenceEquals(Header, candidate);

    private void OnHeaderSlotChanged()
    {
        var previous = _publishedHeader;
        var current = Header;
        _publishedHeader = current;
        ExceptionDispatchInfo? failure = null;

        ExceptionAggregation.Capture(() => OnHeaderChanged(previous, current), ref failure);
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(Header), InvalidationImpact.None),
            ref failure);

        failure?.Throw();
    }
}
