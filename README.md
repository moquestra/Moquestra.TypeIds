# Moquestra.TypeIds

[![Build and test](https://github.com/moquestra/Moquestra.TypeIds/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/moquestra/Moquestra.TypeIds/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/v/Moquestra.TypeIds)](https://www.nuget.org/packages/Moquestra.TypeIds/)

A library for bidirectional mapping between types and integer IDs.

## When to use it

Use it when you need a small, stable identifier for a .NET type:

- Network messages: send a compact ID instead of a type name, and resolve it to the corresponding type on the receiving side.
- Save data: serialized type names can become invalid when types are renamed or moved; explicitly assigned IDs remain stable.
- Handler dispatch: route an incoming ID to the appropriate handler without string comparisons or reflection.

## Installation

```xml
<ItemGroup>
  <PackageReference Include="Moquestra.TypeIds" Version="1.0.0" />
  <PackageReference Include="Moquestra.TypeIds.SourceGenerator" Version="1.0.0" PrivateAssets="all" />
</ItemGroup>
```

The runtime library works on its own; the source generator is optional. Library authors should keep the direct `Moquestra.TypeIds` reference so the dependency reaches their own package, and mark only the generator with `PrivateAssets="all"`. Install both packages at the same version: the attribute and the generator evolve together, and an older generator silently ignores newer attribute options such as `ExcludeFromGeneratedMap`.

### Supported environments

- Runtime library: targets `netstandard2.1` and is compatible with .NET Core 3.0+ and .NET 5+, but not with .NET Framework.
- Source generator: requires a Roslyn 4.3+ compiler host: .NET SDK 6.0.400+ or Visual Studio 2022 17.3+. IDE design-time support also requires a compatible Roslyn host; if generated code is missing only in the editor, update the IDE. If the build reports warning CS9057 and `TypeIdMap` is missing, the host compiler is too old. Generated code compiles under C# 8.0 or later.
- Trimming and Native AOT: `AddFromAssembly` discovers types through reflection, so trimmed deployments can remove annotated types and silently skip them. Prefer the source generator there; its `typeof` references keep the mapped types rooted.

## Usage

```csharp
[TypeId(1)] sealed class LoginRequest { }
[TypeId(2)] sealed class LoginResponse { }
[TypeId]    sealed class HeartbeatCommand { }
[TypeId("Session.Kick", Domain = "Session")] sealed class KickNotification { }

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
- A string alias supplied through `[TypeId("Session.Kick")]` or `Add(type, "Session.Kick")` is hashed instead of the type's full name, so changing the type's name or namespace does not change the ID.
- An ID computed from the type's full name changes when the type is renamed or moved to another namespace. To preserve compatibility with persisted data, use the type's previous full name as its alias; this preserves the previous computed ID.
- If a full name or alias hashes to an ID already mapped to another type, registration throws; assign an explicit ID to either type to resolve the collision.
- `AddFromAssembly` registers every type in the assembly that has a `TypeIdAttribute`. Types without the attribute are ignored, and types registered before a conflict remain in the registry.
- Declaring `Domain`, as in `[TypeId("Session.Kick", Domain = "Session")]`, affects only the source-generated maps: `AddFromAssembly` and `Add` ignore it, and it plays no part in ID computation. See Source generator.
- If a type or ID is already mapped, `Add` throws an `ArgumentException` identifying the existing mapping. Rejected duplicate registrations leave the registry unchanged.
- `TryGetType` and `TryGetId` return `false` when no mapping exists for the supplied ID or type.
- `TypeIdRegistry` is not thread-safe. Finish registration on a single thread during startup, and read concurrently only while no further registrations occur.

## ID computation

A computed ID is the 32-bit FNV-1a hash of the UTF-8 bytes of the type's full name, or of the alias when one is declared, with the sign bit forced so the result is always negative. This algorithm is a compatibility contract for 1.x, so peers in other languages can reproduce the same IDs.

Lookups always take a `Type` or an ID; an alias is consumed when the ID is computed at registration or generation time and plays no part in lookups.

## Source generator

The `Moquestra.TypeIds.SourceGenerator` package provides a compile-time alternative to the runtime registry. It collects `[TypeId]`-annotated types from the current assembly and generates mappings for those that generated code can access unless `ExcludeFromGeneratedMap` is true. The mappings live in map classes generated in the project's `<RootNamespace>.Generated` namespace, falling back to the assembly name when no root namespace is available: types without a domain go to `TypeIdMap`, and each domain declared with `Domain = "Session"` gets its own `SessionTypeIdMap` named with the domain as a prefix. Domains are case-sensitive and used exactly as declared. A domain must start with an ASCII letter or underscore and contain only ASCII letters, digits, and underscores; an invalid name produces error MQTID007. When the root namespace or assembly name cannot be used directly as a namespace, the generator sanitizes it and reports the substitution with warning MQTID006: invalid characters become `_`, a segment gains a leading `_` when it starts with a character that is valid only after the first position (such as a digit) or matches a reserved C# keyword, and an empty segment becomes `_`. Its lookup methods use switch statements, so no registration, reflection, or dictionary is needed at runtime.

Install the generator package alongside the runtime package as shown in Installation. The generated lookup methods mirror the registry's lookup API:

```csharp
Moquestra.TypeIds.Sample.Generated.TypeIdMap.TryGetType(2, out var mappedType);
Moquestra.TypeIds.Sample.Generated.TypeIdMap.TryGetId(typeof(LoginRequest), out var mappedId);
Moquestra.TypeIds.Sample.Generated.SessionTypeIdMap.TryGetId(typeof(KickNotification), out var kickMapId);
```

- IDs are determined at compile time using the same rules as the runtime registry, so generated and runtime mappings use the same ID for every type handled by both paths.
- Assemblies with distinct root namespaces get distinct lookup names, so their maps can be referenced side by side.
- The registry remains available for cases the generator cannot cover, such as assemblies loaded at runtime.
- Set `ExcludeFromGeneratedMap = true`, as in `[TypeId(1, ExcludeFromGeneratedMap = true)]`, to keep a type out of the generated map. The flag does not affect runtime registration, so the generated map can be a subset of the types registered by an assembly scan. The generator still includes excluded types when detecting duplicate IDs, and the map is generated even when every annotated type is excluded.
- A domain partitions only the generated maps: `TypeIdMap` keeps only the types without a domain, while `AddFromAssembly` considers annotated types from every domain.
- Duplicate IDs are detected per domain (MQTID003), so the same ID can be reused across domains. `AddFromAssembly` still throws when reused IDs are scanned into one registry, so use the generated maps when IDs overlap across domains, or register non-conflicting subsets into separate registries with `Add(Type)`.

The generator reports these diagnostics:

| ID | Severity | Description |
|---|---|---|
| MQTID001 | Warning | The annotated type is not accessible to the generated lookup and is skipped. |
| MQTID002 | Error | The annotated type declares a null or empty alias. |
| MQTID003 | Error | An ID is mapped to more than one type. |
| MQTID004 | Error | The generated lookup type conflicts with an existing type in the assembly. |
| MQTID005 | Error | The annotated type is a generic type, which is not supported. |
| MQTID006 | Warning | The root namespace or assembly name could not be used directly as a namespace, so it was sanitized. |
| MQTID007 | Error | The annotated type declares an invalid domain name. |
