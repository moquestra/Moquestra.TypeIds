using System;

using Moquestra.TypeIds;

// The full name of a domain map can be overridden with an assembly attribute:
[assembly: TypeIdMapName("Moquestra.TypeIds.Sample.SessionIds", Domain = "Session")]

namespace Moquestra.TypeIds.Sample
{
    // Example message types
    [TypeId(1)] internal sealed class LoginRequest { }
    [TypeId(2)] internal sealed class LoginResponse { }
    [TypeId(3)] internal sealed class LogoutRequest { }
    [TypeId(4)] internal sealed class LogoutResponse { }
    [TypeId(5)] internal sealed class TimeSyncCommand { }

    // When omitted, the ID is computed from the type's full name and is always negative:
    [TypeId] internal sealed class HeartbeatCommand { }

    // The alias is hashed instead of the type's full name, so changing the type's
    // name or namespace does not change the ID computed from the protocol name "Session.Kick":
    // The domain affects only the generated maps: this type goes into the Session
    // domain's map instead of TypeIdMap, while runtime registration ignores the domain:
    [TypeId("Session.Kick", Domain = "Session")] internal sealed class KickNotification { }

    internal static class Program
    {
        private static void Main()
        {
            var registry = new TypeIdRegistry();

            // Register every type in the assembly that has a TypeIdAttribute:
            registry.AddFromAssembly(typeof(LoginRequest).Assembly);

            // Or register types one by one using TypeIdAttribute:
            // registry.Add(typeof(LoginRequest));
            // registry.Add(typeof(LoginResponse));
            // registry.Add(typeof(LogoutRequest));
            // registry.Add(typeof(LogoutResponse));
            // registry.Add(typeof(TimeSyncCommand));
            // registry.Add(typeof(HeartbeatCommand));
            // registry.Add(typeof(KickNotification));

            // Or register types with explicit IDs:
            // registry.Add(typeof(LoginRequest), 1);
            // registry.Add(typeof(LoginResponse), 2);
            // registry.Add(typeof(LogoutRequest), 3);
            // registry.Add(typeof(LogoutResponse), 4);
            // registry.Add(typeof(TimeSyncCommand), 5);

            // Or register types with aliases:
            // registry.Add(typeof(KickNotification), "Session.Kick");

            // Or use the source-generated lookup class, which performs lookups using
            // generated switch statements without a registry or dictionary:
            // Moquestra.TypeIds.Sample.Generated.TypeIdMap.TryGetType(2, out var mappedType);
            // Moquestra.TypeIds.Sample.Generated.TypeIdMap.TryGetId(typeof(LoginRequest), out var mappedId);

            // The assembly attribute above names the Session domain map SessionIds:
            SessionIds.TryGetId(typeof(KickNotification), out var sessionMapId);

            registry.TryGetId(typeof(LoginRequest), out var id);
            registry.TryGetId(typeof(HeartbeatCommand), out var heartbeatId);
            registry.TryGetId(typeof(KickNotification), out var kickId);
            registry.TryGetType(2, out var type);

            Console.WriteLine($"typeof(LoginRequest) -> {id}");
            Console.WriteLine($"typeof(HeartbeatCommand) -> {heartbeatId}");
            Console.WriteLine($"typeof(KickNotification) -> {kickId}");
            Console.WriteLine($"SessionIds: typeof(KickNotification) -> {sessionMapId}");
            Console.WriteLine($"2 -> {type}");
        }
    }
}
