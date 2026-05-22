using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.Notify.Email;

namespace Mostlylucid.Notify.DependencyInjection;

public static class NotifyServiceCollectionExtensions
{
    /// <summary>
    ///     Bind <c>Notify:Email</c> options + register the template registry. Channel senders
    ///     are then registered explicitly via the returned builder.
    /// </summary>
    public static NotifyBuilder AddNotify(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailNotifyOptions>().Bind(configuration.GetSection("Notify:Email"));
        services.AddSingleton<EmailTemplateRegistry>();
        return new NotifyBuilder(services);
    }

    /// <summary>
    ///     Forces the eager template-registration singletons to materialize. Call this once after
    ///     <see cref="IServiceProvider"/> is built (e.g. in Program.cs after <c>builder.Build()</c>).
    /// </summary>
    public static IServiceProvider ActivateNotifyTemplates(this IServiceProvider provider)
    {
        _ = provider.GetServices<NotifyBuilder.TemplateRegistration>().ToList();
        return provider;
    }

    /// <summary>
    ///     Kicks off the drain pipeline on the Ephemeral coordinator. Call ONCE during host
    ///     startup, after Build(), passing <c>hostApplicationLifetime.ApplicationStopping</c>.
    /// </summary>
    public static IServiceProvider StartNotifyDrain(this IServiceProvider provider, CancellationToken applicationStopping)
    {
        provider.GetService<Drain.INotifyDrainStarter>()?.Start(applicationStopping);
        return provider;
    }
}
