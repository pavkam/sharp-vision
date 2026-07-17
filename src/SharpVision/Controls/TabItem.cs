// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines one content page with a header label inside a <see cref="TabControl"/>.</summary>
public sealed class TabItem: ContentControl
{
    private bool _isSelected;

    /// <summary>Initializes an empty tab page with no header.</summary>
    public TabItem() { }

    /// <summary>Gets or sets the non-null header label shown in the tab bar.</summary>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Terminal.Unicode.Width.Measure(value).Controls > 0)
            {
                throw new ArgumentException("A tab header cannot contain terminal controls.", nameof(value));
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override bool IsSelectedState => _isSelected;

    /// <summary>Commits selected appearance from the owning tab control.</summary>
    /// <param name="value">Whether this page is the selected page.</param>
    internal void CommitSelection(bool value) =>
        _ = SetVisualStateProperty(ref _isSelected, value, nameof(IsSelectedState));

    /// <summary>Clears retained content geometry when this page leaves presentation.</summary>
    internal void ClearPresentedContent()
    {
        if (Content is { } content)
        {
            ArrangeChild(content, default, ResolvedAxes.Both);
        }
    }
}
