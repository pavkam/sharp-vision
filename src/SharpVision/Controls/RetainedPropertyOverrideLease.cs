// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Owns one generation of temporary property overrides for a retained child.</summary>
internal sealed class RetainedPropertyOverrideLease
{
    private readonly Action<ControlBase, RetainedControlProperty>? _authoredValueChanged;
    private readonly Dictionary<RetainedControlProperty, RetainedPropertyOverrideEntry> _entries;
    private readonly Action<RetainedPropertyOverrideLease> _retired;
    private RetainedControlProperty? _writingProperty;
    private object? _writingValue;

    /// <summary>Initializes and installs one unique child generation.</summary>
    /// <param name="child">The owned child whose authored values are captured.</param>
    /// <param name="descriptors">The non-empty distinct property descriptor snapshot.</param>
    /// <param name="authoredValueChanged">Optional callback after a caller request is captured.</param>
    /// <param name="retired">The non-null service callback that releases generation metadata.</param>
    internal RetainedPropertyOverrideLease(
        ControlBase child,
        IReadOnlyList<RetainedPropertyOverrideDescriptor> descriptors,
        Action<ControlBase, RetainedControlProperty>? authoredValueChanged,
        Action<RetainedPropertyOverrideLease> retired)
    {
        Debug.Assert(child is not null, "A retained override lease requires its child.");
        Debug.Assert(descriptors is not null && descriptors.Count > 0, "A lease requires property descriptors.");
        Debug.Assert(retired is not null, "A lease requires retirement coordination.");
        Child = child;
        _authoredValueChanged = authoredValueChanged;
        _retired = retired;
        _entries = new Dictionary<RetainedControlProperty, RetainedPropertyOverrideEntry>(descriptors.Count);

        foreach (var descriptor in descriptors)
        {
            if (!_entries.TryAdd(descriptor.Property, new RetainedPropertyOverrideEntry(descriptor, child)))
            {
                throw new ArgumentException("A retained property override cannot repeat a property.", nameof(descriptors));
            }
        }

        child.InstallRetainedPropertyOverride(this);
    }

    /// <summary>Gets whether this generation still controls its child.</summary>
    internal bool IsCurrent => !IsRetired && ReferenceEquals(Child.RetainedPropertyOverride, this);

    /// <summary>Gets the exact child generation bound by this lease.</summary>
    internal ControlBase Child { get; }

    private bool IsRetired { get; set; }

    /// <summary>Gets whether an owner-attributed live write is active for one property.</summary>
    /// <param name="property">The queried property.</param>
    /// <returns>True only during the matching owner write.</returns>
    internal bool IsWriting(RetainedControlProperty property) => _writingProperty == property;

    /// <summary>Gets the latest caller-authored value for one controlled property.</summary>
    /// <typeparam name="T">The exact property value type.</typeparam>
    /// <param name="property">The controlled property.</param>
    /// <returns>The latest authored value.</returns>
    internal T GetAuthored<T>(RetainedControlProperty property)
        where T : notnull
    {
        var entry = RequireEntry<T>(property);
        return (T) entry.AuthoredValue;
    }

    /// <summary>Writes an owner-imposed live value without recapturing it as authored state.</summary>
    /// <typeparam name="T">The exact property value type.</typeparam>
    /// <param name="property">The controlled property.</param>
    /// <param name="value">The imposed live value.</param>
    internal void SetLive<T>(RetainedControlProperty property, T value)
        where T : notnull
    {
        if (!IsCurrent)
        {
            return;
        }

        var entry = RequireEntry<T>(property);
        var previousProperty = _writingProperty;
        var previousValue = _writingValue;
        _writingProperty = property;
        _writingValue = value;

        try
        {
            entry.Descriptor.Write(Child, value);
        }
        finally
        {
            _writingProperty = previousProperty;
            _writingValue = previousValue;
        }
    }

    /// <summary>Consumes a caller request or permits the attributed owner write to reach storage.</summary>
    /// <typeparam name="T">The exact property value type.</typeparam>
    /// <param name="property">The requested property.</param>
    /// <param name="value">The requested value.</param>
    /// <returns>True when the request updated authored state only; false when normal storage should continue.</returns>
    internal bool TryHandleRequest<T>(RetainedControlProperty property, T value)
        where T : notnull
    {
        if (!IsCurrent || !_entries.TryGetValue(property, out var entry))
        {
            return false;
        }

        if (entry.Descriptor.ValueType != typeof(T))
        {
            throw new InvalidOperationException("A retained property request used the wrong value type.");
        }

        if (_writingProperty == property && Equals(_writingValue, value))
        {
            return false;
        }

        entry.AuthoredValue = value;
        _authoredValueChanged?.Invoke(Child, property);
        return true;
    }

    /// <summary>Restores authored values while this generation remains current, then retires it.</summary>
    internal void Restore()
    {
        ExceptionDispatchInfo? failure = null;

        try
        {
            foreach (var entry in _entries.Values.ToArray())
            {
                if (!IsCurrent || Child.IsDisposed || Child.IsDisposing)
                {
                    break;
                }

                ExceptionAggregation.Capture(() => SetLiveObject(entry), ref failure);
            }
        }
        finally
        {
            Retire();
        }

        failure?.Throw();
    }

    /// <summary>Retires metadata without restoring values.</summary>
    internal void Retire()
    {
        if (IsRetired)
        {
            return;
        }

        IsRetired = true;
        Child.ClearRetainedPropertyOverride(this);
        _entries.Clear();
        _retired(this);
    }

    private RetainedPropertyOverrideEntry RequireEntry<T>(RetainedControlProperty property)
        where T : notnull
    {
        return !_entries.TryGetValue(property, out var entry) || entry.Descriptor.ValueType != typeof(T)
            ? throw new InvalidOperationException("The retained property is not controlled by this lease and type.")
            : entry;
    }

    private void SetLiveObject(RetainedPropertyOverrideEntry entry)
    {
        var previousProperty = _writingProperty;
        var previousValue = _writingValue;
        _writingProperty = entry.Descriptor.Property;
        _writingValue = entry.AuthoredValue;

        try
        {
            entry.Descriptor.Write(Child, entry.AuthoredValue);
        }
        finally
        {
            _writingProperty = previousProperty;
            _writingValue = previousValue;
        }
    }
}
