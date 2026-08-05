using System;

namespace Moquestra.TypeIds.Sample
{
    // Example message types
    [TypeId(1)] internal sealed class LoginRequest { }
    [TypeId(2)] internal sealed class LoginResponse { }
    [TypeId(3)] internal sealed class LogoutRequest { }
    [TypeId(4)] internal sealed class LogoutResponse { }
    [TypeId(5)] internal sealed class TimeSyncCommand { }

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

            // Or register types with explicit IDs:
            // registry.Add(typeof(LoginRequest), 1);
            // registry.Add(typeof(LoginResponse), 2);
            // registry.Add(typeof(LogoutRequest), 3);
            // registry.Add(typeof(LogoutResponse), 4);
            // registry.Add(typeof(TimeSyncCommand), 5);

            registry.TryGetId(typeof(LoginRequest), out var id);
            registry.TryGetType(2, out var type);

            Console.WriteLine($"typeof(LoginRequest) -> {id}");
            Console.WriteLine($"2 -> {type}");
        }
    }
}
