// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using SharpVision.Controls.Input;

/// <summary>Hosts retained dialog actions and reserves only their visible downward shadow overflow.</summary>
internal sealed class DialogActionHost: CompositeControlBase
{
    private readonly Button[] _buttons;

    /// <summary>Initializes an action host around one detached retained composition.</summary>
    /// <param name="content">The detached action composition transferred into this host.</param>
    /// <param name="buttons">The non-null Buttons whose resolved shadows determine bottom clearance.</param>
    /// <exception cref="ArgumentNullException">The content, array, or one Button is null.</exception>
    internal DialogActionHost(ControlBase content, Button[] buttons)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(buttons);

        foreach (var button in buttons)
        {
            ArgumentNullException.ThrowIfNull(button);
        }

        _buttons = buttons;
        InitializeContent(content);

        foreach (var button in _buttons)
        {
            button.PropertyChanged += OnButtonPropertyChanged;
        }

        UpdateShadowClearance();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            foreach (var button in _buttons)
            {
                button.PropertyChanged -= OnButtonPropertyChanged;
            }
        }

        base.OnUnavailable(reason);
    }

    private void OnButtonPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is null or nameof(ActualShadow))
        {
            UpdateShadowClearance();
        }
    }

    private void UpdateShadowClearance()
    {
        var bottom = 0;

        foreach (var button in _buttons)
        {
            bottom = Math.Max(bottom, DownwardShadowRows(button.ActualShadow));
        }

        Padding = new Thickness(left: 0, top: 0, right: 0, bottom);
    }

    private static int DownwardShadowRows(Shadow shadow)
        => !shadow.Visible || shadow.Offset.Y <= 0
            ? 0
            : shadow.Mode == ShadowMode.FractionalBlock
                ? (shadow.Offset.Y / 2) + (shadow.Offset.Y % 2)
                : shadow.Offset.Y;
}
