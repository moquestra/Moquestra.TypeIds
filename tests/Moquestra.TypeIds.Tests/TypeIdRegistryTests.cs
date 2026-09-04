using System;
using System.Collections.Generic;

using Xunit;

namespace Moquestra.TypeIds.Tests
{
    public class TypeIdRegistryTests
    {
        [Fact]
        public void Add_WithDistinctMappings_Succeeds()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);
            registry.Add(typeof(int), 2);
            registry.Add(typeof(DateTime), 3);
        }

        [Fact]
        public void Add_WithNullTypeUsingExplicitIdOverload_ThrowsArgumentNullException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Add(null!, 1));
        }

        [Fact]
        public void Add_WithZeroId_ThrowsArgumentException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentException>(() => registry.Add(typeof(string), 0));
        }

        [Fact]
        public void Add_WithGenericTypeDefinition_ThrowsArgumentException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentException>(() => registry.Add(typeof(List<>), 10));
        }

        [Fact]
        public void Add_WithConstructedGenericType_ThrowsArgumentException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentException>(() => registry.Add(typeof(List<int>), 10));
        }

        [Fact]
        public void Add_WithDuplicateId_ThrowsWithExistingTypeInMessage()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);

            var e = Assert.Throws<ArgumentException>(() => registry.Add(typeof(int), 1));

            Assert.Contains("'System.String'", e.Message);
        }

        [Fact]
        public void Add_WithDuplicateType_ThrowsWithExistingIdInMessage()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);

            var e = Assert.Throws<ArgumentException>(() => registry.Add(typeof(string), 2));

            Assert.Contains("'1'", e.Message);
        }

        [Fact]
        public void Add_AfterDuplicateTypeFailure_LeavesRegistryUnchanged()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);

            Assert.Throws<ArgumentException>(() => registry.Add(typeof(string), 2));

            Assert.True(registry.TryGetId(typeof(string), out var id));

            Assert.Equal(1, id);

            Assert.True(registry.TryGetType(1, out var type));

            Assert.Equal(typeof(string), type);

            Assert.False(registry.TryGetType(2, out _));
        }

        [Fact]
        public void Add_AfterDuplicateIdFailure_LeavesRegistryUnchanged()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);

            Assert.Throws<ArgumentException>(() => registry.Add(typeof(int), 1));

            Assert.True(registry.TryGetId(typeof(string), out var id));

            Assert.Equal(1, id);

            Assert.True(registry.TryGetType(1, out var type));

            Assert.Equal(typeof(string), type);

            Assert.False(registry.TryGetId(typeof(int), out _));
        }

        [Fact]
        public void TryGetType_WithMappedId_ReturnsType()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);

            Assert.True(registry.TryGetType(1, out var type));

            Assert.Equal(typeof(string), type);
        }

        [Fact]
        public void TryGetType_WithUnmappedId_ReturnsFalseAndNull()
        {
            var registry = new TypeIdRegistry();

            Assert.False(registry.TryGetType(1, out var type));

            Assert.Null(type);
        }

        [Fact]
        public void TryGetId_WithMappedType_ReturnsId()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);

            Assert.True(registry.TryGetId(typeof(string), out var id));

            Assert.Equal(1, id);
        }

        [Fact]
        public void TryGetId_WithUnmappedType_ReturnsFalseAndZero()
        {
            var registry = new TypeIdRegistry();

            Assert.False(registry.TryGetId(typeof(string), out var id));

            Assert.Equal(0, id);
        }

        [Fact]
        public void TryGetId_WithNullType_ThrowsArgumentNullException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.TryGetId(null!, out _));
        }

        [Fact]
        public void Add_WithTypeIdAttributeOnClass_MapsSpecifiedId()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(AnnotatedMessage));

            Assert.True(registry.TryGetId(typeof(AnnotatedMessage), out var id));

            Assert.Equal(10, id);
        }

        [Fact]
        public void Add_WithTypeIdAttributeOnInterface_MapsSpecifiedId()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(IAnnotatedMessage));

            Assert.True(registry.TryGetId(typeof(IAnnotatedMessage), out var id));

            Assert.Equal(11, id);
        }

        [Fact]
        public void Add_WithoutTypeIdAttribute_ThrowsArgumentException()
        {
            var registry = new TypeIdRegistry();

            var e = Assert.Throws<ArgumentException>(() => registry.Add(typeof(UnannotatedMessage)));

            Assert.Contains("UnannotatedMessage", e.Message);
        }

        [Fact]
        public void Add_WithNullTypeUsingAttributeBasedOverload_ThrowsArgumentNullException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Add(null!));
        }

        [Fact]
        public void AddFromAssembly_WithNullAssembly_ThrowsArgumentNullException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.AddFromAssembly(null!));
        }

        [Fact]
        public void AddFromAssembly_WithTestAssembly_RegistersAnnotatedTypes()
        {
            var registry = new TypeIdRegistry();

            registry.AddFromAssembly(typeof(TypeIdRegistryTests).Assembly);

            Assert.True(registry.TryGetId(typeof(AnnotatedMessage), out var classId));

            Assert.Equal(10, classId);

            Assert.True(registry.TryGetId(typeof(IAnnotatedMessage), out var interfaceId));

            Assert.Equal(11, interfaceId);
        }

        [Fact]
        public void AddFromAssembly_WithTestAssembly_IgnoresUnannotatedTypes()
        {
            var registry = new TypeIdRegistry();

            registry.AddFromAssembly(typeof(TypeIdRegistryTests).Assembly);

            Assert.False(registry.TryGetId(typeof(UnannotatedMessage), out _));
        }

        [Fact]
        public void AddFromAssembly_WithConflictingExistingMapping_ThrowsArgumentException()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 10);

            Assert.Throws<ArgumentException>(() => registry.AddFromAssembly(typeof(TypeIdRegistryTests).Assembly));
        }

        [Fact]
        public void AddFromAssembly_WithPredicate_RegistersOnlyMatchingTypes()
        {
            var registry = new TypeIdRegistry();

            registry.AddFromAssembly(typeof(TypeIdRegistryTests).Assembly, static type => type == typeof(AnnotatedMessage));

            Assert.True(registry.TryGetId(typeof(AnnotatedMessage), out var id));
            Assert.Equal(10, id);

            Assert.False(registry.TryGetId(typeof(AliasedMessage), out _));
        }

        [Fact]
        public void AddFromAssembly_WithPredicate_EvaluatesOnlyAnnotatedTypes()
        {
            var registry = new TypeIdRegistry();
            var evaluated = new List<Type>();

            registry.AddFromAssembly(typeof(TypeIdRegistryTests).Assembly, type =>
            {
                evaluated.Add(type);
                return false;
            });

            Assert.Contains(typeof(AnnotatedMessage), evaluated);
            Assert.DoesNotContain(typeof(UnannotatedMessage), evaluated);

            Assert.False(registry.TryGetId(typeof(AnnotatedMessage), out _));
        }

        [Fact]
        public void AddFromAssembly_WithNullPredicate_ThrowsArgumentNullException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.AddFromAssembly(typeof(TypeIdRegistryTests).Assembly, null!));
        }

        [Fact]
        public void AddFromAssembly_WithNullAssemblyUsingPredicateOverload_ThrowsArgumentNullException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.AddFromAssembly(null!, static type => true));
        }

        [Fact]
        public void Add_WithParameterlessTypeIdAttribute_MapsComputedNegativeId()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(ComputedMessage));

            Assert.True(registry.TryGetId(typeof(ComputedMessage), out var id));

            Assert.True(id < 0);
        }

        [Fact]
        public void Add_WithParameterlessTypeIdAttribute_MapsSameIdAcrossRegistries()
        {
            var first = new TypeIdRegistry();
            var second = new TypeIdRegistry();

            first.Add(typeof(ComputedMessage));
            second.Add(typeof(ComputedMessage));

            first.TryGetId(typeof(ComputedMessage), out var firstId);
            second.TryGetId(typeof(ComputedMessage), out var secondId);

            Assert.Equal(firstId, secondId);
        }

        [Fact]
        public void Add_WithZeroTypeIdAttribute_MapsComputedNegativeId()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(ZeroIdMessage));

            Assert.True(registry.TryGetId(typeof(ZeroIdMessage), out var id));

            Assert.True(id < 0);
        }

        [Fact]
        public void ComputeId_WithType_ReturnsNegativeId()
        {
            Assert.True(TypeIdRegistry.ComputeId(typeof(string)) < 0);
        }

        [Fact]
        public void ComputeId_WithSameType_ReturnsSameId()
        {
            var first = TypeIdRegistry.ComputeId(typeof(string));
            var second = TypeIdRegistry.ComputeId(typeof(string));

            Assert.Equal(first, second);
        }

        [Fact]
        public void ComputeId_WithTypeWithoutFullName_ThrowsArgumentException()
        {
            var genericParameter = typeof(List<>).GetGenericArguments()[0];

            Assert.Throws<ArgumentException>(() => TypeIdRegistry.ComputeId(genericParameter));
        }

        [Fact]
        public void ComputeId_WithTypeAndItsFullName_ReturnsSameId()
        {
            Assert.Equal(TypeIdRegistry.ComputeId(typeof(string)), TypeIdHelpers.ComputeId(typeof(string).FullName!));
        }

        [Fact]
        public void Add_WithAlias_MapsIdComputedFromAlias()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), "Alias");

            Assert.True(registry.TryGetId(typeof(string), out var id));

            Assert.Equal(TypeIdHelpers.ComputeId("Alias"), id);
        }

        [Fact]
        public void Add_WithAlias_ResolvesTypeByIdComputedFromAlias()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), "Alias");

            Assert.True(registry.TryGetType(TypeIdHelpers.ComputeId("Alias"), out var type));

            Assert.Equal(typeof(string), type);
        }

        [Fact]
        public void Add_WithNullTypeUsingAliasOverload_ThrowsArgumentNullException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Add(null!, "Alias"));
        }

        [Fact]
        public void Add_WithNullAlias_ThrowsArgumentNullException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Add(typeof(string), null!));
        }

        [Fact]
        public void Add_WithEmptyAlias_ThrowsArgumentException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentException>(() => registry.Add(typeof(string), string.Empty));
        }

        [Fact]
        public void Add_WithWhitespaceOnlyAlias_ThrowsArgumentException()
        {
            var registry = new TypeIdRegistry();

            Assert.Throws<ArgumentException>(() => registry.Add(typeof(string), "   "));
            Assert.Throws<ArgumentException>(() => registry.Add(typeof(string), "\t\n"));
        }

        [Fact]
        public void Add_WithSameAliasForAnotherType_ThrowsWithExistingTypeInMessage()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), "Alias");

            var e = Assert.Throws<ArgumentException>(() => registry.Add(typeof(int), "Alias"));

            Assert.Contains("'System.String'", e.Message);
        }

        [Fact]
        public void Add_WithAliasInTypeIdAttribute_MapsIdComputedFromAlias()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(AliasedMessage));

            Assert.True(registry.TryGetId(typeof(AliasedMessage), out var id));

            Assert.Equal(TypeIdHelpers.ComputeId("Aliased.Message"), id);
        }

        [Fact]
        public void AddFromAssembly_WithTestAssembly_MapsIdComputedFromAlias()
        {
            var registry = new TypeIdRegistry();

            registry.AddFromAssembly(typeof(TypeIdRegistryTests).Assembly);

            Assert.True(registry.TryGetId(typeof(AliasedMessage), out var id));

            Assert.Equal(TypeIdHelpers.ComputeId("Aliased.Message"), id);
        }

        [Fact]
        public void Add_WithPreviousFullNameAsAttributeAlias_PreservesIdAcrossRename()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(RenamedMessage));

            Assert.True(registry.TryGetId(typeof(RenamedMessage), out var id));

            Assert.Equal(TypeIdRegistry.ComputeId(typeof(LegacyMessage)), id);
        }

        [Fact]
        public void AddFromAssembly_WithDomainDeclaredTypes_RegistersThem()
        {
            var registry = new TypeIdRegistry();

            registry.AddFromAssembly(typeof(TypeIdRegistryTests).Assembly);

            Assert.True(registry.TryGetId(typeof(AuthMessage), out _));
            Assert.True(registry.TryGetId(typeof(SessionMessage), out _));
            Assert.True(registry.TryGetId(typeof(AuthCommand), out var commandId));
            Assert.Equal(1000, commandId);
        }

        [Fact]
        public void Add_WithDomainDeclaration_RegistersType()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(SessionMessage));

            Assert.True(registry.TryGetId(typeof(SessionMessage), out _));
        }

        [TypeId(10)]
        internal sealed class AnnotatedMessage { }

        [TypeId(11)]
        internal interface IAnnotatedMessage { }

        private sealed class UnannotatedMessage { }

        [TypeId]
        internal sealed class ComputedMessage { }

        [TypeId(0)]
        internal sealed class ZeroIdMessage { }

        [TypeId("Aliased.Message")]
        internal sealed class AliasedMessage { }

        internal sealed class LegacyMessage { }

        [TypeId("Moquestra.TypeIds.Tests.TypeIdRegistryTests+LegacyMessage")]
        internal sealed class RenamedMessage { }

        [TypeId]
        internal struct StructMessage { }

        [TypeId(ExcludeFromGeneratedMap = true)]
        internal sealed class ExcludedMessage { }

        [TypeId(Domain = "Auth")]
        internal sealed class AuthMessage { }

        [TypeId(1000, Domain = "Auth")]
        internal sealed class AuthCommand { }

        [TypeId(Domain = "Session")]
        internal sealed class SessionMessage { }
    }
}
