using System.Linq.Expressions;

namespace Mapo.Attributes;

/// <summary>
/// Marks a partial class as a Mapo mapper. The generator will implement all partial methods
/// declared in this class with compile-time object mapping code.
/// </summary>
/// <example>
/// <code>
/// [Mapper]
/// public partial class UserMapper
/// {
///     public partial UserDto Map(User user);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class MapperAttribute : Attribute
{
    /// <summary>
    /// When <c>true</c>, unmapped target properties produce compile errors instead of warnings (MAPO001).
    /// </summary>
    public bool StrictMode { get; set; }

    /// <summary>
    /// When <c>true</c>, generated code tracks mapped object references to safely handle circular reference graphs.
    /// </summary>
    public bool UseReferenceTracking { get; set; }
}

/// <summary>
/// Registers a derived source→target type pair for polymorphic mapping dispatch.
/// Apply to a partial method that maps a base type to enable runtime type switching.
/// </summary>
/// <example>
/// <code>
/// [MapDerived(typeof(Circle), typeof(CircleDto))]
/// [MapDerived(typeof(Rectangle), typeof(RectangleDto))]
/// public partial ShapeDto Map(Shape shape);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MapDerivedAttribute : Attribute
{
    /// <summary>The concrete source type to match at runtime.</summary>
    public Type SourceType { get; }

    /// <summary>The concrete target type to map to.</summary>
    public Type TargetType { get; }

    public MapDerivedAttribute(Type sourceType, Type targetType)
    {
        SourceType = sourceType;
        TargetType = targetType;
    }
}

/// <summary>
/// Fluent configuration interface for customizing a mapping between <typeparamref name="TSource"/> and <typeparamref name="TTarget"/>.
/// Used inside a static <c>Configure</c> method — parsed at compile time, never called at runtime.
/// </summary>
public interface IMapConfig<TSource, TTarget>
{
    /// <summary>
    /// Maps a target property to a custom source expression.
    /// </summary>
    /// <example><code>config.Map(d => d.FullName, s => s.First + " " + s.Last);</code></example>
    IMapConfig<TSource, TTarget> Map<TTargetValue, TSourceValue>(
        Expression<Func<TTarget, TTargetValue>> target,
        Expression<Func<TSource, TSourceValue>> source);

    /// <summary>
    /// Excludes a target property from mapping. Silences MAPO001 for this property.
    /// </summary>
    IMapConfig<TSource, TTarget> Ignore<TValue>(Expression<Func<TTarget, TValue>> target);

    /// <summary>
    /// Registers a global type converter applied to all properties matching the source→target type pair.
    /// </summary>
    /// <example><code>config.AddConverter&lt;DateTime, string&gt;(dt => dt.ToString("o"));</code></example>
    IMapConfig<TSource, TTarget> AddConverter<TSourceValue, TTargetValue>(Func<TSourceValue, TTargetValue> converter);

    /// <summary>
    /// Generates an inverse mapping from <typeparamref name="TTarget"/> back to <typeparamref name="TSource"/>.
    /// </summary>
    void ReverseMap();
}
