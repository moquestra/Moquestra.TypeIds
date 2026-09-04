using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

using Moquestra.TypeIds.SourceGenerator;

namespace Moquestra.TypeIds.Tests
{
    public class TypeIdGeneratorTests
    {
        [Fact]
        public void Run_WithAnnotatedType_GeneratesLookupWithoutDiagnostics()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class TypeIdMap", generated, StringComparison.Ordinal);
            Assert.Contains("typeof(global::Message)", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithoutAnnotatedTypes_GeneratesNothing()
        {
            var (diagnostics, generated, output) = Run("""
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Equal(string.Empty, generated);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithoutAnnotatedTypes_IgnoresUserDeclaredTypeIdMap()
        {
            var (diagnostics, generated, _) = Run("""
                namespace GeneratorTestAssembly.Generated
                {
                    public static class TypeIdMap { }
                }
                """);

            Assert.Empty(diagnostics);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithoutAnnotatedTypes_IgnoresHyphenatedAssemblyName()
        {
            var (diagnostics, generated, _) = Run("""
                public sealed class Message { }
                """, assemblyName: "Assembly-CSharp");

            Assert.Empty(diagnostics);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithPrivateNestedType_ReportsInaccessibleTypeWarning()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                public class Container
                {
                    [TypeId(1)]
                    private sealed class Hidden { }
                }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID001", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            Assert.Contains("Container.Hidden", diagnostic.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("ExcludeFromGeneratedMap", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithDomainDeclaredPrivateNestedType_ReportsInaccessibleTypeWarningAndGeneratesNothing()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                public class Container
                {
                    [TypeId(1, Domain = "Auth")]
                    private sealed class Hidden { }
                }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID001", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithEmptyAlias_ReportsInvalidAliasError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId("")]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID002", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("Message", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.DoesNotContain("Message", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithNullAlias_ReportsInvalidAliasError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(null)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID002", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("Message", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.DoesNotContain("Message", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithDuplicateExplicitIds_ReportsDuplicateIdError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class First { }

                [TypeId(1)]
                public sealed class Second { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID003", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("First", diagnostic.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("Second", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.Contains("typeof(global::First)", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("typeof(global::Second)", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithUserDeclaredTypeIdMap_ReportsGeneratedTypeConflictError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                namespace GeneratorTestAssembly.Generated
                {
                    public static class TypeIdMap { }
                }

                [TypeId(1)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID004", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("GeneratorTestAssembly.Generated.TypeIdMap", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithUserDeclaredGenericTypeIdMap_GeneratesLookupWithoutConflict()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                namespace GeneratorTestAssembly.Generated
                {
                    public static class TypeIdMap<T> { }
                }

                [TypeId(1)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("typeof(global::Message)", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithUserDeclaredVerbatimTypeIdMap_ReportsGeneratedTypeConflictError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                namespace GeneratorTestAssembly.Generated
                {
                    public static class @TypeIdMap { }
                }

                [TypeId(1)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID004", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithUserDeclaredTypeIdMapDelegate_ReportsGeneratedTypeConflictError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                namespace GeneratorTestAssembly.Generated
                {
                    public delegate void TypeIdMap();
                }

                [TypeId(1)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID004", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithGenericType_ReportsUnsupportedGenericTypeError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Envelope<T> { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID005", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("Envelope", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.DoesNotContain("Envelope", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithTypeNestedInGenericType_ReportsUnsupportedGenericTypeError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                public class Container<T>
                {
                    [TypeId(1)]
                    public sealed class Message { }
                }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID005", diagnostic.Id);

            Assert.Contains("Container<T>.Message", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.DoesNotContain("Message", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithoutRootNamespace_GeneratesLookupUnderAssemblyNamespace()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("namespace GeneratorTestAssembly.Generated", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithRootNamespace_GeneratesLookupUnderRootNamespace()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """, "Custom.Root");

            Assert.Empty(diagnostics);

            Assert.Contains("namespace Custom.Root.Generated", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithEmptyRootNamespace_GeneratesLookupUnderAssemblyNamespace()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """, string.Empty);

            Assert.Empty(diagnostics);

            Assert.Contains("namespace GeneratorTestAssembly.Generated", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithoutRootNamespaceOrAssemblyName_GeneratesLookupUnderDefaultNamespace()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """, assemblyName: null);

            Assert.Empty(diagnostics);

            Assert.Contains("namespace Moquestra.TypeIds.Generated", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithUserDeclaredTypeIdMapUnderRootNamespace_ReportsGeneratedTypeConflictError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                namespace Custom.Root.Generated
                {
                    public static class TypeIdMap { }
                }

                [TypeId(1)]
                public sealed class Message { }
                """, "Custom.Root");

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID004", diagnostic.Id);

            Assert.Contains("Custom.Root.Generated.TypeIdMap", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithUserDeclaredTypeIdMapUnderSanitizedNamespace_ReportsGeneratedTypeConflictError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                namespace Assembly_CSharp.Generated
                {
                    public static class TypeIdMap { }
                }

                [TypeId(1)]
                public sealed class Message { }
                """, assemblyName: "Assembly-CSharp");

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID004", diagnostic.Id);

            Assert.Contains("Assembly_CSharp.Generated.TypeIdMap", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithTypeIdMapInUnrelatedNamespace_GeneratesLookupWithoutConflict()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                namespace Moquestra.TypeIds.Generated
                {
                    public static class TypeIdMap { }
                }

                [TypeId(1)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("typeof(global::Message)", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithHyphenatedAssemblyName_SanitizesGeneratedNamespaceAndWarns()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """, assemblyName: "Assembly-CSharp");

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID006", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            Assert.Contains("Assembly-CSharp", diagnostic.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("Assembly_CSharp.Generated", diagnostic.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("namespace Assembly_CSharp.Generated", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDigitLeadingAssemblyName_SanitizesGeneratedNamespace()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """, assemblyName: "2DGame");

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID006", diagnostic.Id);

            Assert.Contains("namespace _2DGame.Generated", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithEmptyNamespaceSegment_SanitizesGeneratedNamespace()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """, "Custom..Root");

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID006", diagnostic.Id);

            Assert.Contains("namespace Custom._.Root.Generated", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithKeywordAssemblyName_SanitizesGeneratedNamespace()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """, assemblyName: "int");

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID006", diagnostic.Id);

            Assert.Contains("namespace _int.Generated", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithFormatCharacterInAssemblyName_SanitizesGeneratedNamespace()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """, assemblyName: "F\u00ADoo");

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID006", diagnostic.Id);

            Assert.Contains("namespace F_oo.Generated", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithExcludedType_OmitsItFromGeneratedMap()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class First { }

                [TypeId(2, ExcludeFromGeneratedMap = true)]
                public sealed class Second { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("typeof(global::First)", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("typeof(global::Second)", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithExcludedPrivateNestedType_ReportsNoDiagnostics()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                public class Container
                {
                    [TypeId(1, ExcludeFromGeneratedMap = true)]
                    private sealed class Hidden { }
                }
                """);

            Assert.Empty(diagnostics);

            Assert.DoesNotContain("Hidden", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithDuplicateIdOnExcludedType_ReportsDuplicateIdError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class First { }

                [TypeId(1, ExcludeFromGeneratedMap = true)]
                public sealed class Second { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID003", diagnostic.Id);

            Assert.Contains("First", diagnostic.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("Second", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Contains("typeof(global::First)", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("typeof(global::Second)", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithDuplicateIdOnEarlierExcludedType_KeepsIncludedTypeInMap()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, ExcludeFromGeneratedMap = true)]
                public sealed class AExcluded { }

                [TypeId(1)]
                public sealed class ZIncluded { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID003", diagnostic.Id);

            Assert.Contains("typeof(global::ZIncluded)", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("AExcluded", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithInvalidAliasOnExcludedType_ReportsInvalidAliasError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId("", ExcludeFromGeneratedMap = true)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID002", diagnostic.Id);

            Assert.DoesNotContain("Message", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithExcludedGenericType_ReportsUnsupportedGenericTypeError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, ExcludeFromGeneratedMap = true)]
                public sealed class Envelope<T> { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID005", diagnostic.Id);

            Assert.DoesNotContain("Envelope", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithOnlyExcludedTypes_GeneratesMapWithoutCases()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, ExcludeFromGeneratedMap = true)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class TypeIdMap", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("case ", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDomainDeclaredTypes_GeneratesMapPerDomain()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Plain { }

                [TypeId(2, Domain = "Auth")]
                public sealed class Login { }

                [TypeId(3, Domain = "Session")]
                public sealed class Kick { }
                """);

            Assert.Empty(diagnostics);

            var defaultIndex = generated.IndexOf("class TypeIdMap", StringComparison.Ordinal);
            var authIndex = generated.IndexOf("class AuthTypeIdMap", StringComparison.Ordinal);
            var sessionIndex = generated.IndexOf("class SessionTypeIdMap", StringComparison.Ordinal);
            var loginIndex = generated.IndexOf("typeof(global::Login)", StringComparison.Ordinal);

            Assert.True(defaultIndex >= 0);
            Assert.True(authIndex > defaultIndex);
            Assert.True(sessionIndex > authIndex);
            Assert.True(loginIndex > authIndex);
            Assert.True(loginIndex < sessionIndex);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithSameIdInDifferentDomains_ReportsNoDuplicateIdError()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Plain { }

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("typeof(global::Plain)", generated, StringComparison.Ordinal);
            Assert.Contains("typeof(global::Login)", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDuplicateIdInSameDomain_ReportsDuplicateIdError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "Auth")]
                public sealed class First { }

                [TypeId(1, Domain = "Auth")]
                public sealed class Second { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID003", diagnostic.Id);

            Assert.Contains("typeof(global::First)", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("typeof(global::Second)", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithEmptyDomain_ReportsInvalidDomainError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "")]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID007", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("Message", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.DoesNotContain("Message", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithNullDomain_TreatsTypeAsDefaultDomain()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = null)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("typeof(global::Message)", generated, StringComparison.Ordinal);
            Assert.Equal(
                generated.IndexOf("static class ", StringComparison.Ordinal),
                generated.LastIndexOf("static class ", StringComparison.Ordinal));

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDomainOnlyTypes_OmitsDefaultMap()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("class TypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithOnlyExcludedDomainTypes_GeneratesDomainMapWithoutCases()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "Auth", ExcludeFromGeneratedMap = true)]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("case ", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithUserDeclaredTypeIdMapAndDomainOnlyTypes_GeneratesDomainMapWithoutConflict()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                namespace GeneratorTestAssembly.Generated
                {
                    public static class TypeIdMap { }
                }

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithEmptyDomainOnExcludedType_ReportsInvalidDomainError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "", ExcludeFromGeneratedMap = true)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID007", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithHyphenatedDomain_ReportsInvalidDomainError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "My-Auth")]
                public sealed class Login { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID007", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("'My-Auth'", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithWhitespaceDomain_ReportsInvalidDomainError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = " ")]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID007", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithDigitLeadingDomain_ReportsInvalidDomainError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "2D")]
                public sealed class Sprite { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID007", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithNonAsciiDomain_ReportsInvalidDomainError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "인증")]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID007", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithKeywordDomain_GeneratesKeywordPrefixedMap()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "if")]
                public sealed class Branch { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class ifTypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithUnderscoreAndDigitDomain_GeneratesMap()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "_2D")]
                public sealed class Sprite { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class _2DTypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithMapName_UsesConfiguredNameForDefaultMap()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("My.Ids")]

                [TypeId(1)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("namespace My", generated, StringComparison.Ordinal);
            Assert.Contains("class Ids", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("class TypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDomainMapName_UsesConfiguredNameForDomainMap()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.AuthIds", Domain = "Auth")]

                [TypeId(1)]
                public sealed class Plain { }

                [TypeId(2, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("namespace Game", generated, StringComparison.Ordinal);
            Assert.Contains("class AuthIds", generated, StringComparison.Ordinal);
            Assert.Contains("class TypeIdMap", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("AuthTypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithPartialMapNames_KeepsFallbackForOtherDomains()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.AuthIds", Domain = "Auth")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }

                [TypeId(2, Domain = "Session")]
                public sealed class Kick { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class AuthIds", generated, StringComparison.Ordinal);
            Assert.Contains("class SessionTypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithSingleSegmentMapName_ReportsInvalidMapNameError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Ids")]

                [TypeId(1)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID008", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("'Ids'", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.Contains("class TypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithKeywordSegmentMapName_ReportsInvalidMapNameError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("My.if.Ids")]

                [TypeId(1)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID008", diagnostic.Id);

            Assert.Contains("class TypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithEmptySegmentMapName_ReportsInvalidMapNameError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("My..Ids")]

                [TypeId(1)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID008", diagnostic.Id);

            Assert.Contains("class TypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithDuplicateMapNameForSameDomain_ReportsDuplicateError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.FirstIds", Domain = "Auth")]
                [assembly: TypeIdMapName("Game.SecondIds", Domain = "Auth")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Equal(2, diagnostics.Length);
            Assert.All(diagnostics, static diagnostic => Assert.Equal("MQTID009", diagnostic.Id));
            Assert.All(diagnostics, static diagnostic => Assert.Contains("'Auth'", diagnostic.GetMessage(), StringComparison.Ordinal));

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithConflictingMapNames_ReportsNameCollisionError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.Ids", Domain = "Auth")]
                [assembly: TypeIdMapName("Game.Ids", Domain = "Session")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }

                [TypeId(2, Domain = "Session")]
                public sealed class Kick { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID010", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("'Game.Ids'", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithMapNameCollidingWithFallback_ReportsNameCollisionError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("GeneratorTestAssembly.Generated.TypeIdMap", Domain = "Auth")]

                [TypeId(1)]
                public sealed class Plain { }

                [TypeId(2, Domain = "Auth")]
                public sealed class Login { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID010", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithMapNameForUnknownDomain_ReportsUnknownDomainWarning()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.AuthIds", Domain = "auth")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID011", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            Assert.Contains("'auth'", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithMapNameForExcludedOnlyDomain_UsesConfiguredName()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.AuthIds", Domain = "Auth")]

                [TypeId(1, Domain = "Auth", ExcludeFromGeneratedMap = true)]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class AuthIds", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("case ", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithMapNameEqualToFallback_ReportsNoDiagnostics()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("GeneratorTestAssembly.Generated.AuthTypeIdMap", Domain = "Auth")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithAllMapsNamed_SuppressesSanitizedNamespaceWarning()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("My.Ids")]

                [TypeId(1)]
                public sealed class Message { }
                """, assemblyName: "Assembly-CSharp");

            Assert.Empty(diagnostics);

            Assert.Contains("namespace My", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithPartialMapNamesUnderSanitizedAssembly_KeepsSanitizedNamespaceWarning()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.AuthIds", Domain = "Auth")]

                [TypeId(1)]
                public sealed class Plain { }

                [TypeId(2, Domain = "Auth")]
                public sealed class Login { }
                """, assemblyName: "Assembly-CSharp");

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID006", diagnostic.Id);

            Assert.Contains("class AuthIds", generated, StringComparison.Ordinal);
            Assert.Contains("namespace Assembly_CSharp.Generated", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithUserTypeAtConfiguredMapName_FailsWithCompilerError()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.CustomTypeIdMap")]

                namespace Game
                {
                    public static class CustomTypeIdMap { }
                }

                [TypeId(1)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class CustomTypeIdMap", generated, StringComparison.Ordinal);

            Assert.Contains(output.GetDiagnostics(), static diagnostic => diagnostic.Id == "CS0101");
        }

        [Fact]
        public void Run_WithMapNameForDefaultDomainAndNoAnnotatedTypes_ReportsUnknownDomainWarning()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("My.Ids")]

                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID011", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithDefaultAndEmptyDomainDesignations_GeneratesConfiguredDefaultMapAndReportsUnknownDomainWarning()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.DefaultIds")]
                [assembly: TypeIdMapName("Game.EmptyIds", Domain = "")]

                [TypeId(1)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID011", diagnostic.Id);

            Assert.Contains("class DefaultIds", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("EmptyIds", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithInvalidMapNameAndNoAnnotatedTypes_ReportsInvalidMapNameError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Ids")]

                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID008", diagnostic.Id);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithInvalidMapNameWhenAllAnnotatedTypesAreRejected_ReportsInvalidMapNameError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Ids")]

                [TypeId(1)]
                public sealed class Message<T> { }
                """);

            Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "MQTID008");
            Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "MQTID005");

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithDomainTemplate_AppliesToNamedDomains()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.{Domain}Map")]

                [TypeId(1)]
                public sealed class Plain { }

                [TypeId(2, Domain = "Auth")]
                public sealed class Login { }

                [TypeId(3, Domain = "Session")]
                public sealed class Kick { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("namespace Game", generated, StringComparison.Ordinal);
            Assert.Contains("class AuthMap", generated, StringComparison.Ordinal);
            Assert.Contains("class SessionMap", generated, StringComparison.Ordinal);
            Assert.Contains("class TypeIdMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDefaultNameAndDomainTemplate_AppliesEachToItsTarget()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.DefaultMap")]
                [assembly: TypeIdMapName("Game.{Domain}Map")]

                [TypeId(1)]
                public sealed class Plain { }

                [TypeId(2, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class DefaultMap", generated, StringComparison.Ordinal);
            Assert.Contains("class AuthMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDomainTemplate_DoesNotApplyToDefaultDomain()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.{Domain}Map")]

                [TypeId(1)]
                public sealed class Plain { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class TypeIdMap", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("namespace Game", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDomainTemplateAndExplicitName_PrefersExplicitName()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.{Domain}Map")]
                [assembly: TypeIdMapName("Auth.Ids", Domain = "Auth")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }

                [TypeId(2, Domain = "Session")]
                public sealed class Kick { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("namespace Auth", generated, StringComparison.Ordinal);
            Assert.Contains("class Ids", generated, StringComparison.Ordinal);
            Assert.Contains("class SessionMap", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("class AuthMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithDuplicateDomainTemplates_ReportsDuplicateError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.{Domain}Map")]
                [assembly: TypeIdMapName("Other.{Domain}Map")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Equal(2, diagnostics.Length);
            Assert.All(diagnostics, static diagnostic => Assert.Equal("MQTID009", diagnostic.Id));
            Assert.All(diagnostics, static diagnostic => Assert.Contains("all named domains", diagnostic.GetMessage(), StringComparison.Ordinal));

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithUnknownTokenInTemplate_ReportsInvalidMapNameError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.{Assembly}.{Domain}Map")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID008", diagnostic.Id);

            Assert.Contains("{Assembly}", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithKeywordDomainUnderTemplate_FallsBackAndReportsInvalidMapNameError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("My.{Domain}.Ids")]

                [TypeId(1, Domain = "if")]
                public sealed class Branch { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID008", diagnostic.Id);

            Assert.Contains("'My.if.Ids'", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Contains("class ifTypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithTemplateAndDomainMatchingTokenText_AppliesTemplateAndReportsUnknownDomainWarning()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.{Domain}Map")]
                [assembly: TypeIdMapName("Game.OtherMap", Domain = "{Domain}")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID011", diagnostic.Id);

            Assert.Contains("class AuthMap", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("OtherMap", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithTemplateExpansionCollidingWithExplicitName_ReportsNameCollisionError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.{Domain}Map")]
                [assembly: TypeIdMapName("Game.SessionMap", Domain = "Auth")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }

                [TypeId(2, Domain = "Session")]
                public sealed class Kick { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID010", diagnostic.Id);

            Assert.Contains("'Game.SessionMap'", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithTemplateTokenInDomainDesignation_ReportsInvalidMapNameError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Game.{Domain}Map", Domain = "Auth")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID008", diagnostic.Id);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithUserDeclaredDomainMap_ReportsGeneratedTypeConflictError()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                namespace GeneratorTestAssembly.Generated
                {
                    public static class AuthTypeIdMap { }
                }

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID004", diagnostic.Id);

            Assert.Contains("GeneratorTestAssembly.Generated.AuthTypeIdMap", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithAnnotatedType_GeneratesIdConstant()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("public const int Message = 1;", generated, StringComparison.Ordinal);
            Assert.Contains("""The ID mapped to <see cref="global::Message"/>.""", generated, StringComparison.Ordinal);

            Assert.Contains("case global::GeneratorTestAssembly.Generated.TypeIdMap.Message: type = typeof(global::Message); return true;", generated, StringComparison.Ordinal);
            Assert.Contains("id = global::GeneratorTestAssembly.Generated.TypeIdMap.Message; return true;", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithComputedAndAliasIds_GeneratesIdConstants()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId]
                public sealed class Hashed { }

                [TypeId("wire-name")]
                public sealed class Aliased { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("public const int Hashed = ", generated, StringComparison.Ordinal);
            Assert.Contains("public const int Aliased = ", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithDomainType_GeneratesConstantInDomainMap()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);
            Assert.Contains("public const int Login = 1;", generated, StringComparison.Ordinal);
            Assert.Contains("case global::GeneratorTestAssembly.Generated.AuthTypeIdMap.Login: type = typeof(global::Login); return true;", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithDuplicateSimpleNames_ReportsConstantCollisionAndSkipsConstants()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                namespace First
                {
                    [TypeId(1)]
                    public sealed class Message { }
                }

                namespace Second
                {
                    [TypeId(2)]
                    public sealed class Message { }
                }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID013", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            Assert.Contains("Message", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.DoesNotContain("public const int Message", generated, StringComparison.Ordinal);

            Assert.Contains("case 1: type = typeof(global::First.Message); return true;", generated, StringComparison.Ordinal);
            Assert.Contains("case 2: type = typeof(global::Second.Message); return true;", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithTypeNameMatchingLookupMethod_ReportsConstantCollisionAndSkipsConstant()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class TryGetId { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID013", diagnostic.Id);

            Assert.DoesNotContain("public const int TryGetId", generated, StringComparison.Ordinal);

            Assert.Contains("case 1: type = typeof(global::TryGetId); return true;", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithTypeNameMatchingMapName_ReportsConstantCollisionAndSkipsConstant()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                namespace Elsewhere
                {
                    [TypeId(1)]
                    public sealed class TypeIdMap { }
                }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID013", diagnostic.Id);

            Assert.DoesNotContain("public const int TypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithParameterAndPatternLikeTypeNames_QualifiesConstantReferences()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class id { }

                [TypeId(2)]
                public sealed class type { }

                [TypeId(3)]
                public sealed class not { }

                [TypeId(4)]
                public sealed class _ { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("case global::GeneratorTestAssembly.Generated.TypeIdMap.id: ", generated, StringComparison.Ordinal);
            Assert.Contains("case global::GeneratorTestAssembly.Generated.TypeIdMap.type: ", generated, StringComparison.Ordinal);
            Assert.Contains("case global::GeneratorTestAssembly.Generated.TypeIdMap.not: ", generated, StringComparison.Ordinal);
            Assert.Contains("case global::GeneratorTestAssembly.Generated.TypeIdMap._: ", generated, StringComparison.Ordinal);
            Assert.Contains("id = global::GeneratorTestAssembly.Generated.TypeIdMap.type; return true;", generated, StringComparison.Ordinal);

            Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Run_WithTypeNameMatchingObjectMember_ReportsConstantCollisionAndSkipsConstant()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class ToString { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID013", diagnostic.Id);

            Assert.Contains("GeneratorTestAssembly.Generated.TypeIdMap", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.DoesNotContain("public const int ToString", generated, StringComparison.Ordinal);

            Assert.Contains("case 1: type = typeof(global::ToString); return true;", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithExcludedType_GeneratesNoConstant()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Included { }

                [TypeId(2, ExcludeFromGeneratedMap = true)]
                public sealed class Excluded { }
                """);

            Assert.Empty(diagnostics);

            Assert.Contains("public const int Included = 1;", generated, StringComparison.Ordinal);

            Assert.DoesNotContain("public const int Excluded", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithDefaultAndDomainMaps_EmitsOneFilePerMap()
        {
            var (diagnostics, _, output) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1)]
                public sealed class Message { }

                [TypeId(2, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            var paths = output.SyntaxTrees.Skip(1).Select(static tree => tree.FilePath).ToList();

            Assert.Equal(2, paths.Count);
            Assert.Contains(paths, static path => path.EndsWith("GeneratorTestAssembly.Generated.TypeIdMap.g.cs", StringComparison.Ordinal));
            Assert.Contains(paths, static path => path.EndsWith("GeneratorTestAssembly.Generated.AuthTypeIdMap.g.cs", StringComparison.Ordinal));
        }

        [Fact]
        public void Run_WithMapNamesDifferingOnlyByCasing_GeneratesDistinctFileNames()
        {
            var (diagnostics, _, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("Twin.Ids")]
                [assembly: TypeIdMapName("twin.Ids", Domain = "Auth")]

                [TypeId(1)]
                public sealed class Message { }

                [TypeId(2, Domain = "Auth")]
                public sealed class Login { }
                """);

            Assert.Empty(diagnostics);

            var names = output.SyntaxTrees.Skip(1).Select(static tree => Path.GetFileName(tree.FilePath)).ToList();

            Assert.Equal(2, names.Count);
            Assert.Contains(names, static name => name.StartsWith("Twin.Ids.", StringComparison.Ordinal) && name.EndsWith(".g.cs", StringComparison.Ordinal));
            Assert.Contains(names, static name => name.StartsWith("twin.Ids.", StringComparison.Ordinal) && name.EndsWith(".g.cs", StringComparison.Ordinal));

            Assert.NotEqual(names[0].ToUpperInvariant(), names[1].ToUpperInvariant());
        }

        [Fact]
        public void Run_WithDomainsDifferingOnlyByCasing_ReportsCaseTwinWarningAndGeneratesBothMaps()
        {
            var (diagnostics, generated, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }

                [TypeId(2, Domain = "auth")]
                public sealed class Logout { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID012", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            Assert.Contains("'auth'", diagnostic.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("'Auth'", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.Contains("class AuthTypeIdMap", generated, StringComparison.Ordinal);
            Assert.Contains("class authTypeIdMap", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WithThreeDomainCasingVariants_ReportsWarningForEachAdditionalVariant()
        {
            var (diagnostics, _, _) = Run("""
                using Moquestra.TypeIds;

                [TypeId(1, Domain = "Auth")]
                public sealed class First { }

                [TypeId(2, Domain = "auth")]
                public sealed class Second { }

                [TypeId(3, Domain = "AUTH")]
                public sealed class Third { }
                """);

            Assert.Equal(2, diagnostics.Length);
            Assert.All(diagnostics, static diagnostic => Assert.Equal("MQTID012", diagnostic.Id));
        }

        [Fact]
        public void Run_WithCaseTwinDomainsAndDistinctMapNames_GeneratesFileNamesWithoutHashSuffixes()
        {
            var (diagnostics, _, output) = Run("""
                using Moquestra.TypeIds;

                [assembly: TypeIdMapName("First.Ids", Domain = "Auth")]
                [assembly: TypeIdMapName("Second.Ids", Domain = "auth")]

                [TypeId(1, Domain = "Auth")]
                public sealed class Login { }

                [TypeId(2, Domain = "auth")]
                public sealed class Logout { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID012", diagnostic.Id);

            var names = output.SyntaxTrees.Skip(1).Select(static tree => Path.GetFileName(tree.FilePath)).ToList();

            Assert.Contains("First.Ids.g.cs", names);
            Assert.Contains("Second.Ids.g.cs", names);
        }

        private static (ImmutableArray<Diagnostic> Diagnostics, string GeneratedSource, Compilation Output) Run(string source, string? rootNamespace = null, string? assemblyName = "GeneratorTestAssembly")
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp11);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
                BuildReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create(
                new[] { new TypeIdGenerator().AsSourceGenerator() },
                parseOptions: parseOptions,
                optionsProvider: rootNamespace is null ? null : new TestAnalyzerConfigOptionsProvider(rootNamespace));

            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

            var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(static tree => tree.ToString()));

            return (diagnostics, generated, output);
        }

        private static List<MetadataReference> BuildReferences()
        {
            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

            var paths = new[]
            {
                typeof(object).Assembly.Location,
                typeof(TypeIdAttribute).Assembly.Location,
                Path.Combine(runtimeDirectory, "netstandard.dll"),
                Path.Combine(runtimeDirectory, "System.Runtime.dll"),
            };

            return paths
                .Where(File.Exists)
                .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();
        }

        private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly TestAnalyzerConfigOptions _globalOptions;

            public TestAnalyzerConfigOptionsProvider(string rootNamespace)
            {
                _globalOptions = new TestAnalyzerConfigOptions(rootNamespace);
            }

            public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;

            private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
            {
                private readonly string _rootNamespace;

                public TestAnalyzerConfigOptions(string rootNamespace)
                {
                    _rootNamespace = rootNamespace;
                }

                public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
                {
                    if (key == "build_property.RootNamespace")
                    {
                        value = _rootNamespace;

                        return true;
                    }

                    value = null;

                    return false;
                }
            }
        }
    }
}
