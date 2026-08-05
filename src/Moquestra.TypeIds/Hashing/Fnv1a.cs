using System;

namespace Moquestra.TypeIds.Hashing
{
    internal static class Fnv1a
    {
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;

        internal static uint Compute(ReadOnlySpan<byte> bytes)
        {
            var hash = OffsetBasis;

            foreach (var b in bytes)
            {
                hash = unchecked((hash ^ b) * Prime);
            }

            return hash;
        }
    }
}
