using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

                namespace Moquestra.TypeIds.Generated
                {
                    public static class TypeIdMap { }
                }

                [TypeId(1)]
                public sealed class Message { }
                """);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("MQTID004", diagnostic.Id);

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            Assert.Contains("Moquestra.TypeIds.Generated.TypeIdMap", diagnostic.GetMessage(), StringComparison.Ordinal);

            Assert.True(diagnostic.Location.IsInSource);

            Assert.Equal(string.Empty, generated);
        }

        [Fact]
        public void Run_WithUserDeclaredGenericTypeIdMap_GeneratesLookupWithoutConflict()
        {
            var (diagnostics, generated, output) = Run("""
                using Moquestra.TypeIds;

                namespace Moquestra.TypeIds.Generated
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

                namespace Moquestra.TypeIds.Generated
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

                namespace Moquestra.TypeIds.Generated
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

        private static (ImmutableArray<Diagnostic> Diagnostics, string GeneratedSource, Compilation Output) Run(string source)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp11);

            var compilation = CSharpCompilation.Create(
                "GeneratorTestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
                BuildReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create(
                new[] { new TypeIdGenerator().AsSourceGenerator() },
                parseOptions: parseOptions);

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
    }
}
