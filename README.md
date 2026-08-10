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
[TypeId("Session.Kick")] sealed class KickNotification { }

var registry = new TypeIdRegistry();

// Register every type in the assembly that has a TypeIdAttribute:
registry.AddFromAssembly(typeof(LoginRequest).Assembly);

// Or register types one by one using TypeIdAttribute:
// registry.Add(typeof(LoginRequest));
// registry.Add(typeof(LoginResponse));
// registry.Add(typeof(HeartbeatCommand));
// registry.Add(typeof(KickNotification));

// Or register types with explicit IDs:
// registry.Add(typeof(LoginRequest), 1);
// registry.Add(typeof(LoginResponse), 2);

// Or register types with aliases:
// registry.Add(typeof(KickNotification), "Session.Kick");

registry.TryGetId(typeof(LoginRequest), out var id);
registry.TryGetId(typeof(HeartbeatCommand), out var heartbeatId);
registry.TryGetId(typeof(KickNotification), out var kickId);
registry.TryGetType(2, out var type);

Console.WriteLine($"typeof(LoginRequest) -> {id}");
Console.WriteLine($"typeof(HeartbeatCommand) -> {heartbeatId}");
Console.WriteLine($"typeof(KickNotification) -> {kickId}");
Console.WriteLine($"2 -> {type}");
```

Output:

```
typeof(LoginRequest) -> 1
typeof(HeartbeatCommand) -> -416631049
typeof(KickNotification) -> -2036228135
2 -> Moquestra.TypeIds.Sample.LoginResponse
```

- A type can be mapped to only one ID, and an ID to only one type.
- Generic types are not supported. Registering one throws an `ArgumentException`.
- `Add(Type)` determines the ID from the `TypeIdAttribute` applied to the type and throws an `ArgumentException` if the attribute is missing.
- When the attribute specifies neither a nonzero ID nor an alias, the ID is computed from the type's full name. Computed IDs are always negative, so they never collide with positive manual IDs.
- A string alias — `[TypeId("Session.Kick")]` or `Add(type, "Session.Kick")` — is hashed instead of the type's full name, so changing the type's name or namespace does not change the ID.
- An ID computed from the type's full name changes when the type is renamed or moved to another namespace. To preserve compatibility with persisted data, use the type's previous full name as its alias; this preserves the previous computed ID.
- If a full name or alias hashes to an ID already mapped to another type, registration throws; assign an explicit ID to either type to resolve the collision.
- `AddFromAssembly` registers every type in the assembly that has a `TypeIdAttribute`. Types without the attribute are ignored, and types registered before a conflict remain in the registry.
- If a type or ID is already mapped, `Add` throws an `ArgumentException` identifying the existing mapping. Rejected duplicate registrations leave the registry unchanged.
- `TryGetType` and `TryGetId` return `false` when no mapping exists for the supplied ID or type.

## Source generator

The `Moquestra.TypeIds.SourceGenerator` project provides a compile-time alternative to the runtime registry. It collects `[TypeId]`-annotated types from the current assembly, excluding those that generated code cannot reference, and generates a `TypeIdMap` class in the project's `<RootNamespace>.Generated` namespace, falling back to the assembly name when no root namespace is available. Its lookup methods use switch statements, so no registration, reflection, or dictionary is needed at runtime.

Reference the generator project as an analyzer, adjusting the path to its location:

```xml
<ProjectReference Include="..\Moquestra.TypeIds.SourceGenerator\Moquestra.TypeIds.SourceGenerator.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

The generated lookup methods mirror the registry's lookup API:

```csharp
Moquestra.TypeIds.Sample.Generated.TypeIdMap.TryGetType(2, out var mappedType);
Moquestra.TypeIds.Sample.Generated.TypeIdMap.TryGetId(typeof(LoginRequest), out var mappedId);
```

- IDs are determined at compile time using the same rules as the runtime registry, so generated and runtime mappings use the same ID for every type handled by both paths.
- Assemblies with distinct root namespaces get distinct lookup names, so their maps can be referenced side by side.
- The registry remains available for cases the generator cannot cover, such as assemblies loaded at runtime.

The generator reports these diagnostics:

| ID | Severity | Description |
|---|---|---|
| MQTID001 | Warning | The annotated type is not accessible to the generated lookup and is skipped. |
| MQTID002 | Error | The annotated type declares a null or empty alias. |
| MQTID003 | Error | An ID is mapped to more than one type. |
| MQTID004 | Error | The generated lookup type conflicts with an existing type in the assembly. |
| MQTID005 | Error | The annotated type is a generic type, which is not supported. |
