using System;

namespace Moquestra.TypeIds
{
    /// <summary>
    /// Specifies the integer ID to map to a type.
    /// If the ID is omitted, it is computed from the type's full name at registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class TypeIdAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypeIdAttribute"/> class without an explicit ID.
        /// The ID is computed from the type's full name at registration.
        /// </summary>
        public TypeIdAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeIdAttribute"/> class with the specified ID.
        /// </summary>
        /// <param name="id">The integer ID to map to the type, or 0 to have the ID computed at registration.</param>
        public TypeIdAttribute(int id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets the integer ID to map to the type. 0 means the ID is computed at registration.
        /// </summary>
        public int Id { get; }
    }
}
