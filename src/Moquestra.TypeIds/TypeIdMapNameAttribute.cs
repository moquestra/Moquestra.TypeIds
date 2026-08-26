using System;

namespace Moquestra.TypeIds
{
    /// <summary>
    /// Specifies the full name of a source-generated map in this assembly.
    /// The designation applies to the map for <see cref="Domain"/>, or to the default
    /// domain's map when <see cref="Domain"/> is <see langword="null"/>. Maps without a
    /// designation keep their fallback names.
    /// The name is a dot-separated namespace and class name - at least one namespace
    /// segment - used exactly as written. Each segment must start with an ASCII letter
    /// or underscore, contain only ASCII letters, digits, and underscores, and not be
    /// a C# reserved keyword; the source generator reports a violation as an error.
    /// Names must differ across assemblies referenced together; the generator cannot
    /// detect such collisions, which surface as compiler errors in the consumer.
    /// This attribute is not interpreted at runtime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class TypeIdMapNameAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypeIdMapNameAttribute"/> class
        /// with the specified full name.
        /// </summary>
        /// <param name="name">The full name of the map. Cannot be <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
        public TypeIdMapNameAttribute(string name)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            Name = name;
        }

        /// <summary>
        /// Gets the full name of the map.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the domain whose map receives the name. The default is
        /// <see langword="null"/>, which targets the default domain's map. The value is
        /// case-sensitive; when no type belongs to the domain, the source generator
        /// reports a warning and the designation has no effect.
        /// </summary>
        public string? Domain { get; set; }
    }
}
