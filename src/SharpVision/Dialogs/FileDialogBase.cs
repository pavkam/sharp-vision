// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using Controls.Collections;

using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
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

    private FilePickerEntry[] _entries = [];
    private CancellationTokenSource? _loadCancellation;
    private long _loadGeneration;
    private bool _initialFocusPending = true;

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
        MaxHeight = LayoutMath.Add(maxVisibleRows, nonListWindowRows);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        CurrentDirectory = FileSystem.GetFullPath(initialDirectory);
        ShowHidden = showHidden;
        FilterIndex = filterIndex;

        _upButton = new Button
        {
            Content = new Text("↑"),
            Width = Length.Cells(3)
        };
        PathInput = new TextInput
        {
            Text = CurrentDirectory,
            Placeholder = "Directory path",
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        FileList = new UiListView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MaxHeight = maxVisibleRows,
            SelectionMode = selectionMode,
            ItemTemplate = CreateEntryContent
        };
        FileListSurface = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MaxHeight = LayoutMath.Add(maxVisibleRows, _listChromeRows),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
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
            Content = new Text("Show &hidden"),
            IsChecked = showHidden
        };
        StatusText = new Text(Status)
        {
            Overflow = Overflow.Ellipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        _cancelButton = new Button
        {
            Content = new Text("&Cancel"),
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

    /// <summary>Gets concise loading, count, selection, or recoverable-error text.</summary>
    public string Status { get; private set; } = "Ready";

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
    protected string SnapshotStatus { get; private set; } = "Ready";

    #endregion

    #region Composition

    /// <summary>Completes construction by retaining direct window content and wiring interaction handlers.</summary>
    /// <remarks>Called once by derived constructors after creating their dialog-specific controls.</remarks>
    protected void Initialize()
    {
        Content = CreateContent();
        WireInteraction();
    }

    /// <summary>Creates the root grid containing the dialog layout.</summary>
    /// <returns>The non-null root grid.</returns>
    protected abstract Grid CreateContent();

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

    /// <summary>Creates the shared footer grid containing the filter picker, accept button, and cancel button.</summary>
    /// <param name="acceptButton">The dialog-specific accept button (Open or Save).</param>
    /// <returns>The non-null footer grid.</returns>
    protected Grid CreateFooter(Button acceptButton)
    {
        var footer = new Grid
        {
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        footer.Columns.Add(Track.Auto());
        footer.Columns.Add(Track.Star(1, minimum: 1));
        footer.Columns.Add(Track.Auto());
        footer.Columns.Add(Track.Auto());
        footer.Rows.Add(Track.Auto(minimum: 1));
        footer.Rows.Add(Track.Auto(minimum: 1));
        Grid.SetColumn(acceptButton, 2);
        Grid.SetColumn(_cancelButton, 3);
        Grid.SetRow(acceptButton, 1);
        Grid.SetRow(_cancelButton, 1);
        footer.Children.Add(_filterPicker);
        footer.Children.Add(acceptButton);
        footer.Children.Add(_cancelButton);
        return footer;
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
    protected abstract Control GetModalFocusTarget();

    /// <summary>Returns the control to receive focus after the first successful directory load.</summary>
    protected abstract Control GetInitialLoadFocusTarget();

    /// <summary>Called when the list selection changes.</summary>
    protected abstract void OnListSelectionChanged();

    /// <summary>Called when a file (non-directory) entry is invoked in the list.</summary>
    /// <param name="entry">The invoked file entry.</param>
    /// <param name="cause">The activation cause (keyboard or pointer).</param>
    private protected abstract void OnFileItemInvoked(FilePickerEntry entry, ActivationCause cause);

    private void OnUpClicked(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NavigateParent();
    }

    private void OnPathSubmitted(object? sender, SubmittedEventArgs eventArgs)
    {
        _ = sender;
        Navigate(eventArgs.Text);
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
            eventArgs.Stroke.Action == KeyAction.Press &&
            eventArgs.Stroke.Code == Code.Backspace)
        {
            NavigateParent();
            eventArgs.Handled = true;
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

    #region Loading lifecycle

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
        SetLoading(true);
        SetStatus("Loading…");
        var dispatcher = Dispatcher;
        var task = FileSystem.GetEntriesAsync(
            directory,
            _filters[FilterIndex],
            ShowHidden,
            cancellation.Token);
        _ = ObserveLoadAsync(task, dispatcher, directory, generation, cancellation.Token);
    }

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
            dispatcher.Post(() => CommitLoad(directory, entries, generation));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            try
            {
                dispatcher.Post(() => CommitLoadFailure(exception, generation));
            }
            catch (ObjectDisposedException)
            {
            }
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
        // rejection, instead of letting the exception force-stop the application (see #219).
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
        SetLoading(false);
        SnapshotStatus = CountStatus(_entries);
        SetStatus(pathDisplayed ? SnapshotStatus : "Cannot display this directory's name. " + SnapshotStatus);
        NotifyPropertyChanged(nameof(CurrentDirectory), InvalidationImpact.None);

        if (_initialFocusPending)
        {
            _initialFocusPending = false;

            if (PathInput.IsFocused)
            {
                _ = FocusOwner?.Focus(GetInitialLoadFocusTarget());
            }
        }

        ReleaseCompletedLoad();
    }

    private void CommitLoadFailure(Exception exception, long generation)
    {
        if (generation != _loadGeneration || IsDisposed || Dispatcher is null)
        {
            return;
        }

        SetLoading(false);
        SetStatus($"Cannot open directory: {exception.Message}");
        ReleaseCompletedLoad();
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

    private static string CountStatus(FilePickerEntry[] entries)
    {
        var folders = entries.Count(static entry => entry.IsDirectory);
        var files = entries.Length - folders;
        return $"{folders} {(folders == 1 ? "folder" : "folders")} · " +
               $"{files} {(files == 1 ? "file" : "files")}";
    }

    #endregion
}
