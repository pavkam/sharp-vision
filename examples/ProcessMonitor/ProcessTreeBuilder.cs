// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>Arranges one flat <c>ps</c> snapshot into parent/child trees.</summary>
/// <remarks>
/// A process is a root exactly when its reported parent PID does not itself appear in the same
/// snapshot - covering both PID 1 (whose real parent is the kernel, never listed) and any process
/// whose true parent already exited between the parent and child rows being read, which briefly
/// re-parents the child to init/launchd on every Unix but can still race a single <c>ps</c> sample.
/// Treating an unresolvable parent as "this is a root" rather than discarding the process keeps
/// every visible PID reachable somewhere in the forest, matching what a real monitor promises: it
/// never silently drops a running process from view.
/// </remarks>
internal static class ProcessTreeBuilder
{
    /// <summary>Builds one forest of process trees from a flat snapshot.</summary>
    /// <param name="samples">The non-null flat process list for one sampling tick.</param>
    /// <returns>The root nodes, in ascending PID order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="samples"/> is null.</exception>
    internal static IReadOnlyList<ProcessNode> Build(IReadOnlyList<ProcessSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var nodesByPid = new Dictionary<int, ProcessNode>(samples.Count);

        foreach (var sample in samples)
        {
            // A duplicate PID is nonsensical for a single ps sample, but a corrupted or unusually
            // formatted line elsewhere in the same output must not take an otherwise-good sibling
            // down with it, so the first occurrence wins and later ones are ignored.
            _ = nodesByPid.TryAdd(sample.Pid, new ProcessNode(sample));
        }

        var roots = new List<ProcessNode>();

        foreach (var node in nodesByPid.Values)
        {
            if (node.Sample.ParentPid != node.Sample.Pid &&
                nodesByPid.TryGetValue(node.Sample.ParentPid, out var parent))
            {
                parent.AddChild(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        roots.Sort(static (left, right) => left.Sample.Pid.CompareTo(right.Sample.Pid));

        foreach (var node in nodesByPid.Values)
        {
            node.SortChildren();
        }

        return roots;
    }
}
