using Microsoft.CodeAnalysis;

namespace Mapo.Generator.Diagnostics;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnmappedPropertyWarning = new(
        id: "MAPO001",
        title: "Property is not mapped",
        messageFormat: "Property '{0}' in target type '{1}' is not mapped. Add a custom mapping in Configure, use Ignore, or ensure names match.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MapperOnNonPartialClass = new(
        id: "MAPO003",
        title: "Mapper class must be partial",
        messageFormat: "Class '{0}' has [Mapper] but is not declared as partial. Add the 'partial' modifier.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidConfigureSignature = new(
        id: "MAPO004",
        title: "Configure method has wrong signature",
        messageFormat: "Configure method in '{0}' must be static with first parameter of type IMapConfig<TSource, TTarget>. {1}",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoAccessibleProperties = new(
        id: "MAPO005",
        title: "Type has no accessible properties",
        messageFormat: "Mapping from '{0}' to '{1}' has no matchable properties. Source has no gettable properties or target has no settable properties.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateTargetMapping = new(
        id: "MAPO006",
        title: "Duplicate mapping for target property",
        messageFormat: "Target property '{0}' has multiple Map() configurations. Only the first mapping will be used.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidTargetProperty = new(
        id: "MAPO007",
        title: "Invalid target property in mapping configuration",
        messageFormat: "Target lambda references '{0}', which is not a settable property on type '{1}'.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidSourceExpression = new(
        id: "MAPO008",
        title: "Invalid source expression in mapping configuration",
        messageFormat: "Source expression in Map() for target '{0}' contains errors and may not compile correctly.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NullableToNonNullableMapping = new(
        id: "MAPO009",
        title: "Nullable source mapped to non-nullable target",
        messageFormat: "Property '{0}' maps nullable '{1}' to non-nullable '{2}'. Auto-coerced with ?? default. Use AddConverter to customize.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CircularReferenceWithoutTracking = new(
        id: "MAPO010",
        title: "Circular reference detected without UseReferenceTracking",
        messageFormat: "Type '{0}' has a property of type '{1}' which creates a circular reference. Enable UseReferenceTracking on the [Mapper] attribute to handle this safely.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnmatchedEnumMember = new(
        id: "MAPO011",
        title: "Enum member has no match in target",
        messageFormat: "Source enum member '{0}.{1}' has no matching member in target enum '{2}'.",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
