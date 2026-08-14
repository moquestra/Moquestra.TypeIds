using System;

using Xunit;

namespace Moquestra.TypeIds.Tests
{
    public class TypeIdAttributeTests
    {
        [Fact]
        public void Constructor_WithId_ExposesId()
        {
            var attribute = new TypeIdAttribute(1);

            Assert.Equal(1, attribute.Id);
        }

        [Fact]
        public void Constructor_WithoutId_ExposesZeroId()
        {
            var attribute = new TypeIdAttribute();

            Assert.Equal(0, attribute.Id);
        }

        [Fact]
        public void Constructor_WithAlias_ExposesAliasAndZeroId()
        {
            var attribute = new TypeIdAttribute("Alias");

            Assert.Equal("Alias", attribute.Alias);

            Assert.Equal(0, attribute.Id);
        }

        [Fact]
        public void Constructor_WithNullAlias_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TypeIdAttribute(null!));
        }

        [Fact]
        public void ExcludeFromGeneratedMap_ByDefault_IsFalse()
        {
            var attribute = new TypeIdAttribute(1);

            Assert.False(attribute.ExcludeFromGeneratedMap);
        }

        [Fact]
        public void ExcludeFromGeneratedMap_WhenSet_ExposesTrue()
        {
            var attribute = new TypeIdAttribute(1)
            {
                ExcludeFromGeneratedMap = true,
            };

            Assert.True(attribute.ExcludeFromGeneratedMap);
        }

        [Fact]
        public void AttributeUsage_WhenInspected_AllowsClassesStructsAndInterfaces()
        {
            var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(TypeIdAttribute), typeof(AttributeUsageAttribute));

            Assert.NotNull(usage);

            Assert.Equal(
                AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
                usage.ValidOn);
        }

        [Fact]
        public void AttributeUsage_WhenInspected_DisallowsMultipleApplicationsAndInheritance()
        {
            var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(TypeIdAttribute), typeof(AttributeUsageAttribute));

            Assert.NotNull(usage);

            Assert.False(usage.AllowMultiple);

            Assert.False(usage.Inherited);
        }
    }
}
