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
