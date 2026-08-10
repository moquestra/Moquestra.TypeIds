using System;
using System.Text;

using Xunit;

using Moquestra.TypeIds.Hashing;

namespace Moquestra.TypeIds.Tests
{
    public class Fnv1aTests
    {
        [Fact]
        public void Compute_WithSameBytes_ReturnsSameHash()
        {
            var bytes = Encoding.UTF8.GetBytes("Moquestra.TypeIds.Sample.LoginRequest");

            var first = Fnv1a.Compute(bytes);
            var second = Fnv1a.Compute(bytes);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Compute_WithKnownVectors_ReturnsStandardValues()
        {
            Assert.Equal(2166136261u, Fnv1a.Compute(ReadOnlySpan<byte>.Empty));

            Assert.Equal(0xE40C292Cu, Fnv1a.Compute(Encoding.UTF8.GetBytes("a")));
            Assert.Equal(0xC40BF6CCu, Fnv1a.Compute(Encoding.UTF8.GetBytes("A")));
            Assert.Equal(0xE8AC0D30u, Fnv1a.Compute(Encoding.UTF8.GetBytes("Moquestra.TypeIds.Sample.LoginRequest")));
        }

        [Fact]
        public void Compute_WithDifferentBytes_ReturnsDifferentHashes()
        {
            var first = Fnv1a.Compute(Encoding.UTF8.GetBytes("A"));
            var second = Fnv1a.Compute(Encoding.UTF8.GetBytes("B"));

            Assert.NotEqual(first, second);
        }
    }
}
