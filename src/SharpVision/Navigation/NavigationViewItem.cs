// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Controls;

/// <summary>Defines one focusable, selectable entry in a <see cref="NavigationView"/>.</summary>
[PublicAPI]
public sealed class NavigationViewItem: InputBase, IStyled<NavigationViewItemStyle>
{
    private bool _isSelected;
    private readonly StyleSlot<NavigationViewItemStyle> _style;

    /// <summary>Initializes a navigation entry with a fixed one-cell height.</summary>
    public NavigationViewItem()
    {
        EnablePressActivation();
        EnableCommand();
        _style = InitializeStyle(NavigationViewItemStyle.Definition);
        Height = Length.Cells(1);
    }

    /// <summary>Raised after keyboard or pointer activation requests navigation.</summary>
    public event EventHandler<ActivationEventArgs>? Invoked;

    /// <summary>Gets or sets the non-null label text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public override string Text
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentException.ThrowIfContainsControls(value, nameof(value), "A navigation item text cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override string? AccessKeyText => Text;

    /// <summary>Gets or sets an optional glyph prefix shown before the header.</summary>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public string? Glyph
    {
        get;
        set
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfContainsControls(
                    value,
                    nameof(value),
                    "A navigation item glyph cannot contain terminal controls.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets whether this entry is the navigation view's selected item.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public NavigationViewItemStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public NavigationViewItemStyle ActualStyle => _style.Actual;

    /// <summary>Commits the visual selected state from the containing navigation view.</summary>
    internal void CommitSelection(bool value) =>
        _ = SetVisualStateProperty(ref _isSelected, value, nameof(IsSelected));

    /// <summary>Activates this item on behalf of its focus-owning navigation view.</summary>
    /// <param name="cause">The validated semantic activation source.</param>
    internal void ActivateFromOwner(ActivationCause cause) => Activate(cause);

    /// <summary>Activates this item through the programmatic path when it is available.</summary>
    /// <exception cref="InvalidOperationException">The attached item is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public void PerformInvoke()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override bool IsSelectedState => _isSelected;

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        var command = CaptureCommand();
        Invoked?.Invoke(this, new ActivationEventArgs(cause));
        ExecuteCommandIfAny(command);
    }

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inside the
    /// content box and outside the caption itself, after the fixed marker and glyph prefix.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inside the
    /// content box and outside the caption itself.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var prefix = Glyph is not null ? MeasureCells(Glyph) + 1 : 0;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        return new Size(
            (int) Math.Min(
                int.MaxValue,
                3L + prefix + affixes.StartCells + affixes.EndCells + Text.Measure(CellPolicy.AmbiguousWidth, UseMnemonic)),
            1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;

        if (this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(Bounds, style);
        }

        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var current = _isSelected || IsPointerOver;
        var themed = current ? ControlGlyphs.Navigation.ItemCurrent : ControlGlyphs.Navigation.ItemIdle;
        var marker = (current ? ActualStyle.CurrentMarker : ActualStyle.IdleMarker).Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
        var prefix = Glyph is not null ? $"{Glyph} " : string.Empty;
        var clipped = canvas.Clip(bounds);
        var leading = clipped.Draw(
            $" {marker} {prefix}".AsSpan(),
            new Point(bounds.X, bounds.Y),
            style,
            background: BackgroundMode.Transparent);

        // The marker and glyph prefix stay outboard, unaffected by affixes; everything from here
        // to the content box's right edge is the affix-and-caption region: [start][gap] text [gap][end].
        var textRegion = new Rect(
            leading.Final.X,
            bounds.Y,
            Math.Max(0, bounds.Right - leading.Final.X),
            bounds.Height);
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        RenderAffixes(clipped, textRegion, affixes, StartAffix, EndAffix, style);

        var deflated = DeflateForAffixes(textRegion, affixes);
        _ = Text.Draw(
            clipped.Clip(deflated),
            new Point(deflated.X, deflated.Y),
            style,
            BackgroundMode.Transparent,
            CellPolicy.AmbiguousWidth,
            UseMnemonic,
            EffectiveIsEnabled ? Theme?.Hotkey ?? Color.Default : null);
    }

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;
        return FindNavigationView()?.InvokeAccessKey(this) == true;
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (focused)
        {
            var view = FindNavigationView();
            Debug.Assert(view is not null, "A focused NavigationViewItem belongs to a NavigationView.");
            view.NotifyItemFocused(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Invoked = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    [Pure]
    internal NavigationView? FindNavigationView() => FindAncestor<NavigationView>();

    /// <inheritdoc/>
    internal override void OnDirectDisposalRequested()
    {
        if (FindAncestor<NavigationViewGroup>() is { } group)
        {
            group.RemoveItemForDisposal(this);
        }
        else
        {
            FindNavigationView()?.RemoveEntryForDisposal(this);
        }

        base.OnDirectDisposalRequested();
    }
}
