# Mapo v2 — Nullable & Real-World Readiness Fix Plan

This plan addresses the 7 bugs documented in `Mapo_Analysis.md`, discovered while mapping a real-world OCPI domain model. Every fix preserves backward compatibility — all 132 existing tests must continue to pass.

## Guiding Principles

1. **Fix at the right layer.** Bugs 1/4/5/6 are all nullable-unaware code. The fix is a shared `TypeHelpers.StripNullableAnnotation()` utility, not scattered ad-hoc patches.
2. **Don't break existing behavior.** Non-nullable types behave identically. Null checks on top-level `Map()` calls still throw `ArgumentNullException`.
3. **Test before and after each step.** Every step includes unit tests that reproduce the exact bug, then the fix that makes them pass.
4. **Smallest possible diffs.** Each step is self-contained and independently committable.

---

## Step 1: Add `TypeHelpers.StripNullableAnnotation()` Utility

**Why first:** Steps 2-6 all need this helper. Building the foundation before the fixes.

**File:** `src/Mapo.Generator/TypeHelpers.cs`

**Change:** Add two new methods:

```csharp
/// <summary>
/// Returns the underlying type with NullableAnnotation removed.
/// For reference types: GeoCoordinates? -> GeoCoordinates
/// For Nullable<T> value types: int? -> int (extracts T)
/// For non-nullable types: returns as-is.
/// </summary>
public static ITypeSymbol StripNullableAnnotation(ITypeSymbol type)
{
    // Nullable<T> value type — unwrap to T
    if (type is INamedTypeSymbol named
        && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        && named.TypeArguments.Length == 1)
    {
        return named.TypeArguments[0];
    }

    // Nullable reference type — strip the annotation
    if (!type.IsValueType && type.NullableAnnotation == NullableAnnotation.Annotated)
    {
        return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
    }

    return type;
}

/// <summary>
/// Returns the display string of a type suitable for use in `new T()` expressions.
/// Strips nullable reference type annotations (e.g. "GeoCoordinates?" -> "GeoCoordinates").
/// </summary>
public static string ToConstructableDisplayString(ITypeSymbol type)
{
    return StripNullableAnnotation(type).ToDisplayString();
}
```

**Tests:** No new tests yet — this is a pure utility. Tested indirectly by Steps 2-6.

**Risk:** None. New methods only, no existing code touched.

---

## Step 2: Fix Bug 1 — `new Type?()` for Nullable Reference Types

**Problem:** `ObjectEmitter` uses `mapping.TargetTypeDisplayString` in `new` expressions. When the target type is a nullable reference type like `GeoCoordinates?`, it generates invalid `new GeoCoordinates?()`.

**Root cause:** The `TargetTypeDisplayString` is set from `targetType.ToDisplayString()` in `MethodMappingFactory.cs:291`, which includes the `?` for nullable reference types.

### Fix Strategy

Fix at two levels:

**A. Strip nullable annotation at discovery time (`PropertyMatcher.cs`)**

When a nested object pair is added to the discovery list, strip the nullable annotation from both source and target types. The nullable handling (null checks, `?? default`) is already handled separately in the expression generation.

In `PropertyMatcher.cs`, lines 341-361 (NestedObject discovery), and in all other `discoveryList.Add(pair)` calls for non-collection object types, use stripped types:

```csharp
// Before: var pair = (sourceProp.Type, targetType);
// After:
var strippedSource = TypeHelpers.StripNullableAnnotation(sourceProp.Type);
var strippedTarget = TypeHelpers.StripNullableAnnotation(targetType);
var pair = (strippedSource, strippedTarget);
```

This ensures `MethodMappingFactory` receives `GeoCoordinates` (not `GeoCoordinates?`), and `ToDisplayString()` produces `"GeoCoordinates"`.

**B. Safety net in `ObjectEmitter.cs` (lines 157, 178)**

As a defense-in-depth measure, strip the `?` from `TargetTypeDisplayString` when used in `new` expressions:

```csharp
// ObjectEmitter.cs line 157 and 178
var constructorType = mapping.TargetTypeDisplayString.TrimEnd('?');
cw.AppendLine($"var {targetName} = new {constructorType}({ctorArgs})");
```

This is a simple safety net — the primary fix is in (A).

### Handling nullable nested object mapping expressions

When the source property is nullable (e.g., `CoordinatesDto?`), the generated expression `MapCoordinatesDtoToGeoCoordinates(source.Coordinates)` will pass a potentially-null value. The generated internal method already has `if (src == null) throw new ArgumentNullException(...)`. For nullable nested objects, we need the expression to be null-conditional:

In `PropertyMatcher.cs`, when generating the nested object expression at line 358, check if the source property is nullable:

```csharp
if (sourceProp.NullableAnnotation == NullableAnnotation.Annotated
    || (sourceProp.Type is INamedTypeSymbol sn
        && sn.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T))
{
    expression = $"({sourceName}.{sourceProp.Name} != null ? {mName}({sourceName}.{sourceProp.Name}) : default)";
}
else
{
    expression = $"{mName}({sourceName}.{sourceProp.Name})";
}
```

### Files Modified
- `src/Mapo.Generator/Syntax/PropertyMatcher.cs` — strip nullable at discovery, null-conditional expressions
- `src/Mapo.Generator/Emit/ObjectEmitter.cs` — `TrimEnd('?')` safety net on `new` expressions

### Tests
```
NullableNestedObject_GeneratesValidCode — GeoCoordinates? target compiles
NullableNestedObject_NullSource_ReturnsNull — null CoordinatesDto → null GeoCoordinates
NullableNestedObject_NonNullSource_MapsCorrectly — non-null maps as expected
```

---

## Step 3: Fix Bug 2 — `List<string>` to `List<Enum>` Collection Mapping

**Problem:** Collection element mapping requires `sItem.SpecialType == SpecialType.None` for both element types. `string` has `SpecialType.System_String`, so the entire collection branch is skipped.

**Root cause:** The guard was designed to prevent mapping primitives (`int` → `int`) through a method call. But it also blocks legitimate cross-type element conversions like `string` → `enum`.

### Fix Strategy

After the existing collection-of-complex-types check fails (lines 275-316), add a new branch that handles collections where element types have known scalar conversions:

In `PropertyMatcher.cs`, after the existing collection block at line 316, add:

```csharp
// Collection element conversion: List<string> → List<Enum>, List<Enum> → List<string>
if (TypeHelpers.IsCollection(sourceProp.Type) && TypeHelpers.IsCollection(targetType))
{
    var sItem = TypeHelpers.GetItemType(sourceProp.Type);
    var tItem = TypeHelpers.GetItemType(targetType);
    if (sItem != null && tItem != null)
    {
        string? elementExpr = null;

        // string → enum
        if (sItem.SpecialType == SpecialType.System_String && tItem.TypeKind == TypeKind.Enum)
        {
            elementExpr = $"System.Enum.Parse<{tItem.ToDisplayString()}>(_item)";
        }
        // enum → string
        else if (sItem.TypeKind == TypeKind.Enum && tItem.SpecialType == SpecialType.System_String)
        {
            elementExpr = "_item.ToString()";
        }

        if (elementExpr != null)
        {
            // Generate inline LINQ expression instead of a method call
            var sourceExpr = $"{sourceName}.{sourceProp.Name}";
            // Handle nullable source collection
            if (sourceProp.NullableAnnotation == NullableAnnotation.Annotated)
            {
                expression = $"({sourceExpr}?.Select(_item => {elementExpr}).ToList() ?? new List<{tItem.ToDisplayString()}>())";
            }
            else
            {
                expression = $"{sourceExpr}.Select(_item => {elementExpr}).ToList()";
            }
            mappingOrigin = "Collection";
            return true;
        }
    }
}
```

This generates an inline LINQ expression rather than discovering a mapping method, which avoids the need for a separate `MapstringToFacility` method entirely.

### Files Modified
- `src/Mapo.Generator/Syntax/PropertyMatcher.cs` — add collection element conversion branch

### Tests
```
ListString_ToListEnum_GeneratesInlineConversion — generates .Select(Enum.Parse)
ListEnum_ToListString_GeneratesInlineConversion — generates .Select(.ToString())
ListNullableString_ToListEnum_HandlesNull — nullable collection returns empty list
```

---

## Step 4: Fix Bug 3 — Lambda Inlining Lacks Namespace Imports

**Problem:** Generated `.g.cs` files only have hardcoded `System.*` usings. Lambda bodies referencing project types fail to compile.

**Root cause:** `MapperEmitter.cs` has a fixed list of `using` directives. No mechanism collects namespaces from types referenced in lambda expressions.

### Fix Strategy

Collect all namespaces referenced in the mapper's source file and include them in the generated file.

**A. Collect namespaces in `MapperParser.cs`**

After parsing the class, collect all `using` directives from the syntax tree that contains the mapper class:

```csharp
// In MapperParser.Parse(), before building MapperInfo
var usings = classDeclaration.SyntaxTree.GetRoot(ct)
    .DescendantNodes()
    .OfType<UsingDirectiveSyntax>()
    .Where(u => u.Name != null)
    .Select(u => u.Name!.ToString())
    .Where(ns => !ns.StartsWith("System") && ns != "Mapo.Attributes")
    .Distinct()
    .ToList();
```

**B. Add `UserUsings` to `MapperInfo`**

Add a `List<string> UserUsings` property to `MapperInfo` that stores these namespace strings.

**C. Emit user usings in `MapperEmitter.cs`**

After the hardcoded system usings (line 34), emit the user's namespaces:

```csharp
foreach (var ns in mapper.UserUsings)
{
    cw.AppendLine($"using {ns};");
}
```

### Alternative approach: Fully qualify all types

Instead of collecting usings, we could fully qualify all type references in lambda bodies using `ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)`. However, this would make generated code much harder to read and would require rewriting the lambda body AST to replace all type references — significantly more complex and error-prone.

**Decision:** Collecting usings is simpler, safer, and covers all cases. The user's own `using` directives define what types are available in their lambdas — forwarding those to the generated file is the natural fix.

### Files Modified
- `src/Mapo.Generator/Models/MapperInfo.cs` — add `UserUsings` property
- `src/Mapo.Generator/Syntax/MapperParser.cs` — collect usings from syntax tree
- `src/Mapo.Generator/Emit/MapperEmitter.cs` — emit user usings

### Tests
```
LambdaWithProjectType_IncludesNamespace — lambda using project enum generates with correct using
LambdaWithMultipleNamespaces_AllIncluded — multiple project usings all forwarded
```

---

## Step 5: Fix Bug 4 — `AddConverter` Doesn't Match Nullable Source Types

**Problem:** Converter matching uses exact string equality: `"string" != "string?"`. A converter registered as `AddConverter<string, Guid>` won't match a `string?` source property.

**Root cause:** `PropertyMatcher.cs:224-226` compares `sourceProp.Type.ToDisplayString()` against `converter.SourceTypeDisplayString` without considering nullable annotations.

### Fix Strategy

In `PropertyMatcher.cs`, when matching converters, also try matching with the stripped nullable annotation. Two locations need fixing:

**Location 1: Lines 224-237 (auto-matched property path)**

```csharp
var sourceDisplay = sourceProp.Type.ToDisplayString();
var strippedSourceDisplay = TypeHelpers.StripNullableAnnotation(sourceProp.Type).ToDisplayString();
var targetDisplay = targetType.ToDisplayString();
var strippedTargetDisplay = TypeHelpers.StripNullableAnnotation(targetType).ToDisplayString();

var gConverter = globalConverters.FirstOrDefault(c =>
    (c.SourceTypeDisplayString == sourceDisplay || c.SourceTypeDisplayString == strippedSourceDisplay)
    && (c.TargetTypeDisplayString == targetDisplay || c.TargetTypeDisplayString == strippedTargetDisplay)
);
```

**Location 2: Lines 65-68 (custom mapping path)**

Same fix — also check stripped nullable display string when matching converters.

### Null safety consideration

When the converter is `string → Guid` and the source is `string?`, the generated expression will pass a nullable value to the converter lambda. We should wrap with a null check:

```csharp
if (sourceDisplay != strippedSourceDisplay) // source was nullable
{
    expression = $"({sourceName}.{sourceProp.Name} != null ? {converterExpr} : default)";
}
```

### Files Modified
- `src/Mapo.Generator/Syntax/PropertyMatcher.cs` — nullable-aware converter matching at both locations

### Tests
```
AddConverter_NullableSource_MatchesConverter — string? matches AddConverter<string, Guid>
AddConverter_NullableTarget_MatchesConverter — reverse direction also works
AddConverter_NullableSource_NullValue_ReturnsDefault — null string? → default(Guid)
```

---

## Step 6: Fix Bug 5 — Nullable Warnings in Generated Code

**Problem:** `string?` → `enum` generates `Enum.Parse<T>(source.Prop)` without null guards, producing CS8604. Similarly, `string?` → `string` generates a direct assignment producing CS8601.

**Root cause:** The string→enum conversion at `PropertyMatcher.cs:334-338` and the direct assignment at lines 240-256 don't check `NullableAnnotation`.

### Fix Strategy

**A. String→Enum with nullable source (`PropertyMatcher.cs:334-338`)**

Check if the source property is nullable and generate a null-guarded expression:

```csharp
if (sourceProp.Type.SpecialType == SpecialType.System_String && targetType.TypeKind == TypeKind.Enum)
{
    if (sourceProp.NullableAnnotation == NullableAnnotation.Annotated)
    {
        // Nullable string → enum: guard against null
        expression = $"System.Enum.Parse<{targetType.ToDisplayString()}>({sourceName}.{sourceProp.Name}!)";
    }
    else
    {
        expression = $"System.Enum.Parse<{targetType.ToDisplayString()}>({sourceName}.{sourceProp.Name})";
    }
    mappingOrigin = "EnumConversion";
    hasNullableMismatch = sourceProp.NullableAnnotation == NullableAnnotation.Annotated;
    return true;
}
```

The `!` (null-forgiving operator) tells the compiler "I know this isn't null at this point." This matches the existing behavior (throw on null input) but suppresses the warning. Users who want custom null handling can use `AddConverter` or `.Map()`.

**B. Direct string? → string assignment (`PropertyMatcher.cs:240-256`)**

The existing code at line 246-253 detects the nullable mismatch for reference types and sets `hasNullableMismatch = true`, but the expression is still just `source.Prop` without the `!` operator.

Add the null-forgiving operator when there's a nullable→non-nullable reference type mismatch:

```csharp
if (SymbolEqualityComparer.Default.Equals(sourceProp.Type, targetType))
{
    expression = $"{sourceName}.{sourceProp.Name}";

    if (!sourceProp.Type.IsValueType
        && sourceProp.NullableAnnotation == NullableAnnotation.Annotated
        && targetType.NullableAnnotation == NullableAnnotation.NotAnnotated)
    {
        hasNullableMismatch = true;
        expression = $"{sourceName}.{sourceProp.Name}!";
    }

    return true;
}
```

**C. Enum→String with nullable source (`PropertyMatcher.cs:327-332`)**

Same pattern — guard nullable enum sources:

```csharp
if (sourceProp.Type.TypeKind == TypeKind.Enum && targetType.SpecialType == SpecialType.System_String)
{
    expression = $"{sourceName}.{sourceProp.Name}.ToString()";
    // Enum is a value type, no nullable concern here normally.
    // But if it's a Nullable<Enum>, it needs handling.
    mappingOrigin = "EnumConversion";
    return true;
}
```

This case is already safe for non-nullable enums. For `Nullable<Enum>`, the existing nullable value type coercion at lines 259-270 would handle it first.

### Files Modified
- `src/Mapo.Generator/Syntax/PropertyMatcher.cs` — null-forgiving operator for nullable→non-nullable mismatches

### Tests
```
NullableString_ToEnum_GeneratesNullForgiving — string? → enum uses !
NullableString_ToNonNullableString_GeneratesNullForgiving — string? → string uses !
NullableString_ToEnum_CompilesWithoutWarnings — verify no CS8604
NullableString_ToString_CompilesWithoutWarnings — verify no CS8601
```

---

## Step 7: Fix Bug 6 — Null Collections Throw Instead of Returning Empty

**Problem:** Auto-generated internal collection methods throw `ArgumentNullException` on null input, but the source collection property may be nullable (`List<T>?`), making null a valid value.

**Root cause:** `CollectionEmitter.cs:101-102` unconditionally generates `throw`.

### Fix Strategy

This bug manifests in two paths:

**A. Standalone collection methods (`CollectionEmitter.EmitInternalBody`, line 101)**

The `MethodMapping.SourceTypeDisplayString` contains the `?` for nullable sources. Check if it ends with `?` and return an empty list instead of throwing:

```csharp
// CollectionEmitter.EmitInternalBody, replacing line 101-103
if (mapping.SourceTypeDisplayString.EndsWith("?"))
{
    cw.AppendLine($"if ({mapping.SourceName} == null) return new List<{tItem}>();");
}
else
{
    cw.AppendLine($"if ({mapping.SourceName} == null) throw new ArgumentNullException(nameof({mapping.SourceName}));");
}
```

**B. Inline collection loops (`CollectionEmitter.EmitLoopBlock`, lines 10-37)**

When a collection property is mapped inline (not via a separate method), the loop code accesses `src.Count` directly without a null check. For nullable collections, wrap the entire block:

```csharp
// Add to EmitLoopBlock, before the list creation
if (loop.SourceCollectionExpr contains nullable access)
{
    cw.AppendLine($"List<{loop.TargetItemTypeDisplay}> {varName};");
    cw.AppendLine($"if ({loop.SourceCollectionExpr} != null)");
    using (cw.Block())
    {
        // existing loop code
    }
    cw.AppendLine("else");
    using (cw.Block())
    {
        cw.AppendLine($"{varName} = new List<{loop.TargetItemTypeDisplay}>();");
    }
}
```

However, tracking nullability through the `CollectionLoopInfo` model is cleaner. Add an `IsSourceNullable` property to `CollectionLoopInfo`.

**C. Propagate nullability info to `CollectionLoopInfo`**

In `MethodMappingFactory.cs`, when creating `CollectionLoopInfo` for collection property mappings, check the source property's nullable annotation and pass it through.

Add `bool IsSourceNullable` to `CollectionLoopInfo`.

### Files Modified
- `src/Mapo.Generator/Models/PropertyMapping.cs` — add `IsSourceNullable` to `CollectionLoopInfo`
- `src/Mapo.Generator/Emit/CollectionEmitter.cs` — null-safe code for nullable collections
- `src/Mapo.Generator/Syntax/PropertyMatcher.cs` — propagate nullable info to collection expressions

### Tests
```
NullableCollection_ReturnsEmpty_NotThrow — null List<T>? → empty List<T>
NullableCollection_NonNull_MapsNormally — non-null List<T>? maps correctly
NullableCollection_InlineLoop_HandlesNull — inline collection loop handles null
```

---

## Step 8: Fix Bug 7 — Spurious Circular Reference Warning (MAPO010)

**Problem:** MAPO010 fires for diamond dependencies (multiple parents sharing a child type), not actual circular references.

**Root cause:** `MapperParser.cs:306-326` treats "type pair already processed" as "cycle detected." This is wrong for diamond-shaped type graphs.

### Fix Strategy

Replace the current heuristic with actual cycle detection. Track the **ancestry chain** (current processing path) and only flag a cycle when a type pair appears in its own ancestry.

**A. Track ancestry in discovery loop (`MapperParser.cs`)**

Add a dictionary that maps each queued pair to the pair that discovered it (its parent). When a pair is re-discovered, walk the parent chain to check for an actual cycle:

```csharp
// Add after line 171
var parentMap = new Dictionary<(ITypeSymbol, ITypeSymbol), (ITypeSymbol, ITypeSymbol)?>(new TypePairComparer());

// When enqueuing initial user methods (line 190):
parentMap[key] = null; // root, no parent

// When enqueuing discovered pairs (line 293-305):
parentMap[discovered] = (sourceType, targetType); // track who discovered it

// Replace the MAPO010 check (lines 306-326):
else if (!useReferenceTracking && processedPairs.Contains(discovered))
{
    // Check if this is an actual cycle by walking the parent chain
    var current = (sourceType, targetType);
    bool isCycle = false;
    while (current != null)
    {
        if (TypePairComparer.Equals(current, discovered))
        {
            isCycle = true;
            break;
        }
        parentMap.TryGetValue(current, out var parent);
        if (parent == null) break;
        current = parent.Value;
    }

    if (isCycle && !TypeHelpers.IsCollection(discovered.Item1)
                && !TypeHelpers.IsCollection(discovered.Item2))
    {
        diagnostics.Add(...MAPO010...);
    }
}
```

This correctly distinguishes:
- **Diamond:** `LocationDto` → `ImageDto` and `EvseDto` → `ImageDto` (no cycle, no warning)
- **True cycle:** `UserDto` → `DepartmentDto` → `UserDto` (actual cycle, warning)

### Files Modified
- `src/Mapo.Generator/Syntax/MapperParser.cs` — ancestry-based cycle detection

### Tests
```
DiamondDependency_NoCircularWarning — two parents sharing child type produces no MAPO010
TrueCircularReference_EmitsWarning — A → B → A produces MAPO010
CollectionDiamond_NoWarning — collection wrappers of shared types produce no MAPO010
```

---

## Step 9: Comprehensive Regression Tests

After all fixes, add a single large integration test that mimics the OCPI scenario from `Mapo_Analysis.md`:

### Test: `OcpiStyleMapping_AllBugsFixed`

```csharp
// Exercises all 7 fixes in one mapper:
// - Nullable reference type nested objects (Bug 1)
// - List<string> → List<Enum> collection (Bug 2)
// - Lambda with project type reference (Bug 3)
// - AddConverter<string, Guid> with string? source (Bug 4)
// - string? → enum without warnings (Bug 5)
// - Null collection → empty list (Bug 6)
// - Shared nested type from multiple parents (Bug 7)
```

### Files Created
- `tests/Mapo.Generator.Tests/NullableReferenceTypeTests.cs` — all unit tests for bugs 1, 4, 5, 6
- `tests/Mapo.Generator.Tests/CollectionConversionTests.cs` — unit tests for bug 2
- `tests/Mapo.Generator.Tests/LambdaNamespaceTests.cs` — unit tests for bug 3
- `tests/Mapo.Generator.Tests/CycleDetectionTests.cs` — unit tests for bug 7/8

---

## Execution Order & Dependencies

```
Step 1 (TypeHelpers utility)      ← Foundation, no dependencies
Step 2 (Bug 1: new Type?())      ← Depends on Step 1
Step 3 (Bug 2: List<string>)     ← Independent of Step 2, but do after Step 1
Step 4 (Bug 3: Lambda usings)    ← Independent, do in parallel with 2-3
Step 5 (Bug 4: Converter match)  ← Depends on Step 1
Step 6 (Bug 5: Nullable warns)   ← Depends on Step 1, pairs with Step 5
Step 7 (Bug 6: Null collections) ← Independent
Step 8 (Bug 7: MAPO010 false +)  ← Independent
Step 9 (Integration tests)       ← Depends on all above
```

## Verification After Each Step

```bash
# Must pass after EVERY step
dotnet build Mapo.slnx --configuration Release
dotnet test tests/Mapo.Generator.Tests/ --configuration Release

# After all steps
dotnet test tests/Mapo.IntegrationTests/ --configuration Release
```

## Summary

| Step | Bug | Fix | Files Changed | New Tests | Risk |
|------|-----|-----|--------------|-----------|------|
| 1 | — | Add `StripNullableAnnotation` utility | TypeHelpers.cs | 0 | None |
| 2 | 1 | Strip `?` from `new` expressions | PropertyMatcher, ObjectEmitter | 3 | Low |
| 3 | 2 | Inline LINQ for collection element conversion | PropertyMatcher | 3 | Medium |
| 4 | 3 | Forward user `using` directives to generated file | MapperInfo, MapperParser, MapperEmitter | 2 | Low |
| 5 | 4 | Nullable-aware converter matching | PropertyMatcher | 3 | Low |
| 6 | 5 | Null-forgiving operator for nullable→non-nullable | PropertyMatcher | 4 | Low |
| 7 | 6 | Return empty collection for null source | CollectionEmitter, CollectionLoopInfo | 3 | Medium |
| 8 | 7 | Ancestry-based cycle detection | MapperParser | 3 | Medium |
| 9 | All | OCPI-style integration test | Test project only | 1 | None |

**Total: ~22 new tests, 8 source files modified**

## Non-Goals

- **No new public API** — all fixes are internal to the generator
- **No performance changes** — generated code quality stays the same (or improves by eliminating unnecessary method calls for scalar collection conversions)
- **No breaking changes** — existing mappers produce identical output for non-nullable types
- **No `BeforeMap`/`AfterMap` hooks** — out of scope, add complexity without clear benefit for the bugs being fixed
