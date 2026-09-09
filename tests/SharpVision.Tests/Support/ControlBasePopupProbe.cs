// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Composes <see cref="ControlBase.EnablePopup"/> directly, without deriving
/// <see cref="InputBase"/> and without overriding <c>MeasureOverride</c>/<c>ArrangeOverride</c>,
/// to prove the base class measures and arranges an owned popup on behalf of a plain
/// control.</summary>
internal sealed class ControlBasePopupProbe: ControlBase
{
    /// <summary>Initializes a focusable probe with an owned popup wrapping one list.</summary>
    internal ControlBasePopupProbe()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsFocusable = true;
        IsTabStop = true;
        Content = new UiListView { Items = ["First", "Second", "Third"] };
        Popup = EnablePopup(Content, focusOnOpen: false);
    }

    /// <summary>Gets the popup's owned list content.</summary>
    internal UiListView Content { get; }

    /// <summary>Gets the constructed, owned popup.</summary>
    internal Popup Popup { get; }

    /// <summary>Gets the ordered sequence of drop-down lifecycle hooks invoked so far.</summary>
    internal List<string> LifecycleEvents { get; } = [];

    /// <inheritdoc/>
    protected override void OnDropDownOpened()
    {
        LifecycleEvents.Add(nameof(OnDropDownOpened));
        base.OnDropDownOpened();
    }

    /// <inheritdoc/>
    protected override void OnDropDownClosed()
    {
        LifecycleEvents.Add(nameof(OnDropDownClosed));
        base.OnDropDownClosed();
    }
}
