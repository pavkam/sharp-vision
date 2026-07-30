// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using Controls.Collections;

using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;

using Text = Controls.Display.Text;

/// <summary>Provides a responsive modal surface for choosing a file path to save to.</summary>
/// <remarks>
/// The retained dialog enumerates one directory away from the dispatcher and commits only the newest
/// result on the owning dispatcher. The user types or selects a filename; when a file exists and
/// <see cref="SaveFileOptions.ConfirmOverwrite"/> is set, a confirmation MessageBox is shown before
/// completing with the canonical save path.
/// </remarks>
[PublicAPI]
public sealed class SaveFileDialog: FileDialogBase<SaveFileResult>
{
    private const int _nonListWindowRows = 23;

    private readonly bool _confirmOverwrite;
    private readonly TextInput _fileNameInput;
    private readonly Button _saveButton;

    #region Construction and state

    /// <summary>Initializes a save-file dialog with default options.</summary>
    public SaveFileDialog()
        : this(options: null)
    {
    }

    /// <summary>Initializes a save-file dialog from an owned snapshot of optional configuration.</summary>
    /// <param name="options">Optional configuration; null selects defaults.</param>
    /// <exception cref="ArgumentException">The initial directory path or active filter is invalid.</exception>
    public SaveFileDialog(SaveFileOptions? options)
        : this(options, new SystemFilePickerFileSystem())
    {
    }

    /// <summary>Initializes a save-file dialog over a deterministic filesystem source.</summary>
    /// <param name="options">Optional configuration; null selects defaults.</param>
    /// <param name="fileSystem">The non-null canonical path and enumeration source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> is null.</exception>
    /// <exception cref="ArgumentException">The initial directory path or active filter is invalid.</exception>
    internal SaveFileDialog(SaveFileOptions? options, IFilePickerFileSystem fileSystem)
        : base(
            fileSystem,
            (options = (options ?? new SaveFileOptions()).Copy()).Title,
            options.InitialDirectory,
            options.ShowHidden,
            options.FilterIndex,
            options.MaxVisibleRows,
            _nonListWindowRows,
            options.Filters,
            ListSelectionMode.Single,
            SaveFileResult.Cancelled)
    {
        _confirmOverwrite = options.ConfirmOverwrite;
        _fileNameInput = new TextInput
        {
            Text = options.InitialFileName,
            Placeholder = "File name",
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _saveButton = new Button
        {
            Content = new Text("&Save"),
            IsDefault = true,
            IsEnabled = !string.IsNullOrEmpty(options.InitialFileName)
        };
        Initialize();
    }

    /// <summary>Gets the current filename typed or selected by the user.</summary>
    public string FileName => _fileNameInput.Text;

    #endregion

    #region Presentation

    /// <summary>Shows a temporary modal save-file dialog owned by one attached control.</summary>
    /// <param name="owner">The attached control whose Screen or container hosts the dialog.</param>
    /// <param name="options">Optional copied dialog configuration.</param>
    /// <param name="cancellationToken">Cancels the returned task and tears down the presentation.</param>
    /// <returns>A task completing with a confirmed canonical path or a cancelled semantic result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="ArgumentException">The owner is detached or has no presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public static Task<SaveFileResult> ShowAsync(
        Control owner,
        SaveFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = owner.Dispatcher ??
            throw new ArgumentException("The save-file owner must be attached.", nameof(owner));
        dispatcher.VerifyAccess();
        var host = FindHost(owner) ??
            throw new ArgumentException("The save-file owner must have a presentation host.", nameof(owner));
        var dialog = new SaveFileDialog(options);
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

        var fileNameRow = new Grid
        {
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        fileNameRow.Columns.Add(Track.Auto());
        fileNameRow.Columns.Add(Track.Star(1, minimum: 8));
        var fileNameLabel = new Text("Name:")
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_fileNameInput, 1);
        fileNameRow.Children.Add(fileNameLabel);
        fileNameRow.Children.Add(_fileNameInput);

        var hidden = new Overlay
        {
            Children = { HiddenToggle }
        };

        var footer = CreateFooter(_saveButton);

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
        root.Rows.Add(Track.Auto(minimum: 1));
        root.Rows.Add(Track.Auto(minimum: 3));
        Grid.SetRow(FileListSurface, 1);
        Grid.SetRow(fileNameRow, 2);
        Grid.SetRow(StatusText, 3);
        Grid.SetRow(hidden, 4);
        Grid.SetRow(footer, 5);
        root.Children.Add(location);
        root.Children.Add(FileListSurface);
        root.Children.Add(fileNameRow);
        root.Children.Add(StatusText);
        root.Children.Add(hidden);
        root.Children.Add(footer);
        return root;
    }

    /// <inheritdoc/>
    protected override void WireAcceptInteraction()
    {
        _fileNameInput.TextChanged += OnFileNameChanged;
        _fileNameInput.Submitted += OnFileNameSubmitted;
        _saveButton.Click += OnSaveClicked;
    }

    #endregion

    #region Interaction

    /// <inheritdoc/>
    protected override Control GetModalFocusTarget() => _fileNameInput;

    /// <inheritdoc/>
    protected override Control GetInitialLoadFocusTarget() => _fileNameInput;

    /// <inheritdoc/>
    protected override void OnListSelectionChanged() => PopulateFileNameFromSelection();

    /// <inheritdoc/>
    private protected override void OnFileItemInvoked(FilePickerEntry entry, ActivationCause cause)
    {
        _fileNameInput.Text = entry.Name;
        UpdateSaveEnabled();

        if (cause == ActivationCause.Keyboard)
        {
            CompleteAcceptedAsync();
        }
    }

    private async void CompleteAcceptedAsync()
    {
        var fileName = _fileNameInput.Text.Trim();

        if (string.IsNullOrEmpty(fileName))
        {
            return;
        }

        string fullPath;

        try
        {
            fullPath = FileSystem.GetFullPath(Path.Combine(CurrentDirectory, fileName));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            SetStatus($"Invalid file name: {exception.Message}");
            return;
        }

        if (_confirmOverwrite && FileSystem.FileExists(fullPath))
        {
            var dispatcher = Dispatcher;

            if (dispatcher is null)
            {
                return;
            }

            var confirmation = await MessageBox.ShowAsync(
                this,
                $"'{Path.GetFileName(fullPath)}' already exists.\nDo you want to replace it?",
                "Confirm Save As",
                MessageBoxButtons.YesNo);

            // The MessageBox completion resumes on a background thread. Post back to the
            // owning dispatcher so that Complete can safely modify attached control state.
            // This is an async void method, so an unhandled exception here is unobservable
            // by any caller and becomes process-fatal; the dispatcher may already be
            // stopping by the time this continuation resumes.
            try
            {
                dispatcher.Post(() =>
                {
                    if (confirmation == MessageBoxResult.Yes)
                    {
                        _ = Complete(SaveFileResult.FromPath(fullPath));
                    }
                });
            }
            catch (ObjectDisposedException)
            {
            }

            return;
        }

        _ = Complete(SaveFileResult.FromPath(fullPath));
    }

    private void OnFileNameChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        UpdateSaveEnabled();
    }

    private void OnFileNameSubmitted(object? sender, SubmittedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CompleteAcceptedAsync();
    }

    private void OnSaveClicked(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CompleteAcceptedAsync();
    }

    private void PopulateFileNameFromSelection()
    {
        var selected = FileList.SelectedItems
            .OfType<FilePickerEntry>()
            .FirstOrDefault(static entry => !entry.IsDirectory);

        if (selected is not null)
        {
            _fileNameInput.Text = selected.Name;
        }

        UpdateSaveEnabled();
    }

    private void UpdateSaveEnabled()
    {
        _saveButton.IsEnabled = !string.IsNullOrWhiteSpace(_fileNameInput.Text);
        NotifyPropertyChanged(nameof(FileName), InvalidationImpact.None);
    }

    #endregion

    #region Loading lifecycle

    /// <inheritdoc/>
    private protected override void OnLoadCommitted(FilePickerEntry[] entries)
    {
    }

    #endregion
}
