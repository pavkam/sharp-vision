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
public sealed class SaveFileDialog: FileDialogBase<SaveFileResult>, IStyled<SaveFileDialogStyle>
{
    private const int _nonListWindowRows = 21;

    private readonly bool _confirmOverwrite;
    private readonly TextInput _fileNameInput;
    private readonly Text _fileNameLabelText;
    private readonly Button _saveButton;
    private readonly StyleSlot<ButtonStyle> _saveButtonStyle;
    private readonly StyleSlot<SaveFileDialogStyle> _style;
    private long _acceptanceVersion;

    /// <summary>Gets or sets a deterministic overwrite-confirmation source for lifecycle tests
    /// that must hold the asynchronous boundary while dispatcher ownership changes.</summary>
    internal Func<Task<MessageBoxResult>>? ConfirmOverwriteForLifecycleTest { get; set; }

    /// <summary>Gets or sets a lifecycle-test hook invoked immediately before the captured
    /// dispatcher post, proving the continuation has crossed its asynchronous boundary.</summary>
    internal Action? PostAcceptanceHookForLifecycleTest { get; set; }

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
        _style = InitializeStyle(SaveFileDialogStyle.Definition);
        _confirmOverwrite = options.ConfirmOverwrite;
        _fileNameInput = new TextInput
        {
            Text = options.InitialFileName,
            Placeholder = FileNamePlaceholder,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _fileNameLabelText = new Text(FileNameLabel)
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        _saveButton = new Button
        {
            Text = SaveText,
            IsDefault = true,
            IsEnabled = !string.IsNullOrEmpty(options.InitialFileName)
        };
        Initialize();
        _saveButtonStyle = InitializePartStyle(
            ButtonStyle.ForwardingDefinition,
            nameof(SaveButtonStyle));
        BindStyle(_saveButtonStyle, _saveButton);
        Style = options.Style;
        CancelButtonStyle = options.CancelButtonStyle;
        ShowHiddenCheckBoxStyle = options.ShowHiddenCheckBoxStyle;
        FileListScrollBarStyle = options.FileListScrollBarStyle;
        FilterScrollBarStyle = options.FilterScrollBarStyle;
        SaveButtonStyle = options.SaveButtonStyle;
        ParentDirectoryText = options.ParentDirectoryText;
        DirectoryPlaceholder = options.DirectoryPlaceholder;
        ShowHiddenText = options.ShowHiddenText;
        CancelText = options.CancelText;
        SaveText = options.SaveText;
        FileNameLabel = options.FileNameLabel;
        FileNamePlaceholder = options.FileNamePlaceholder;
        OverwriteTitle = options.OverwriteTitle;
        OverwriteYesText = options.OverwriteYesText;
        OverwriteNoText = options.OverwriteNoText;
        OverwriteStyle = options.OverwriteStyle;

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

        if (options.OverwriteMessageFormat is { } overwriteMessageFormat)
        {
            OverwriteMessageFormat = overwriteMessageFormat;
        }
    }

    /// <summary>Gets the current filename typed or selected by the user.</summary>
    public string FileName => _fileNameInput.Text;

    /// <summary>Gets or sets the complete local aggregate presentation, or null to let the active
    /// Theme's <see cref="WindowStyle"/> role section own the frame and structural
    /// geometry.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public SaveFileDialogStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved complete aggregate presentation.</summary>
    public SaveFileDialogStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets the complete local presentation applied to the Save Button, or null to
    /// let it use its own semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public ButtonStyle? SaveButtonStyle
    {
        get => _saveButtonStyle.Local;
        set => _saveButtonStyle.Local = value;
    }

    /// <summary>Gets the resolved Save Button style.</summary>
    public ButtonStyle ActualSaveButtonStyle => _saveButtonStyle.Actual;

    /// <summary>Gets or sets the non-null caption for the Save action.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string SaveText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => _saveButton.Text = SaveText);
        }
    } = "&Save";

    /// <summary>Gets or sets the non-null label preceding the filename input.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string FileNameLabel
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => _fileNameLabelText.Content = FileNameLabel);
        }
    } = "Name:";

    /// <summary>Gets or sets the non-null placeholder shown in the empty filename input.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string FileNamePlaceholder
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.None,
                () => _fileNameInput.Placeholder = FileNamePlaceholder);
        }
    } = "File name";

    /// <summary>Gets or sets the non-null title for the overwrite-confirmation MessageBox.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string OverwriteTitle
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = "Confirm Save As";

    /// <summary>Gets or sets the non-null formatter that builds the overwrite-confirmation message
    /// from the existing file's display name, supplied structurally rather than through unchecked
    /// caller <c>string.Format</c> composition.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public Func<string, string> OverwriteMessageFormat
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = DefaultOverwriteMessageFormat;

    /// <summary>Gets or sets the non-null caption for the overwrite-confirmation Yes action.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string OverwriteYesText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = "&Yes";

    /// <summary>Gets or sets the non-null caption for the overwrite-confirmation No action.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string OverwriteNoText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = "&No";

    /// <summary>Gets or sets the complete local presentation applied to the overwrite-confirmation
    /// MessageBox, or null to let it use the active Theme's own default
    /// <see cref="MessageBoxStyle"/>.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public MessageBoxStyle? OverwriteStyle
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    #endregion

    private protected override FileDialogStyle ResolveDialogStyle() => ActualStyle;

    private protected override WindowStyle ResolveCloseChromeStyle(Theme? theme) =>
        SaveFileDialogStyle.Definition.Resolve(_style.LocalValue, theme);

    private static string DefaultOverwriteMessageFormat(string fileName) =>
        $"'{fileName}' already exists.\nDo you want to replace it?";

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
        ControlBase owner,
        SaveFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = owner.Dispatcher ??
            throw new ArgumentException("The save-file owner must be attached.", nameof(owner));
        dispatcher.VerifyAccess();
        var dialog = new SaveFileDialog(options);
        return dialog.PresentAsync(owner, dialog.GetModalFocusTarget(), cancellationToken);
    }

    #endregion

    #region Composition

    /// <inheritdoc/>
    protected override Grid CreateContent()
    {
        var location = CreateLocationBar();
        var listArea = CreateFileListArea();

        var fileNameRow = new Grid
        {
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        fileNameRow.Columns.Add(Track.Auto());
        fileNameRow.Columns.Add(Track.Star(1, minimum: 8));
        Grid.SetColumn(_fileNameInput, 1);
        fileNameRow.Children.Add(_fileNameLabelText);
        fileNameRow.Children.Add(_fileNameInput);

        var hidden = new Overlay
        {
            Children = { HiddenToggle }
        };

        var footer = CreateFooter(_saveButton);

        var root = new Grid
        {
            RowSpacing = 1,
            Padding = new Thickness(left: 1, top: 1, right: 1, bottom: 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Columns.Add(Track.Star(1, minimum: 8));
        root.Rows.Add(Track.Auto(minimum: 3));
        root.Rows.Add(Track.Star(1, minimum: Math.Min(8, ((int) FileListSurface.MaxHeight!.Value.Value).Add(3))));
        root.Rows.Add(Track.Auto(minimum: 3));
        root.Rows.Add(Track.Auto(minimum: 1));
        root.Rows.Add(Track.Auto());
        Grid.SetRow(listArea, 1);
        Grid.SetRow(fileNameRow, 2);
        Grid.SetRow(hidden, 3);
        Grid.SetRow(footer, 4);
        root.Children.Add(location);
        root.Children.Add(listArea);
        root.Children.Add(fileNameRow);
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
    protected override ControlBase GetModalFocusTarget() => _fileNameInput;

    /// <inheritdoc/>
    protected override ControlBase GetInitialLoadFocusTarget() => _fileNameInput;

    /// <inheritdoc/>
    protected override void OnListSelectionChanged() => PopulateFileNameFromSelection();

    /// <inheritdoc/>
    private protected override void OnFileItemInvoked(FilePickerEntry entry, ActivationCause cause)
    {
        _ = cause;
        TrySetFileName(entry.Name);
        UpdateSaveEnabled();
        CompleteAcceptedAsync();
    }

    private async void CompleteAcceptedAsync()
    {
        var acceptanceVersion = ++_acceptanceVersion;
        var dispatcher = Dispatcher;
        var attachment = dispatcher is null ? null : CaptureAttachment();

        try
        {
            await CompleteAcceptedCoreAsync(acceptanceVersion, attachment);
        }
        catch (Exception exception)
        {
            ReportAcceptanceFailure(attachment, acceptanceVersion, exception);
        }
    }

    private async Task CompleteAcceptedCoreAsync(
        long acceptanceVersion,
        ControlAttachmentToken? attachment)
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

        // FileExists (File.Exists) returns false for an existing directory, so a typed name that
        // happens to match a subfolder of CurrentDirectory would otherwise skip the overwrite
        // check entirely and complete with a "confirmed" path that actually names a directory.
        if (FileSystem.DirectoryExists(fullPath))
        {
            SetStatus($"'{Path.GetFileName(fullPath)}' is a directory.");
            return;
        }

        if (_confirmOverwrite && FileSystem.FileExists(fullPath))
        {
            if (attachment is null)
            {
                return;
            }

            var confirmation = ConfirmOverwriteForLifecycleTest is { } confirm
                ? await confirm()
                : await MessageBox.ShowAsync(
                    this,
                    OverwriteMessageFormat(Path.GetFileName(fullPath)),
                    new MessageBoxOptions
                    {
                        Title = OverwriteTitle,
                        Buttons = MessageBoxButtons.YesNo,
                        YesText = OverwriteYesText,
                        NoText = OverwriteNoText,
                        Style = OverwriteStyle,
                        ButtonStyle = SaveButtonStyle
                    });

            // The MessageBox completion resumes on a background thread. Post back to the
            // owning dispatcher so that Complete can safely modify attached control state.
            // This is an async void method, so an unhandled exception here is unobservable
            // by any caller and becomes process-fatal; the dispatcher may already be
            // stopping by the time this continuation resumes. A momentarily full queue is
            // swallowed the same way as disposal: propagating InvalidOperationException here
            // would crash the whole application over one confirmed "overwrite" click the user
            // can simply retry, strictly worse than silently dropping it.
            PostAcceptanceHookForLifecycleTest?.Invoke();

            try
            {
                PostForCurrentAttachment(
                    attachment,
                    () => _ = Complete(SaveFileResult.FromPath(fullPath)),
                    () => confirmation == MessageBoxResult.Yes &&
                          _acceptanceVersion == acceptanceVersion);
            }
            catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
            {
            }

            return;
        }

        _ = Complete(SaveFileResult.FromPath(fullPath));
    }

    private void ReportAcceptanceFailure(
        ControlAttachmentToken? attachment,
        long acceptanceVersion,
        Exception exception)
    {
        if (attachment is null)
        {
            return;
        }

        void CommitFailure()
        {
            if (IsCurrent(attachment) && _acceptanceVersion == acceptanceVersion)
            {
                SetStatus($"Cannot confirm overwrite: {exception.Message}");
            }
        }

        if (attachment.Dispatcher.CheckAccess())
        {
            CommitFailure();
            return;
        }

        try
        {
            PostForCurrentAttachment(
                attachment,
                CommitFailure,
                () => _acceptanceVersion == acceptanceVersion);
        }
        catch (Exception postException) when (postException is ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    private void OnFileNameChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        _acceptanceVersion++;
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
            TrySetFileName(selected.Name);
        }

        UpdateSaveEnabled();
    }

    /// <summary>Assigns a filesystem-supplied name to the file-name field, degrading to a status
    /// message instead of letting an unrepresentable name (a control character, which POSIX
    /// permits in a filename) force-stop the application.</summary>
    private void TrySetFileName(string name)
    {
        try
        {
            _fileNameInput.Text = name;
        }
        catch (ArgumentException)
        {
            SetStatus("Cannot display this file's name.");
        }
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
