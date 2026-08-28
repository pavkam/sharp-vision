// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

using System.Runtime.ExceptionServices;

/// <summary>Owns the live bindings registered against one retained control.</summary>
internal sealed class ControlBindingRegistry
{
    private readonly List<Binding> _bindings = [];
    private int _targetUpdateDepth;

    /// <summary>Gets whether an items binding is committing coordinated target state.</summary>
    public bool IsTargetUpdateActive => _targetUpdateDepth > 0;

    /// <summary>Adds one binding after rejecting a duplicate target property.</summary>
    public void Add(Binding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (_bindings.Any(candidate =>
                string.Equals(
                    candidate.TargetPropertyName,
                    binding.TargetPropertyName,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"The target property '{binding.TargetPropertyName}' already has a binding.",
                nameof(binding));
        }

        _bindings.Add(binding);
    }

    /// <summary>Removes one identical binding after it releases subscriptions.</summary>
    public void Remove(Binding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _ = _bindings.Remove(binding);
    }

    /// <summary>Reschedules dirty source updates after the target commits a dispatcher attachment.</summary>
    public void OnDispatcherAttached()
    {
        foreach (var binding in _bindings.ToArray())
        {
            binding.OnTargetAttached();
        }
    }

    /// <summary>Invalidates queued source updates captured under the target's former dispatcher.</summary>
    public void OnDispatcherDetached()
    {
        foreach (var binding in _bindings.ToArray())
        {
            binding.OnTargetDetached();
        }
    }

    /// <summary>Releases every binding while retaining the first cleanup failure.</summary>
    public void DisposeAll()
    {
        ExceptionDispatchInfo? failure = null;

        for (var index = _bindings.Count - 1; index >= 0; index--)
        {
            ExceptionAggregation.Capture(_bindings[index].DisposeFromOwner, ref failure);
        }

        _bindings.Clear();

        failure?.Throw();
    }

    /// <summary>Begins one coordinated target update.</summary>
    public void EnterTargetUpdate() => _targetUpdateDepth++;

    /// <summary>Ends one coordinated target update and refreshes selection after success.</summary>
    public void ExitTargetUpdate(bool succeeded)
    {
        Debug.Assert(_targetUpdateDepth > 0, "A target update exits only after entry.");
        _targetUpdateDepth--;

        if (succeeded && _targetUpdateDepth == 0)
        {
            foreach (var binding in _bindings.ToArray())
            {
                binding.RefreshAfterItemsChanged();
            }
        }
    }
}
