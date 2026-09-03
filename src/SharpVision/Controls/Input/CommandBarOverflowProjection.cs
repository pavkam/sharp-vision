// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.ComponentModel;

using Menus;

/// <summary>Bridges one semantic command item into one private overflow-menu face.</summary>
/// <remarks>
/// The bridge borrows presentation and availability facts only. It never copies the semantic
/// command or parameter, so every invocation still converges on the original retained item.
/// </remarks>
internal sealed class CommandBarOverflowProjection: IDisposable
{
    private readonly CommandBar _owner;
    private bool _isDisposed;

    /// <summary>Initializes and synchronizes one retained projection.</summary>
    /// <param name="owner">The command bar owning the private popup.</param>
    /// <param name="source">The semantic source retained by that bar.</param>
    internal CommandBarOverflowProjection(CommandBar owner, CommandBarItem source)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(source);
        _owner = owner;
        Source = source;
        Item = new MenuItem();
        Item.Invoked += OnInvoked;
        Source.PropertyChanged += OnSourcePropertyChanged;
        Synchronize();
    }

    /// <summary>Gets the private menu face owned by the projection snapshot.</summary>
    internal MenuItem Item { get; }

    /// <summary>Gets the semantic source identity represented by this projection.</summary>
    internal CommandBarItem Source { get; }

    /// <summary>Releases the source bridge after the private face has detached from its menu.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Detach();
        Item.Dispose();
    }

    /// <summary>Releases callbacks while leaving the menu-owned face for ancestor disposal.</summary>
    internal void Detach()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Source.PropertyChanged -= OnSourcePropertyChanged;
        Item.Invoked -= OnInvoked;
    }

    private void OnInvoked(object? sender, MenuItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _owner.InvokeProjection(Source, eventArgs.Cause);
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(CommandBarItem.Text) or
            nameof(CommandBarItem.StartAffix) or
            nameof(CommandBarItem.EndAffix) or
            nameof(CommandBarItem.IsEnabled) or
            nameof(CommandBarItem.EffectiveIsEnabled) or
            nameof(CommandBarItem.Style))
        {
            Synchronize();
        }
    }

    private void Synchronize()
    {
        if (_isDisposed || Item.IsDisposed)
        {
            return;
        }

        Item.Text = Source.Text;
        Item.StartAffix = Source.StartAffix;
        Item.EndAffix = Source.EndAffix;
        Item.IsEnabled = Source.EffectiveIsEnabled;
        Item.Style = MapStyle(Source.Style);
    }

    [Pure]
    private static MenuItemStyle? MapStyle(CommandBarItemStyle? source)
    {
        if (source is null)
        {
            return null;
        }

        var markers = MenuItemStyle.Default;
        return new MenuItemStyle(
            source.Face,
            source.Border,
            source.Shadow,
            markers.UncheckedGlyph,
            markers.CheckedGlyph,
            markers.RadioUncheckedGlyph,
            markers.RadioCheckedGlyph)
        {
            AffixGap = source.AffixGap
        };
    }
}
