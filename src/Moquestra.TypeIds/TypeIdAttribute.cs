using System;

namespace Moquestra.TypeIds
{
    /// <summary>
    /// Specifies the integer ID to map to a type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class TypeIdAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypeIdAttribute"/> class with the specified ID.
        /// </summary>
        /// <param name="id">The integer ID to map to the type. Cannot be 0.</param>
        public TypeIdAttribute(int id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets the integer ID to map to the type.
        /// </summary>
        public int Id { get; }
    }
}
