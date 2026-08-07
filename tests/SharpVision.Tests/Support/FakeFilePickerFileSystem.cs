// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides deterministic in-memory immediate-child snapshots to mounted file-picker tests.</summary>
internal sealed class FakeFilePickerFileSystem: IFilePickerFileSystem
{
    private readonly Dictionary<string, FilePickerEntry[]> _directories =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<Func<Task<IReadOnlyList<FilePickerEntry>>>>> _next =
        new(StringComparer.Ordinal);

    /// <summary>Adds or replaces one canonical directory snapshot.</summary>
    /// <param name="path">The non-null, non-blank directory path.</param>
    /// <param name="entries">The non-null entry snapshot.</param>
    internal void AddDirectory(string path, params FilePickerEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(entries);
        _directories[GetFullPath(path)] = [.. entries];
    }

    /// <summary>Defers the next request for one directory until the returned completion is resolved.</summary>
    /// <param name="path">The non-null, non-blank directory path.</param>
    /// <returns>The controllable entry completion; it deliberately ignores request cancellation to test stale guards.</returns>
    internal TaskCompletionSource<IReadOnlyList<FilePickerEntry>> DeferNext(string path)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<FilePickerEntry>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(path, () => completion.Task);
        return completion;
    }

    /// <summary>Fails the next request for one directory with the supplied recoverable exception.</summary>
    /// <param name="path">The non-null, non-blank directory path.</param>
    /// <param name="exception">The non-null exception to observe.</param>
    internal void FailNext(string path, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Enqueue(path, () => Task.FromException<IReadOnlyList<FilePickerEntry>>(exception));
    }

    /// <inheritdoc/>
    public bool FileExists(string path)
    {
        var canonical = GetFullPath(path);

        foreach (var entries in _directories.Values)
        {
            foreach (var entry in entries)
            {
                if (!entry.Directory &&
                    string.Equals(entry.FullPath, canonical, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => _directories.ContainsKey(GetFullPath(path));

    /// <inheritdoc/>
    public string GetFullPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("A fake filesystem path cannot be blank.", nameof(path))
            : Path.GetFullPath(path);
    }

    /// <inheritdoc/>
    public string? GetParent(string path) => Directory.GetParent(GetFullPath(path))?.FullName;

    /// <inheritdoc/>
    public Task<IReadOnlyList<FilePickerEntry>> GetEntriesAsync(
        string directory,
        FilePickerFilter filter,
        bool showHidden,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(filter);
        cancellationToken.ThrowIfCancellationRequested();
        var canonical = GetFullPath(directory);

        if (_next.TryGetValue(canonical, out var requests) && requests.TryDequeue(out var request))
        {
            return request();
        }

        if (!_directories.TryGetValue(canonical, out var entries))
        {
            throw new DirectoryNotFoundException($"Fake directory '{canonical}' does not exist.");
        }

        IReadOnlyList<FilePickerEntry> result =
        [
            .. entries.Where(entry =>
                (showHidden || !entry.Hidden) &&
                (entry.Directory || filter.Matches(entry.Name)))
        ];
        return Task.FromResult(result);
    }

    private void Enqueue(string path, Func<Task<IReadOnlyList<FilePickerEntry>>> request)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(request);
        var canonical = GetFullPath(path);

        if (!_next.TryGetValue(canonical, out var requests))
        {
            requests = new Queue<Func<Task<IReadOnlyList<FilePickerEntry>>>>();
            _next.Add(canonical, requests);
        }

        requests.Enqueue(request);
    }
}
