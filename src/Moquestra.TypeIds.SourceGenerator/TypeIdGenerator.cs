using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using CodegenCS;

namespace Moquestra.TypeIds.SourceGenerator
{
    /// <summary>
    /// Collects types annotated with <c>[TypeId]</c> at compile time and generates a
    /// per-assembly lookup class that can be used instead of <c>TypeIdRegistry</c> for
    /// lookups. IDs become compile-time constants computed by the same rules as the
    /// runtime, and lookups are implemented as switch statements without a dictionary.
    /// </summary>
    [Generator]
    public sealed class TypeIdGenerator : IIncrementalGenerator
    {
        private const string AttributeMetadataName = "Moquestra.TypeIds.TypeIdAttribute";
        private const string GeneratedMapNamespace = "Moquestra.TypeIds.Generated";
        private const string GeneratedMapName = "TypeIdMap";

        private static readonly DiagnosticDescriptor InaccessibleType = new DiagnosticDescriptor(
            "MQTID001",
            "Type is not accessible to the generated lookup",
            "Type '{0}' is skipped because generated code cannot reference it; make the type accessible from another type in the same assembly, or register it with reflection",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidAlias = new DiagnosticDescriptor(
            "MQTID002",
            "Alias cannot be null or empty",
            "Type '{0}' declares a null or empty alias, so an ID cannot be computed",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor DuplicateId = new DiagnosticDescriptor(
            "MQTID003",
            "ID is mapped to more than one type",
            "ID '{0}' is mapped to both '{1}' and '{2}'",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor GeneratedTypeConflict = new DiagnosticDescriptor(
            "MQTID004",
            "Generated lookup type conflicts with an existing type",
            "The generated lookup type '{0}' conflicts with an existing type in this assembly",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor UnsupportedGenericType = new DiagnosticDescriptor(
            "MQTID005",
            "Generic types are not supported",
            "Type '{0}' is a generic type, which is not supported",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <inheritdoc/>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var candidates = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AttributeMetadataName,
                    static (node, _) => node is TypeDeclarationSyntax,
                    static (attributeContext, _) => Capture(attributeContext))
                .Collect();

            var conflicts = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => IsPotentialConflict(node),
                    static (syntaxContext, _) => CaptureConflict(syntaxContext))
                .Where(static location => location is not null)
                .Select(static (location, _) => location!)
                .Collect();

            context.RegisterSourceOutput(
                candidates.Combine(conflicts),
                static (productionContext, input) => Emit(productionContext, input.Left, input.Right));
        }

        private static TypeIdCandidate Capture(GeneratorAttributeSyntaxContext context)
        {
            var symbol = (INamedTypeSymbol)context.TargetSymbol;
            var attribute = context.Attributes[0];

            int? explicitId = null;
            string? alias = null;
            var hasInvalidAlias = false;

            if (attribute.ConstructorArguments.Length == 1)
            {
                var argument = attribute.ConstructorArguments[0];

                if (argument.Type?.SpecialType == SpecialType.System_Int32)
                {
                    // 0 is treated as the "unspecified" sentinel, just like at runtime,
                    // and takes the computed path.
                    var id = (int)argument.Value!;

                    if (id != 0)
                        explicitId = id;
                }
                else if (argument.Value is string aliasValue && aliasValue.Length > 0)
                {
                    alias = aliasValue;
                }
                else
                {
                    hasInvalidAlias = true;
                }
            }

            return new TypeIdCandidate(
                BuildTypeofExpression(symbol),
                BuildFullName(symbol),
                symbol.ToDisplayString(),
                explicitId,
                alias,
                hasInvalidAlias,
                IsGenericType(symbol),
                IsAccessibleFromGeneratedCode(symbol),
                symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None);
        }

        private static bool IsPotentialConflict(SyntaxNode node)
        {
            return node switch
            {
                BaseTypeDeclarationSyntax declaration => declaration.Identifier.ValueText == GeneratedMapName,
                DelegateDeclarationSyntax declaration => declaration.Identifier.ValueText == GeneratedMapName,
                _ => false,
            };
        }

        private static Location? CaptureConflict(GeneratorSyntaxContext context)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

            return symbol is not null &&
                symbol.MetadataName == GeneratedMapName &&
                symbol.ContainingType is null &&
                symbol.ContainingNamespace.ToDisplayString() == GeneratedMapNamespace
                ? GetIdentifierLocation(context.Node)
                : null;
        }

        private static Location GetIdentifierLocation(SyntaxNode node)
        {
            return node switch
            {
                BaseTypeDeclarationSyntax declaration => declaration.Identifier.GetLocation(),
                DelegateDeclarationSyntax declaration => declaration.Identifier.GetLocation(),
                _ => node.GetLocation(),
            };
        }

        private static void Emit(SourceProductionContext context, ImmutableArray<TypeIdCandidate> candidates, ImmutableArray<Location> conflicts)
        {
            if (conflicts.Length > 0)
            {
                foreach (var conflict in conflicts)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratedTypeConflict,
                        conflict,
                        GeneratedMapNamespace + "." + GeneratedMapName));
                }

                return;
            }

            var mappings = new List<TypeIdMappingModel>();

            foreach (var candidate in candidates)
            {
                if (candidate.IsGenericType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(UnsupportedGenericType, candidate.Location, candidate.DisplayName));

                    continue;
                }

                if (!candidate.IsAccessible)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InaccessibleType, candidate.Location, candidate.DisplayName));

                    continue;
                }

                if (candidate.HasInvalidAlias)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidAlias, candidate.Location, candidate.DisplayName));

                    continue;
                }

                var id = candidate.ExplicitId ?? TypeIdHelpers.ComputeId(candidate.Alias ?? candidate.FullName);

                mappings.Add(new TypeIdMappingModel(candidate, id));
            }

            mappings.Sort(static (l, r) => string.CompareOrdinal(l.Candidate.FullName, r.Candidate.FullName));

            var seenIds = new Dictionary<int, TypeIdMappingModel>();
            var accepted = new List<TypeIdMappingModel>();

            foreach (var mapping in mappings)
            {
                if (seenIds.TryGetValue(mapping.Id, out var existing))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateId,
                        mapping.Candidate.Location,
                        mapping.Id.ToString(CultureInfo.InvariantCulture),
                        existing.Candidate.DisplayName,
                        mapping.Candidate.DisplayName));

                    continue;
                }

                seenIds.Add(mapping.Id, mapping);
                accepted.Add(mapping);
            }

            var typeCases = new List<string>();
            var idCases = new List<string>();

            foreach (var mapping in accepted)
            {
                var typeofExpression = mapping.Candidate.TypeofExpression;
                var id = mapping.Id.ToString(CultureInfo.InvariantCulture);

                typeCases.Add($"case {id}: type = typeof({typeofExpression}); return true;");
                idCases.Add($"case \"{mapping.Candidate.FullName}\" when type == typeof({typeofExpression}): id = {id}; return true;");
            }

            var writer = new CodegenTextWriter();

            writer.Write($$"""
            // <auto-generated/>

            #nullable enable

            namespace Moquestra.TypeIds.Generated
            {
                /// <summary>
                /// Provides a source-generated bidirectional mapping between the
                /// <c>[TypeId]</c>-annotated types that generated code can access in this
                /// assembly and their integer IDs.
                /// </summary>
                public static class TypeIdMap
                {
                    /// <summary>
                    /// Attempts to get the type mapped to the specified ID.
                    /// </summary>
                    /// <param name="id">The ID to look up.</param>
                    /// <param name="type">The type mapped to the specified ID, or <see langword="null"/> if no mapping exists.</param>
                    /// <returns><see langword="true"/> if the specified ID is mapped to a type; otherwise, <see langword="false"/>.</returns>
                    public static bool TryGetType(int id, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::System.Type? type)
                    {
                        switch (id)
                        {
                            {{string.Join("\n", typeCases)}}
                            default: type = null; return false;
                        }
                    }

                    /// <summary>
                    /// Attempts to get the ID mapped to the specified type.
                    /// </summary>
                    /// <param name="type">The type to look up. Cannot be <see langword="null"/>.</param>
                    /// <param name="id">The ID mapped to the specified type, or 0 if no mapping exists.</param>
                    /// <returns><see langword="true"/> if the specified type is mapped to an ID; otherwise, <see langword="false"/>.</returns>
                    /// <exception cref="global::System.ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
                    public static bool TryGetId(global::System.Type type, out int id)
                    {
                        if (type is null)
                            throw new global::System.ArgumentNullException(nameof(type));

                        switch (type.FullName)
                        {
                            {{string.Join("\n", idCases)}}
                            default: id = 0; return false;
                        }
                    }
                }
            }
            """);

            context.AddSource("TypeIdMap.g.cs", writer.GetContents().Replace("\r\n", "\n"));
        }

        // Mirrors Type.IsGenericType: a type nested in a generic type is itself generic
        // because it carries the containing type's type parameters.
        private static bool IsGenericType(INamedTypeSymbol symbol)
        {
            var current = symbol;

            while (current is not null)
            {
                if (current.Arity > 0)
                    return true;

                current = current.ContainingType;
            }

            return false;
        }

        // The generated code references types with typeof from a separate class,
        // so the type and all of its containing types must be at least internal.
        private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol symbol)
        {
            var current = symbol;

            while (current is not null)
            {
                switch (current.DeclaredAccessibility)
                {
                    case Accessibility.Public:
                    case Accessibility.Internal:
                    case Accessibility.ProtectedOrInternal:
                        break;
                    default:
                        return false;
                }

                current = current.ContainingType;
            }

            return true;
        }

        // Builds the same format as Type.FullName: '.' for namespaces, '+' for nested
        // types, and MetadataName's backtick arity suffix for generic types.
        private static string BuildFullName(INamedTypeSymbol symbol)
        {
            var names = new Stack<string>();
            var current = symbol;

            while (current is not null)
            {
                names.Push(current.MetadataName);
                current = current.ContainingType;
            }

            var name = string.Join("+", names);
            var containingNamespace = symbol.ContainingNamespace;

            return containingNamespace.IsGlobalNamespace ? name : containingNamespace.ToDisplayString() + "." + name;
        }

        private static string BuildTypeofExpression(INamedTypeSymbol symbol)
        {
            var names = new Stack<string>();
            var current = symbol;

            while (current is not null)
            {
                var name = current.Name;

                if (current.Arity > 0)
                    name += "<" + new string(',', current.Arity - 1) + ">";

                names.Push(name);
                current = current.ContainingType;
            }

            var qualified = string.Join(".", names);
            var containingNamespace = symbol.ContainingNamespace;

            return containingNamespace.IsGlobalNamespace
                ? "global::" + qualified
                : "global::" + containingNamespace.ToDisplayString() + "." + qualified;
        }

        private sealed class TypeIdCandidate
        {
            public TypeIdCandidate(
                string typeofExpression,
                string fullName,
                string displayName,
                int? explicitId,
                string? alias,
                bool hasInvalidAlias,
                bool isGenericType,
                bool isAccessible,
                Location location)
            {
                TypeofExpression = typeofExpression;
                FullName = fullName;
                DisplayName = displayName;
                ExplicitId = explicitId;
                Alias = alias;
                HasInvalidAlias = hasInvalidAlias;
                IsGenericType = isGenericType;
                IsAccessible = isAccessible;
                Location = location;
            }

            public string TypeofExpression { get; }

            public string FullName { get; }

            public string DisplayName { get; }

            public int? ExplicitId { get; }

            public string? Alias { get; }

            public bool HasInvalidAlias { get; }

            public bool IsGenericType { get; }

            public bool IsAccessible { get; }

            public Location Location { get; }
        }

        private sealed class TypeIdMappingModel
        {
            public TypeIdMappingModel(TypeIdCandidate candidate, int id)
            {
                Candidate = candidate;
                Id = id;
            }

            public TypeIdCandidate Candidate { get; }

            public int Id { get; }
        }
    }
}
