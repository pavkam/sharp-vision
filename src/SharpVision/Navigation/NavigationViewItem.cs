// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Controls;

/// <summary>Defines one focusable, selectable entry in a <see cref="NavigationView"/>.</summary>
[PublicAPI]
public sealed class NavigationViewItem: PressableBase
{
    private bool _isSelected;
    private Rune? _idleMarker;
    private Rune? _currentMarker;

    /// <summary>Initializes a navigation entry with a fixed one-cell height.</summary>
    public NavigationViewItem() => Height = Length.Cells(1);

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
            value.ThrowIfContainsControls(
                nameof(value),
                "A navigation item text cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override string? AccessKeyText => Text;

    /// <summary>Gets or sets an optional glyph prefix shown before the header.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public string? Glyph
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets whether this entry is the navigation view's selected item.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>Gets or sets the local idle item marker.</summary>
    public Rune IdleMarker
    {
        get => _idleMarker ?? ControlGlyphs.Navigation.ItemIdle.Value;
        set => SetMarker(ref _idleMarker, value, nameof(IdleMarker));
    }

    /// <summary>Gets or sets the local current item marker.</summary>
    public Rune CurrentMarker
    {
        get => _currentMarker ?? ControlGlyphs.Navigation.ItemCurrent.Value;
        set => SetMarker(ref _currentMarker, value, nameof(CurrentMarker));
    }

    /// <summary>Clears both local item markers to the code-owned defaults.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public void ResetMarkers()
    {
        VerifyMutable();

        if (_idleMarker.HasValue)
        {
            _idleMarker = null;
            NotifyPropertyChanged(nameof(IdleMarker), InvalidationImpact.Render);
        }

        if (_currentMarker.HasValue)
        {
            _currentMarker = null;
            NotifyPropertyChanged(nameof(CurrentMarker), InvalidationImpact.Render);
        }
    }

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
        Invoked?.Invoke(this, new ActivationEventArgs(cause));
        ExecuteCommandIfAny();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var prefix = Glyph is not null ? Terminal.Unicode.Width.Measure(Glyph).Cells + 1 : 0;
        return new Size(
            (int) Math.Min(
                int.MaxValue,
                3L + prefix + Text.Measure(CellPolicy.AmbiguousWidth, UseMnemonic)),
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
        var marker = (current ? CurrentMarker : IdleMarker).Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
        var prefix = Glyph is not null ? $"{Glyph} " : string.Empty;
        var clipped = canvas.Clip(bounds);
        var leading = clipped.Draw(
            $" {marker} {prefix}".AsSpan(),
            new Point(bounds.X, bounds.Y),
            style,
            background: BackgroundMode.Transparent);
        _ = Text.Draw(
            clipped,
            leading.Final,
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

    internal NavigationView? FindNavigationView() => FindAncestor<NavigationView>();

    private void SetMarker(ref Rune? storage, Rune value, string propertyName)
    {
        _ = new ControlGlyph(value, value);
        VerifyMutable();
        if (storage == value)
        {
            return;
        }

        storage = value;
        NotifyPropertyChanged(propertyName, InvalidationImpact.Render);
    }
}
