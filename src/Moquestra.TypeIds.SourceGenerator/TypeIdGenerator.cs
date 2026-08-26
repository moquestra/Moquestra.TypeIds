using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using CodegenCS;

namespace Moquestra.TypeIds.SourceGenerator
{
    /// <summary>
    /// Collects types annotated with <c>[TypeId]</c> at compile time and generates
    /// per-assembly lookup classes, partitioned by domain, that can be used instead of
    /// <c>TypeIdRegistry</c> for lookups. IDs become compile-time constants computed by
    /// the same rules as the runtime, and lookups are implemented as switch statements
    /// without a dictionary.
    /// </summary>
    [Generator]
    public sealed class TypeIdGenerator : IIncrementalGenerator
    {
        private const string AttributeMetadataName = "Moquestra.TypeIds.TypeIdAttribute";
        private const string GeneratedNamespaceSuffix = ".Generated";
        private const string GeneratedMapName = "TypeIdMap";

        private static readonly DiagnosticDescriptor InaccessibleType = new DiagnosticDescriptor(
            "MQTID001",
            "Type is not accessible to the generated lookup",
            "Type '{0}' is skipped because generated code cannot reference it; make the type accessible from another type in the same assembly, or set ExcludeFromGeneratedMap to true and register it with reflection",
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

        private static readonly DiagnosticDescriptor InvalidDomain = new DiagnosticDescriptor(
            "MQTID007",
            "Invalid domain name",
            "Type '{0}' declares the invalid domain '{1}'; a domain must start with an ASCII letter or underscore and contain only ASCII letters, digits, and underscores",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SanitizedNamespace = new DiagnosticDescriptor(
            "MQTID006",
            "Generated namespace was sanitized",
            "The root namespace or assembly name '{0}' cannot be used directly as a namespace, so the generated lookup is placed in '{1}'; set a root namespace or assembly name that can be used as a namespace without sanitization to control the generated namespace",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Warning,
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
                .Where(static conflict => conflict is not null)
                .Select(static (conflict, _) => conflict!.Value)
                .Collect();

            // Only the assembly name is selected out of the compilation so that the
            // provider stays cacheable across edits.
            var mapNamespace = context.AnalyzerConfigOptionsProvider
                .Select(static (provider, _) =>
                    provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var value) ? value : null)
                .Combine(context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName))
                .Select(static (names, _) => BuildGeneratedNamespace(names.Left, names.Right));

            context.RegisterSourceOutput(
                candidates.Combine(conflicts).Combine(mapNamespace),
                static (productionContext, input) => Emit(productionContext, input.Left.Left, input.Left.Right, input.Right));
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

            var excludeFromGeneratedMap = false;
            string? domain = null;
            var hasInvalidDomain = false;

            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key == "ExcludeFromGeneratedMap" && namedArgument.Value.Value is bool excluded)
                    excludeFromGeneratedMap = excluded;

                // An explicit Domain = null is indistinguishable from no declaration,
                // so it counts as the default domain.
                if (namedArgument.Key == "Domain" && namedArgument.Value.Value is string domainValue)
                {
                    domain = domainValue;
                    hasInvalidDomain = !IsValidDomainName(domainValue);
                }
            }

            return new TypeIdCandidate(
                BuildTypeofExpression(symbol),
                BuildFullName(symbol),
                symbol.ToDisplayString(),
                explicitId,
                alias,
                hasInvalidAlias,
                excludeFromGeneratedMap,
                domain,
                hasInvalidDomain,
                IsGenericType(symbol),
                IsAccessibleFromGeneratedCode(symbol),
                symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None);
        }

        // Domain map names are dynamic, so the predicate cannot know the exact names.
        // The suffix is matched broadly and compared against the actual generated
        // names during emission.
        private static bool IsPotentialConflict(SyntaxNode node)
        {
            return node switch
            {
                BaseTypeDeclarationSyntax declaration => declaration.Identifier.ValueText.EndsWith(GeneratedMapName, StringComparison.Ordinal),
                DelegateDeclarationSyntax declaration => declaration.Identifier.ValueText.EndsWith(GeneratedMapName, StringComparison.Ordinal),
                _ => false,
            };
        }

        // The generated namespace is not known at capture time, so the containing
        // namespace is recorded here and compared against it during emission.
        private static (string Namespace, string Name, Location Location)? CaptureConflict(GeneratorSyntaxContext context)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

            if (symbol is null || symbol.ContainingType is not null)
                return null;

            if (!symbol.MetadataName.EndsWith(GeneratedMapName, StringComparison.Ordinal))
                return null;

            var containingNamespace = symbol.ContainingNamespace;

            return (
                containingNamespace.IsGlobalNamespace ? string.Empty : containingNamespace.ToDisplayString(),
                symbol.MetadataName,
                GetIdentifierLocation(context.Node));
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

        // The lookup namespace is derived from the consuming project's root namespace,
        // falling back to its assembly name. Assemblies with distinct derived namespaces
        // can each expose a public map without colliding. A name that cannot be used
        // directly as a namespace is sanitized to keep the generated file compiling, and
        // the original name is returned with the sanitized namespace so MQTID006 can
        // report both (Unity's default Assembly-CSharp assembly is the canonical example).
        private static (string Namespace, string? SanitizedFrom) BuildGeneratedNamespace(string? rootNamespace, string? assemblyName)
        {
            var prefix = string.IsNullOrEmpty(rootNamespace) ? assemblyName : rootNamespace;

            // A compilation without an assembly name falls back to the previous fixed namespace.
            if (string.IsNullOrEmpty(prefix))
                return ("Moquestra.TypeIds" + GeneratedNamespaceSuffix, null);

            var sanitized = SanitizeNamespace(prefix!);

            return (sanitized + GeneratedNamespaceSuffix, sanitized == prefix ? null : prefix);
        }

        // Invalid characters are replaced with '_', segments that start with a character
        // valid only after the first position (such as a digit) or match a C# reserved
        // keyword are prefixed with '_', and empty segments become '_'. Character and
        // keyword checks use the compiler's own SyntaxFacts, except that Unicode format
        // characters are treated as invalid: they pass the identifier check but the
        // compiler strips them from the semantic name, which would let the source
        // spelling and the semantic namespace diverge. The generator owns this name, so
        // it can be transformed instead of escaped with verbatim '@' identifiers, and
        // the map namespace plays no part in ID computation, leaving the hash contract
        // unaffected.
        private static string SanitizeNamespace(string prefix)
        {
            var segments = prefix.Split('.');
            var builder = new StringBuilder(prefix.Length + 1);

            for (var i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                    builder.Append('.');

                builder.Append(SanitizeSegment(segments[i]));
            }

            return builder.ToString();
        }

        private static string SanitizeSegment(string segment)
        {
            if (segment.Length == 0)
                return "_";

            var builder = new StringBuilder(segment.Length + 1);
            var first = segment[0];

            if (!SyntaxFacts.IsIdentifierStartCharacter(first) && CanPreserveIdentifierCharacter(first))
                builder.Append('_');

            builder.Append(CanPreserveIdentifierCharacter(first) ? first : '_');

            for (var i = 1; i < segment.Length; i++)
            {
                var next = segment[i];

                builder.Append(CanPreserveIdentifierCharacter(next) ? next : '_');
            }

            var sanitized = builder.ToString();

            return SyntaxFacts.GetKeywordKind(sanitized) == SyntaxKind.None ? sanitized : "_" + sanitized;
        }

        private static bool CanPreserveIdentifierCharacter(char value)
        {
            return SyntaxFacts.IsIdentifierPartCharacter(value)
                && CharUnicodeInfo.GetUnicodeCategory(value) != UnicodeCategory.Format;
        }

        // The domain is used as a class name prefix exactly as spelled, so only the
        // ASCII identifier set is allowed. Keywords are not rejected because the
        // suffix makes them valid identifiers.
        private static bool IsValidDomainName(string domain)
        {
            if (domain.Length == 0)
                return false;

            if (!IsAsciiLetterOrUnderscore(domain[0]))
                return false;

            for (var i = 1; i < domain.Length; i++)
            {
                var next = domain[i];

                if (!IsAsciiLetterOrUnderscore(next) && (next < '0' || next > '9'))
                    return false;
            }

            return true;
        }

        private static bool IsAsciiLetterOrUnderscore(char value)
        {
            return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z') || value == '_';
        }

        private static void Emit(SourceProductionContext context, ImmutableArray<TypeIdCandidate> candidates, ImmutableArray<(string Namespace, string Name, Location Location)> conflicts, (string Namespace, string? SanitizedFrom) mapNamespace)
        {
            // Installing the generator alone must not change the assembly's public API.
            // When no types are annotated, nothing is emitted, so a user-declared type with
            // a map name cannot conflict with generated code that does not exist.
            if (candidates.IsEmpty)
                return;

            var mappings = new List<TypeIdMappingModel>();

            foreach (var candidate in candidates)
            {
                if (candidate.IsGenericType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(UnsupportedGenericType, candidate.Location, candidate.DisplayName));

                    continue;
                }

                // Excluded types are never referenced by generated code, so accessibility is
                // not required, and MQTID001 would be noise for an intended registry-only use.
                if (!candidate.IsAccessible && !candidate.IsExcludedFromMap)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InaccessibleType, candidate.Location, candidate.DisplayName));

                    continue;
                }

                if (candidate.HasInvalidAlias)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidAlias, candidate.Location, candidate.DisplayName));

                    continue;
                }

                if (candidate.HasInvalidDomain)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidDomain, candidate.Location, candidate.DisplayName, candidate.Domain));

                    continue;
                }

                var id = candidate.ExplicitId ?? TypeIdHelpers.ComputeId(candidate.Alias ?? candidate.FullName);

                mappings.Add(new TypeIdMappingModel(candidate, id));
            }

            mappings.Sort(static (l, r) => string.CompareOrdinal(l.Candidate.FullName, r.Candidate.FullName));

            // A map class is emitted only for domains represented in mappings, including
            // the default domain. Domains with only excluded mappings keep an empty map.
            var domains = new List<string?>();
            var declaredDomains = new SortedSet<string>(StringComparer.Ordinal);
            var hasDefaultDomain = false;

            foreach (var mapping in mappings)
            {
                if (mapping.Candidate.Domain is null)
                    hasDefaultDomain = true;
                else
                    declaredDomains.Add(mapping.Candidate.Domain);
            }

            if (hasDefaultDomain)
                domains.Add(null);

            domains.AddRange(declaredDomains);

            if (domains.Count == 0)
                return;

            var classNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var domain in domains)
                classNames.Add(BuildClassName(domain));

            var hasConflict = false;

            foreach (var conflict in conflicts)
            {
                if (conflict.Namespace != mapNamespace.Namespace || !classNames.Contains(conflict.Name))
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratedTypeConflict,
                    conflict.Location,
                    mapNamespace.Namespace + "." + conflict.Name));

                hasConflict = true;
            }

            if (hasConflict)
                return;

            // Report only when sanitizing changed the name, and only in a compilation
            // that actually emits the lookup.
            if (mapNamespace.SanitizedFrom is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SanitizedNamespace,
                    Location.None,
                    mapNamespace.SanitizedFrom,
                    mapNamespace.Namespace));
            }

            var classBlocks = new List<string>();

            foreach (var domain in domains)
            {
                var seenIds = new Dictionary<int, TypeIdMappingModel>();
                var emittedIds = new HashSet<int>();
                var accepted = new List<TypeIdMappingModel>();

                // Duplicate detection is per domain - the same ID in another domain is not
                // a collision. Excluded types still share their domain's ID space, so they
                // are checked, while lookup cases are selected independently from
                // non-excluded mappings so an excluded type's earlier ID claim cannot
                // suppress an included case.
                foreach (var mapping in mappings)
                {
                    if (!string.Equals(mapping.Candidate.Domain, domain, StringComparison.Ordinal))
                        continue;

                    if (seenIds.TryGetValue(mapping.Id, out var existing))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DuplicateId,
                            mapping.Candidate.Location,
                            mapping.Id.ToString(CultureInfo.InvariantCulture),
                            existing.Candidate.DisplayName,
                            mapping.Candidate.DisplayName));
                    }
                    else
                    {
                        seenIds.Add(mapping.Id, mapping);
                    }

                    if (!mapping.Candidate.IsExcludedFromMap && emittedIds.Add(mapping.Id))
                        accepted.Add(mapping);
                }

                classBlocks.Add(BuildMapClass(BuildClassName(domain), domain, accepted));
            }

            var writer = new CodegenTextWriter();

            writer.Write($$"""
            // <auto-generated/>

            #nullable enable

            namespace {{mapNamespace.Namespace}}
            {
                {{string.Join("\n\n", classBlocks)}}
            }
            """);

            context.AddSource("TypeIdMap.g.cs", writer.GetContents().Replace("\r\n", "\n"));
        }

        // The domain becomes the prefix exactly as spelled, without normalization.
        private static string BuildClassName(string? domain)
        {
            return domain is null ? GeneratedMapName : domain + GeneratedMapName;
        }

        private static string BuildMapClass(string className, string? domain, List<TypeIdMappingModel> accepted)
        {
            var typeCases = new List<string>();
            var idCases = new List<string>();

            foreach (var mapping in accepted)
            {
                var typeofExpression = mapping.Candidate.TypeofExpression;
                var id = mapping.Id.ToString(CultureInfo.InvariantCulture);

                typeCases.Add($"case {id}: type = typeof({typeofExpression}); return true;");
                idCases.Add($"case \"{mapping.Candidate.FullName}\" when type == typeof({typeofExpression}): id = {id}; return true;");
            }

            var summary = domain is null
                ? "/// Provides a source-generated bidirectional mapping between the\n/// <c>[TypeId]</c>-annotated types included in this map and their\n/// integer IDs."
                : $"/// Provides a source-generated bidirectional mapping between the\n/// <c>[TypeId]</c>-annotated types included in this map for the\n/// '{domain}' domain and their integer IDs.";

            var writer = new CodegenTextWriter();

            writer.Write($$"""
            /// <summary>
            {{summary}}
            /// </summary>
            public static class {{className}}
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
            """);

            return writer.GetContents();
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
                bool isExcludedFromMap,
                string? domain,
                bool hasInvalidDomain,
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
                IsExcludedFromMap = isExcludedFromMap;
                Domain = domain;
                HasInvalidDomain = hasInvalidDomain;
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

            public bool IsExcludedFromMap { get; }

            public string? Domain { get; }

            public bool HasInvalidDomain { get; }

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
