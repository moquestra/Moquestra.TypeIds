# Moquestra.TypeIds

A library for bidirectional mapping between .NET types and integer IDs.

## When to use it

Use it when you need a small, stable identifier for a .NET type:

- Network messages: send a compact ID instead of a type name, and resolve it to the corresponding type on the receiving side.
- Save data: serialized type names can become invalid when types are renamed or moved; explicitly assigned IDs remain stable.
- Handler dispatch: route an incoming ID to the appropriate handler without string comparisons or reflection.

## Usage

```csharp
[TypeId(1)] sealed class LoginRequest { }
[TypeId(2)] sealed class LoginResponse { }

var registry = new TypeIdRegistry();

// Register types using TypeIdAttribute:
registry.Add(typeof(LoginRequest));
registry.Add(typeof(LoginResponse));

// Alternatively, register types with explicit IDs:
// registry.Add(typeof(LoginRequest), 1);
// registry.Add(typeof(LoginResponse), 2);

registry.TryGetId(typeof(LoginRequest), out var id);
registry.TryGetType(2, out var type);

Console.WriteLine($"typeof(LoginRequest) -> {id}");
Console.WriteLine($"2 -> {type}");
```

Output:

```
typeof(LoginRequest) -> 1
2 -> LoginResponse
```

- A type can be mapped to only one ID, and an ID to only one type.
- `Add(Type)` reads the ID from the `TypeIdAttribute` applied to the type and throws an `ArgumentException` if the attribute is missing.
- If a type or ID is already mapped, `Add` throws an `ArgumentException` identifying the existing mapping. Rejected duplicate registrations leave the registry unchanged.
- `TryGetType` and `TryGetId` return `false` when no mapping exists for the supplied ID or type.
