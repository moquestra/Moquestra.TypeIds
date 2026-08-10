using System;
using System.Runtime.InteropServices;

using Moquestra.TypeIds.Hashing;

namespace Moquestra.TypeIds
{
    internal static class TypeIdHelpers
    {
        // Computes an ID from a name by hashing it with FNV-1a and setting the sign bit
        // to place the result in the negative ID domain. The runtime library and the
        // source generator compile this shared source file, ensuring that both use the
        // same algorithm.
        internal static int ComputeId(string name)
        {
            var hash = Fnv1a.Compute(MemoryMarshal.AsBytes(name.AsSpan()));

            return unchecked((int)(hash | 0x80000000));
        }
    }
}
