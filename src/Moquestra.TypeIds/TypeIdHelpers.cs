using System.Text;

using Moquestra.TypeIds.Hashing;

namespace Moquestra.TypeIds
{
    internal static class TypeIdHelpers
    {
        // Computes an ID from a name by hashing its UTF-8 bytes with FNV-1a and setting
        // the sign bit to place the result in the negative ID domain. The runtime library
        // and the source generator compile this shared source file, ensuring that both
        // use the same algorithm.
        internal static int ComputeId(string name)
        {
            var hash = Fnv1a.Compute(Encoding.UTF8.GetBytes(name));

            return unchecked((int)(hash | 0x80000000));
        }
    }
}
