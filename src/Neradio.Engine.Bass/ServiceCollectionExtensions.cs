using Microsoft.Extensions.DependencyInjection;
using Neradio.Core.Playback.Abstractions;

namespace Neradio.Engine.Bass;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNeradioEngineBass(this IServiceCollection services)
    {
        services.AddSingleton<IAudioEngine, BassEngine>();
        return services;
    }
}
