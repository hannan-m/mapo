namespace Mapo.Attributes;

/// <summary>
/// Used internally by Mapo to track object references and prevent infinite recursion in circular graphs.
/// A new instance is created per public mapping call — do not share across threads or reuse between calls.
/// </summary>
public class MappingContext
{
    private readonly Dictionary<object, object> _references = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Returns a previously mapped target for the given source, or <c>false</c> if not yet mapped.
    /// </summary>
    public bool TryGet<T>(object source, out T? target)
    {
        if (_references.TryGetValue(source, out var t))
        {
            target = (T)t;
            return true;
        }
        target = default;
        return false;
    }

    /// <summary>
    /// Registers a source→target pair so subsequent circular references resolve to the same target.
    /// </summary>
    public void Add(object source, object target)
    {
        _references[source] = target;
    }
}

internal class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
