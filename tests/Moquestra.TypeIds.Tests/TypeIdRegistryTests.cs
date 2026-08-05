using System;

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
    }
}
