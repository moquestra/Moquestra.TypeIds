using System.Text;

using Xunit;

using Moquestra.TypeIds.Hashing;

namespace Moquestra.TypeIds.Tests
{
    public class TypeIdHelpersTests
    {
        [Fact]
        public void ComputeId_WithNonAsciiName_ReturnsUtf8BasedId()
        {
            Assert.Equal(-429468543, TypeIdHelpers.ComputeId("한글"));
        }

        [Fact]
        public void ComputeId_WithHighBitClearHash_SetsTheSignBit()
        {
            Assert.Equal(0u, Fnv1a.Compute(Encoding.UTF8.GetBytes("0")) & 0x80000000);

            Assert.Equal(-1257461585, TypeIdHelpers.ComputeId("0"));
        }

        [Fact]
        public void ComputeId_WithHighBitSetHash_KeepsTheHash()
        {
            Assert.NotEqual(0u, Fnv1a.Compute(Encoding.UTF8.GetBytes("a")) & 0x80000000);

            Assert.Equal(-468965076, TypeIdHelpers.ComputeId("a"));
        }
    }
}
