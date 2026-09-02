// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using Controls.Collections;

using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;

/// <summary>Provides a responsive modal surface for choosing one or more existing local files or
/// directories.</summary>
/// <remarks>
/// The retained dialog enumerates one directory away from the dispatcher and commits only the newest
/// result on the owning dispatcher. A directory is always a navigation target on double-click or
/// Enter; whether it can also be a final accepted selection depends on
/// <see cref="FilePickerOptions.SelectionMode"/> - with the default
/// <see cref="FileSelectionMode.Files"/>, a directory never appears in accepted paths.
/// </remarks>
[PublicAPI]
public sealed class FilePickerDialog: FileDialogBase<FilePickerResult>, IStyled<FilePickerDialogStyle>
{
    private const int _nonListWindowRows = 19;

    private readonly Button _openButton;
    private readonly StyleSlot<ButtonStyle> _openButtonStyle;
    private readonly StyleSlot<FilePickerDialogStyle> _style;
    private readonly FileSelectionMode _selectionMode;

    private FilePickerEntry[] _selectedEntries = [];

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
        _style = InitializeStyle(FilePickerDialogStyle.Definition);
        _selectionMode = options.SelectionMode;
        _openButton = new Button
        {
            Text = OpenText,
            IsDefault = true,
            IsEnabled = false
        };
        Initialize();
        _openButtonStyle = InitializePartStyle(
            ButtonStyle.ForwardingDefinition,
            nameof(OpenButtonStyle));
        BindStyle(_openButtonStyle, _openButton);
        Style = options.Style;
        CancelButtonStyle = options.CancelButtonStyle;
        ShowHiddenCheckBoxStyle = options.ShowHiddenCheckBoxStyle;
        FileListScrollBarStyle = options.FileListScrollBarStyle;
        FilterScrollBarStyle = options.FilterScrollBarStyle;
        OpenButtonStyle = options.OpenButtonStyle;
        ParentDirectoryText = options.ParentDirectoryText;
        DirectoryPlaceholder = options.DirectoryPlaceholder;
        ShowHiddenText = options.ShowHiddenText;
        CancelText = options.CancelText;
        OpenText = options.OpenText ?? DefaultOpenText(_selectionMode);

        if (options.ReadyText is { } readyText)
        {
            ReadyText = readyText;
        }

        if (options.LoadingText is { } loadingText)
        {
            LoadingText = loadingText;
        }

        if (options.CountFormat is { } countFormat)
        {
            CountFormat = countFormat;
        }

        if (options.SelectionFormat is { } selectionFormat)
        {
            SelectionFormat = selectionFormat;
        }
    }

    /// <summary>Gets the selected canonical paths accepted by <see cref="FilePickerOptions.SelectionMode"/>,
    /// in stable display order. With the default <see cref="FileSelectionMode.Files"/>, a selected
    /// directory row is excluded.</summary>
    public IReadOnlyList<string> SelectedPaths { get; private set; } = Array.AsReadOnly<string>([]);

    /// <summary>Gets or sets the complete local aggregate presentation, or null to let the active
    /// Theme's <see cref="WindowStyle"/> role section own the frame and structural
    /// geometry.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public FilePickerDialogStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved complete aggregate presentation.</summary>
    public FilePickerDialogStyle ActualStyle => _style.Actual;

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

    /// <summary>Gets or sets the non-null caption for the Open action. Defaults to
    /// <see cref="FilePickerOptions.OpenText"/> when the caller supplied one, otherwise to
    /// <c>"&amp;Select"</c> in <see cref="FileSelectionMode.Directories"/> mode - since there is
    /// nothing to "open" when only directories are pickable - and to <c>"&amp;Open"</c> in every
    /// other mode.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string OpenText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => _openButton.Text = OpenText);
        }
    } = "&Open";

    /// <summary>Gets or sets the non-null selected-file-count formatter used to build the status text
    /// while at least one file is selected.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public Func<int, string> SelectionFormat
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = DefaultSelectionFormat;

    #endregion

    private protected override FileDialogStyle ResolveDialogStyle() => ActualStyle;

    private protected override WindowStyle ResolveCloseChromeStyle(Theme? theme) =>
        FilePickerDialogStyle.Definition.Resolve(_style.LocalValue, theme);

    private static string DefaultSelectionFormat(int count) => $"{count} {(count == 1 ? "file" : "files")} selected";

    /// <summary>Resolves the mode-aware default Open-action caption used when the caller did not
    /// explicitly supply <see cref="FilePickerOptions.OpenText"/>.</summary>
    /// <param name="selectionMode">The picker's selection mode.</param>
    /// <returns><c>"&amp;Select"</c> in pure directory mode; <c>"&amp;Open"</c> otherwise.</returns>
    private static string DefaultOpenText(FileSelectionMode selectionMode) =>
        selectionMode == FileSelectionMode.Directories ? "&Select" : "&Open";

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
        var dialog = new FilePickerDialog(options);
        return dialog.PresentAsync(owner, dialog.GetModalFocusTarget(), cancellationToken);
    }

    #endregion

    #region Composition

    /// <inheritdoc/>
    protected override Grid CreateContent()
    {
        var location = CreateLocationBar();
        var listArea = CreateFileListArea();

        var hidden = new Overlay
        {
            Children = { HiddenToggle }
        };

        var footer = CreateFooter(_openButton);

        var root = new Grid
        {
            RowSpacing = 1,
            Padding = new Thickness(left: 1, top: 1, right: 1, bottom: 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Columns.Add(Track.Star(1, minimum: Length.Cells(8)));
        root.Rows.Add(Track.Auto(minimum: Length.Cells(3)));
        root.Rows.Add(Track.Star(1, minimum: Length.Cells(Math.Min(8, ((int) FileListSurface.MaxHeight!.Value.Value).Add(3)))));
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
        if (_selectedEntries.Length > 0)
        {
            _ = Complete(FilePickerResult.Accept(
                _selectedEntries
                    .Select(static entry => new FilePickerResultEntry(entry.FullPath, entry.IsDirectory))
                    .ToArray()));
        }
    }

    /// <inheritdoc/>
    private protected override bool TryAcceptTypedDirectory(string canonicalDirectory)
    {
        if (_selectionMode == FileSelectionMode.Files)
        {
            return false;
        }

        _ = Complete(FilePickerResult.Accept([new FilePickerResultEntry(canonicalDirectory, isDirectory: true)]));
        return true;
    }

    private void OnOpenClicked(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CompleteAccepted();
    }

    private bool AcceptsEntry(FilePickerEntry entry) => _selectionMode switch
    {
        FileSelectionMode.Directories => entry.IsDirectory,
        FileSelectionMode.FilesAndDirectories => true,
        FileSelectionMode.Files => !entry.IsDirectory,
        _ => !entry.IsDirectory
    };

    private void PublishSelection()
    {
        _selectedEntries =
        [
            .. FileList.SelectedItems
                .OfType<FilePickerEntry>()
                .Where(AcceptsEntry)
        ];
        SelectedPaths = Array.AsReadOnly(_selectedEntries.Select(static entry => entry.FullPath).ToArray());
        _openButton.IsEnabled = SelectedPaths.Count > 0;
        SetStatus(SelectedPaths.Count == 0
            ? SnapshotStatus
            : SelectionFormat(SelectedPaths.Count));

        if (IsDisposed)
        {
            return;
        }

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
