using System;

namespace Moquestra.TypeIds
{
    /// <summary>
    /// Specifies how the integer ID for a type is determined during registration or source generation.
    /// A nonzero ID is used directly, while an alias is used to compute the ID.
    /// If neither is specified, the ID is computed from the type's full name.
    /// <see cref="ExcludeFromGeneratedMap"/> controls whether the source generator includes
    /// the type in its generated map.
    /// <see cref="Domain"/> controls which generated map the source generator places the type in.
    /// Applying this attribute to a generic type is not supported.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class TypeIdAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypeIdAttribute"/> class without an explicit ID.
        /// The ID is computed from the type's full name during registration or source generation.
        /// </summary>
        public TypeIdAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeIdAttribute"/> class with the specified ID.
        /// </summary>
        /// <param name="id">The integer ID to map to the type, or 0 to have the ID computed during registration or source generation.</param>
        public TypeIdAttribute(int id)
        {
            Id = id;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeIdAttribute"/> class with the specified alias.
        /// The ID is computed from the alias instead of the type's full name during registration or source generation.
        /// </summary>
        /// <param name="alias">The alias used to compute the ID. Cannot be <see langword="null"/>; an empty or whitespace-only alias is rejected during registration or source generation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="alias"/> is <see langword="null"/>.</exception>
        public TypeIdAttribute(string alias)
        {
            if (alias is null)
                throw new ArgumentNullException(nameof(alias));

            Alias = alias;
        }

        /// <summary>
        /// Gets the integer ID to map to the type. 0 means the ID is computed during registration
        /// or source generation.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the alias used to compute the ID. <see langword="null"/> means no alias is specified,
        /// and the ID is determined by <see cref="Id"/> or the type's full name.
        /// </summary>
        public string? Alias { get; }

        /// <summary>
        /// Gets or sets whether the source generator excludes this type from its generated map.
        /// The default is <see langword="false"/>. When set to <see langword="true"/>, the type
        /// is excluded only from the generated map; registration by <see cref="TypeIdRegistry"/>
        /// is unaffected.
        /// </summary>
        public bool ExcludeFromGeneratedMap { get; set; }

        /// <summary>
        /// Gets or sets the domain that determines which source-generated map the type is placed in.
        /// The default is <see langword="null"/>: the type belongs to the default domain.
        /// With fallback naming, the default domain uses <c>TypeIdMap</c>, while a named domain
        /// uses its declared name as the map class prefix, such as <c>AuthTypeIdMap</c> for the
        /// "Auth" domain. A domain is case-sensitive and must start with an ASCII letter or
        /// underscore and contain only ASCII letters, digits, and underscores; the source
        /// generator reports a violation as an error. The full name of a map can be overridden
        /// with <see cref="TypeIdMapNameAttribute"/>.
        /// <see cref="ExcludeFromGeneratedMap"/> still controls whether the type appears in that
        /// map's lookup cases. Registration by <see cref="TypeIdRegistry"/> and ID computation
        /// both ignore the domain.
        /// </summary>
        public string? Domain { get; set; }
    }
}
