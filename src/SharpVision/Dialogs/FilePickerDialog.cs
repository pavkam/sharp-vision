// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using Controls.Collections;

using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;

/// <summary>Provides a responsive modal surface for choosing one or more existing local files.</summary>
/// <remarks>
/// The retained dialog enumerates one directory away from the dispatcher and commits only the newest
/// result on the owning dispatcher. Directories are navigation targets and never appear in accepted paths.
/// </remarks>
[PublicAPI]
public sealed class FilePickerDialog: FileDialogBase<FilePickerResult>
{
    private const int _nonListWindowRows = 21;

    private readonly Button _openButton;
    private readonly StyleSlot<ButtonStyle> _openButtonStyle;

    #region Construction and state

    /// <summary>Initializes a file picker with default options.</summary>
    public FilePickerDialog()
        : this(options: null)
    {
    }

    /// <summary>Initializes a file picker from an owned snapshot of optional configuration.</summary>
    /// <param name="options">Optional configuration; null selects defaults.</param>
    /// <exception cref="ArgumentException">The initial directory path or active filter is invalid.</exception>
    public FilePickerDialog(FilePickerOptions? options)
        : this(options, new SystemFilePickerFileSystem())
    {
    }

    /// <summary>Initializes a file picker over a deterministic filesystem source.</summary>
    /// <param name="options">Optional configuration; null selects defaults.</param>
    /// <param name="fileSystem">The non-null canonical path and enumeration source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> is null.</exception>
    /// <exception cref="ArgumentException">The initial directory path or active filter is invalid.</exception>
    internal FilePickerDialog(FilePickerOptions? options, IFilePickerFileSystem fileSystem)
        : base(
            fileSystem,
            (options = (options ?? new FilePickerOptions()).Copy()).Title,
            options.InitialDirectory,
            options.ShowHidden,
            options.FilterIndex,
            options.MaxVisibleRows,
            _nonListWindowRows,
            options.Filters,
            options.AllowMultiple ? ListSelectionMode.Multiple : ListSelectionMode.Single,
            FilePickerResult.Cancelled)
    {
        _openButton = new Button
        {
            Text = "&Open",
            IsDefault = true,
            IsEnabled = false
        };
        Initialize();
        _openButtonStyle = InitializePartStyle(
            ButtonStyle.ForwardingDefinition,
            nameof(OpenButtonStyle));
        BindStyle(_openButtonStyle, _openButton);
        CancelButtonStyle = options.CancelButtonStyle;
        ShowHiddenCheckBoxStyle = options.ShowHiddenCheckBoxStyle;
        FileListScrollBarStyle = options.FileListScrollBarStyle;
        FilterScrollBarStyle = options.FilterScrollBarStyle;
        OpenButtonStyle = options.OpenButtonStyle;
    }

    /// <summary>Gets the selected canonical file paths in stable display order.</summary>
    public IReadOnlyList<string> SelectedPaths { get; private set; } = Array.AsReadOnly<string>([]);

    /// <summary>Gets or sets the complete local presentation applied to the Open Button, or null to
    /// let it use its own semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public ButtonStyle? OpenButtonStyle
    {
        get => _openButtonStyle.Local;
        set => _openButtonStyle.Local = value;
    }

    /// <summary>Gets the resolved Open Button style.</summary>
    public ButtonStyle ActualOpenButtonStyle => _openButtonStyle.Actual;

    #endregion

    #region Presentation

    /// <summary>Shows a temporary modal file picker owned by one attached control.</summary>
    /// <param name="owner">The attached control whose Screen or container hosts the dialog.</param>
    /// <param name="options">Optional copied picker configuration.</param>
    /// <param name="cancellationToken">Cancels the returned task and tears down the presentation.</param>
    /// <returns>A task completing with accepted canonical paths or a cancelled semantic result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="ArgumentException">The owner is detached or has no presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public static Task<FilePickerResult> ShowAsync(
        ControlBase owner,
        FilePickerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = owner.Dispatcher ??
            throw new ArgumentException("The file-picker owner must be attached.", nameof(owner));
        dispatcher.VerifyAccess();
        var host = FindHost(owner) ??
            throw new ArgumentException("The file-picker owner must have a presentation host.", nameof(owner));
        var dialog = new FilePickerDialog(options);
        host.Add(dialog);

        try
        {
            return dialog.PresentAsync(host, dialog.GetModalFocusTarget(), cancellationToken);
        }
        catch
        {
            _ = host.Remove(dialog);
            dialog.Dispose();
            throw;
        }
    }

    #endregion

    #region Composition

    /// <inheritdoc/>
    protected override Grid CreateContent()
    {
        var location = CreateLocationBar();

        var hidden = new Overlay
        {
            Children = { HiddenToggle }
        };

        var footer = CreateFooter(_openButton);

        var root = new Grid
        {
            RowSpacing = 1,
            Padding = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Columns.Add(Track.Star(1, minimum: 8));
        root.Rows.Add(Track.Auto(minimum: 3));
        root.Rows.Add(Track.Star(
            1,
            minimum: Math.Min(5, FileListSurface.MaxHeight),
            maximum: FileListSurface.MaxHeight));
        root.Rows.Add(Track.Auto(minimum: 1));
        root.Rows.Add(Track.Auto(minimum: 1));
        root.Rows.Add(Track.Auto(minimum: 3));
        Grid.SetRow(FileListSurface, 1);
        Grid.SetRow(StatusText, 2);
        Grid.SetRow(hidden, 3);
        Grid.SetRow(footer, 4);
        root.Children.Add(location);
        root.Children.Add(FileListSurface);
        root.Children.Add(StatusText);
        root.Children.Add(hidden);
        root.Children.Add(footer);
        return root;
    }

    /// <inheritdoc/>
    protected override void WireAcceptInteraction() =>
        _openButton.Click += OnOpenClicked;

    #endregion

    #region Interaction

    /// <inheritdoc/>
    protected override ControlBase GetModalFocusTarget() => PathInput;

    /// <inheritdoc/>
    protected override ControlBase GetInitialLoadFocusTarget() => FileList;

    /// <inheritdoc/>
    protected override void OnListSelectionChanged() => PublishSelection();

    /// <inheritdoc/>
    private protected override void OnFileItemInvoked(FilePickerEntry entry, ActivationCause cause)
    {
        _ = cause;
        CompleteAccepted();
    }

    private void CompleteAccepted()
    {
        if (SelectedPaths.Count > 0)
        {
            _ = Complete(FilePickerResult.Accept(SelectedPaths));
        }
    }

    private void OnOpenClicked(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CompleteAccepted();
    }

    private void PublishSelection()
    {
        SelectedPaths = Array.AsReadOnly(
            FileList.SelectedItems
                .OfType<FilePickerEntry>()
                .Where(static entry => !entry.IsDirectory)
                .Select(static entry => entry.FullPath)
                .ToArray());
        _openButton.IsEnabled = SelectedPaths.Count > 0;
        SetStatus(SelectedPaths.Count == 0
            ? SnapshotStatus
            : $"{SelectedPaths.Count} {(SelectedPaths.Count == 1 ? "file" : "files")} selected");
        NotifyPropertyChanged(nameof(SelectedPaths), InvalidationImpact.None);
    }

    #endregion

    #region Loading lifecycle

    /// <inheritdoc/>
    private protected override void OnLoadCommitted(FilePickerEntry[] entries)
    {
        _ = entries;
        PublishSelection();
    }

    #endregion
}
