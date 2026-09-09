// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using Controls.Collections;

using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
using SharpVision.Controls.Scrolling;
using SharpVision.Terminal.Input;

using Text;

using Text = Controls.Display.Text;
using UiListView = Controls.Collections.ListView;

/// <summary>Provides shared retained-composition, navigation, and loading logic for modal file dialogs.</summary>
/// <typeparam name="TResult">The dialog-specific result type produced on completion.</typeparam>
[PublicAPI]
public abstract class FileDialogBase<TResult>: Dialog<TResult>
    where TResult : class
{
    private const int _listChromeRows = 2;

    private readonly IReadOnlyList<FilePickerFilter> _filters;
    private readonly Button _upButton;
    private readonly ComboBox _filterPicker;
    private readonly Button _cancelButton;
    private readonly StyleSlot<ButtonStyle> _cancelButtonStyle;
    private readonly StyleSlot<CheckBoxStyle> _showHiddenCheckBoxStyle;
    private readonly StyleSlot<ScrollBarStyle> _fileListScrollBarStyle;
    private readonly StyleSlot<ScrollBarStyle> _filterScrollBarStyle;

    private FilePickerEntry[] _entries = [];
    private readonly LatestControlOperation _loadOperation = new();
    private bool _initialFocusPending = true;
    private Grid? _rootContent;

    #region Construction and state

    /// <summary>Initializes shared dialog state and controls.</summary>
    /// <param name="fileSystem">The canonical path and enumeration source.</param>
    /// <param name="title">The dialog window title.</param>
    /// <param name="initialDirectory">The initial directory path.</param>
    /// <param name="showHidden">Whether hidden entries are initially shown.</param>
    /// <param name="filterIndex">The zero-based initially active filter index.</param>
    /// <param name="maxVisibleRows">The maximum visible file-list content rows.</param>
    /// <param name="nonListWindowRows">The window rows consumed by chrome outside the file list.</param>
    /// <param name="filters">The owned filter snapshot.</param>
    /// <param name="selectionMode">The list selection mode.</param>
    /// <param name="cancelledResult">The result instance representing cancellation.</param>
    /// <remarks>
    /// A derivative calls this constructor first, then creates its own dialog-specific controls
    /// (an accept Button, a filename input, or whatever else its own layout needs), then calls
    /// <see cref="Initialize"/> exactly once to retain the composed content and wire the shared
    /// interaction handlers. Everything this constructor allocates - the up Button, <see
    /// cref="PathInput"/>, <see cref="FileList"/>, <see cref="FileListSurface"/>, the filter
    /// ComboBox, <see cref="HiddenToggle"/>, <see cref="StatusText"/>, and the Cancel Button - is
    /// available to read and compose immediately after the call returns; only <see
    /// cref="Initialize"/> actually attaches any of it to <see cref="ContentControl.Content"/>.
    /// </remarks>
    protected FileDialogBase(
        IFilePickerFileSystem fileSystem,
        string title,
        string initialDirectory,
        bool showHidden,
        int filterIndex,
        int maxVisibleRows,
        int nonListWindowRows,
        IReadOnlyList<FilePickerFilter> filters,
        ListSelectionMode selectionMode,
        TResult cancelledResult)
        : base(cancelledResult)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        FileSystem = fileSystem;
        _filters = filters;
        Header = title;
        CanMove = true;
        Width = Length.Percent(80);
        Height = Length.Percent(80);
        MaxWidth = Length.Cells(96);
        MaxHeight = Length.Cells(maxVisibleRows.Add(nonListWindowRows));
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        CurrentDirectory = FileSystem.GetFullPath(initialDirectory);
        ShowHidden = showHidden;
        FilterIndex = filterIndex;
        Status = ReadyText;
        SnapshotStatus = ReadyText;

        _upButton = new Button
        {
            Text = ParentDirectoryText,
            Width = Length.Cells(5)
        };
        PathInput = new TextInput
        {
            Text = CurrentDirectory,
            Placeholder = DirectoryPlaceholder,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        FileList = new UiListView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MaxHeight = Length.Cells(maxVisibleRows),
            SelectionMode = selectionMode,
            ItemTemplate = CreateEntryContent,
            ItemInvocation = ListItemInvocation.DoubleClick
        };
        FileListSurface = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MaxHeight = Length.Cells(maxVisibleRows.Add(_listChromeRows)),
            Children = { FileList }
        };
        _filterPicker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Items = filters.Select(static filter => (object?) filter.Name).ToArray(),
            SelectedIndex = filterIndex
        };
        HiddenToggle = new CheckBox
        {
            Text = ShowHiddenText,
            IsChecked = showHidden
        };
        StatusText = new Text(Status)
        {
            Overflow = Overflow.Ellipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        _cancelButtonStyle = InitializePartStyle(
            ButtonStyle.ForwardingDefinition,
            nameof(CancelButtonStyle));
        _showHiddenCheckBoxStyle = InitializePartStyle(
            CheckBoxStyle.ForwardingDefinition,
            nameof(ShowHiddenCheckBoxStyle));
        _fileListScrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(FileListScrollBarStyle));
        _filterScrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(FilterScrollBarStyle));
        _cancelButton = new Button
        {
            Text = CancelText,
            IsCancel = true
        };
    }

    /// <summary>Gets the canonical directory represented by the last successful snapshot.</summary>
    public string CurrentDirectory { get; private set; }

    /// <summary>Gets whether hidden entries are included in the current request.</summary>
    public bool ShowHidden { get; private set; }

    /// <summary>Gets the zero-based active filter index.</summary>
    public int FilterIndex { get; private set; }

    /// <summary>Gets whether one asynchronous directory request is outstanding.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets or sets the non-null caption for the parent-directory navigation action.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string ParentDirectoryText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => _upButton.Text = ParentDirectoryText);
        }
    } = "↑";

    /// <summary>Gets or sets the non-null placeholder shown in the empty directory path input.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string DirectoryPlaceholder
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.None,
                () => PathInput.Placeholder = DirectoryPlaceholder);
        }
    } = "Directory path";

    /// <summary>Gets or sets the non-null caption for the hidden-entry toggle.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string ShowHiddenText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => HiddenToggle.Text = ShowHiddenText);
        }
    } = "Show &hidden";

    /// <summary>Gets or sets the non-null caption for the Cancel action.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string CancelText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => _cancelButton.Text = CancelText);
        }
    } = "&Cancel";

    /// <summary>Gets or sets the non-null status text used while no request is outstanding.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string ReadyText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var previous = field;

            if (!SetProperty(ref field, value, InvalidationImpact.None))
            {
                return;
            }

            // The construction-time status is seeded from this property's default before a
            // derived dialog gets a chance to apply an authored value from its options, and no
            // later path ever re-reads ReadyText. A dialog that still shows the previous ready
            // text (nothing has loaded yet) therefore has to be re-seeded here, or the authored
            // text could never appear anywhere.
            if (string.Equals(Status, previous, StringComparison.Ordinal))
            {
                SnapshotStatus = value;
                SetStatus(value);
            }
        }
    } = "Ready";

    /// <summary>Gets or sets the non-null status text shown while a directory request is outstanding.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public string LoadingText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = "Loading…";

    /// <summary>Gets or sets the non-null folder/file count formatter used to build
    /// <see cref="Status"/> after a successful directory load.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public Func<int, int, string> CountFormat
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = DefaultCountFormat;

    /// <summary>Gets concise loading, count, selection, or recoverable-error text.</summary>
    public string Status { get; private set; }

    /// <summary>Gets the shared file list control.</summary>
    protected UiListView FileList { get; }

    /// <summary>Gets the library-owned bordered surface around the semantic file list.</summary>
    protected Dock FileListSurface { get; }

    /// <summary>Gets the shared status text control.</summary>
    protected Text StatusText { get; }

    /// <summary>Gets the shared hidden-entry toggle control.</summary>
    protected CheckBox HiddenToggle { get; }

    /// <summary>Gets the shared directory path input control.</summary>
    protected TextInput PathInput { get; }

    /// <summary>Gets the canonical path and enumeration source every navigation, typed-path, and
    /// directory-load operation in this base runs through. A derivative reaches the same source
    /// (for example, to canonicalize or test a path before completing) instead of calling
    /// <see cref="System.IO"/> directly, which would bypass the deterministic fake a test
    /// substitutes through the constructor's <c>fileSystem</c> parameter.</summary>
    protected IFilePickerFileSystem FileSystem { get; }

    /// <summary>Gets the status text last committed by a successful directory load.</summary>
    protected string SnapshotStatus { get; private set; }

    /// <summary>Gets or sets the complete local presentation applied to the Cancel Button, or null
    /// to let it use its own semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public ButtonStyle? CancelButtonStyle
    {
        get => _cancelButtonStyle.Local;
        set => _cancelButtonStyle.Local = value;
    }

    /// <summary>Gets the resolved Cancel Button style.</summary>
    public ButtonStyle ActualCancelButtonStyle => _cancelButtonStyle.Actual;

    /// <summary>Gets or sets the complete local presentation applied to the hidden-entry toggle, or
    /// null to let it use its own semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public CheckBoxStyle? ShowHiddenCheckBoxStyle
    {
        get => _showHiddenCheckBoxStyle.Local;
        set => _showHiddenCheckBoxStyle.Local = value;
    }

    /// <summary>Gets the resolved hidden-entry toggle style.</summary>
    public CheckBoxStyle ActualShowHiddenCheckBoxStyle => _showHiddenCheckBoxStyle.Actual;

    /// <summary>Gets or sets the complete local style for the file list's generated scrollbars, or
    /// null to let it use its own semantic profile.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public ScrollBarStyle? FileListScrollBarStyle
    {
        get => _fileListScrollBarStyle.Local;
        set => _fileListScrollBarStyle.Local = value;
    }

    /// <summary>Gets the resolved file-list generated-scrollbar style.</summary>
    public ScrollBarStyle ActualFileListScrollBarStyle => _fileListScrollBarStyle.Actual;

    /// <summary>Gets or sets the complete local style for the filter picker's generated scrollbar,
    /// or null to let it use its own semantic profile.</summary>
    /// <exception cref="InvalidOperationException">The attached dialog is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The dialog is disposed.</exception>
    public ScrollBarStyle? FilterScrollBarStyle
    {
        get => _filterScrollBarStyle.Local;
        set => _filterScrollBarStyle.Local = value;
    }

    /// <summary>Gets the resolved filter-picker generated-scrollbar style.</summary>
    public ScrollBarStyle ActualFilterScrollBarStyle => _filterScrollBarStyle.Actual;

    #endregion

    #region Composition

    /// <summary>Completes construction by retaining direct window content and wiring interaction handlers.</summary>
    /// <remarks>Called once by derived constructors after creating their dialog-specific controls.</remarks>
    protected void Initialize()
    {
        _rootContent = CreateContent();
        Content = _rootContent;
        ApplyDialogStyle(ResolveDialogStyle());
        BindStyle(_cancelButtonStyle, _cancelButton);
        BindStyle(_showHiddenCheckBoxStyle, HiddenToggle);
        BindStyle(_fileListScrollBarStyle, FileList, nameof(FileList.ScrollBarStyle));
        BindStyle(_filterScrollBarStyle, _filterPicker, nameof(_filterPicker.ScrollBarStyle));
        WireInteraction();
    }

    /// <summary>Creates the root grid containing the dialog layout.</summary>
    /// <returns>The non-null root grid.</returns>
    protected abstract Grid CreateContent();

    /// <summary>Resolves the concrete dialog's own complete resolved aggregate style. Called by the
    /// shared base to apply the frame-adjacent structural presentation every layout pass, since a
    /// concrete style type (<c>FilePickerDialogStyle</c>/<c>SaveFileDialogStyle</c>) is owned by the
    /// derived dialog, not this generic base. The base calls this once from <see cref="Initialize"/>
    /// and again from every <see cref="MeasureOverride(Constraint)"/> pass, so a derivative's
    /// implementation must be cheap and side-effect free - typically a single resolved-style
    /// property read, exactly as <c>FilePickerDialog</c> and <c>SaveFileDialog</c> both implement
    /// it.</summary>
    protected abstract FileDialogStyle ResolveDialogStyle();

    private void ApplyDialogStyle(FileDialogStyle style)
    {
        if (_rootContent is { } root)
        {
            root.Padding = style.RootPadding;
            root.RowSpacing = style.ContentSpacing;
        }

        FileListSurface.Border = style.FileListBorder;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        ApplyDialogStyle(ResolveDialogStyle());
        return base.MeasureOverride(constraint);
    }

    /// <summary>Creates the shared location bar containing the up button and path input.</summary>
    /// <returns>The non-null location bar grid.</returns>
    protected Grid CreateLocationBar()
    {
        var location = new Grid
        {
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        location.Columns.Add(Track.Auto());
        location.Columns.Add(Track.Star(1, minimum: Length.Cells(8)));
        Grid.SetColumn(PathInput, 1);
        location.Children.Add(_upButton);
        location.Children.Add(PathInput);
        return location;
    }

    /// <summary>Creates the shared metadata row containing the filter picker and trailing status.
    /// A derivative never needs to call this directly - <see cref="CreateFileListArea"/> already
    /// composes it directly below the bordered file list - but it stays available on its own for a
    /// derivative that wants the filter/status row without <see cref="CreateFileListArea"/>'s
    /// bordered list above it.</summary>
    /// <returns>The non-null full-width metadata grid.</returns>
    protected Grid CreateMetadata()
    {
        var metadata = new Grid
        {
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        metadata.Columns.Add(Track.Star(1, minimum: Length.Cells(8)));
        metadata.Columns.Add(Track.Star(1, minimum: Length.Cells(8)));
        StatusText.HorizontalAlignment = HorizontalAlignment.Right;
        StatusText.VerticalAlignment = VerticalAlignment.Center;
        var statusHost = new Overlay
        {
            Children = { StatusText }
        };
        Grid.SetColumn(statusHost, 1);
        metadata.Children.Add(_filterPicker);
        metadata.Children.Add(statusHost);
        return metadata;
    }

    /// <summary>Creates the bordered file list followed immediately by its filter and status row.
    /// Both <c>FilePickerDialog</c> and <c>SaveFileDialog</c> place this directly below their own
    /// <see cref="CreateLocationBar"/> row and above their own dialog-specific rows (a filename
    /// input, in <c>SaveFileDialog</c>'s case), so a derivative typically calls this once from its
    /// own <see cref="CreateContent"/> implementation the same way.</summary>
    /// <returns>The non-null full-width file-list area.</returns>
    protected Grid CreateFileListArea()
    {
        var metadata = CreateMetadata();
        var listArea = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        listArea.Columns.Add(Track.Star(1, minimum: Length.Cells(8)));
        var maximumListHeight = (int) FileListSurface.MaxHeight!.Value.Value;
        listArea.Rows.Add(Track.Star(
            1,
            minimum: Length.Cells(Math.Min(5, maximumListHeight)),
            maximum: Length.Cells(maximumListHeight)));
        listArea.Rows.Add(Track.Auto(minimum: Length.Cells(3)));
        Grid.SetRow(metadata, 1);
        listArea.Children.Add(FileListSurface);
        listArea.Children.Add(metadata);
        return listArea;
    }

    /// <summary>Creates the shared footer containing a delimiter and trailing dialog actions.</summary>
    /// <param name="acceptButton">The dialog-specific accept button (Open or Save).</param>
    /// <returns>The non-null footer grid.</returns>
    protected Grid CreateFooter(Button acceptButton)
    {
        var actions = new Grid
        {
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        actions.Columns.Add(Track.Star(1, minimum: Length.Cells(1)));
        actions.Columns.Add(Track.Auto());
        actions.Columns.Add(Track.Auto());
        Grid.SetColumn(acceptButton, 1);
        Grid.SetColumn(_cancelButton, 2);
        actions.Children.Add(acceptButton);
        actions.Children.Add(_cancelButton);
        return CreateActionBar(actions, [acceptButton, _cancelButton], out _);
    }

    private void WireInteraction()
    {
        _upButton.Click += OnUpClicked;
        PathInput.Submitted += OnPathSubmitted;
        FileList.SelectionChanged += OnSelectionChanged;
        FileList.ItemInvoked += OnItemInvoked;
        _ = FileList.AddHandler(Events.Key, OnListKey);
        _filterPicker.SelectionChanged += OnFilterChanged;
        HiddenToggle.StateChanged += OnHiddenChanged;
        _cancelButton.Click += OnCancelClicked;
        WireAcceptInteraction();
    }

    /// <summary>Wires dialog-specific accept button and input handlers.</summary>
    protected abstract void WireAcceptInteraction();

    private static Text CreateEntryContent(object? value)
    {
        var entry = value as FilePickerEntry ??
            throw new ArgumentException("A file-dialog ListView item must be a FilePickerEntry.", nameof(value));
        var prefix = entry.IsDirectory ? "▸ " : "· ";
        var suffix = entry.IsDirectory ? Path.DirectorySeparatorChar.ToString() : string.Empty;
        return new Text($"{prefix}{Text.Escape(entry.Name)}{suffix}")
        {
            Overflow = Overflow.Ellipsis,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    #endregion

    #region Interaction

    /// <summary>Returns the control to receive initial focus when the modal scope is created.</summary>
    protected abstract ControlBase GetModalFocusTarget();

    /// <summary>Returns the control to receive focus after the first successful directory load.</summary>
    protected abstract ControlBase GetInitialLoadFocusTarget();

    /// <summary>Called when the list selection changes.</summary>
    protected abstract void OnListSelectionChanged();

    /// <summary>Called when a file (non-directory) entry is invoked in the list - by Enter, a
    /// double-click, or whatever <see cref="UiListView.ItemInvocation"/> the shared <see
    /// cref="FileList"/> is configured with. A directory entry never reaches this hook: the base
    /// always treats it as a navigation target and calls <see cref="Navigate"/> instead, so a
    /// derivative implementing this hook can assume <paramref name="entry"/>.<see
    /// cref="FilePickerEntry.IsDirectory"/> is always false. <c>FilePickerDialog</c> completes the
    /// dialog with the current selection; <c>SaveFileDialog</c> populates its filename input from
    /// the entry and completes asynchronously after any overwrite confirmation.</summary>
    /// <param name="entry">The invoked file entry.</param>
    /// <param name="cause">The activation cause (keyboard or pointer).</param>
    protected abstract void OnFileItemInvoked(FilePickerEntry entry, ActivationCause cause);

    /// <summary>Called when a submitted location-input path canonicalizes to an existing directory,
    /// before that directory would otherwise be treated as a navigation target. A dialog that
    /// overrides this to accept the directory as a final selection (mirroring what a directory
    /// click followed by its own commit control would do) returns true, which skips navigation
    /// entirely. The base implementation always returns false, so <see cref="Navigate"/> runs
    /// exactly as before this hook existed - the correct behavior for a dialog with no
    /// directory-selection concept.</summary>
    /// <param name="canonicalDirectory">The canonical directory path the submitted text resolved to.</param>
    /// <returns>true if the directory was accepted as a selection; false to navigate into it as usual.</returns>
    protected virtual bool TryAcceptTypedDirectory(string canonicalDirectory)
    {
        _ = canonicalDirectory;
        return false;
    }

    private void OnUpClicked(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NavigateParent();
    }

    private void OnPathSubmitted(object? sender, SubmittedEventArgs eventArgs)
    {
        _ = sender;
        var text = eventArgs.Text;

        if (TryAcceptTypedPathAsDirectory(text))
        {
            return;
        }

        Navigate(text);
    }

    /// <summary>Canonicalizes <paramref name="path"/> and, if it names an existing directory,
    /// offers it to <see cref="TryAcceptTypedDirectory"/> instead of treating it as a navigation
    /// target. Any canonicalization failure falls through to the caller's existing
    /// <see cref="Navigate"/> fallback, which reports the same failure consistently.</summary>
    /// <param name="path">The raw submitted text.</param>
    /// <returns>true if the path was accepted as a directory selection.</returns>
    private bool TryAcceptTypedPathAsDirectory(string path)
    {
        try
        {
            var canonical = FileSystem.GetFullPath(path);
            return FileSystem.DirectoryExists(canonical) && TryAcceptTypedDirectory(canonical);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        OnListSelectionChanged();
    }

    private void OnItemInvoked(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Item is not FilePickerEntry entry)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            Navigate(entry.FullPath);
        }
        else
        {
            OnFileItemInvoked(entry, eventArgs.Cause);
        }
    }

    private void OnListKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase == RoutingPhase.Bubble &&
            eventArgs.IsInitialKeyDown &&
            eventArgs.Stroke.Code == Code.Backspace &&
            KeyboardModifierPolicy.MatchesCommand(eventArgs.Stroke.Modifiers, Modifiers.None))
        {
            NavigateParent();
            eventArgs.IsHandled = true;
        }
    }

    private void OnFilterChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var index = _filterPicker.SelectedIndex;

        if (index < 0 || index == FilterIndex)
        {
            return;
        }

        FilterIndex = index;
        NotifyPropertyChanged(nameof(FilterIndex), InvalidationImpact.None);

        if (Dispatcher is not null)
        {
            BeginLoadOrReportFailure(CurrentDirectory);
        }
    }

    private void OnHiddenChanged(object? sender, CheckChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var value = HiddenToggle.IsChecked == true;

        if (ShowHidden == value)
        {
            return;
        }

        ShowHidden = value;
        NotifyPropertyChanged(nameof(ShowHidden), InvalidationImpact.None);

        if (Dispatcher is not null)
        {
            BeginLoadOrReportFailure(CurrentDirectory);
        }
    }

    private void OnCancelClicked(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _ = Cancel();
    }

    private void NavigateParent()
    {
        if (FileSystem.GetParent(CurrentDirectory) is { } parent)
        {
            Navigate(parent);
        }
    }

    private void Navigate(string path)
    {
        try
        {
            var canonical = FileSystem.GetFullPath(path);

            if (Dispatcher is not null)
            {
                BeginLoad(canonical);
            }
            else
            {
                CurrentDirectory = canonical;
                PathInput.Text = canonical;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            SetStatus($"Cannot open directory: {exception.Message}");
        }
    }

    #endregion

    #region IsLoading lifecycle

    /// <summary>Called after a successful directory load commits entries to the list. By the time
    /// this runs, <see cref="FileList"/>.Items already holds the ordered <paramref name="entries"/>
    /// snapshot and <see cref="CurrentDirectory"/> already reflects the new directory, but <see
    /// cref="IsLoading"/> is still true. A status message set here is superseded immediately
    /// afterward: the base always follows this call by computing and publishing its own
    /// folder/file-count <see cref="Status"/> text, so this hook exists to refresh
    /// selection-dependent state rather than to own the status line. <c>FilePickerDialog</c>
    /// republishes its selection membership against the fresh entries; <c>SaveFileDialog</c> has no
    /// selection-dependent state and implements this as a no-op.</summary>
    /// <param name="entries">The committed entry snapshot.</param>
    protected abstract void OnLoadCommitted(FilePickerEntry[] entries);

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        BeginLoadOrReportFailure(CurrentDirectory);
    }

    /// <summary>Starts a directory request, degrading a synchronous file-system rejection to a
    /// recoverable status line.</summary>
    /// <remarks>
    /// <see cref="IFilePickerFileSystem.GetEntriesAsync"/> may throw synchronously for a missing,
    /// malformed, or inaccessible directory. Navigate always reported that as status text, but
    /// the hidden toggle, the filter picker, and attachment used to call the request bare, so the
    /// same rejection escaped a routed input handler and force-stopped the application instead of
    /// leaving the dialog open with an explanation.
    /// </remarks>
    /// <param name="directory">The canonical directory to request.</param>
    private void BeginLoadOrReportFailure(string directory)
    {
        try
        {
            BeginLoad(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            SetStatus($"Cannot open directory: {exception.Message}");
        }
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        CancelLoad();
        base.OnDetached();
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        CancelLoad();
        base.OnDisposing();
    }

    private void BeginLoad(string directory)
    {
        Debug.Assert(Dispatcher is not null, "An attached dialog owns a dispatcher.");
        CancelLoad();
        var lease = _loadOperation.Begin();
        var attachment = CaptureAttachment();

        try
        {
            SetLoading(true);

            if (!IsCurrentLoad(lease, attachment))
            {
                return;
            }

            SetStatus(LoadingText);

            if (!IsCurrentLoad(lease, attachment))
            {
                return;
            }

            var task = FileSystem.GetEntriesAsync(
                directory,
                _filters[FilterIndex],
                ShowHidden,
                lease.CancellationToken);
            LastLoadObservation = ObserveLoadAsync(task, attachment, directory, lease);
        }
        catch
        {
            AbortStartingLoad(lease);
            throw;
        }
    }

    /// <summary>
    /// Gets the most recently started load-observation task. Exposed only so a test can await the
    /// fire-and-forget loop directly and prove <see cref="ObjectDisposedException"/> still guards
    /// both the success and failure completion posts silently against a genuinely disposed
    /// dispatcher, instead of relying on the load's normal discard, which would turn an unguarded
    /// fault into an invisible unobserved task exception. This method runs off the dispatcher
    /// thread and is never awaited by production code, so a transiently full bounded post queue
    /// (<see cref="InvalidOperationException"/>) is no longer left to propagate out of it either -
    /// doing so would only fault this unobserved task, never reach
    /// <see cref="Dispatcher.UnhandledException"/>. It is bridged instead: the failed post is
    /// retried once with a callback whose only job is to rethrow the caught exception, so the
    /// dispatcher's own callback-failure path picks it up exactly as it would a synchronous
    /// dispatcher-callback failure. A second full queue on that retry is the deliberately accepted
    /// edge - dropped rather than retried indefinitely, leaving this task complete successfully
    /// regardless.
    /// </summary>
    internal Task? LastLoadObservation { get; private set; }

    private async Task ObserveLoadAsync(
        Task<IReadOnlyList<FilePickerEntry>> task,
        ControlAttachmentToken attachment,
        string directory,
        LatestControlOperationLease lease)
    {
        try
        {
            var entries = await task.ConfigureAwait(false);
            PostBackgroundCompletionForCurrentAttachment(
                attachment,
                () => CommitLoad(directory, entries, lease, attachment),
                () => IsCurrentLoad(lease, attachment));
        }
        catch (OperationCanceledException) when (lease.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            PostBackgroundCompletionForCurrentAttachment(
                attachment,
                () => CommitLoadFailure(exception, lease, attachment),
                () => IsCurrentLoad(lease, attachment));
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void CommitLoad(
        string directory,
        IReadOnlyList<FilePickerEntry> entries,
        LatestControlOperationLease lease,
        ControlAttachmentToken attachment)
    {
        if (!IsCurrentLoad(lease, attachment))
        {
            return;
        }

        _entries = [.. entries.OrderBy(static entry => entry, FilePickerEntryComparer.Instance)];
        FileList.Items = _entries.Cast<object?>().ToArray();
        CurrentDirectory = directory;

        // A directory name is filesystem data, not consumer input - POSIX permits every byte
        // except NUL and '/', so it can contain control characters TextInput.Text rejects.
        // Degrading to a status message matches Navigate's existing handling of the same
        // rejection, instead of letting the exception force-stop the application.
        var pathDisplayed = true;

        try
        {
            PathInput.Text = directory;
        }
        catch (ArgumentException)
        {
            pathDisplayed = false;
        }

        _upButton.IsEnabled = FileSystem.GetParent(directory) is not null;
        OnLoadCommitted(_entries);

        if (!IsCurrentLoad(lease, attachment))
        {
            return;
        }

        SetLoading(false);

        if (!IsCurrentLoad(lease, attachment))
        {
            return;
        }

        SnapshotStatus = CountStatus(_entries);
        SetStatus(pathDisplayed ? SnapshotStatus : "Cannot display this directory's name. " + SnapshotStatus);

        if (!IsCurrentLoad(lease, attachment))
        {
            return;
        }

        NotifyPropertyChanged(nameof(CurrentDirectory), InvalidationImpact.None);

        if (!IsCurrentLoad(lease, attachment))
        {
            return;
        }

        if (_initialFocusPending)
        {
            _initialFocusPending = false;

            if (PathInput.IsFocused)
            {
                _ = FocusOwner?.Focus(GetInitialLoadFocusTarget());
            }
        }

        _ = _loadOperation.TryComplete(lease);
    }

    private void CommitLoadFailure(
        Exception exception,
        LatestControlOperationLease lease,
        ControlAttachmentToken attachment)
    {
        if (!IsCurrentLoad(lease, attachment))
        {
            return;
        }

        SetLoading(false);

        if (!IsCurrentLoad(lease, attachment))
        {
            return;
        }

        SetStatus($"Cannot open directory: {exception.Message}");
        _ = _loadOperation.TryComplete(lease);
    }

    private bool IsCurrentLoad(
        LatestControlOperationLease lease,
        ControlAttachmentToken attachment) =>
        !IsDisposed &&
        _loadOperation.IsCurrent(lease) &&
        IsCurrentAttachment(attachment);

    private void AbortStartingLoad(LatestControlOperationLease lease)
    {
        if (!_loadOperation.TryAbort(lease))
        {
            return;
        }

        // The rejected request has already published IsLoading as true, so the reset must be
        // published as well: a silent field write leaves every IsLoading observer - a binding or a
        // busy indicator - stuck on "loading" after the status line has reported the failure. A
        // dialog disposed by one of the notifications reached above cannot publish anything more.
        if (IsDisposed)
        {
            IsLoading = false;
            return;
        }

        SetLoading(false);
    }

    private void CancelLoad()
    {
        try
        {
            _loadOperation.Cancel();
        }
        catch (Exception)
        {
            // Swallowed, not reported: a consumer-registered cancellation callback threw, and
            // that must not abort the caller's own cleanup or skip the fresh Begin() that
            // immediately follows this call in BeginLoad.
        }
    }

    private void SetLoading(bool value)
    {
        IsLoading = value;
        NotifyPropertyChanged(nameof(IsLoading), InvalidationImpact.None);
    }

    /// <summary>Sets the concise status text.</summary>
    /// <param name="value">The non-null status message.</param>
    protected void SetStatus(string value)
    {
        Status = value;
        StatusText.Content = value;
        NotifyPropertyChanged(nameof(Status), InvalidationImpact.None);
    }

    private string CountStatus(FilePickerEntry[] entries)
    {
        var folders = entries.Count(static entry => entry.IsDirectory);
        var files = entries.Length - folders;
        return CountFormat(folders, files);
    }

    private static string DefaultCountFormat(int folders, int files) =>
        $"{folders} {(folders == 1 ? "folder" : "folders")} · " +
        $"{files} {(files == 1 ? "file" : "files")}";

    #endregion
}
