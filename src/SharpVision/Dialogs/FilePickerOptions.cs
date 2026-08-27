// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using SharpVision.Controls.Input;
using SharpVision.Controls.Scrolling;

/// <summary>Configures one file-picker presentation before its retained dialog is constructed.</summary>
[PublicAPI]
public sealed class FilePickerOptions
{
    /// <summary>Gets or sets the non-null, non-blank dialog title.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value is blank.</exception>
    public string Title
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The file-picker title cannot be blank.", nameof(value));
            }

            field = value;
        }
    } = "Open File";

    /// <summary>Gets or sets the non-null, non-blank initial directory path.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value is blank.</exception>
    public string InitialDirectory
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The initial directory cannot be blank.", nameof(value));
            }

            field = value;
        }
    } = Environment.CurrentDirectory;

    /// <summary>Gets or sets whether the picker permits more than one selected file.</summary>
    public bool AllowMultiple { get; set; }

    /// <summary>Gets or sets which entry kinds the picker accepts into its final selection. The
    /// default, <see cref="FileSelectionMode.Files"/>, matches the picker's behavior before this
    /// mode existed: a directory is always a navigation target and never a final selection.
    /// Navigation-on-invoke (double-click or Enter on a directory) still navigates into it in
    /// every mode.</summary>
    public FileSelectionMode SelectionMode { get; set; } = FileSelectionMode.Files;

    /// <summary>Gets or sets whether hidden entries are initially listed.</summary>
    public bool ShowHidden { get; set; }

    /// <summary>Gets or sets the positive maximum number of visible file-list content rows.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxVisibleRows
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 20;

    /// <summary>Gets or sets an owned non-empty snapshot of non-null filters.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">
    /// The value is empty, contains null, or cannot retain the current <see cref="FilterIndex"/>.
    /// </exception>
    public IReadOnlyList<FilePickerFilter> Filters
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Count == 0)
            {
                throw new ArgumentException("A file picker requires at least one filter.", nameof(value));
            }

            var copy = new FilePickerFilter[value.Count];

            for (var index = 0; index < value.Count; index++)
            {
                copy[index] = value[index] ??
                    throw new ArgumentException("File picker filters cannot contain null.", nameof(value));
            }

            if (FilterIndex >= copy.Length)
            {
                throw new ArgumentException("The filters do not contain the current filter index.", nameof(value));
            }

            field = Array.AsReadOnly(copy);
        }
    } = Array.AsReadOnly([FilePickerFilter.AllFiles]);

    /// <summary>Gets or sets the zero-based initially active filter index.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside <see cref="Filters"/>.</exception>
    public int FilterIndex
    {
        get;
        set
        {
            if ((uint) value >= (uint) Filters.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The filter index is outside Filters.");
            }

            field = value;
        }
    }

    /// <summary>Gets or sets the complete local presentation applied to the dialog's Cancel Button,
    /// or null to let it use its own semantic input profile.</summary>
    public ButtonStyle? CancelButtonStyle { get; set; }

    /// <summary>Gets or sets the complete local presentation applied to the Show-hidden toggle, or
    /// null to let it use its own semantic input profile.</summary>
    public CheckBoxStyle? ShowHiddenCheckBoxStyle { get; set; }

    /// <summary>Gets or sets the complete local style for the file list's generated scrollbars, or
    /// null to let it use its own semantic profile.</summary>
    public ScrollBarStyle? FileListScrollBarStyle { get; set; }

    /// <summary>Gets or sets the complete local style for the filter picker's generated scrollbar,
    /// or null to let it use its own semantic profile.</summary>
    public ScrollBarStyle? FilterScrollBarStyle { get; set; }

    /// <summary>Gets or sets the complete local presentation applied to the Open Button, or null to
    /// let it use its own semantic input profile.</summary>
    public ButtonStyle? OpenButtonStyle { get; set; }

    /// <summary>Gets or sets the complete local aggregate presentation applied to the dialog's frame
    /// and structural geometry, or null to let the active Theme own it.</summary>
    public FilePickerDialogStyle? Style { get; set; }

    /// <summary>Gets or sets the non-null caption for the parent-directory navigation action.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string ParentDirectoryText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = "↑";

    /// <summary>Gets or sets the non-null placeholder shown in the empty directory path input.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string DirectoryPlaceholder
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = "Directory path";

    /// <summary>Gets or sets the non-null caption for the hidden-entry toggle.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string ShowHiddenText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = "Show &hidden";

    /// <summary>Gets or sets the non-null caption for the Cancel action.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string CancelText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = "&Cancel";

    /// <summary>Gets or sets the non-null caption for the Open action.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string OpenText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = "&Open";

    /// <summary>Gets or sets the status text used while no request is outstanding, or null to let
    /// the dialog use its own default.</summary>
    public string? ReadyText { get; set; }

    /// <summary>Gets or sets the status text shown while a directory request is outstanding, or
    /// null to let the dialog use its own default.</summary>
    public string? LoadingText { get; set; }

    /// <summary>Gets or sets the folder/file count formatter used to build the status text after a
    /// successful directory load, or null to let the dialog use its own default.</summary>
    public Func<int, int, string>? CountFormat { get; set; }

    /// <summary>Gets or sets the selected-file-count formatter used to build the status text while
    /// at least one file is selected, or null to let the dialog use its own default.</summary>
    public Func<int, string>? SelectionFormat { get; set; }

    /// <summary>Creates an independent configuration snapshot for one dialog.</summary>
    /// <returns>A new options object with equivalent owned values.</returns>
    internal FilePickerOptions Copy() => new()
    {
        Title = Title,
        InitialDirectory = InitialDirectory,
        AllowMultiple = AllowMultiple,
        SelectionMode = SelectionMode,
        ShowHidden = ShowHidden,
        MaxVisibleRows = MaxVisibleRows,
        Filters = Filters,
        FilterIndex = FilterIndex,
        CancelButtonStyle = CancelButtonStyle,
        ShowHiddenCheckBoxStyle = ShowHiddenCheckBoxStyle,
        FileListScrollBarStyle = FileListScrollBarStyle,
        FilterScrollBarStyle = FilterScrollBarStyle,
        OpenButtonStyle = OpenButtonStyle,
        Style = Style,
        ParentDirectoryText = ParentDirectoryText,
        DirectoryPlaceholder = DirectoryPlaceholder,
        ShowHiddenText = ShowHiddenText,
        CancelText = CancelText,
        OpenText = OpenText,
        ReadyText = ReadyText,
        LoadingText = LoadingText,
        CountFormat = CountFormat,
        SelectionFormat = SelectionFormat
    };
}
