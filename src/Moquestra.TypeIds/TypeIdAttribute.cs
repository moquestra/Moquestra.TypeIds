using System;

namespace Moquestra.TypeIds
{
    /// <summary>
    /// Specifies how the integer ID for a type is determined at registration.
    /// A nonzero ID is used directly, while an alias is used to compute the ID.
    /// If neither is specified, the ID is computed from the type's full name.
    /// Applying this attribute to a generic type is not supported.
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
        /// Initializes a new instance of the <see cref="TypeIdAttribute"/> class with the specified alias.
        /// The ID is computed from the alias instead of the type's full name during registration or source generation.
        /// </summary>
        /// <param name="alias">The alias used to compute the ID. Cannot be <see langword="null"/>; an empty alias is rejected during registration or source generation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="alias"/> is <see langword="null"/>.</exception>
        public TypeIdAttribute(string alias)
        {
            if (alias is null)
                throw new ArgumentNullException(nameof(alias));

            Alias = alias;
        }

        /// <summary>
        /// Gets the integer ID to map to the type. 0 means the ID is computed at registration.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the alias used to compute the ID. <see langword="null"/> means no alias is specified,
        /// and the ID is determined by <see cref="Id"/> or the type's full name.
        /// </summary>
        public string? Alias { get; }
    }
}
