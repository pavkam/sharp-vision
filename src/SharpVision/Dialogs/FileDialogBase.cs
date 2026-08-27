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
    private CancellationTokenSource? _loadCancellation;
    private long _loadGeneration;
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
    private protected FileDialogBase(
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
        MaxWidth = 96;
        MaxHeight = maxVisibleRows.Add(nonListWindowRows);
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
            MaxHeight = maxVisibleRows,
            SelectionMode = selectionMode,
            ItemTemplate = CreateEntryContent,
            ItemInvocation = ListItemInvocation.DoubleClick
        };
        FileListSurface = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MaxHeight = maxVisibleRows.Add(_listChromeRows),
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
            _ = SetProperty(ref field, value, InvalidationImpact.None);
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

    /// <summary>Gets the filesystem abstraction.</summary>
    private protected IFilePickerFileSystem FileSystem { get; }

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
    /// derived dialog, not this generic base.</summary>
    private protected abstract FileDialogStyle ResolveDialogStyle();

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
        location.Columns.Add(Track.Star(1, minimum: 8));
        Grid.SetColumn(PathInput, 1);
        location.Children.Add(_upButton);
        location.Children.Add(PathInput);
        return location;
    }

    /// <summary>Creates the shared metadata row containing the filter picker and trailing status.</summary>
    /// <returns>The non-null full-width metadata grid.</returns>
    private protected Grid CreateMetadata()
    {
        var metadata = new Grid
        {
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        metadata.Columns.Add(Track.Star(1, minimum: 8));
        metadata.Columns.Add(Track.Star(1, minimum: 8));
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

    /// <summary>Creates the bordered file list followed immediately by its filter and status row.</summary>
    /// <returns>The non-null full-width file-list area.</returns>
    private protected Grid CreateFileListArea()
    {
        var metadata = CreateMetadata();
        var listArea = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        listArea.Columns.Add(Track.Star(1, minimum: 8));
        listArea.Rows.Add(Track.Star(
            1,
            minimum: Math.Min(5, FileListSurface.MaxHeight),
            maximum: FileListSurface.MaxHeight));
        listArea.Rows.Add(Track.Auto(minimum: 3));
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
        actions.Columns.Add(Track.Star(1, minimum: 1));
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

    /// <summary>Called when a file (non-directory) entry is invoked in the list.</summary>
    /// <param name="entry">The invoked file entry.</param>
    /// <param name="cause">The activation cause (keyboard or pointer).</param>
    private protected abstract void OnFileItemInvoked(FilePickerEntry entry, ActivationCause cause);

    /// <summary>Called when a submitted location-input path canonicalizes to an existing directory,
    /// before that directory would otherwise be treated as a navigation target. A dialog that
    /// overrides this to accept the directory as a final selection (mirroring what a directory
    /// click followed by its own commit control would do) returns true, which skips navigation
    /// entirely. The base implementation always returns false, so <see cref="Navigate"/> runs
    /// exactly as before this hook existed - the correct behavior for a dialog with no
    /// directory-selection concept.</summary>
    /// <param name="canonicalDirectory">The canonical directory path the submitted text resolved to.</param>
    /// <returns>true if the directory was accepted as a selection; false to navigate into it as usual.</returns>
    private protected virtual bool TryAcceptTypedDirectory(string canonicalDirectory)
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
            BeginLoad(CurrentDirectory);
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
            BeginLoad(CurrentDirectory);
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

    /// <summary>Called after a successful directory load commits entries to the list.</summary>
    /// <param name="entries">The committed entry snapshot.</param>
    private protected abstract void OnLoadCommitted(FilePickerEntry[] entries);

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        BeginLoad(CurrentDirectory);
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
        var generation = ++_loadGeneration;
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        var dispatcher = Dispatcher;

        try
        {
            SetLoading(true);

            if (!IsCurrentLoad(generation, cancellation, dispatcher))
            {
                return;
            }

            SetStatus(LoadingText);

            if (!IsCurrentLoad(generation, cancellation, dispatcher))
            {
                return;
            }

            var task = FileSystem.GetEntriesAsync(
                directory,
                _filters[FilterIndex],
                ShowHidden,
                cancellation.Token);
            LastLoadObservation = ObserveLoadAsync(task, dispatcher, directory, generation, cancellation.Token);
        }
        catch
        {
            AbortStartingLoad(generation, cancellation);
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

    /// <summary>Posts <paramref name="action"/>; a full bounded queue
    /// (<see cref="InvalidOperationException"/>) is bridged into the dispatcher's own
    /// callback-failure path by re-posting a callback that rethrows the caught exception, so a
    /// failure originating off the dispatcher thread is reported exactly like one thrown by a
    /// callback already running on it. A second full queue on that retry, or a disposed dispatcher
    /// at either attempt, is dropped silently.</summary>
    /// <param name="dispatcher">The target dispatcher.</param>
    /// <param name="action">The callback to post.</param>
    private void PostOrReportFault(Dispatcher dispatcher, Action action)
    {
        try
        {
            dispatcher.Post(action);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException exception)
        {
            PostRetryHookForTests?.Invoke();

            try
            {
                dispatcher.Post(() => throw exception);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    /// <summary>
    /// Test-only synchronization seam. When set, invoked once by <see cref="PostOrReportFault"/>
    /// immediately after a first <see cref="Dispatcher.Post(Action)"/> attempt is rejected for a
    /// full queue, but before the bridging retry attempt - letting a test deterministically free
    /// the queue slot the retry needs in the otherwise nanosecond-wide window between the two
    /// attempts, rather than racing a genuine drain. Instance-scoped, like the analogous
    /// <c>TreeViewItem.PostRetryHookForTests</c> seam, so parallel tests on different dialogs
    /// cannot interfere with each other.
    /// </summary>
    internal Action? PostRetryHookForTests { get; set; }

    private async Task ObserveLoadAsync(
        Task<IReadOnlyList<FilePickerEntry>> task,
        Dispatcher dispatcher,
        string directory,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await task.ConfigureAwait(false);
            PostOrReportFault(dispatcher, () => CommitLoad(directory, entries, generation));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            PostOrReportFault(dispatcher, () => CommitLoadFailure(exception, generation));
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void CommitLoad(string directory, IReadOnlyList<FilePickerEntry> entries, long generation)
    {
        if (generation != _loadGeneration || IsDisposed || Dispatcher is null)
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

        if (!IsCurrentLoad(generation, _loadCancellation, Dispatcher))
        {
            return;
        }

        ReleaseCompletedLoad();
        SetLoading(false);

        if (generation != _loadGeneration || IsDisposed || Dispatcher is null)
        {
            return;
        }

        SnapshotStatus = CountStatus(_entries);
        SetStatus(pathDisplayed ? SnapshotStatus : "Cannot display this directory's name. " + SnapshotStatus);

        if (generation != _loadGeneration || IsDisposed || Dispatcher is null)
        {
            return;
        }

        NotifyPropertyChanged(nameof(CurrentDirectory), InvalidationImpact.None);

        if (generation != _loadGeneration || IsDisposed || Dispatcher is null)
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
    }

    private void CommitLoadFailure(Exception exception, long generation)
    {
        if (generation != _loadGeneration || IsDisposed || Dispatcher is null)
        {
            return;
        }

        ReleaseCompletedLoad();
        SetLoading(false);

        if (generation != _loadGeneration || IsDisposed || Dispatcher is null)
        {
            return;
        }

        SetStatus($"Cannot open directory: {exception.Message}");
    }

    private bool IsCurrentLoad(
        long generation,
        CancellationTokenSource? cancellation,
        Dispatcher? dispatcher) =>
        generation == _loadGeneration &&
        cancellation is not null &&
        ReferenceEquals(_loadCancellation, cancellation) &&
        !IsDisposed &&
        dispatcher is not null &&
        ReferenceEquals(Dispatcher, dispatcher);

    private void AbortStartingLoad(long generation, CancellationTokenSource cancellation)
    {
        if (generation != _loadGeneration || !ReferenceEquals(_loadCancellation, cancellation))
        {
            return;
        }

        _loadGeneration++;
        _loadCancellation = null;
        IsLoading = false;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void CancelLoad()
    {
        _loadGeneration++;
        var cancellation = _loadCancellation;
        _loadCancellation = null;

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void ReleaseCompletedLoad()
    {
        _loadCancellation?.Dispose();
        _loadCancellation = null;
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
