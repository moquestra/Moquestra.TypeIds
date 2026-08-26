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
        private const string MapNameAttributeMetadataName = "Moquestra.TypeIds.TypeIdMapNameAttribute";
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

        private static readonly DiagnosticDescriptor InvalidMapName = new DiagnosticDescriptor(
            "MQTID008",
            "Invalid map name",
            "The map name '{0}' is invalid; it must be a dot-separated namespace and class name, and each segment must start with an ASCII letter or underscore, contain only ASCII letters, digits, and underscores, and not be a C# reserved keyword; a domain-less template may additionally use the exact {{Domain}} token",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor DuplicateMapName = new DiagnosticDescriptor(
            "MQTID009",
            "Duplicate map name designation",
            "More than one map name designation targets {0}; specify only one",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor MapNameCollision = new DiagnosticDescriptor(
            "MQTID010",
            "Generated map name collision",
            "The maps for domain '{1}' and domain '{2}' both use the name '{0}'; specify distinct names",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor UnknownMapNameDomain = new DiagnosticDescriptor(
            "MQTID011",
            "Map name for an unknown domain",
            "No type belongs to domain '{0}', so the configured map name has no effect; check the spelling and casing",
            "Moquestra.TypeIds",
            DiagnosticSeverity.Warning,
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

            var mapNames = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    MapNameAttributeMetadataName,
                    static (node, _) => node is CompilationUnitSyntax,
                    static (attributeContext, _) => CaptureMapNames(attributeContext))
                .SelectMany(static (designations, _) => designations)
                .Collect();

            context.RegisterSourceOutput(
                candidates.Combine(conflicts).Combine(mapNamespace).Combine(mapNames),
                static (productionContext, input) => Emit(productionContext, input.Left.Left.Left, input.Left.Left.Right, input.Left.Right, input.Right));
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

        private static ImmutableArray<MapNameDesignation> CaptureMapNames(GeneratorAttributeSyntaxContext context)
        {
            var builder = ImmutableArray.CreateBuilder<MapNameDesignation>(context.Attributes.Length);

            foreach (var attribute in context.Attributes)
            {
                string? name = null;

                if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string nameValue)
                    name = nameValue;

                string? domain = null;

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key == "Domain" && namedArgument.Value.Value is string domainValue)
                        domain = domainValue;
                }

                var syntax = attribute.ApplicationSyntaxReference?.GetSyntax();

                builder.Add(new MapNameDesignation(name, domain, syntax?.GetLocation() ?? Location.None));
            }

            return builder.MoveToImmutable();
        }

        // The conflict predicate covers fallback-style names only: the TypeIdMap suffix
        // is matched broadly and compared against the actual generated names during
        // emission. Collisions with arbitrary configured names are left to compiler errors.
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

        private static void Emit(SourceProductionContext context, ImmutableArray<TypeIdCandidate> candidates, ImmutableArray<(string Namespace, string Name, Location Location)> conflicts, (string Namespace, string? SanitizedFrom) mapNamespace, ImmutableArray<MapNameDesignation> mapNames)
        {
            // Installing the generator alone must not change the assembly's public API:
            // with no annotated types or map name designations, nothing is emitted, so
            // no user-declared type can conflict with generated code.
            if (candidates.IsEmpty && mapNames.IsEmpty)
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

            // A domain exists when any type declares it with a valid name - eligibility
            // is ignored so rejection errors do not cascade into unused-name warnings.
            var declaredDomainNames = new HashSet<string>(StringComparer.Ordinal);
            var hasDefaultDeclaration = false;

            foreach (var candidate in candidates)
            {
                if (candidate.HasInvalidDomain)
                    continue;

                if (candidate.Domain is null)
                    hasDefaultDeclaration = true;
                else
                    declaredDomainNames.Add(candidate.Domain);
            }

            var designationCounts = new Dictionary<(bool IsDefault, string Domain), int>();
            var templateCount = 0;

            foreach (var designation in mapNames)
            {
                if (IsTemplateCandidate(designation))
                {
                    templateCount++;

                    continue;
                }

                var key = BuildDesignationKey(designation.Domain);

                designationCounts[key] = designationCounts.TryGetValue(key, out var count) ? count + 1 : 1;
            }

            var assignedNames = new Dictionary<(bool IsDefault, string Domain), (string Namespace, string ClassName, Location Location)>();
            string? domainTemplate = null;
            var domainTemplateLocation = Location.None;

            foreach (var designation in mapNames)
            {
                var isTemplate = IsTemplateCandidate(designation);
                var isDuplicate = isTemplate
                    ? templateCount > 1
                    : designationCounts[BuildDesignationKey(designation.Domain)] > 1;

                if (isDuplicate)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateMapName,
                        designation.Location,
                        DescribeDesignationTarget(designation, isTemplate)));

                    continue;
                }

                if (isTemplate)
                {
                    // A valid placeholder stands in for the token to validate the template
                    // itself - leftover braces (unknown tokens) and shape errors are
                    // caught independently of any domain.
                    var probe = designation.Name!.Replace("{Domain}", "X");

                    if (probe.Contains("{") || probe.Contains("}") || !IsValidMapName(probe))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidMapName,
                            designation.Location,
                            designation.Name));

                        continue;
                    }

                    domainTemplate = designation.Name;
                    domainTemplateLocation = designation.Location;

                    continue;
                }

                if (!IsValidMapName(designation.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidMapName,
                        designation.Location,
                        designation.Name ?? string.Empty));

                    continue;
                }

                var exists = designation.Domain is null
                    ? hasDefaultDeclaration
                    : declaredDomainNames.Contains(designation.Domain);

                if (!exists)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnknownMapNameDomain,
                        designation.Location,
                        designation.Domain ?? "(default)"));

                    continue;
                }

                var separator = designation.Name!.LastIndexOf('.');

                assignedNames.Add(BuildDesignationKey(designation.Domain), (
                    designation.Name.Substring(0, separator),
                    designation.Name.Substring(separator + 1),
                    designation.Location));
            }

            if (domains.Count == 0)
                return;

            var finalNames = new Dictionary<(bool IsDefault, string Domain), (string Namespace, string ClassName)>();

            // MQTID004 covers fallback-named maps only; configured names are left to
            // compiler errors even when they end in TypeIdMap.
            var fallbackIdentities = new HashSet<(string Namespace, string ClassName)>();
            var usesFallbackName = false;

            foreach (var domain in domains)
            {
                var key = BuildDesignationKey(domain);

                if (assignedNames.TryGetValue(key, out var assigned))
                {
                    finalNames[key] = (assigned.Namespace, assigned.ClassName);

                    continue;
                }

                // The template applies to named domains only.
                if (domain is not null && domainTemplate is not null)
                {
                    var expanded = domainTemplate.Replace("{Domain}", domain);

                    // The domain value can invalidate an expanded segment, for example
                    // by forming a reserved keyword.
                    if (IsValidMapName(expanded))
                    {
                        var separator = expanded.LastIndexOf('.');

                        finalNames[key] = (expanded.Substring(0, separator), expanded.Substring(separator + 1));

                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidMapName,
                        domainTemplateLocation,
                        expanded));
                }

                finalNames[key] = (mapNamespace.Namespace, BuildClassName(domain));
                fallbackIdentities.Add(finalNames[key]);
                usesFallbackName = true;
            }

            var fullNameOwners = new Dictionary<string, string?>(StringComparer.Ordinal);
            var hasNameCollision = false;

            foreach (var domain in domains)
            {
                var name = finalNames[BuildDesignationKey(domain)];
                var fullName = name.Namespace + "." + name.ClassName;

                if (fullNameOwners.TryGetValue(fullName, out var owner))
                {
                    var location = assignedNames.TryGetValue(BuildDesignationKey(domain), out var assigned)
                        ? assigned.Location
                        : assignedNames.TryGetValue(BuildDesignationKey(owner), out var ownerAssigned) ? ownerAssigned.Location : Location.None;

                    context.ReportDiagnostic(Diagnostic.Create(
                        MapNameCollision,
                        location,
                        fullName,
                        owner ?? "(default)",
                        domain ?? "(default)"));

                    hasNameCollision = true;
                }
                else
                {
                    fullNameOwners.Add(fullName, domain);
                }
            }

            if (hasNameCollision)
                return;

            var hasConflict = false;

            foreach (var conflict in conflicts)
            {
                if (!fallbackIdentities.Contains((conflict.Namespace, conflict.Name)))
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratedTypeConflict,
                    conflict.Location,
                    conflict.Namespace + "." + conflict.Name));

                hasConflict = true;
            }

            if (hasConflict)
                return;

            // Report only when the generated code uses the sanitized fallback prefix.
            if (usesFallbackName && mapNamespace.SanitizedFrom is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SanitizedNamespace,
                    Location.None,
                    mapNamespace.SanitizedFrom,
                    mapNamespace.Namespace));
            }

            var namespaceOrder = new List<string>();
            var namespaceBlocks = new Dictionary<string, List<string>>(StringComparer.Ordinal);

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

                var (targetNamespace, className) = finalNames[BuildDesignationKey(domain)];

                if (!namespaceBlocks.TryGetValue(targetNamespace, out var blocks))
                {
                    blocks = new List<string>();
                    namespaceBlocks.Add(targetNamespace, blocks);
                    namespaceOrder.Add(targetNamespace);
                }

                blocks.Add(BuildMapClass(className, domain, accepted));
            }

            var namespaceTexts = new List<string>();

            foreach (var name in namespaceOrder)
            {
                var namespaceWriter = new CodegenTextWriter();

                namespaceWriter.Write($$"""
                namespace {{name}}
                {
                    {{string.Join("\n\n", namespaceBlocks[name])}}
                }
                """);

                namespaceTexts.Add(namespaceWriter.GetContents());
            }

            var writer = new CodegenTextWriter();

            writer.Write($$"""
            // <auto-generated/>

            #nullable enable

            {{string.Join("\n\n", namespaceTexts)}}
            """);

            context.AddSource("TypeIdMap.g.cs", writer.GetContents().Replace("\r\n", "\n"));
        }

        // The domain becomes the prefix exactly as spelled, without normalization.
        private static string BuildClassName(string? domain)
        {
            return domain is null ? GeneratedMapName : domain + GeneratedMapName;
        }

        private static (bool IsDefault, string Domain) BuildDesignationKey(string? domain)
        {
            return domain is null ? (true, string.Empty) : (false, domain);
        }

        // Broad on purpose - a left brace routes the designation to template validation,
        // where unknown tokens are rejected.
        private static bool IsTemplateCandidate(MapNameDesignation designation)
        {
            return designation.Domain is null && designation.Name is not null && designation.Name.Contains("{");
        }

        private static string DescribeDesignationTarget(MapNameDesignation designation, bool isTemplate)
        {
            if (isTemplate)
                return "all named domains";

            return designation.Domain is null ? "the default domain" : "domain '" + designation.Domain + "'";
        }

        // Segments follow the domain rule plus a keyword ban - without a suffix to
        // absorb them, keyword segments would appear as bare identifiers.
        private static bool IsValidMapName(string? name)
        {
            if (name is null)
                return false;

            var segments = name.Split('.');

            if (segments.Length < 2)
                return false;

            foreach (var segment in segments)
            {
                if (!IsValidDomainName(segment))
                    return false;

                if (SyntaxFacts.GetKeywordKind(segment) != SyntaxKind.None)
                    return false;
            }

            return true;
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

        private sealed class MapNameDesignation
        {
            public MapNameDesignation(string? name, string? domain, Location location)
            {
                Name = name;
                Domain = domain;
                Location = location;
            }

            public string? Name { get; }

            public string? Domain { get; }

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
