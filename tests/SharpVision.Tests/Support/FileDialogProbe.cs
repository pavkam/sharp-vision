// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Derives from <see cref="FileDialogBase{TResult}"/> in the test assembly - a real
/// external-to-<c>SharpVision</c> assembly - implementing every protected hook trivially, so a test
/// proves the base's authoring seams are genuinely reachable by a third-party dialog instead of only
/// by <c>FilePickerDialog</c> and <c>SaveFileDialog</c> inside the production assembly.</summary>
internal sealed class FileDialogProbe: FileDialogBase<string>
{
    private readonly Button _acceptButton = new() { Text = "Accept" };

    /// <summary>Initializes a probe dialog over a deterministic file system.</summary>
    /// <param name="fileSystem">The non-null canonical path and enumeration source.</param>
    /// <param name="initialDirectory">The canonical initial directory.</param>
    internal FileDialogProbe(IFilePickerFileSystem fileSystem, string initialDirectory)
        : base(
            fileSystem,
            title: "Probe",
            initialDirectory,
            showHidden: false,
            filterIndex: 0,
            maxVisibleRows: 5,
            nonListWindowRows: 10,
            filters: [FilePickerFilter.AllFiles],
            selectionMode: ListSelectionMode.Single,
            cancelledResult: "cancelled") =>
        Initialize();

    /// <summary>Gets the entry snapshot from the most recent <see cref="OnLoadCommitted"/> call, or
    /// null before the first directory load commits.</summary>
    internal FilePickerEntry[]? LastCommittedEntries { get; private set; }

    /// <summary>Gets the entry from the most recent <see cref="OnFileItemInvoked"/> call, or null
    /// before any file entry is invoked.</summary>
    internal FilePickerEntry? LastInvokedEntry { get; private set; }

    /// <summary>Gets the activation cause from the most recent <see cref="OnFileItemInvoked"/> call.</summary>
    internal ActivationCause LastInvokedCause { get; private set; }

    /// <inheritdoc/>
    protected override Grid CreateContent()
    {
        var location = CreateLocationBar();
        var listArea = CreateFileListArea();
        var hidden = new Overlay { Children = { HiddenToggle } };
        var footer = CreateFooter(_acceptButton);
        var root = new Grid
        {
            RowSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Columns.Add(Track.Star(1, minimum: Length.Cells(8)));
        root.Rows.Add(Track.Auto(minimum: Length.Cells(3)));
        root.Rows.Add(Track.Star(1, minimum: Length.Cells(5)));
        root.Rows.Add(Track.Auto(minimum: Length.Cells(1)));
        root.Rows.Add(Track.Auto());
        Grid.SetRow(listArea, 1);
        Grid.SetRow(hidden, 2);
        Grid.SetRow(footer, 3);
        root.Children.Add(location);
        root.Children.Add(listArea);
        root.Children.Add(hidden);
        root.Children.Add(footer);
        return root;
    }

    /// <inheritdoc/>
    protected override FileDialogStyle ResolveDialogStyle() => FilePickerDialogStyle.Default;

    /// <inheritdoc/>
    protected override void WireAcceptInteraction()
    {
    }

    /// <inheritdoc/>
    protected override ControlBase GetModalFocusTarget() => PathInput;

    /// <inheritdoc/>
    protected override ControlBase GetInitialLoadFocusTarget() => FileList;

    /// <inheritdoc/>
    protected override void OnListSelectionChanged()
    {
    }

    /// <inheritdoc/>
    protected override void OnFileItemInvoked(FilePickerEntry entry, ActivationCause cause)
    {
        LastInvokedEntry = entry;
        LastInvokedCause = cause;
    }

    /// <inheritdoc/>
    protected override void OnLoadCommitted(FilePickerEntry[] entries) => LastCommittedEntries = entries;
}
