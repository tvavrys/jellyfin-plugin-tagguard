using Jellyfin.Plugin.TagGuard.Configuration;
using Jellyfin.Plugin.TagGuard.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.TagGuard.Plugins;

/// <summary>
/// Registers TagGuard services with Jellyfin's dependency injection container.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<TagGuardConfigurationValidator>();
        serviceCollection.AddSingleton<TagSanitizer>();
        serviceCollection.AddHostedService<NewItemEnforcementService>();
    }
}
