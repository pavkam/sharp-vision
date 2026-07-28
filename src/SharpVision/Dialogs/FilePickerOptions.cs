// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

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

    /// <summary>Creates an independent configuration snapshot for one dialog.</summary>
    /// <returns>A new options object with equivalent owned values.</returns>
    internal FilePickerOptions Copy() => new()
    {
        Title = Title,
        InitialDirectory = InitialDirectory,
        AllowMultiple = AllowMultiple,
        ShowHidden = ShowHidden,
        MaxVisibleRows = MaxVisibleRows,
        Filters = Filters,
        FilterIndex = FilterIndex
    };
}
