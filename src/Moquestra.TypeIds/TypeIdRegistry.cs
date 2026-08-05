using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Moquestra.TypeIds
{
    /// <summary>
    /// Provides a bidirectional mapping between <see cref="Type"/> instances and integer IDs.
    /// </summary>
    public sealed class TypeIdRegistry
    {
        private readonly Dictionary<int, Type> _typeById;
        private readonly Dictionary<Type, int> _idByType;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeIdRegistry"/> class.
        /// </summary>
        public TypeIdRegistry()
        {
            _typeById = new Dictionary<int, Type>();
            _idByType = new Dictionary<Type, int>();
        }

        /// <summary>
        /// Registers a mapping between a type and an ID.
        /// A type can be mapped to only one ID, and an ID to only one type.
        /// Validation failures leave the registry state unchanged.
        /// </summary>
        /// <param name="type">The type to register. Cannot be null.</param>
        /// <param name="id">The integer ID to map to the type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">The type or ID is already registered.</exception>
        public void Add(Type type, int id)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (_typeById.TryGetValue(id, out var existingType))
                throw new ArgumentException($"ID '{id}' is already mapped to type '{existingType}'.", nameof(id));

            if (_idByType.TryGetValue(type, out var existingId))
                throw new ArgumentException($"Type '{type}' is already mapped to ID '{existingId}'.", nameof(type));

            _typeById.Add(id, type);
            _idByType.Add(type, id);
        }

        /// <summary>
        /// Attempts to get the type mapped to the specified ID.
        /// </summary>
        /// <param name="id">The ID to look up.</param>
        /// <param name="type">The type mapped to the specified ID, or null if no mapping exists.</param>
        /// <returns>true if the ID is registered; otherwise, false.</returns>
        public bool TryGetType(int id, [NotNullWhen(true)] out Type? type)
        {
            return _typeById.TryGetValue(id, out type);
        }

        /// <summary>
        /// Attempts to get the ID mapped to the specified type.
        /// </summary>
        /// <param name="type">The type to look up. Cannot be null.</param>
        /// <param name="id">The ID mapped to the specified type, or 0 if no mapping exists.</param>
        /// <returns>true if the type is registered; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
        public bool TryGetId(Type type, out int id)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return _idByType.TryGetValue(type, out id);
        }
    }
}
