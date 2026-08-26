using System;

using Xunit;

namespace Moquestra.TypeIds.Tests
{
    public class TypeIdMapNameAttributeTests
    {
        [Fact]
        public void Constructor_WithName_ExposesName()
        {
            var attribute = new TypeIdMapNameAttribute("Game.Ids");

            Assert.Equal("Game.Ids", attribute.Name);
        }

        [Fact]
        public void Constructor_WithNullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TypeIdMapNameAttribute(null!));
        }

        [Fact]
        public void Domain_ByDefault_IsNull()
        {
            var attribute = new TypeIdMapNameAttribute("Game.Ids");

            Assert.Null(attribute.Domain);
        }

        [Fact]
        public void Domain_WhenSet_ExposesValue()
        {
            var attribute = new TypeIdMapNameAttribute("Game.Ids")
            {
                Domain = "Auth",
            };

            Assert.Equal("Auth", attribute.Domain);
        }
    }
}
