using Xunit;

namespace Moquestra.TypeIds.Tests
{
    public class TypeIdHelpersTests
    {
        [Fact]
        public void ComputeId_WithNonAsciiName_ReturnsUtf8BasedId()
        {
            Assert.Equal(-429468543, TypeIdHelpers.ComputeId("한글"));
        }
    }
}
