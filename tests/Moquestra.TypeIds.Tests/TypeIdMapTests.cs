using System;
using System.Reflection;
using System.Reflection.Emit;

using Xunit;

using Moquestra.TypeIds.Tests.Generated;

namespace Moquestra.TypeIds.Tests
{
    public class TypeIdMapTests
    {
        private static readonly Type[] MappedTypes =
        {
            typeof(TypeIdRegistryTests.AnnotatedMessage),
            typeof(TypeIdRegistryTests.IAnnotatedMessage),
            typeof(TypeIdRegistryTests.ComputedMessage),
            typeof(TypeIdRegistryTests.ZeroIdMessage),
            typeof(TypeIdRegistryTests.AliasedMessage),
            typeof(TypeIdRegistryTests.RenamedMessage),
            typeof(TypeIdRegistryTests.StructMessage),
        };

        [Fact]
        public void TryGetId_WithExplicitId_ReturnsSpecifiedId()
        {
            Assert.True(TypeIdMap.TryGetId(typeof(TypeIdRegistryTests.AnnotatedMessage), out var id));

            Assert.Equal(10, id);
        }

        [Fact]
        public void TryGetId_WithComputedId_MatchesRuntimeComputation()
        {
            Assert.True(TypeIdMap.TryGetId(typeof(TypeIdRegistryTests.ComputedMessage), out var id));

            Assert.Equal(TypeIdRegistry.ComputeId(typeof(TypeIdRegistryTests.ComputedMessage)), id);
        }

        [Fact]
        public void TryGetId_WithAlias_ReturnsIdComputedFromAlias()
        {
            Assert.True(TypeIdMap.TryGetId(typeof(TypeIdRegistryTests.AliasedMessage), out var id));

            Assert.Equal(TypeIdHelpers.ComputeId("Aliased.Message"), id);
        }

        [Fact]
        public void TryGetId_WithPreviousFullNameAsAttributeAlias_PreservesIdAcrossRename()
        {
            Assert.True(TypeIdMap.TryGetId(typeof(TypeIdRegistryTests.RenamedMessage), out var id));

            Assert.Equal(TypeIdRegistry.ComputeId(typeof(TypeIdRegistryTests.LegacyMessage)), id);
        }

        [Fact]
        public void TryGetId_WithMappedType_MatchesAssemblyScan()
        {
            var scanned = new TypeIdRegistry();

            scanned.AddFromAssembly(typeof(TypeIdMapTests).Assembly);

            foreach (var type in MappedTypes)
            {
                Assert.True(TypeIdMap.TryGetId(type, out var mapId));

                Assert.True(scanned.TryGetId(type, out var scannedId));

                Assert.Equal(scannedId, mapId);
            }
        }

        [Fact]
        public void TryGetType_WithMappedId_MatchesAssemblyScan()
        {
            var scanned = new TypeIdRegistry();

            scanned.AddFromAssembly(typeof(TypeIdMapTests).Assembly);

            foreach (var type in MappedTypes)
            {
                Assert.True(scanned.TryGetId(type, out var id));

                Assert.True(TypeIdMap.TryGetType(id, out var mappedType));

                Assert.Equal(type, mappedType);
            }
        }

        [Fact]
        public void TryGetId_WithExcludedType_ReturnsFalseWhileScanRegistersIt()
        {
            var scanned = new TypeIdRegistry();

            scanned.AddFromAssembly(typeof(TypeIdMapTests).Assembly);

            Assert.True(scanned.TryGetId(typeof(TypeIdRegistryTests.ExcludedMessage), out var scannedId));
            Assert.Equal(TypeIdRegistry.ComputeId(typeof(TypeIdRegistryTests.ExcludedMessage)), scannedId);

            Assert.False(TypeIdMap.TryGetId(typeof(TypeIdRegistryTests.ExcludedMessage), out var mapId));

            Assert.Equal(0, mapId);
        }

        [Fact]
        public void TryGetType_WithExcludedTypeId_ReturnsFalseAndNull()
        {
            var id = TypeIdRegistry.ComputeId(typeof(TypeIdRegistryTests.ExcludedMessage));

            Assert.False(TypeIdMap.TryGetType(id, out var type));

            Assert.Null(type);
        }

        [Fact]
        public void TryGetId_WithUnmappedType_ReturnsFalseAndZero()
        {
            Assert.False(TypeIdMap.TryGetId(typeof(string), out var id));

            Assert.Equal(0, id);
        }

        [Fact]
        public void TryGetType_WithUnmappedId_ReturnsFalseAndNull()
        {
            Assert.False(TypeIdMap.TryGetType(12345, out var type));

            Assert.Null(type);
        }

        [Fact]
        public void TryGetId_WithNullType_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TypeIdMap.TryGetId(null!, out _));
        }

        [Fact]
        public void TryGetId_WithUnmappedTypeSharingFullName_ReturnsFalseAndZero()
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("Moquestra.TypeIds.Tests.Doppelganger"),
                AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("Main");
            var outer = module.DefineType("Moquestra.TypeIds.Tests.TypeIdRegistryTests", TypeAttributes.Public);
            var nested = outer.DefineNestedType("AnnotatedMessage", TypeAttributes.NestedPublic);
            outer.CreateType();
            var doppelganger = nested.CreateType()!;

            Assert.Equal(typeof(TypeIdRegistryTests.AnnotatedMessage).FullName, doppelganger.FullName);

            Assert.False(TypeIdMap.TryGetId(doppelganger, out var id));

            Assert.Equal(0, id);
        }
    }
}
