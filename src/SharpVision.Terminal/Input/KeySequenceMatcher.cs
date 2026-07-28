// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;
/// <summary>Incrementally applies bounded longest-match decoding to non-signature key strings.</summary>
internal sealed class KeySequenceMatcher
{
    private IReadOnlyList<KeyBinding>? _bindings;
    private KeySequenceNode[]? _nodes;
    private KeySequenceEdge[]? _edges;
    private byte[]? _pending;
    private int _lastBinding = -1;
    private int _lastLength;
    private int _length;
    private int _node;

    /// <summary>Builds a finite trie from non-empty, conflict-free fallback bindings.</summary>
    /// <param name="bindings">The immutable fallback binding snapshot.</param>
    public KeySequenceMatcher(IReadOnlyList<KeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        _bindings = bindings;
        var nodes = new List<KeySequenceNode> { new() };
        var edges = new List<KeySequenceEdge>();
        var maximumLength = 0;

        for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            var sequence = bindings[bindingIndex].Sequence.Span;
            maximumLength = Math.Max(maximumLength, sequence.Length);
            var node = 0;

            foreach (var value in sequence)
            {
                var child = FindChild(nodes, edges, node, value);

                if (child < 0)
                {
                    child = nodes.Count;
                    nodes.Add(new KeySequenceNode());
                    var parent = nodes[node];
                    edges.Add(new KeySequenceEdge(value, child, parent.FirstEdge));
                    parent.FirstEdge = edges.Count - 1;
                    nodes[node] = parent;
                }

                node = child;
            }

            var terminal = nodes[node];
            terminal.BindingIndex = bindingIndex;
            nodes[node] = terminal;
        }

        _nodes = [.. nodes];
        _edges = [.. edges];
        _pending = new byte[maximumLength];
    }

    /// <summary>Gets whether bytes are retained as a possible key prefix.</summary>
    public bool IsPending => _length > 0;

    /// <summary>Gets the maximum retained candidate and replay byte count.</summary>
    public int MaximumLength => _pending?.Length ?? 0;

    /// <summary>Gets whether the matcher retains bindings or owned arrays.</summary>
    public bool RetainsStorage =>
        _bindings is not null ||
        _nodes is not null ||
        _edges is not null ||
        _pending is not null;

    /// <summary>Consumes one byte and returns a match or an exact replay slice.</summary>
    /// <param name="value">The next transport byte.</param>
    /// <param name="binding">The longest completed binding, when present.</param>
    /// <param name="replayOffset">The first retained byte not consumed by a match.</param>
    /// <param name="replayLength">The exact retained byte count to replay.</param>
    /// <returns>The incremental match status.</returns>
    public KeySequenceMatchStatus Add(
        byte value,
        out KeyBinding binding,
        out int replayOffset,
        out int replayLength)
    {
        ThrowIfDisposed();
        var pending = _pending;
        var bindings = _bindings;
        var nodes = _nodes;
        Debug.Assert(pending is not null, "An active matcher owns pending-byte storage.");
        Debug.Assert(bindings is not null, "An active matcher retains its binding snapshot.");
        Debug.Assert(nodes is not null, "An active matcher owns trie nodes.");
        Debug.Assert(pending.Length > 0, "A matcher exists only for a non-empty fallback map.");
        Debug.Assert(_length < pending.Length, "A key prefix never exceeds the longest described key.");

        pending[_length++] = value;

        var child = FindChild(_node, value);

        if (child >= 0)
        {
            _node = child;
            var terminal = nodes[_node].BindingIndex;

            if (terminal >= 0)
            {
                _lastBinding = terminal;
                _lastLength = _length;
            }

            if (nodes[_node].FirstEdge >= 0)
            {
                binding = default;
                replayOffset = 0;
                replayLength = 0;
                return KeySequenceMatchStatus.Pending;
            }

            Debug.Assert(_lastBinding >= 0, "A trie leaf always terminates one binding.");
            binding = bindings[_lastBinding];
            replayOffset = _lastLength;
            replayLength = _length - _lastLength;
            Reset();
            return KeySequenceMatchStatus.Match;
        }

        if (_lastBinding >= 0)
        {
            binding = bindings[_lastBinding];
            replayOffset = _lastLength;
            replayLength = _length - _lastLength;
            Reset();
            return KeySequenceMatchStatus.Match;
        }

        binding = default;
        replayOffset = 0;
        replayLength = _length;
        Reset();
        return KeySequenceMatchStatus.Replay;
    }

    /// <summary>Completes a retained prefix as its longest key or exact replay.</summary>
    /// <param name="binding">The longest completed binding, when present.</param>
    /// <param name="replayOffset">The first retained byte not consumed by a match.</param>
    /// <param name="replayLength">The exact retained byte count to replay.</param>
    /// <returns>The completion result.</returns>
    public KeySequenceMatchStatus Complete(
        out KeyBinding binding,
        out int replayOffset,
        out int replayLength)
    {
        ThrowIfDisposed();
        Debug.Assert(IsPending, "Only a retained prefix can be completed.");
        var bindings = _bindings;
        Debug.Assert(bindings is not null, "An active matcher retains its binding snapshot.");

        if (_lastBinding >= 0)
        {
            binding = bindings[_lastBinding];
            replayOffset = _lastLength;
            replayLength = _length - _lastLength;
            Reset();
            return KeySequenceMatchStatus.Match;
        }

        binding = default;
        replayOffset = 0;
        replayLength = _length;
        Reset();
        return KeySequenceMatchStatus.Replay;
    }

    /// <summary>Gets the exact retained replay bytes until the next matcher operation.</summary>
    /// <param name="offset">The validated replay offset.</param>
    /// <param name="length">The validated replay count.</param>
    /// <returns>The borrowed replay span.</returns>
    /// <exception cref="ObjectDisposedException">The matcher is disposed.</exception>
    public ReadOnlySpan<byte> Replay(int offset, int length)
    {
        ThrowIfDisposed();
        return _pending.AsSpan(offset, length);
    }

    /// <summary>Clears retained bytes and releases the binding snapshot and owned arrays.</summary>
    public void Dispose()
    {
        if (!RetainsStorage)
        {
            return;
        }

        _pending.AsSpan().Clear();
        Reset();
        _bindings = null;
        _nodes = null;
        _edges = null;
        _pending = null;
    }

    private void Reset()
    {
        _node = 0;
        _length = 0;
        _lastBinding = -1;
        _lastLength = 0;
    }

    private int FindChild(int node, byte value)
    {
        var nodes = _nodes;
        var edges = _edges;
        Debug.Assert(nodes is not null, "An active matcher owns trie nodes.");
        Debug.Assert(edges is not null, "An active matcher owns trie edges.");

        for (var edge = nodes[node].FirstEdge; edge >= 0; edge = edges[edge].Next)
        {
            if (edges[edge].Value == value)
            {
                return edges[edge].Child;
            }
        }

        return -1;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(!RetainsStorage, this);

    private static int FindChild(
        List<KeySequenceNode> nodes,
        List<KeySequenceEdge> edges,
        int node,
        byte value)
    {
        for (var edge = nodes[node].FirstEdge; edge >= 0; edge = edges[edge].Next)
        {
            if (edges[edge].Value == value)
            {
                return edges[edge].Child;
            }
        }

        return -1;
    }
}
