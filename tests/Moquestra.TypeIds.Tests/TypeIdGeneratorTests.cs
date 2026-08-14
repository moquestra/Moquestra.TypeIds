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

            Assert.True(diagnostic.Location.IsInSource);

            Assert.DoesNotContain("Hidden", generated, StringComparison.Ordinal);
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
