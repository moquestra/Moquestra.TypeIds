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
[TypeId]    sealed class HeartbeatCommand { }

var registry = new TypeIdRegistry();

// Register every type in the assembly that has a TypeIdAttribute:
registry.AddFromAssembly(typeof(LoginRequest).Assembly);

// Or register types one by one using TypeIdAttribute:
// registry.Add(typeof(LoginRequest));
// registry.Add(typeof(LoginResponse));
// registry.Add(typeof(HeartbeatCommand));

// Or register types with explicit IDs:
// registry.Add(typeof(LoginRequest), 1);
// registry.Add(typeof(LoginResponse), 2);

registry.TryGetId(typeof(LoginRequest), out var id);
registry.TryGetId(typeof(HeartbeatCommand), out var heartbeatId);
registry.TryGetType(2, out var type);

Console.WriteLine($"typeof(LoginRequest) -> {id}");
Console.WriteLine($"typeof(HeartbeatCommand) -> {heartbeatId}");
Console.WriteLine($"2 -> {type}");
```

Output:

```
typeof(LoginRequest) -> 1
typeof(HeartbeatCommand) -> -810957197
2 -> Moquestra.TypeIds.Sample.LoginResponse
```

- A type can be mapped to only one ID, and an ID to only one type.
- `Add(Type)` reads the ID from the `TypeIdAttribute` applied to the type and throws an `ArgumentException` if the attribute is missing.
- An omitted attribute ID is computed from the type's full name. Computed IDs are always negative, so they never collide with positive manual IDs.
- A computed ID changes when the type is renamed or moved to another namespace. Before renaming or moving a type whose ID has been persisted, assign its current computed ID explicitly.
- If two full names hash to the same ID, registration throws; assign an explicit ID to either type to resolve the collision.
- `AddFromAssembly` registers every type in the assembly that has a `TypeIdAttribute`. Types without the attribute are ignored, and types registered before a conflict remain in the registry.
- If a type or ID is already mapped, `Add` throws an `ArgumentException` identifying the existing mapping. Rejected duplicate registrations leave the registry unchanged.
- `TryGetType` and `TryGetId` return `false` when no mapping exists for the supplied ID or type.
