using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Moquestra.TypeIds
{
    /// <summary>
    /// Provides a bidirectional mapping between <see cref="Type"/> instances and integer IDs.
    /// This type is not thread-safe: complete registration on a single thread before performing concurrent lookups.
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
        /// <param name="type">The type to register. Cannot be <see langword="null"/> or a generic type.</param>
        /// <param name="id">The integer ID to map to the type. Cannot be 0.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The type is a generic type, <paramref name="id"/> is 0, the type is already mapped to an ID, or the ID is already mapped to a type.</exception>
        public void Add(Type type, int id)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (type.IsGenericType)
                throw new ArgumentException($"Type '{type}' is a generic type, which is not supported.", nameof(type));

            // 0 is reserved because TryGetId returns 0 when no mapping exists.
            if (id == 0)
                throw new ArgumentException("ID 0 is reserved to indicate that no mapping exists.", nameof(id));

            if (_typeById.TryGetValue(id, out var existingType))
                throw new ArgumentException($"ID '{id}' is already mapped to type '{existingType}'.", nameof(id));

            if (_idByType.TryGetValue(type, out var existingId))
                throw new ArgumentException($"Type '{type}' is already mapped to ID '{existingId}'.", nameof(type));

            _typeById.Add(id, type);
            _idByType.Add(type, id);
        }

        /// <summary>
        /// Registers a type with an ID computed from the alias.
        /// The alias is hashed instead of the type's full name, so changes to the type's full name do not change the ID.
        /// Using the type's previous full name as the alias preserves the previous computed ID, maintaining compatibility with persisted data.
        /// </summary>
        /// <param name="type">The type to register. Cannot be <see langword="null"/> or a generic type.</param>
        /// <param name="alias">The alias used to compute the ID. Cannot be <see langword="null"/>, empty, or whitespace-only.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> or <paramref name="alias"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="alias"/> is empty or whitespace-only, the type is a generic type, the type is already mapped to an ID, or the computed ID is already mapped to another type.</exception>
        public void Add(Type type, string alias)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (alias is null)
                throw new ArgumentNullException(nameof(alias));

            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("Alias cannot be empty or whitespace-only.", nameof(alias));

            Add(type, TypeIdHelpers.ComputeId(alias));
        }

        /// <summary>
        /// Registers a type using its <see cref="TypeIdAttribute"/>.
        /// If the attribute declares an alias, the ID is computed from the alias instead of the type's full name.
        /// </summary>
        /// <param name="type">The type to register. Cannot be <see langword="null"/> or a generic type, and must have a <see cref="TypeIdAttribute"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The type does not have a <see cref="TypeIdAttribute"/>, the type is a generic type, a computed ID is required but the type has no full name, the declared alias is empty or whitespace-only, the type is already mapped to an ID, or the resolved ID is already mapped to another type.</exception>
        public void Add(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            var attribute = type.GetCustomAttribute<TypeIdAttribute>(inherit: false);

            if (attribute is null)
                throw new ArgumentException($"Type '{type}' does not have a TypeIdAttribute.", nameof(type));

            Add(type, attribute);
        }

        /// <summary>
        /// Registers every type in the assembly that has a <see cref="TypeIdAttribute"/>.
        /// Types without the attribute are ignored.
        /// If a scanned type declares an alias, the ID is computed from the alias instead of the type's full name.
        /// </summary>
        /// <param name="assembly">The assembly to scan. Cannot be <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
        /// <exception cref="ReflectionTypeLoadException">The assembly contains types that cannot be loaded.</exception>
        /// <exception cref="ArgumentException">A scanned type is a generic type, requires a computed ID but has no full name, its declared alias is empty or whitespace-only, the type is already mapped to an ID, or its resolved ID is already mapped to another type.
        /// Types registered before the exception remain in the registry; the scan follows the assembly's type enumeration order, which is unspecified.</exception>
        public void AddFromAssembly(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            AddFromAssemblyCore(assembly, null);
        }

        /// <summary>
        /// Registers the types in the assembly that have a <see cref="TypeIdAttribute"/> and pass the predicate.
        /// Types without the attribute are ignored without evaluating the predicate.
        /// If a selected type declares an alias, the ID is computed from the alias instead of the type's full name.
        /// </summary>
        /// <param name="assembly">The assembly to scan. Cannot be <see langword="null"/>.</param>
        /// <param name="predicate">The predicate that selects the types to register. Cannot be <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
        /// <exception cref="ReflectionTypeLoadException">The assembly contains types that cannot be loaded.</exception>
        /// <exception cref="ArgumentException">A type selected by the predicate is a generic type, requires a computed ID but has no full name, its declared alias is empty or whitespace-only, the type is already mapped to an ID, or its resolved ID is already mapped to another type.
        /// Types registered before the exception remain in the registry; the scan follows the assembly's type enumeration order, which is unspecified.</exception>
        public void AddFromAssembly(Assembly assembly, Func<Type, bool> predicate)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            AddFromAssemblyCore(assembly, predicate);
        }

        private void AddFromAssemblyCore(Assembly assembly, Func<Type, bool>? predicate)
        {
            foreach (var type in assembly.GetTypes())
            {
                var attribute = type.GetCustomAttribute<TypeIdAttribute>(inherit: false);

                if (attribute is null)
                    continue;

                if (predicate is not null && !predicate(type))
                    continue;

                Add(type, attribute);
            }
        }

        private void Add(Type type, TypeIdAttribute attribute)
        {
            if (attribute.Alias is null)
                Add(type, attribute.Id == 0 ? ComputeId(type) : attribute.Id);
            else
                Add(type, attribute.Alias);
        }

        /// <summary>
        /// Computes a deterministic ID from the type's full name.
        /// The sign bit is always set so the result is negative and never 0, separating computed IDs
        /// from manual IDs, which are positive by convention.
        /// string.GetHashCode() is not used because its result can vary per process.
        /// </summary>
        internal static int ComputeId(Type type)
        {
            var name = type.FullName;

            if (name is null)
                throw new ArgumentException($"Type '{type}' has no full name, so an ID cannot be computed.", nameof(type));

            return TypeIdHelpers.ComputeId(name);
        }

        /// <summary>
        /// Attempts to get the type mapped to the specified ID.
        /// </summary>
        /// <param name="id">The ID to look up.</param>
        /// <param name="type">The type mapped to the specified ID, or <see langword="null"/> if no mapping exists.</param>
        /// <returns><see langword="true"/> if the specified ID is mapped to a type; otherwise, <see langword="false"/>.</returns>
        public bool TryGetType(int id, [NotNullWhen(true)] out Type? type)
        {
            return _typeById.TryGetValue(id, out type);
        }

        /// <summary>
        /// Attempts to get the ID mapped to the specified type.
        /// </summary>
        /// <param name="type">The type to look up. Cannot be <see langword="null"/>.</param>
        /// <param name="id">The ID mapped to the specified type, or 0 if no mapping exists.</param>
        /// <returns><see langword="true"/> if the specified type is mapped to an ID; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        public bool TryGetId(Type type, out int id)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return _idByType.TryGetValue(type, out id);
        }
    }
}
