using System;

namespace Moquestra.TypeIds.Sample
{
    // Example message types
    internal sealed class LoginRequest { }
    internal sealed class LoginResponse { }
    internal sealed class LogoutRequest { }
    internal sealed class LogoutResponse { }
    internal sealed class TimeSyncCommand { }

    internal static class Program
    {
        private static void Main()
        {
            var registry = new TypeIdRegistry();

            registry.Add(typeof(LoginRequest), 1);
            registry.Add(typeof(LoginResponse), 2);
            registry.Add(typeof(LogoutRequest), 3);
            registry.Add(typeof(LogoutResponse), 4);
            registry.Add(typeof(TimeSyncCommand), 5);

            registry.TryGetId(typeof(LoginRequest), out var id);
            registry.TryGetType(2, out var type);

            Console.WriteLine($"typeof(LoginRequest) -> {id}");
            Console.WriteLine($"2 -> {type}");
        }
    }
}
