using Microsoft.Extensions.DependencyInjection;
using Neradio.Core.Playback;

namespace Neradio.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNeradioCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        // Created explicitly: .NET's default handler on Android is the platform's HTTP stack, whose
        // cleartext policy blocks plain-http streams.
        services.AddSingleton(_ =>
        {
            var handler = new SocketsHttpHandler();
            return new HttpClient(handler);
        });
        services.AddSingleton<Player>();
        return services;
    }
}
