using System;
using System.Collections.Generic;

using Xunit;

namespace Moquestra.TypeIds.Tests
{
    public class TypeIdRegistryTests
    {
        [Fact]
        public void Add_WithDistinctPairs_Succeeds()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);
            registry.Add(typeof(int), 2);
            registry.Add(typeof(DateTime), 3);
        }

        [Fact]
        public void Add_WithNullType_ThrowsArgumentNullException()
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
        public void TryGetType_WithRegisteredId_ReturnsType()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);

            Assert.True(registry.TryGetType(1, out var type));

            Assert.Equal(typeof(string), type);
        }

        [Fact]
        public void TryGetType_WithUnregisteredId_ReturnsFalseAndNull()
        {
            var registry = new TypeIdRegistry();

            Assert.False(registry.TryGetType(1, out var type));

            Assert.Null(type);
        }

        [Fact]
        public void TryGetId_WithRegisteredType_ReturnsId()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(string), 1);

            Assert.True(registry.TryGetId(typeof(string), out var id));

            Assert.Equal(1, id);
        }

        [Fact]
        public void TryGetId_WithUnregisteredType_ReturnsFalseAndZero()
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
        public void Add_WithNullTypeUsingAttributeOverload_ThrowsArgumentNullException()
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

        [TypeId(10)]
        private sealed class AnnotatedMessage { }

        [TypeId(11)]
        private interface IAnnotatedMessage { }

        private sealed class UnannotatedMessage { }

        [TypeId]
        private sealed class ComputedMessage { }

        [TypeId(0)]
        private sealed class ZeroIdMessage { }
    }
}
